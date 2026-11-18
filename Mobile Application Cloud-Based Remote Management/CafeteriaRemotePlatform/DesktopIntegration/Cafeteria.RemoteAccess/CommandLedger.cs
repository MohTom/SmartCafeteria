using System.Text.Json;

namespace Cafeteria.RemoteAccess;

public static class CommandLedgerStatus
{
    public const string Started = "started";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Interrupted = "interrupted";
}

public sealed class CommandLedgerEntry
{
    public Guid CommandId { get; set; }
    public string Status { get; set; } = CommandLedgerStatus.Started;
    public string? ResponseJson { get; set; }
    public string? Error { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed record CommandLedgerDecision(
    bool ShouldExecute,
    bool IsInProgress,
    CommandLedgerEntry? TerminalEntry)
{
    public static CommandLedgerDecision Execute { get; } = new(true, false, null);
    public static CommandLedgerDecision InProgress { get; } = new(false, true, null);
}

public sealed class CommandLedger
{
    private const int MaxEntries = 1_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<Guid, CommandLedgerEntry>? _entries;

    public CommandLedger(string path)
    {
        _path = path;
    }

    public async Task<CommandLedgerDecision> TryBeginAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await LoadCoreAsync(cancellationToken);

            if (entries.TryGetValue(commandId, out var existing))
            {
                return existing.Status == CommandLedgerStatus.Started
                    ? CommandLedgerDecision.InProgress
                    : new CommandLedgerDecision(false, false, existing);
            }

            entries[commandId] = new CommandLedgerEntry
            {
                CommandId = commandId,
                Status = CommandLedgerStatus.Started,
                StartedAt = DateTimeOffset.UtcNow
            };

            await SaveCoreAsync(entries, cancellationToken);
            return CommandLedgerDecision.Execute;
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task CompleteAsync(
        Guid commandId,
        string responseJson,
        CancellationToken cancellationToken = default) =>
        SetTerminalAsync(commandId, CommandLedgerStatus.Completed, responseJson, null, cancellationToken);

    public Task FailAsync(
        Guid commandId,
        string error,
        CancellationToken cancellationToken = default) =>
        SetTerminalAsync(commandId, CommandLedgerStatus.Failed, null, error, cancellationToken);

    public Task InterruptAsync(
        Guid commandId,
        string error,
        CancellationToken cancellationToken = default) =>
        SetTerminalAsync(commandId, CommandLedgerStatus.Interrupted, null, error, cancellationToken);

    private async Task SetTerminalAsync(
        Guid commandId,
        string status,
        string? responseJson,
        string? error,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var entries = await LoadCoreAsync(cancellationToken);
            var entry = entries.TryGetValue(commandId, out var existing)
                ? existing
                : new CommandLedgerEntry
                {
                    CommandId = commandId,
                    StartedAt = DateTimeOffset.UtcNow
                };

            entry.Status = status;
            entry.ResponseJson = responseJson;
            entry.Error = error;
            entry.CompletedAt = DateTimeOffset.UtcNow;
            entries[commandId] = entry;

            await SaveCoreAsync(entries, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<Guid, CommandLedgerEntry>> LoadCoreAsync(
        CancellationToken cancellationToken)
    {
        if (_entries is not null)
            return _entries;

        if (!File.Exists(_path))
        {
            _entries = new Dictionary<Guid, CommandLedgerEntry>();
            return _entries;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_path, cancellationToken);
            var entries = JsonSerializer.Deserialize<List<CommandLedgerEntry>>(json, JsonOptions)
                ?? new List<CommandLedgerEntry>();

            _entries = entries
                .GroupBy(entry => entry.CommandId)
                .ToDictionary(group => group.Key, group => group.Last());

            var repaired = false;
            foreach (var entry in _entries.Values)
            {
                if (entry.Status != CommandLedgerStatus.Started)
                    continue;

                entry.Status = CommandLedgerStatus.Interrupted;
                entry.Error = "The desktop app stopped while this command was running. Verify the result manually.";
                entry.CompletedAt = DateTimeOffset.UtcNow;
                repaired = true;
            }

            if (repaired)
                await SaveCoreAsync(_entries, cancellationToken);

            return _entries;
        }
        catch (JsonException ex)
        {
            throw new RemoteAccessException(
                "The remote command ledger is invalid. Remote commands are disabled until it is repaired.",
                ex);
        }
    }

    private async Task SaveCoreAsync(
        Dictionary<Guid, CommandLedgerEntry> entries,
        CancellationToken cancellationToken)
    {
        RemoteStateDirectory.Ensure(Path.GetDirectoryName(_path)!);

        if (entries.Count > MaxEntries)
        {
            var keep = entries.Values
                .OrderByDescending(entry => entry.CompletedAt ?? entry.StartedAt)
                .Take(MaxEntries)
                .Select(entry => entry.CommandId)
                .ToHashSet();

            foreach (var commandId in entries.Keys.Where(id => !keep.Contains(id)).ToList())
                entries.Remove(commandId);
        }

        var json = JsonSerializer.Serialize(entries.Values.ToList(), JsonOptions);
        var tempPath = _path + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, _path, true);
    }
}
