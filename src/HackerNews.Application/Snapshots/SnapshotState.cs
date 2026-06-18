using HackerNews.Domain;

namespace HackerNews.Application.Snapshots;

public sealed record SnapshotState(long Version, IReadOnlyList<Story> Stories, DateTimeOffset UpdatedAt);

public sealed record SnapshotMeta(long Version, DateTimeOffset UpdatedAt);
