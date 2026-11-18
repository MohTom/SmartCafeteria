using SmartCafeteria.Licensing.Crypto;
using SmartCafeteria.Licensing.Hardware;
using SmartCafeteria.Licensing.Models;
using SmartCafeteria.Licensing.Service;
using SmartCafeteria.Licensing.State;
using Xunit;

namespace SmartCafeteria.Licensing.Tests;

public sealed class LicenseServiceTests
{
    [Fact]
    public async Task FirstRun_StartsFourteenDayTrial()
    {
        using var context = new LicenseTestContext();

        var status = await context.Service.GetStatusAsync();

        Assert.True(status.IsTrial);
        Assert.False(status.IsLocked);
        Assert.Equal(14, status.TrialDaysRemaining);
        Assert.True(File.Exists(context.StatePath));
    }

    [Fact]
    public async Task Trial_LocksAfterFourteenDays()
    {
        using var context = new LicenseTestContext();
        await context.Service.GetStatusAsync();

        context.Time.UtcNow = context.Time.UtcNow.AddDays(15);
        var status = await context.Service.GetStatusAsync();

        Assert.True(status.IsLocked);
        Assert.Equal(LockReason.TrialExpired, status.LockReason);
    }

    [Fact]
    public async Task ClockRollback_DoesNotExtendTrial()
    {
        using var context = new LicenseTestContext();
        await context.Service.GetStatusAsync();

        context.Time.UtcNow = context.Time.UtcNow.AddDays(10);
        var dayTen = await context.Service.GetStatusAsync();
        Assert.Equal(4, dayTen.TrialDaysRemaining);

        context.Time.UtcNow = context.Time.UtcNow.AddDays(-8);
        var rolledBack = await context.Service.GetStatusAsync();

        Assert.False(rolledBack.IsLocked);
        Assert.Equal(4, rolledBack.TrialDaysRemaining);
    }

    [Fact]
    public async Task MonthlyActivation_PersistsAndUnlocks()
    {
        using var context = new LicenseTestContext();
        var request = await context.Service.CreateRequestAsync(LicenseKind.Monthly);
        var code = context.Issuer.Issue(
            LicenseKind.Monthly,
            request.RequestedUtc,
            LicensePolicy.MonthlyExpiry(request.RequestedUtc),
            context.Hardware.IdBytes,
            request.Nonce);

        var result = await context.Service.ActivateAsync(code);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Status);
        Assert.Equal(LicenseKind.Monthly, result.Status!.Kind);
        Assert.False(result.Status.IsLocked);

        var reloaded = await context.CreateService().GetStatusAsync();
        Assert.Equal(LicenseKind.Monthly, reloaded.Kind);
        Assert.False(reloaded.IsLocked);
    }

    [Fact]
    public async Task ExpiredMonthlyLicense_IsLocked()
    {
        using var context = new LicenseTestContext();
        var request = await context.Service.CreateRequestAsync(LicenseKind.Monthly);
        var code = context.Issuer.Issue(
            LicenseKind.Monthly,
            request.RequestedUtc,
            request.RequestedUtc.AddDays(10),
            context.Hardware.IdBytes,
            request.Nonce);

        Assert.True((await context.Service.ActivateAsync(code)).Success);

        context.Time.UtcNow = context.Time.UtcNow.AddDays(11);
        var status = await context.Service.GetStatusAsync();

        Assert.True(status.IsLocked);
        Assert.Equal(LockReason.LicenseExpired, status.LockReason);
    }

    [Fact]
    public async Task PermanentActivation_RemainsUnlocked()
    {
        using var context = new LicenseTestContext();
        var request = await context.Service.CreateRequestAsync(LicenseKind.Permanent);
        var code = context.Issuer.Issue(
            LicenseKind.Permanent,
            request.RequestedUtc,
            null,
            context.Hardware.IdBytes,
            request.Nonce);

        var result = await context.Service.ActivateAsync(code);

        Assert.True(result.Success, result.Message);
        Assert.True(result.Status!.IsPermanent);

        context.Time.UtcNow = context.Time.UtcNow.AddYears(10);
        var status = await context.Service.GetStatusAsync();
        Assert.False(status.IsLocked);
        Assert.True(status.IsPermanent);
    }

    [Fact]
    public async Task MonthlyActivation_RejectsNonExtendingCode()
    {
        using var context = new LicenseTestContext();
        var firstRequest = await context.Service.CreateRequestAsync(LicenseKind.Monthly);
        var expiry = LicensePolicy.MonthlyExpiry(firstRequest.RequestedUtc);
        var firstCode = context.Issuer.Issue(
            LicenseKind.Monthly,
            firstRequest.RequestedUtc,
            expiry,
            context.Hardware.IdBytes,
            firstRequest.Nonce);

        Assert.True((await context.Service.ActivateAsync(firstCode)).Success);

        var secondRequest = await context.Service.CreateRequestAsync(LicenseKind.Monthly);
        var secondCode = context.Issuer.Issue(
            LicenseKind.Monthly,
            secondRequest.RequestedUtc,
            expiry,
            context.Hardware.IdBytes,
            secondRequest.Nonce);

        var result = await context.Service.ActivateAsync(secondCode);

        Assert.False(result.Success);
        Assert.Contains("يمدد", result.Message);
    }

    [Fact]
    public async Task TamperedState_LocksApplication()
    {
        using var context = new LicenseTestContext();
        await context.Service.GetStatusAsync();
        File.WriteAllText(context.StatePath, "tampered");

        var status = await context.Service.GetStatusAsync();

        Assert.True(status.IsLocked);
        Assert.Equal(LockReason.StateTampered, status.LockReason);
    }

    [Fact]
    public async Task InvalidActivationCode_ReturnsFailure()
    {
        using var context = new LicenseTestContext();

        var result = await context.Service.ActivateAsync("not-a-valid-code");

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.Message));
    }

    private sealed class LicenseTestContext : IDisposable
    {
        private readonly TempDirectory _directory = new();
        private readonly EcdsaFixture _keys = new();
        private readonly EncryptedLicenseStateStore _store;

        public LicenseTestContext()
        {
            Hardware = new FakeHardwareProvider().GetIdentity();
            HardwareProvider = new FakeHardwareProvider();
            Time = new MutableTimeProvider();
            _store = new EncryptedLicenseStateStore(
                HardwareProvider,
                TestLog.Create(),
                new[] { StatePath },
                includeRegistry: false);
            Issuer = new EcdsaLicenseIssuer(_keys.Key);
            Service = CreateService();
        }

        public FakeHardwareProvider HardwareProvider { get; }

        public HardwareIdentity Hardware { get; }

        public MutableTimeProvider Time { get; }

        public EcdsaLicenseIssuer Issuer { get; }

        public LicenseService Service { get; }

        public string StatePath => _directory.File("license.dat");

        public LicenseService CreateService() => new(
            HardwareProvider,
            _store,
            new EcdsaLicenseVerifier(new[] { _keys.PublicKey }),
            TestLog.Create(),
            Time);

        public void Dispose()
        {
            _keys.Dispose();
            _directory.Dispose();
        }
    }
}
