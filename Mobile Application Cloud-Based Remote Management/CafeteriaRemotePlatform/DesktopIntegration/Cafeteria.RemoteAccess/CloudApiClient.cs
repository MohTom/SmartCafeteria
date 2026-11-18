using System.Net.Http.Json;
using System.Text.Json;
using Cafeteria.Contracts;

namespace Cafeteria.RemoteAccess;

public sealed class CloudApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;

    public CloudApiClient(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SmartCafeteriaDesktop/1.0");
    }

    public async Task<ClaimInstallationResponse> ClaimAsync(
        string cloudBaseUrl,
        ClaimInstallationRequest request,
        CancellationToken cancellationToken)
    {
        return await PostAsync<ClaimInstallationRequest, ClaimInstallationResponse>(
            BuildUri(cloudBaseUrl, "api/installations/claim"),
            request,
            null,
            cancellationToken);
    }

    public async Task<InstallationTokenResponse> GetTokenAsync(
        string cloudBaseUrl,
        InstallationTokenRequest request,
        CancellationToken cancellationToken)
    {
        return await PostAsync<InstallationTokenRequest, InstallationTokenResponse>(
            BuildUri(cloudBaseUrl, "api/installations/token"),
            request,
            null,
            cancellationToken);
    }

    public async Task SendHeartbeatAsync(
        string cloudBaseUrl,
        string accessToken,
        HeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri(cloudBaseUrl, "api/installations/heartbeat"))
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        message.Headers.Authorization = new("Bearer", accessToken);

        using var response = await _http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateExceptionAsync(response, cancellationToken);
    }

    public async Task<EventBatchResponse> SendEventsAsync(
        string cloudBaseUrl,
        string accessToken,
        EventBatchRequest request,
        CancellationToken cancellationToken)
    {
        return await PostAsync<EventBatchRequest, EventBatchResponse>(
            BuildUri(cloudBaseUrl, "api/installations/events"),
            request,
            accessToken,
            cancellationToken);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(
        Uri uri,
        TRequest request,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };

        if (!string.IsNullOrWhiteSpace(accessToken))
            message.Headers.Authorization = new("Bearer", accessToken);

        using var response = await _http.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw await CreateExceptionAsync(response, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<TResponse>(
            JsonOptions,
            cancellationToken);

        return result ?? throw new RemoteAccessException(
            "The cloud service returned an empty response.");
    }

    private static Uri BuildUri(string cloudBaseUrl, string relativePath) =>
        new($"{NormalizeBaseUrl(cloudBaseUrl)}/{relativePath.TrimStart('/')}");

    public static string NormalizeBaseUrl(string cloudBaseUrl)
    {
        if (!Uri.TryCreate(cloudBaseUrl?.Trim(), UriKind.Absolute, out var uri))
            throw new RemoteAccessException("Enter a valid cloud service URL.");

        if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
            throw new RemoteAccessException("The cloud service URL must use HTTPS.");

        var isLoopback = uri.IsLoopback ||
            string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);

        if (uri.Scheme == Uri.UriSchemeHttp && !isLoopback)
            throw new RemoteAccessException("The cloud service URL must use HTTPS.");

        if (!string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new RemoteAccessException("The cloud service URL cannot contain a query or fragment.");

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
    }

    private static async Task<RemoteAccessException> CreateExceptionAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var fallback = $"The cloud service returned {(int)response.StatusCode} {response.ReasonPhrase}.";

        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);

            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                !string.IsNullOrWhiteSpace(message.GetString()))
            {
                return new RemoteAccessException(message.GetString()!);
            }
        }
        catch (JsonException)
        {
        }
        catch (HttpRequestException)
        {
        }

        return new RemoteAccessException(fallback);
    }

    public void Dispose() => _http.Dispose();
}
