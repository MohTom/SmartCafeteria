using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cafeteria.RemoteAccess;

public sealed record LocalEventRequest(string Type, string PayloadJson);

public sealed class LocalApiHost : IAsyncDisposable
{
    private readonly Func<RemoteAccessStatus> _statusProvider;
    private readonly IEventOutbox _outbox;
    private readonly string _token;
    private WebApplication? _app;

    public LocalApiHost(
        Func<RemoteAccessStatus> statusProvider,
        IEventOutbox outbox)
    {
        _statusProvider = statusProvider;
        _outbox = outbox;
        _token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public string? Url { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_app is not null)
            return;

        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        var app = builder.Build();

        app.Use(async (context, next) =>
        {
            if (context.Request.Path == "/local/health")
            {
                await next();
                return;
            }

            if (!context.Request.Headers.TryGetValue("X-Local-Token", out var token) ||
                !string.Equals(token.ToString(), _token, StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next();
        });

        app.MapGet("/local/health", () => Results.Ok(new { status = "ok" }));

        app.MapGet("/local/status", () => Results.Ok(_statusProvider()));

        app.MapPost("/local/events", async (
            LocalEventRequest request,
            CancellationToken requestCancellation) =>
        {
            if (string.IsNullOrWhiteSpace(request.Type))
                return Results.BadRequest(new { error = "Event type is required." });

            await _outbox.EnqueueAsync(
                new[]
                {
                    new OutboxEvent
                    {
                        EventId = Guid.NewGuid().ToString("N"),
                        Type = request.Type,
                        PayloadJson = request.PayloadJson ?? "{}",
                        OccurredAt = DateTimeOffset.UtcNow
                    }
                },
                requestCancellation);

            return Results.Accepted();
        });

        await app.StartAsync(cancellationToken);

        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()
            ?.Addresses;

        Url = addresses?.FirstOrDefault();
        _app = app;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_app is null)
            return;

        try
        {
            await _app.StopAsync(cancellationToken);
        }
        finally
        {
            await _app.DisposeAsync();
            _app = null;
            Url = null;
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
