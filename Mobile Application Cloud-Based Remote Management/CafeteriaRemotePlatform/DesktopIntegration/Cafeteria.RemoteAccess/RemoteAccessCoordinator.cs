using System.Reflection;
using Cafeteria.Contracts;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cafeteria.RemoteAccess;

public sealed class RemoteAccessCoordinator : IRemoteAccessCoordinator, IAsyncDisposable
{
    private static readonly TimeSpan[] ReconnectDelays =
    {
        TimeSpan.Zero,
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30)
    };

    private readonly IRemoteAccessSettingsStore _settingsStore;
    private readonly CloudApiClient _api;
    private readonly IEventOutbox _outbox;
    private readonly CommandLedger _ledger;
    private readonly IDesktopCommandHandler _handler;
    private readonly DesktopEventCollector _collector;
    private readonly IRemoteExecutionContext _execution;
    private readonly ILogger<RemoteAccessCoordinator> _logger;

    private readonly object _statusLock = new();
    private readonly SemaphoreSlim _tokenGate = new(1, 1);
    private readonly SemaphoreSlim _cloudGate = new(1, 1);
    private readonly SemaphoreSlim _commandGate = new(1, 1);
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    private RemoteAccessStatus _status = RemoteAccessStatus.Unlinked;
    private RemoteAccessSettings? _settings;
    private CancellationTokenSource? _lifetimeCts;
    private CancellationTokenSource? _cloudCts;
    private HubConnection? _hub;
    private LocalApiHost? _localApi;
    private Task? _collectorTask;
    private Task? _supervisorTask;
    private Task? _heartbeatTask;
    private Task? _eventPumpTask;
    private Task? _pendingSyncTask;
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt;
    private bool _started;
    private bool _disposed;

    public RemoteAccessCoordinator(
        IRemoteAccessSettingsStore settingsStore,
        CloudApiClient api,
        IEventOutbox outbox,
        CommandLedger ledger,
        IDesktopCommandHandler handler,
        DesktopEventCollector collector,
        IRemoteExecutionContext execution,
        ILogger<RemoteAccessCoordinator>? logger = null)
    {
        _settingsStore = settingsStore;
        _api = api;
        _outbox = outbox;
        _ledger = ledger;
        _handler = handler;
        _collector = collector;
        _execution = execution;
        _logger = logger ?? NullLogger<RemoteAccessCoordinator>.Instance;
    }

    public RemoteAccessStatus Status
    {
        get
        {
            lock (_statusLock)
                return _status;
        }
    }

    public event EventHandler<RemoteAccessStatus>? StatusChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_started)
            return;

        _started = true;
        _lifetimeCts = new CancellationTokenSource();

        try
        {
            _settings = await _settingsStore.LoadAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _settings = null;
            UpdateStatus(status => status with
            {
                IsLinked = false,
                IsEnabled = false,
                LastError = ex is RemoteAccessException
                    ? ex.Message
                    : "The saved remote-access configuration could not be read."
            });
        }

        var isLinked = _settings?.IsLinked == true;
        var isEnabled = _settings?.Enabled == true;

        UpdateStatus(status => status with
        {
            IsEnabled = isEnabled,
            IsLinked = isLinked,
            InstallationName = _settings?.InstallationName ?? string.Empty,
            CloudBaseUrl = _settings?.CloudBaseUrl ?? string.Empty
        });

        _localApi = new LocalApiHost(() => Status, _outbox);
        try
        {
            await _localApi.StartAsync(_lifetimeCts.Token);
            var url = _localApi.Url;
            UpdateStatus(status => status with
            {
                IsLocalApiRunning = url is not null,
                LocalApiUrl = url
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not start the local remote-access API.");
            UpdateStatus(status => status with
            {
                IsLocalApiRunning = false,
                LastError = "The local remote-access API could not start."
            });
        }

        _collectorTask = CollectorLoopAsync(_lifetimeCts.Token);
        _supervisorTask = SupervisorLoopAsync(_lifetimeCts.Token);

        if (isLinked && isEnabled)
        {
            try
            {
                await StartCloudAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not connect to the cloud service.");
                UpdateStatus(status => status with
                {
                    IsCloudConnected = false,
                    LastError = ex.Message
                });
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!_started)
            return;

        _started = false;
        _lifetimeCts?.Cancel();

        await StopCloudAsync(cancellationToken);

        if (_collectorTask is not null)
            await IgnoreCancellationAsync(_collectorTask);

        if (_supervisorTask is not null)
            await IgnoreCancellationAsync(_supervisorTask);

        if (_localApi is not null)
        {
            await _localApi.StopAsync(cancellationToken);
            _localApi = null;
        }

        _lifetimeCts?.Dispose();
        _lifetimeCts = null;

        UpdateStatus(status => status with
        {
            IsLocalApiRunning = false,
            IsCloudConnected = false,
            LocalApiUrl = null
        });
    }

    public async Task<RemoteLinkResult> LinkAsync(
        RemoteLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var cloudBaseUrl = CloudApiClient.NormalizeBaseUrl(request.CloudBaseUrl);
            var name = string.IsNullOrWhiteSpace(request.InstallationName)
                ? Environment.MachineName
                : request.InstallationName.Trim();

            if (string.IsNullOrWhiteSpace(request.ClaimCode))
                return new RemoteLinkResult(false, "Enter the claim code from the mobile app.", Status);

            if (_settings?.IsLinked == true)
                return new RemoteLinkResult(false, "This computer is already linked.", Status);

            var claim = await _api.ClaimAsync(
                cloudBaseUrl,
                new ClaimInstallationRequest(
                    request.ClaimCode.Trim(),
                    name,
                    Environment.MachineName,
                    AppVersion),
                cancellationToken);

            var settings = new RemoteAccessSettings
            {
                Enabled = true,
                CloudBaseUrl = cloudBaseUrl,
                InstallationId = claim.InstallationId,
                InstallationSecret = claim.InstallationSecret,
                TenantId = claim.TenantId,
                InstallationName = claim.InstallationName,
                LinkedAt = DateTimeOffset.UtcNow
            };

            await _settingsStore.SaveAsync(settings, cancellationToken);
            _settings = settings;
            _accessToken = null;

            UpdateStatus(status => status with
            {
                IsLinked = true,
                IsEnabled = true,
                InstallationName = settings.InstallationName,
                CloudBaseUrl = settings.CloudBaseUrl,
                LastError = null
            });

            await StartCloudAsync(cancellationToken);
            return new RemoteLinkResult(true, null, Status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is RemoteAccessException or HttpRequestException)
        {
            UpdateStatus(status => status with { LastError = ex.Message });
            return new RemoteLinkResult(false, ex.Message, Status);
        }
    }

    public async Task UnlinkAsync(CancellationToken cancellationToken = default)
    {
        await StopCloudAsync(cancellationToken);
        await _settingsStore.ClearAsync(cancellationToken);

        _settings = null;
        _accessToken = null;

        UpdateStatus(status => status with
        {
            IsLinked = false,
            IsEnabled = false,
            IsCloudConnected = false,
            InstallationName = string.Empty,
            CloudBaseUrl = string.Empty,
            LastConnectedAt = null,
            LastHeartbeatAt = null,
            LastError = null
        });
    }

    public async Task SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        if (_settings is null || !_settings.IsLinked)
            throw new RemoteAccessException("This computer is not linked to a cloud account.");

        _settings.Enabled = enabled;
        await _settingsStore.SaveAsync(_settings, cancellationToken);

        UpdateStatus(status => status with
        {
            IsEnabled = enabled,
            LastError = null
        });

        if (enabled)
            await StartCloudAsync(cancellationToken);
        else
            await StopCloudAsync(cancellationToken);
    }

    private async Task StartCloudAsync(CancellationToken cancellationToken)
    {
        await _cloudGate.WaitAsync(cancellationToken);
        try
        {
            if (_settings is null || !_settings.IsLinked || !_settings.Enabled)
                return;

            await StopCloudCoreAsync(cancellationToken);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(
                _lifetimeCts?.Token ?? CancellationToken.None);

            var hub = new HubConnectionBuilder()
                .WithUrl(
                    $"{_settings.CloudBaseUrl.TrimEnd('/')}/hubs/installation",
                    options =>
                    {
                        options.AccessTokenProvider = () => GetAccessTokenAsync(false, CancellationToken.None)!;
                    })
                .WithAutomaticReconnect(ReconnectDelays)
                .Build();

            hub.On<RemoteCommandDto>("CommandReceived", command => ProcessCommandAsync(command, CancellationToken.None));
            hub.Reconnecting += _ =>
            {
                UpdateStatus(status => status with { IsCloudConnected = false });
                return Task.CompletedTask;
            };
            hub.Reconnected += async _ =>
            {
                _accessToken = null;
                UpdateStatus(status => status with
                {
                    IsCloudConnected = true,
                    LastConnectedAt = DateTimeOffset.UtcNow,
                    LastError = null
                });

                await SyncPendingAsync();
            };
            hub.Closed += _ =>
            {
                UpdateStatus(status => status with
                {
                    IsCloudConnected = false,
                    LastError = "The cloud connection was lost."
                });
                return Task.CompletedTask;
            };

            await hub.StartAsync(cancellationToken);

            _hub = hub;
            _cloudCts = cts;
            _heartbeatTask = HeartbeatLoopAsync(cts.Token);
            _eventPumpTask = EventPumpLoopAsync(cts.Token);
            _pendingSyncTask = PendingSyncLoopAsync(cts.Token);

            UpdateStatus(status => status with
            {
                IsCloudConnected = true,
                LastConnectedAt = DateTimeOffset.UtcNow,
                LastError = null
            });

            _ = SyncPendingAsync();
        }
        finally
        {
            _cloudGate.Release();
        }
    }

    private async Task StopCloudAsync(CancellationToken cancellationToken)
    {
        await _cloudGate.WaitAsync(cancellationToken);
        try
        {
            await StopCloudCoreAsync(cancellationToken);
        }
        finally
        {
            _cloudGate.Release();
        }
    }

    private async Task StopCloudCoreAsync(CancellationToken cancellationToken)
    {
        _cloudCts?.Cancel();

        var tasks = new[] { _heartbeatTask, _eventPumpTask, _pendingSyncTask }
            .Where(task => task is not null)
            .Select(task => task!)
            .ToArray();

        if (tasks.Length > 0)
            await Task.WhenAll(tasks.Select(IgnoreCancellationAsync));

        _heartbeatTask = null;
        _eventPumpTask = null;
        _pendingSyncTask = null;

        if (_hub is not null)
        {
            try
            {
                await _hub.StopAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            finally
            {
                await _hub.DisposeAsync();
                _hub = null;
            }
        }

        _cloudCts?.Dispose();
        _cloudCts = null;

        UpdateStatus(status => status with { IsCloudConnected = false });
    }

    private async Task SupervisorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (_settings is null || !_settings.IsLinked || !_settings.Enabled)
                continue;

            if (_hub is not null && _hub.State is not HubConnectionState.Disconnected)
                continue;

            try
            {
                await StartCloudAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloud reconnect attempt failed.");
                UpdateStatus(status => status with { LastError = ex.Message });
            }
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_hub?.State == HubConnectionState.Connected)
                {
                    await _hub.InvokeAsync(
                        "Heartbeat",
                        new HeartbeatRequest(AppVersion),
                        cancellationToken);

                    UpdateStatus(status => status with
                    {
                        LastHeartbeatAt = DateTimeOffset.UtcNow
                    });
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cloud heartbeat failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task PendingSyncLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await SyncPendingAsync();
        }
    }

    private async Task SyncPendingAsync()
    {
        if (!await _syncGate.WaitAsync(0))
            return;

        try
        {
            if (_hub?.State != HubConnectionState.Connected)
                return;

            var commands = await _hub.InvokeAsync<IReadOnlyList<RemoteCommandDto>>(
                "GetPendingCommands");

            foreach (var command in commands)
                await ProcessCommandAsync(command, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch pending remote commands.");
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private async Task ProcessCommandAsync(
        RemoteCommandDto command,
        CancellationToken cancellationToken)
    {
        await _commandGate.WaitAsync(cancellationToken);
        try
        {
            var decision = await _ledger.TryBeginAsync(command.Id, cancellationToken);

            if (decision.IsInProgress)
                return;

            if (!decision.ShouldExecute)
            {
                await SendCompletionAsync(command, decision.TerminalEntry!, cancellationToken);
                return;
            }

            try
            {
                using var scope = _execution.BeginRemoteAdmin(
                    command.RequestedByUserId,
                    "Remote administrator");

                var response = await _handler.ExecuteAsync(
                    new CommandExecutionRequest(
                        command.Id,
                        command.Type,
                        command.PayloadJson,
                        command.RequestedByUserId,
                        "Remote administrator"),
                    cancellationToken);

                if (response.Success)
                {
                    await _ledger.CompleteAsync(
                        command.Id,
                        response.ResultJson ?? "null",
                        cancellationToken);
                }
                else
                {
                    await _ledger.FailAsync(
                        command.Id,
                        response.Error ?? "The command failed.",
                        cancellationToken);
                }

                await SendCompletionAsync(
                    command,
                    response.Success,
                    response.ResultJson,
                    response.Error,
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await _ledger.InterruptAsync(
                    command.Id,
                    "The desktop app stopped while this command was running. Verify the result manually.",
                    CancellationToken.None);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Remote command {CommandId} failed.", command.Id);
                await _ledger.FailAsync(
                    command.Id,
                    "The desktop app could not complete the command.",
                    CancellationToken.None);
                await SendCompletionAsync(
                    command,
                    false,
                    null,
                    "The desktop app could not complete the command.",
                    CancellationToken.None);
            }
        }
        finally
        {
            _commandGate.Release();
        }
    }

    private async Task SendCompletionAsync(
        RemoteCommandDto command,
        CommandLedgerEntry entry,
        CancellationToken cancellationToken)
    {
        var success = entry.Status == CommandLedgerStatus.Completed;
        await SendCompletionAsync(
            command,
            success,
            entry.ResponseJson,
            success ? null : entry.Error,
            cancellationToken);
    }

    private async Task SendCompletionAsync(
        RemoteCommandDto command,
        bool success,
        string? resultJson,
        string? error,
        CancellationToken cancellationToken)
    {
        if (_hub?.State != HubConnectionState.Connected)
            return;

        try
        {
            await _hub.InvokeAsync<CompleteCommandResponse>(
                "CompleteCommand",
                command.Id,
                new CompleteCommandRequest(success, resultJson, error),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not report completion for remote command {CommandId}.",
                command.Id);
        }
    }

    private async Task CollectorLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _collector.PollAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                await _collector.PollAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Event polling failed.");
            }
        }
    }

    private async Task EventPumpLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_settings is not null && _settings.IsLinked)
                {
                    var due = await _outbox.GetDueAsync(100, DateTimeOffset.UtcNow, cancellationToken);
                    if (due.Count > 0)
                    {
                        var token = await GetAccessTokenAsync(false, cancellationToken);
                        var request = new EventBatchRequest(due
                            .Select(item => new EventDto(
                                item.EventId,
                                item.Type,
                                item.PayloadJson,
                                item.OccurredAt))
                            .ToList());

                        await _api.SendEventsAsync(
                            _settings.CloudBaseUrl,
                            token,
                            request,
                            cancellationToken);

                        await _outbox.RemoveAsync(
                            due.Select(item => item.EventId).ToList(),
                            cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Event upload failed.");

                try
                {
                    var due = await _outbox.GetDueAsync(
                        100,
                        DateTimeOffset.UtcNow,
                        CancellationToken.None);

                    if (due.Count > 0)
                    {
                        var attempts = due.Max(item => item.Attempts) + 1;
                        var delaySeconds = Math.Min(300, 15 * (int)Math.Pow(2, Math.Min(attempts, 5)));
                        await _outbox.MarkFailedAsync(
                            due.Select(item => item.EventId).ToList(),
                            ex.Message,
                            DateTimeOffset.UtcNow.AddSeconds(delaySeconds),
                            CancellationToken.None);
                    }
                }
                catch (Exception markError)
                {
                    _logger.LogWarning(markError, "Could not update event retry state.");
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task<string> GetAccessTokenAsync(
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var settings = _settings;
        if (settings is null || !settings.IsLinked)
            throw new RemoteAccessException("This computer is not linked to a cloud account.");

        await _tokenGate.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh &&
                _accessToken is not null &&
                _accessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(2))
            {
                return _accessToken;
            }

            var response = await _api.GetTokenAsync(
                settings.CloudBaseUrl,
                new InstallationTokenRequest(
                    settings.InstallationId,
                    settings.InstallationSecret),
                cancellationToken);

            _accessToken = response.AccessToken;
            _accessTokenExpiresAt = response.ExpiresAt;
            return _accessToken;
        }
        finally
        {
            _tokenGate.Release();
        }
    }

    private static string AppVersion =>
        typeof(RemoteAccessCoordinator).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            .Split('+')[0]
        ?? "1.0.0";

    private void UpdateStatus(Func<RemoteAccessStatus, RemoteAccessStatus> update)
    {
        RemoteAccessStatus status;
        lock (_statusLock)
        {
            _status = update(_status);
            status = _status;
        }

        StatusChanged?.Invoke(this, status);
    }

    private static async Task IgnoreCancellationAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await StopAsync();
        _api.Dispose();
        _tokenGate.Dispose();
        _cloudGate.Dispose();
        _commandGate.Dispose();
        _syncGate.Dispose();
    }
}
