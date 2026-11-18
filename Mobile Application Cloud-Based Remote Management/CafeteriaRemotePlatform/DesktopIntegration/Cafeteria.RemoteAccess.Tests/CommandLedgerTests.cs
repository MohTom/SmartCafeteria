using Cafeteria.RemoteAccess;
using Xunit;

namespace Cafeteria.RemoteAccess.Tests;

public sealed class CommandLedgerTests
{
    [Fact]
    public async Task TryBegin_ExecutesOnce_AndReportsCompletion()
    {
        using var temp = new TempDirectory();
        var ledger = new CommandLedger(temp.CreatePaths().CommandLedgerFile);
        var commandId = Guid.NewGuid();

        var first = await ledger.TryBeginAsync(commandId);
        var second = await ledger.TryBeginAsync(commandId);

        Assert.True(first.ShouldExecute);
        Assert.True(second.IsInProgress);

        await ledger.CompleteAsync(commandId, "{\"ok\":true}");

        var afterCompletion = await ledger.TryBeginAsync(commandId);
        Assert.False(afterCompletion.ShouldExecute);
        Assert.Equal(CommandLedgerStatus.Completed, afterCompletion.TerminalEntry!.Status);
        Assert.Equal("{\"ok\":true}", afterCompletion.TerminalEntry.ResponseJson);
    }

    [Fact]
    public async Task TryBegin_AfterRestart_ReportsInterrupted()
    {
        using var temp = new TempDirectory();
        var path = temp.CreatePaths().CommandLedgerFile;
        var commandId = Guid.NewGuid();

        var firstProcess = new CommandLedger(path);
        var decision = await firstProcess.TryBeginAsync(commandId);
        Assert.True(decision.ShouldExecute);

        var secondProcess = new CommandLedger(path);
        var recovered = await secondProcess.TryBeginAsync(commandId);

        Assert.False(recovered.ShouldExecute);
        Assert.False(recovered.IsInProgress);
        Assert.Equal(CommandLedgerStatus.Interrupted, recovered.TerminalEntry!.Status);
    }

    [Fact]
    public async Task FailAsync_PersistsFailureMessage()
    {
        using var temp = new TempDirectory();
        var ledger = new CommandLedger(temp.CreatePaths().CommandLedgerFile);
        var commandId = Guid.NewGuid();

        await ledger.TryBeginAsync(commandId);
        await ledger.FailAsync(commandId, "Product not found.");

        var decision = await ledger.TryBeginAsync(commandId);
        Assert.Equal(CommandLedgerStatus.Failed, decision.TerminalEntry!.Status);
        Assert.Equal("Product not found.", decision.TerminalEntry.Error);
    }
}
