using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using FlaUI.Core;

namespace SmartCafeteria.UI.Tests;

internal interface IManagedProcess : IDisposable
{
    int Id { get; }
    bool HasExited { get; }
    string? ExecutablePath { get; }
    void Kill(bool entireProcessTree);
    void WaitForExit(int milliseconds);
}

internal interface IProcessManager
{
    int CurrentProcessId { get; }
    IManagedProcess? Wrap(Process process);
    IManagedProcess? GetProcessById(int processId);
    IReadOnlyList<IManagedProcess> GetProcessesByName(string processName);
}

internal interface IProcessLifetime
{
    event EventHandler? ProcessExit;
    void Unsubscribe(EventHandler handler);
}

internal sealed class SystemProcessManager : IProcessManager
{
    public int CurrentProcessId => Environment.ProcessId;

    public IManagedProcess? Wrap(Process process)
    {
        try
        {
            return process is null ? null : new SystemManagedProcess(process);
        }
        catch
        {
            return null;
        }
    }

    public IManagedProcess? GetProcessById(int processId)
    {
        try
        {
            return Wrap(Process.GetProcessById(processId));
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<IManagedProcess> GetProcessesByName(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName)
                .Select(Wrap)
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList();
        }
        catch
        {
            return Array.Empty<IManagedProcess>();
        }
    }
}

internal sealed class SystemManagedProcess : IManagedProcess
{
    private readonly Process _process;

    public SystemManagedProcess(Process process)
    {
        _process = process;
    }

    public int Id => _process.Id;

    public bool HasExited
    {
        get
        {
            try { return _process.HasExited; }
            catch { return false; }
        }
    }

    public string? ExecutablePath
    {
        get
        {
            try { return _process.MainModule?.FileName; }
            catch { return null; }
        }
    }

    public void Kill(bool entireProcessTree)
    {
        try { _process.Kill(entireProcessTree); }
        catch (InvalidOperationException) { }
        catch (NotSupportedException) { }
    }

    public void WaitForExit(int milliseconds)
    {
        try { _process.WaitForExit(milliseconds); }
        catch { }
    }

    public void Dispose()
    {
        try { _process.Dispose(); }
        catch { }
    }
}

internal sealed class SystemProcessLifetime : IProcessLifetime
{
    public event EventHandler? ProcessExit
    {
        add => AppDomain.CurrentDomain.ProcessExit += value;
        remove => AppDomain.CurrentDomain.ProcessExit -= value;
    }

    public void Unsubscribe(EventHandler handler)
    {
        AppDomain.CurrentDomain.ProcessExit -= handler;
    }
}

internal sealed class TestAppProcessGuard : IDisposable
{
    private const string AppProcessName = "SmartCafeteria";
    private const string AppExeName = "SmartCafeteria.exe";

    private static readonly TestAppProcessGuard Shared = new(
        new SystemProcessManager(),
        new SystemProcessLifetime(),
        AppContext.BaseDirectory,
        killStaleOnCreate: true);

    private readonly IProcessManager _processes;
    private readonly IProcessLifetime _lifetime;
    private readonly string _testOutputRoot;
    private readonly ConcurrentDictionary<int, IManagedProcess> _tracked = new();
    private readonly EventHandler _exitHandler;
    private bool _disposed;

    internal static TestAppProcessGuard Instance => Shared;

    internal TestAppProcessGuard(
        IProcessManager processes,
        IProcessLifetime lifetime,
        string testOutputRoot,
        bool killStaleOnCreate = false)
    {
        _processes = processes;
        _lifetime = lifetime;
        _testOutputRoot = testOutputRoot;
        _exitHandler = (_, _) => ReleaseAll();
        _lifetime.ProcessExit += _exitHandler;

        if (killStaleOnCreate)
            KillStaleTestOutputProcesses();
    }

    internal Application Launch(string exePath)
    {
        var app = Application.Launch(exePath);
        try
        {
            var process = _processes.Wrap(Process.GetProcessById(app.ProcessId));
            if (process is not null)
                Track(process);
        }
        catch { }
        return app;
    }

    internal void Track(IManagedProcess process)
    {
        _tracked[process.Id] = process;
    }

    internal bool IsTracked(int processId) => _tracked.ContainsKey(processId);

    internal void Release(Application? app)
    {
        if (app is null)
            return;

        try
        {
            Release(app.ProcessId);
        }
        catch { }

        try { app.Dispose(); }
        catch { }
    }

    internal void Release(int processId)
    {
        if (_tracked.TryRemove(processId, out var process))
        {
            KillProcess(process);
            return;
        }

        var fallback = _processes.GetProcessById(processId);
        if (fallback is not null)
            KillProcess(fallback);
    }

    internal void ReleaseAll()
    {
        foreach (var processId in _tracked.Keys.ToArray())
            Release(processId);
    }

    internal void KillStaleTestOutputProcesses()
    {
        var root = NormalizeRoot(_testOutputRoot);
        foreach (var process in _processes.GetProcessesByName(AppProcessName))
        {
            try
            {
                if (process.Id == _processes.CurrentProcessId || _tracked.ContainsKey(process.Id))
                    continue;

                var path = process.ExecutablePath;
                if (string.IsNullOrWhiteSpace(path))
                    continue;

                var fullPath = Path.GetFullPath(path);
                if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.Equals(Path.GetFileName(fullPath), AppExeName, StringComparison.OrdinalIgnoreCase))
                    continue;

                KillProcess(process);
            }
            catch { }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _lifetime.Unsubscribe(_exitHandler);
        ReleaseAll();
    }

    private static string NormalizeRoot(string root)
    {
        var fullPath = Path.GetFullPath(root);
        return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }

    private static void KillProcess(IManagedProcess process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch { }
        finally
        {
            process.Dispose();
        }
    }
}

internal static class TestAppProcessGuardInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        _ = TestAppProcessGuard.Instance;
    }
}
