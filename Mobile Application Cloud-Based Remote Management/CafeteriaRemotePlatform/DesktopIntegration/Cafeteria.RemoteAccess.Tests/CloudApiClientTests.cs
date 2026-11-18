using Cafeteria.RemoteAccess;
using Xunit;

namespace Cafeteria.RemoteAccess.Tests;

public sealed class CloudApiClientTests
{
    [Theory]
    [InlineData("https://cloud.example.com/", "https://cloud.example.com")]
    [InlineData("https://cloud.example.com/base/", "https://cloud.example.com/base")]
    [InlineData("http://localhost:5000/", "http://localhost:5000")]
    [InlineData("http://127.0.0.1:5000/", "http://127.0.0.1:5000")]
    public void NormalizeBaseUrl_AcceptsValidUrls(string input, string expected)
    {
        Assert.Equal(expected, CloudApiClient.NormalizeBaseUrl(input));
    }

    [Theory]
    [InlineData("http://cloud.example.com")]
    [InlineData("ftp://cloud.example.com")]
    [InlineData("not-a-url")]
    [InlineData("https://cloud.example.com?x=1")]
    public void NormalizeBaseUrl_RejectsInvalidUrls(string input)
    {
        Assert.Throws<RemoteAccessException>(() => CloudApiClient.NormalizeBaseUrl(input));
    }
}
