# HackerNews Best Stories API

REST API that returns the best Hacker News stories, sorted by score (highest first).

A background service fetches the stories from the HackerNews Firebase API on a timer, keeps them in memory as an immutable snapshot, and serves every request straight from that snapshot. It runs as a single node by default, and can scale across replicas with an optional Redis layer (see [Distributed Mode](#distributed-mode-redis)).

## Running

```bash
dotnet run --project src/HackerNews.Api
```

Or with Docker:

```bash
docker build -f src/HackerNews.Api/Dockerfile -t hackernews-api .
docker run -p 8080:8080 hackernews-api
```

The API is available at `http://localhost:8080` (Docker) or `http://localhost:5001` (dotnet run, port set in `launchSettings.json`).

Swagger UI is enabled in the `Development` and `Staging` environments at `/swagger`.

To run several replicas behind Redis, see [Distributed Mode](#distributed-mode-redis).

## API

### Get Best Stories

```
GET /api/v1/stories/best?n=[count]
```

Returns the top `n` stories sorted by score (descending). If `n` is omitted, the full cached snapshot is returned.

**Response** `200 OK`

```json
[
  {
    "title": "A uBlock Origin update was rejected from the Chrome Web Store",
    "uri": "https://github.com/AnotherExample",
    "postedBy": "author",
    "time": "2024-11-14T18:23:45+00:00",
    "score": 1542,
    "commentCount": 387
  }
]
```

**Error responses**
- `400`, invalid request (for example `n` is zero or negative)
- `503`, cache not populated yet (the first refresh hasn't finished)

## Health Checks

| Endpoint | Purpose |
|---|---|
| `/health/startup` | All registered checks |
| `/health/live` | Liveness (always healthy) |
| `/health/ready` | Readiness (healthy once the first snapshot is loaded) |

## Configuration

Core settings live under the `HackerNews` section in `appsettings.json`:

| Key | Default | Description |
|---|---|---|
| `BaseUrl` | `https://hacker-news.firebaseio.com/` | HackerNews API base URL |
| `HttpTimeoutSeconds` | `10` | Per-attempt HTTP timeout (a Polly timeout policy, applied to each try) |
| `RefreshIntervalSeconds` | `60` | Time between background cache refreshes |
| `SnapshotSize` | `200` | How many stories to fetch and cache per cycle |
| `MaxParallelFetches` | `20` | Concurrent story requests during a refresh |
| `RetryCount` | `3` | Retries for transient HTTP errors |
| `RetryMedianFirstDelayMs` | `200` | Median delay of the first retry (decorrelated jitter backoff) |
| `CircuitBreakerFailureThreshold` | `10` | Failures before the circuit breaker opens |
| `CircuitBreakerDurationSeconds` | `30` | How long the breaker stays open |
| `MaxFailureRatio` | `0.25` | Fraction of failed fetches that aborts a cycle and keeps the previous snapshot |

CORS allowed origins are set in the `Cors:AllowedOrigins` array (empty allows any origin; only `GET` is permitted).

Redis settings for distributed mode are under the `Redis` section, covered in [Distributed Mode](#distributed-mode-redis).

## Resilience

Calls to the HackerNews API are wrapped by:

- **Retry**, 3 retries with decorrelated jitter backoff (first delay around 200ms), for transient errors and `429 Too Many Requests`.
- **Circuit breaker**, opens after 10 consecutive failures and stays open for 30 seconds.
- **Per-attempt timeout**, each try is bounded by `HttpTimeoutSeconds`, so a slow request is retried instead of stalling the whole operation.

If too many fetches fail in a cycle (above `MaxFailureRatio`), the refresh is aborted and the previous snapshot is kept instead of being replaced by a truncated one.

## Distributed Mode (Redis)

With several replicas, you don't want each one fetching from HackerNews on its own. Set `Redis:Enabled` to `true` and every pod keeps the same fast in-memory snapshot (L1), while Redis acts as the shared source of truth (L2) and the notification bus.

- **Single writer per cycle.** Every `RefreshIntervalSeconds` each pod tries to take a Redis lock (`SET NX PX`, waiting up to `LockWaitSeconds`). Whoever wins checks the last update time: if the snapshot is stale it refreshes from HackerNews, otherwise it does nothing. Pods that lose the lock skip the cycle. The upstream API is hit at most once per interval, regardless of replica count.
- **Shared store.** The snapshot, a monotonic `version`, and `updatedAt` live in a Redis hash (`beststories:state`), written atomically by a Lua script.
- **Propagation.** After a refresh the writer publishes the whole snapshot on the `beststories:updates` channel. Each pod's subscriber takes it straight from the message and swaps its L1, skipping versions it already has. No extra Redis read on this path.
- **Cold start and missed messages.** A new or restarted pod loads its L1 from Redis on boot (no HackerNews call), then subscribes. On reconnect it reloads from Redis, covering anything missed while it was disconnected.
- **Graceful degradation.** Reads never touch Redis, so if Redis goes down each pod keeps serving its last snapshot. The connection is a singleton with `AbortOnConnectFail=false` and reconnects on its own.

Redis settings live under the `Redis` section:

| Key | Default | Description |
|---|---|---|
| `Enabled` | `false` | Master switch. When `false` the app runs single-node with no Redis dependency. |
| `ConnectionString` | `""` | StackExchange.Redis connection string (for example `redis:6379`). |
| `KeyPrefix` | `beststories` | Prefix for the state key, lock key, and channel. |
| `LockTtlSeconds` | `30` | TTL of the refresh lock. |
| `LockWaitSeconds` | `5` | How long a pod waits for the lock before skipping the cycle. |

### Testing the distributed setup

`docker-compose.yml` starts Redis, three API replicas, and an nginx round-robin load balancer (one endpoint on `:8080`, with an `X-Upstream` header showing which pod answered):

```bash
docker compose up --build -d

# round-robin across the 3 pods
curl -s -D - -o /dev/null "http://localhost:8080/api/v1/stories/best?n=5" | grep -i x-upstream

# convergence: every pod returns the same snapshot (expect 1 unique hash)
for i in $(seq 1 9); do curl -s "http://localhost:8080/api/v1/stories/best?n=200" | sha256sum; done | sort | uniq -c

# inspect the shared state and watch the channel
docker exec hn-redis redis-cli HGET beststories:state version
docker exec hn-redis redis-cli SUBSCRIBE beststories:updates

# cold start: a restarted pod warms from Redis (look for "Primed snapshot version N" in its log)
docker compose restart api2 && docker logs hn-api2 2>&1 | grep "Primed snapshot"

docker compose down -v
```

## Tests

```bash
dotnet test
```

## Benchmark

Load tests with [ApacheBench](https://httpd.apache.org/docs/2.4/programs/ab.html), against a warm cache (`SnapshotSize = 200`, response payload around 46 KB).

### Single node

```bash
ab -n 50000 -c 200 http://localhost:5001/api/v1/stories/best
```

| Metric | Value |
|---|---|
| Total requests | 50,000 |
| Concurrency | 200 |
| Failed requests | 0 |
| Time taken | 2.25 s |
| **Throughput** | **22,232 req/s** |
| Transfer rate | 978 MB/s |
| Latency p50 | 7 ms |
| Latency p90 | 13 ms |
| Latency p95 | 16 ms |
| Latency p99 | 42 ms |
| Latency max | 120 ms |

### Distributed (docker-compose)

Same load profile (`-c 200`, 0 failed requests) against the Redis-backed stack from [Distributed Mode](#distributed-mode-redis), measured a few ways to show where the time goes:

| Path | Throughput | p50 | p99 |
|---|---|---|---|
| One pod, direct (no LB) | 14,596 req/s | 14 ms | 20 ms |
| Through nginx, 3 pods | 6,722 req/s | 30 ms | 33 ms |

These numbers are from a single laptop where Redis, nginx, and the three pods all share the same cores, so they measure that machine, not the design. Two things to read from them:

- Redis is not on the read path. A single pod still serves about 14.6k req/s straight from its in-memory snapshot; Redis is only touched by the background writer and the subscriber. Per-pod throughput stays close to single-node.
- The nginx hop roughly halves throughput here because every 46 KB response is moved twice (pod to nginx, nginx to client) on the same contended cores. On real infrastructure, with each pod on its own node and a dedicated load balancer, throughput scales close to linearly with the replica count.

## Technical Design

### Problem

The HackerNews Firebase API has no pagination for story details. Getting the top N stories means 1 call for the ID list plus N calls for the details. Doing that on every request would overwhelm the upstream API under any real load.

### Solution: split reads from writes with a pre-built snapshot

Upstream fetching and request serving are kept fully separate: a single background writer builds the data, and a lock-free in-memory cache serves it. The two paths never block each other.

**Write path** (one `BackgroundService`, on a timer, off the request pipeline):

1. `BestStoriesRefreshService` wakes up every `RefreshIntervalSeconds`.
2. Fetches the best story IDs (`GET /v0/beststories.json`).
3. Fetches up to `SnapshotSize` story details in parallel (`MaxParallelFetches` at a time), with the retry, circuit breaker, and timeout policies above.
4. Sorts by score, drops deleted, dead, and null entries.
5. Swaps the whole snapshot in one reference assignment.

**Read path** (HTTP pipeline, synchronous, no I/O):

1. Reads the current snapshot reference (one volatile read).
2. Takes the top `n` from the already-sorted list.
3. Returns.

The read path does no I/O, no blocking, and no allocations when `n` is at least the cache size, so it serves thousands of concurrent requests cheaply. Only the background writer ever calls HackerNews, at a fixed interval, no matter how much traffic the API gets.

### Lock-free cache via volatile snapshot swap

`BestStoriesCache` is thread-safe without `lock`, `Mutex`, `ReaderWriterLockSlim`, or any other primitive. Two things make that work:

1. **The `volatile` field.** It forces every read to come from main memory and blocks JIT reordering. Without it a reader could keep seeing a stale reference because of CPU-level caching.

2. **An immutable snapshot record.** It bundles `Stories` (an `ImmutableArray<Story>`) and `IsReady` into one object. The writer builds the full snapshot, freezes the list, and assigns the reference in a single instruction. The swap is atomic at the reference level: a reader sees either the whole old snapshot or the whole new one. There is no window where `IsReady` is `true` but `Stories` is still empty, because both fields live in the same object.

```
Writer:  snapshot_v1 ------------ snapshot_v2 ------------ snapshot_v3
              |                        |                        |
Reader A: ----+ (reads v1)             |                        |
Reader B: -----------------------------+ (reads v2)             |
Reader C: -----------------------------+ (reads v2)             |
Reader D: ------------------------------------------------------+ (reads v3)
```

Every reader gets a consistent, immutable view. Readers never block the writer, and the writer never blocks readers.

### Single-node default

With `Redis:Enabled = false` the cache is process-local: no shared state, and each instance runs its own `BestStoriesRefreshService`. For one upstream API and a small dataset that refreshes periodically, a single instance with more CPU and RAM is a fine trade-off. It avoids distributed-cache complexity, extra network hops, and cache-invalidation headaches, and still serves thousands of concurrent requests from one process.

Running several instances this way would have each one calling HackerNews on its own cycle, multiplying upstream load. To scale out without that cost, turn on Redis.

### Horizontal scaling with Redis

With `Redis:Enabled = true` the service runs as N replicas without changing the read path and without multiplying upstream load. The only thing that needs coordination is the upstream fetch, handled by a Redis lock, and the result is shared through a small notification backed by an authoritative Redis copy. See [Distributed Mode](#distributed-mode-redis) for the settings.

**Two cache tiers:**

- **L1 (per pod).** The same lock-free `BestStoriesCache`. All HTTP reads come from L1, in-process, with no Redis call on the request path.
- **L2 (Redis).** A hash `beststories:state` with the JSON `payload`, a monotonic `version`, and `updatedAt`. It survives restarts and lets a fresh pod catch up right away. It is read only on cold start and reconnect, never on the request path.

**Single writer per cycle.** Every `RefreshIntervalSeconds` each pod tries the lock (`SET beststories:lock <token> NX PX`, waiting up to `LockWaitSeconds`). The winner reads `updatedAt`: if the snapshot is still within the interval it does nothing, otherwise it runs the usual fetch, writes L2 atomically (a Lua script doing `HINCRBY version` plus `HSET payload, updatedAt`), and publishes the new version. Losers skip the cycle. The lock is released with a Lua compare-and-delete, so a pod never frees a lease it no longer owns. The write is idempotent and last-writer-wins on the monotonic version, so a rare double-write under a GC pause is harmless. The lock only avoids redundant upstream load; it is not a safety-critical mutex.

**Propagation by Pub/Sub.** The message on `beststories:updates` carries the full snapshot (version, stories, updatedAt). Each pod's `SnapshotSubscriberService` compares the version to the one it last applied and, if newer, swaps L1 straight from the message, with no second trip to Redis. The payload is small (around 46 KB, once a minute), so shipping it on the channel beats making every pod fetch it again. L2 stays authoritative, but only for cold start and reconnect.

**Cold start and missed messages.** On boot a pod loads L1 straight from L2 (no HackerNews call), then subscribes, so it serves correct data immediately and `/health/ready` turns healthy as soon as the snapshot is loaded. On reconnect it reloads from L2, catching anything published while it was disconnected.

```
Each pod, every RefreshIntervalSeconds:
  TryAcquire(beststories:lock, NX PX, wait <= LockWaitSeconds)
    lost -> skip this cycle (L1 stays fresh via Pub/Sub)
    won  -> if now - updatedAt < interval: release, skip
            else: fetch HN -> build snapshot -> cache.TryUpdate() (local L1)
                  -> Lua: HINCRBY version + HSET payload, updatedAt
                  -> PUBLISH beststories:updates <snapshot> -> release lock

Every pod (subscriber):
  on message <snapshot>:
    if version > appliedVersion: cache.TryUpdate(snapshot) (L1, no Redis read)
  on boot or reconnect:
    load beststories:state -> cache.TryUpdate() (L1)

Any pod (HTTP request):
  Kestrel -> Controller -> cache.Current (volatile read, in-process) -> Response
```

The read path is the same in every pod and never touches Redis. `IBestStoriesCacheWriter.TryUpdate()` is called either by the local refresh (the pod that won the lock) or by the subscriber (every other pod); the lock-free snapshot swap doesn't change.

**Why an in-pod lock.** One image, no extra Deployment, no internal endpoint to protect, and any pod can take over if another dies. A Kubernetes `CronJob` calling an internal `POST /internal/refresh` (with `concurrencyPolicy: Forbid`), or a dedicated single-replica worker, are valid alternatives that move the trigger out of the API pods, and both can reuse the same L2 and Pub/Sub. The lock was chosen here for simplicity.

The cost is a short window between `PUBLISH` and each pod applying it (usually sub-millisecond in a cluster), during which pods may briefly serve different versions. For data that refreshes about once a minute, that's not a concern.
