using System.Text.Json;

namespace Cafeteria.RemoteAccess;

public sealed class OutboxEvent
{
    public string EventId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
}

public interface IEventOutbox
{
    Task EnqueueAsync(
        IReadOnlyList<OutboxEvent> events,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxEvent>> GetDueAsync(
        int max,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(
        IReadOnlyCollection<string> eventIds,
        CancellationToken cancellationToken = default);

    Task MarkFailedAsync(
        IReadOnlyCollection<string> eventIds,
        string error,
        DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default);
}

public sealed class FileEventOutbox : IEventOutbox
{
    private const int MaxEvents = 2_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<OutboxEvent>? _events;

    public FileEventOutbox(string path)
    {
        _path = path;
    }

    public async Task EnqueueAsync(
        IReadOnlyList<OutboxEvent> events,
        CancellationToken cancellationToken = default)
    {
        if (events.Count == 0)
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var existing = await LoadCoreAsync(cancellationToken);
            var known = existing.Select(item => item.EventId).ToHashSet(StringComparer.Ordinal);
            var added = 0;

            foreach (var item in events)
            {
                if (string.IsNullOrWhiteSpace(item.EventId) || !known.Add(item.EventId))
                    continue;

                existing.Add(item);
                added++;
            }

            if (added == 0)
                return;

            if (existing.Count > MaxEvents)
            {
                var removeCount = existing.Count - MaxEvents;
                existing.RemoveRange(0, removeCount);
            }

            await SaveCoreAsync(existing, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<OutboxEvent>> GetDueAsync(
        int max,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var events = await LoadCoreAsync(cancellationToken);
            return events
                .Where(item => item.NextAttemptAt is null || item.NextAttemptAt <= now)
                .OrderBy(item => item.OccurredAt)
                .Take(max)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveAsync(
        IReadOnlyCollection<string> eventIds,
        CancellationToken cancellationToken = default)
    {
        if (eventIds.Count == 0)
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var events = await LoadCoreAsync(cancellationToken);
            var remove = eventIds.ToHashSet(StringComparer.Ordinal);
            var removed = events.RemoveAll(item => remove.Contains(item.EventId));
            if (removed > 0)
                await SaveCoreAsync(events, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task MarkFailedAsync(
        IReadOnlyCollection<string> eventIds,
        string error,
        DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default)
    {
        if (eventIds.Count == 0)
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var events = await LoadCoreAsync(cancellationToken);
            var affected = eventIds.ToHashSet(StringComparer.Ordinal);

            foreach (var item in events)
            {
                if (!affected.Contains(item.EventId))
                    continue;

                item.Attempts++;
                item.LastError = error;
                item.NextAttemptAt = nextAttemptAt;
            }

            await SaveCoreAsync(events, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<OutboxEvent>> LoadCoreAsync(CancellationToken cancellationToken)
    {
        if (_events is not null)
            return _events;

        if (!File.Exists(_path))
        {
            _events = new List<OutboxEvent>();
            return _events;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_path, cancellationToken);
            _events = JsonSerializer.Deserialize<List<OutboxEvent>>(json, JsonOptions)
                ?? new List<OutboxEvent>();
        }
        catch (JsonException)
        {
            var corruptPath = $"{_path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
            File.Move(_path, corruptPath, true);
            _events = new List<OutboxEvent>();
        }

        return _events;
    }

    private async Task SaveCoreAsync(
        List<OutboxEvent> events,
        CancellationToken cancellationToken)
    {
        RemoteStateDirectory.Ensure(Path.GetDirectoryName(_path)!);
        var json = JsonSerializer.Serialize(events, JsonOptions);
        var tempPath = _path + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, _path, true);
    }
}
