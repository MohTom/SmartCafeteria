using System.Diagnostics;
using System.IO;
using Xunit;

namespace SmartCafeteria.UI.Tests;

public sealed class TestAppProcessGuardTests
{
    private const string TestOutputRoot = @"C:\repo\tests\SmartCafeteria.UI.Tests\bin\Debug\net10.0-windows";

    [Fact]
    public void Release_TrackedRunningProcess_KillsEntireProcessTreeOnce()
    {
        var (guard, manager, _) = CreateGuard();
        var process = NewProcess(4242, manager, Path.Combine(TestOutputRoot, "SmartCafeteria.exe"));
        guard.Track(process);

        guard.Release(4242);

        var call = Assert.Single(manager.KillCalls);
        Assert.Equal(4242, call.ProcessId);
        Assert.True(call.EntireProcessTree);
        Assert.False(guard.IsTracked(4242));
    }

    [Fact]
    public void Release_ExitedProcess_DoesNotKillAndDoesNotThrow()
    {
        var (guard, manager, _) = CreateGuard();
        var process = NewProcess(4243, manager, Path.Combine(TestOutputRoot, "SmartCafeteria.exe"), hasExited: true);
        guard.Track(process);

        var error = Record.Exception(() => guard.Release(4243));

        Assert.Null(error);
        Assert.Empty(manager.KillCalls);
        Assert.False(guard.IsTracked(4243));
    }

    [Fact]
    public void Release_UntrackedProcessId_KillsByProcessId()
    {
        var (guard, manager, _) = CreateGuard();
        manager.Processes.Add(NewProcess(4245, manager, Path.Combine(TestOutputRoot, "SmartCafeteria.exe")));

        guard.Release(4245);

        var call = Assert.Single(manager.KillCalls);
        Assert.Equal(4245, call.ProcessId);
        Assert.True(call.EntireProcessTree);
    }

    [Fact]
    public void Release_IsIdempotent_KillsTrackedProcessOnlyOnce()
    {
        var (guard, manager, _) = CreateGuard();
        guard.Track(NewProcess(4244, manager, Path.Combine(TestOutputRoot, "SmartCafeteria.exe")));

        guard.Release(4244);
        guard.Release(4244);

        Assert.Single(manager.KillCalls);
    }

    [Fact]
    public void Dispose_Guard_ForceKillsAllTrackedRunningProcesses()
    {
        var (guard, manager, _) = CreateGuard();
        guard.Track(NewProcess(5001, manager, Path.Combine(TestOutputRoot, "SmartCafeteria.exe")));
        guard.Track(NewProcess(5002, manager, Path.Combine(TestOutputRoot, "SmartCafeteria.exe")));
        guard.Track(NewProcess(5003, manager, Path.Combine(TestOutputRoot, "SmartCafeteria.exe"), hasExited: true));

        guard.Dispose();

        Assert.Equal(2, manager.KillCalls.Count);
        Assert.Contains(manager.KillCalls, x => x.ProcessId == 5001 && x.EntireProcessTree);
        Assert.Contains(manager.KillCalls, x => x.ProcessId == 5002 && x.EntireProcessTree);
        Assert.DoesNotContain(manager.KillCalls, x => x.ProcessId == 5003);
    }

    [Fact]
    public void ProcessExit_WhenRaised_KillsTrackedRunningProcesses()
    {
        var (guard, manager, lifetime) = CreateGuard();
        guard.Track(NewProcess(6001, manager, Path.Combine(TestOutputRoot, "SmartCafeteria.exe")));

        lifetime.RaiseProcessExit();

        var call = Assert.Single(manager.KillCalls);
        Assert.Equal(6001, call.ProcessId);
        Assert.True(call.EntireProcessTree);
    }

    [Fact]
    public void KillStaleTestOutputProcesses_KillsOnlyMatchingExeUnderTestOutputRoot()
    {
        var (guard, manager, _) = CreateGuard();
        manager.CurrentProcessId = 9999;
        manager.Processes.Add(NewProcess(7001, manager, Path.Combine(TestOutputRoot, "SmartCafeteria.exe")));
        manager.Processes.Add(NewProcess(7002, manager, @"C:\Program Files\SmartCafeteria\SmartCafeteria.exe"));
        manager.Processes.Add(NewProcess(7003, manager, Path.Combine(TestOutputRoot, "OtherApp.exe")));
        manager.Processes.Add(NewProcess(9999, manager, Path.Combine(TestOutputRoot, "testhost.exe")));

        guard.KillStaleTestOutputProcesses();

        var call = Assert.Single(manager.KillCalls);
        Assert.Equal(7001, call.ProcessId);
        Assert.True(call.EntireProcessTree);
    }

