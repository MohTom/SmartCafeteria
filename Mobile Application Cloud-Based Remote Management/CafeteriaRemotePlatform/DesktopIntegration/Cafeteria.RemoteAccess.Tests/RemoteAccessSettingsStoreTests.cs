using Cafeteria.RemoteAccess;
using Xunit;

namespace Cafeteria.RemoteAccess.Tests;

public sealed class RemoteAccessSettingsStoreTests
{
    [Fact]
    public async Task SaveLoadAndClear_RoundTripsSettings()
    {
        using var temp = new TempDirectory();
        var store = new RemoteAccessSettingsStore(
            temp.CreatePaths(),
            new PassThroughProtector(),
            applyAcl: false);

        var settings = new RemoteAccessSettings
        {
            Enabled = true,
            CloudBaseUrl = "https://cloud.example.com",
            InstallationId = Guid.NewGuid(),
            InstallationSecret = "secret-value",
            TenantId = Guid.NewGuid(),
            InstallationName = "Cafeteria PC",
            LinkedAt = DateTimeOffset.UtcNow
        };

        await store.SaveAsync(settings);
        var loaded = await store.LoadAsync();

        Assert.NotNull(loaded);
        Assert.Equal(settings.InstallationId, loaded!.InstallationId);
        Assert.Equal(settings.TenantId, loaded.TenantId);
        Assert.Equal(settings.InstallationSecret, loaded.InstallationSecret);
        Assert.Equal(settings.CloudBaseUrl, loaded.CloudBaseUrl);
        Assert.True(loaded.IsLinked);

        await store.ClearAsync();
        Assert.Null(await store.LoadAsync());
    }

    [Fact]
    public void DpapiSecretProtector_RoundTripsOnThisMachine()
    {
        var protector = new DpapiSecretProtector();
        var data = System.Text.Encoding.UTF8.GetBytes("smart-cafeteria");

        var protectedData = protector.Protect(data);
        var restored = protector.Unprotect(protectedData);

        Assert.Equal(data, restored);
        Assert.NotEqual(data, protectedData);
    }
}
