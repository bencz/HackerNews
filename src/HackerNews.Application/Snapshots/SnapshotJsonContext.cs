using System.Text.Json.Serialization;
using HackerNews.Domain;

namespace HackerNews.Application.Snapshots;

[JsonSerializable(typeof(Story[]))]
[JsonSerializable(typeof(SnapshotState))]
public sealed partial class SnapshotJsonContext : JsonSerializerContext;
