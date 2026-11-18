using Cafeteria.RemoteAccess;
using Xunit;

namespace Cafeteria.RemoteAccess.Tests;

public sealed class LocalApiHostTests
{
    [Fact]
    public async Task StartAsync_ServesHealthOnLoopback()
    {
        await using var host = new LocalApiHost(
            () => RemoteAccessStatus.Unlinked,
            new RecordingOutbox());

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await host.StartAsync(timeout.Token);

        Assert.False(string.IsNullOrWhiteSpace(host.Url));
        using var client = new HttpClient();
        var response = await client.GetAsync($"{host.Url}/local/health", timeout.Token);

        Assert.True(response.IsSuccessStatusCode);

        await host.StopAsync(timeout.Token);
    }
}
