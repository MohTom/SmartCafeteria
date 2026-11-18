using System.Threading.RateLimiting;
using Cafeteria.Api.Authentication;
using Cafeteria.Api.BackgroundServices;
using Cafeteria.Api.Endpoints;
using Cafeteria.Api.Errors;
using Cafeteria.Api.Hubs;
using Cafeteria.Api.Realtime;
using Cafeteria.Application;
using Cafeteria.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCafeteriaInfrastructure(builder.Configuration);

var jwt = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? throw new InvalidOperationException("JWT configuration is required.");

builder.Services.AddCafeteriaAuthentication(jwt);
builder.Services.AddSignalR();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IRealtimePublisher, SignalRRealtimePublisher>();
builder.Services.AddSingleton<IInstallationCommandPusher, SignalRInstallationCommandPusher>();
builder.Services.AddHostedService<CommandDispatcherService>();
builder.Services.AddHostedService<CommandExpiryService>();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
        else if (builder.Environment.IsDevelopment())
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
        limiter.AutoReplenishment = true;
    });
});

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapHub<UserHub>("/hubs/user");
app.MapHub<InstallationHub>("/hubs/installation");

app.MapAuthEndpoints();
app.MapTenantEndpoints();
app.MapInstallationEndpoints();
app.MapCommandEndpoints();
app.MapNotificationEndpoints();

if (app.Configuration.GetValue("Database:AutoMigrate", true))
    await DatabaseInitializer.InitializeAsync(app.Services);

app.Run();

public partial class Program
{
}