    [Fact]
    public void KillStaleTestOutputProcesses_SkipsTrackedCurrentRunProcess()
    {
        var (guard, manager, _) = CreateGuard();
        var process = NewProcess(7004, manager, Path.Combine(TestOutputRoot, "SmartCafeteria.exe"));
        guard.Track(process);
        manager.Processes.Add(process);

        guard.KillStaleTestOutputProcesses();

        Assert.Empty(manager.KillCalls);
    }

    [Fact]
    public void KillStaleTestOutputProcesses_WhenKillThrows_ContinuesAndReportsNoUnhandledException()
    {
        var (guard, manager, _) = CreateGuard();
        manager.Processes.Add(NewProcess(7101, manager, Path.Combine(TestOutputRoot, "SmartCafeteria.exe"), throwOnKill: true));
        manager.Processes.Add(NewProcess(7102, manager, Path.Combine(TestOutputRoot, "SmartCafeteria.exe")));

        var error = Record.Exception(() => guard.KillStaleTestOutputProcesses());

        Assert.Null(error);
        Assert.Equal(2, manager.KillCalls.Count);
        Assert.Contains(manager.KillCalls, x => x.ProcessId == 7101);
        Assert.Contains(manager.KillCalls, x => x.ProcessId == 7102);
    }

    private static (TestAppProcessGuard Guard, FakeProcessManager Manager, FakeLifetime Lifetime) CreateGuard()
    {
        var manager = new FakeProcessManager();
        var lifetime = new FakeLifetime();
        return (new TestAppProcessGuard(manager, lifetime, TestOutputRoot), manager, lifetime);
    }

    private static FakeProcess NewProcess(
        int id,
        FakeProcessManager manager,
        string executablePath,
        bool hasExited = false,
        bool throwOnKill = false)
    {
        return new FakeProcess(id, manager, executablePath, hasExited, throwOnKill);
    }

    private sealed class FakeProcessManager : IProcessManager
    {
        public List<FakeProcess> Processes { get; } = new();
        public List<KillCall> KillCalls { get; } = new();
        public int CurrentProcessId { get; set; } = 9998;

        public IManagedProcess? Wrap(Process process) => throw new NotSupportedException();

        public IManagedProcess? GetProcessById(int processId) =>
            Processes.FirstOrDefault(x => x.Id == processId);

        public IReadOnlyList<IManagedProcess> GetProcessesByName(string processName) =>
            Processes.Cast<IManagedProcess>().ToList();
    }

    private sealed class FakeLifetime : IProcessLifetime
    {
        private EventHandler? _handler;

        public event EventHandler? ProcessExit
        {
            add => _handler += value;
            remove => _handler -= value;
        }

        public void Unsubscribe(EventHandler handler) => _handler -= handler;

        public void RaiseProcessExit() => _handler?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeProcess : IManagedProcess
    {
        private readonly FakeProcessManager _manager;

        public FakeProcess(
            int id,
            FakeProcessManager manager,
            string executablePath,
            bool hasExited,
            bool throwOnKill)
        {
            Id = id;
            _manager = manager;
            ExecutablePath = executablePath;
            HasExited = hasExited;
            ThrowOnKill = throwOnKill;
        }

        public int Id { get; }
        public bool HasExited { get; private set; }
        public string? ExecutablePath { get; }
        public bool ThrowOnKill { get; }

        public void Kill(bool entireProcessTree)
        {
            _manager.KillCalls.Add(new KillCall(Id, entireProcessTree));
            if (ThrowOnKill)
                throw new InvalidOperationException();
            HasExited = true;
        }

        public void WaitForExit(int milliseconds) { }

        public void Dispose() { }
    }

    private readonly record struct KillCall(int ProcessId, bool EntireProcessTree);
}
