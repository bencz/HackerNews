using System.Text.Json;
using HackerNews.Application.Snapshots;
using HackerNews.Domain;

namespace HackerNews.UnitTests.Application.Snapshots;

[TestFixture]
public class SnapshotJsonContextTests
{
    [Test]
    public void SnapshotState_RoundTripsThroughSourceGen()
    {
        var state = new SnapshotState(
            7,
            [
                new Story(1, "First", "https://one.com", "alice", DateTimeOffset.UnixEpoch, 300, 12),
                new Story(2, "Second", "https://two.com", "bob", DateTimeOffset.UnixEpoch.AddHours(1), 150, 4)
            ],
            DateTimeOffset.UnixEpoch.AddMinutes(30));

        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, SnapshotJsonContext.Default.SnapshotState);
        var back = JsonSerializer.Deserialize(bytes, SnapshotJsonContext.Default.SnapshotState);

        Assert.That(back, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(back.Version, Is.EqualTo(7));
            Assert.That(back.UpdatedAt, Is.EqualTo(state.UpdatedAt));
            Assert.That(back.Stories, Has.Count.EqualTo(2));
            Assert.That(back.Stories[0].Title, Is.EqualTo("First"));
            Assert.That(back.Stories[0].Score, Is.EqualTo(300));
            Assert.That(back.Stories[1].CommentCount, Is.EqualTo(4));
        });
    }
}
