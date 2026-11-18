using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cafeteria.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cafeteria.Infrastructure;

public sealed class ExpoPushSender : IPushSender
{
    private const int MaxChunkSize = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly ExpoPushOptions _options;
    private readonly ILogger<ExpoPushSender> _logger;

    public ExpoPushSender(
        HttpClient httpClient,
        IOptions<ExpoPushOptions> options,
        ILogger<ExpoPushSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(
        IReadOnlyCollection<string> expoPushTokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        CancellationToken cancellationToken)
    {
        if (!_options.Enabled || expoPushTokens.Count == 0)
            return;

        var tokens = expoPushTokens
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        foreach (var chunk in tokens.Chunk(MaxChunkSize))
        {
            var messages = chunk.Select(token => new
            {
                to = token,
                title,
                body,
                data,
                sound = "default",
                priority = "high"
            });

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
                {
                    Content = JsonContent.Create(messages, options: JsonOptions)
                };
                request.Headers.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                if (!string.IsNullOrWhiteSpace(_options.AccessToken))
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", _options.AccessToken);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning(
                        "Expo push request failed with status {StatusCode}. Response: {Response}",
                        (int)response.StatusCode,
                        Truncate(responseBody, 500));
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogWarning(ex, "Expo push request failed.");
            }
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
