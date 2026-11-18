using Cafeteria.RemoteAccess;
using Xunit;

namespace Cafeteria.RemoteAccess.Tests;

public sealed class FileEventOutboxTests
{
    [Fact]
    public async Task Enqueue_DeduplicatesAndRetriesFailedEvents()
    {
        using var temp = new TempDirectory();
        var outbox = new FileEventOutbox(temp.CreatePaths().OutboxFile);
        var now = DateTimeOffset.UtcNow;

        await outbox.EnqueueAsync(new[]
        {
            new OutboxEvent
            {
                EventId = "event-1",
                Type = "order.created",
                PayloadJson = "{}",
                OccurredAt = now
            },
            new OutboxEvent
            {
                EventId = "event-1",
                Type = "order.created",
                PayloadJson = "{}",
                OccurredAt = now
            }
        });

        var due = await outbox.GetDueAsync(10, now);
        Assert.Single(due);

        var retryAt = now.AddMinutes(5);
        await outbox.MarkFailedAsync(new[] { "event-1" }, "network", retryAt);

        Assert.Empty(await outbox.GetDueAsync(10, now));
        var retry = await outbox.GetDueAsync(10, retryAt);
        Assert.Single(retry);
        Assert.Equal(1, retry[0].Attempts);
        Assert.Equal("network", retry[0].LastError);

        await outbox.RemoveAsync(new[] { "event-1" });
        Assert.Empty(await outbox.GetDueAsync(10, retryAt));
    }
}
