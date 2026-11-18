using SmartCafeteria.Core.Interfaces;
using SmartCafeteria.Core.Models;

namespace Cafeteria.RemoteAccess.Tests;

internal sealed class PassThroughProtector : ISecretProtector
{
    public byte[] Protect(byte[] data) => data.ToArray();
    public byte[] Unprotect(byte[] data) => data.ToArray();
}

internal sealed class RecordingOutbox : IEventOutbox
{
    public List<OutboxEvent> Events { get; } = new();

    public Task EnqueueAsync(
        IReadOnlyList<OutboxEvent> events,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in events)
        {
            if (!Events.Any(existing => existing.EventId == item.EventId))
                Events.Add(item);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxEvent>> GetDueAsync(
        int max,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OutboxEvent>>(
            Events
                .Where(item => item.NextAttemptAt is null || item.NextAttemptAt <= now)
                .Take(max)
                .ToList());

    public Task RemoveAsync(
        IReadOnlyCollection<string> eventIds,
        CancellationToken cancellationToken = default)
    {
        Events.RemoveAll(item => eventIds.Contains(item.EventId));
        return Task.CompletedTask;
    }

    public Task MarkFailedAsync(
        IReadOnlyCollection<string> eventIds,
        string error,
        DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default)
    {
        foreach (var item in Events.Where(item => eventIds.Contains(item.EventId)))
        {
            item.Attempts++;
            item.LastError = error;
            item.NextAttemptAt = nextAttemptAt;
        }

        return Task.CompletedTask;
    }
}

internal sealed class FakeCurrentUserContext : ICurrentUserContext
{
    public User? CurrentUser { get; set; }
    public bool IsActiveAdmin { get; set; }
}

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "SmartCafeteria.RemoteAccess.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public RemoteAccessPaths CreatePaths() => new(
        System.IO.Path.Combine(Path, "settings"),
        System.IO.Path.Combine(Path, "settings", "settings.dat"),
        System.IO.Path.Combine(Path, "state"),
        System.IO.Path.Combine(Path, "state", "event-state.json"),
        System.IO.Path.Combine(Path, "state", "event-outbox.json"),
        System.IO.Path.Combine(Path, "state", "command-ledger.json"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
