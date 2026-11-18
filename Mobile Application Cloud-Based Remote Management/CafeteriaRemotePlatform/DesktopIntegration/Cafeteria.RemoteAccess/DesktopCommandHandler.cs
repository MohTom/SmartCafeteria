using System.Text.Json;
using Cafeteria.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartCafeteria.Core.Interfaces;

namespace Cafeteria.RemoteAccess;

public sealed class DesktopCommandHandler : IDesktopCommandHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IUserService _users;
    private readonly IProductService _products;
    private readonly IOrderService _orders;
    private readonly ISettingsService _settings;
    private readonly IAnalyticsService _analytics;
    private readonly ILogger<DesktopCommandHandler> _logger;

    public DesktopCommandHandler(
        IUserService users,
        IProductService products,
        IOrderService orders,
        ISettingsService settings,
        IAnalyticsService analytics,
        ILogger<DesktopCommandHandler>? logger = null)
    {
        _users = users;
        _products = products;
        _orders = orders;
        _settings = settings;
        _analytics = analytics;
        _logger = logger ?? NullLogger<DesktopCommandHandler>.Instance;
    }

    public async Task<CommandExecutionResponse> ExecuteAsync(
        CommandExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = request.Type switch
            {
                "dashboard.snapshot" => await DashboardSnapshotAsync(cancellationToken),
                "reports.sales" => await SalesReportAsync(request.PayloadJson, cancellationToken),
                "users.list" => await UsersListAsync(cancellationToken),
                "users.lock" => await SetUserActiveAsync(request.PayloadJson, false, cancellationToken),
                "users.unlock" => await SetUserActiveAsync(request.PayloadJson, true, cancellationToken),
                "inventory.list" => await InventoryListAsync(cancellationToken),
                "inventory.adjust" => await AdjustInventoryAsync(request.PayloadJson, cancellationToken),
                _ => Failure($"Command '{request.Type}' is not supported by this desktop version.")
            };

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            return Failure(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Failure(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return Failure("The desktop app denied this operation.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote command {CommandType} failed.", request.Type);
            return Failure("The desktop app could not complete the command.");
        }
    }

    private async Task<CommandExecutionResponse> DashboardSnapshotAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = await _settings.GetSettingsAsync();
        var stats = await _orders.GetDashboardStatsAsync(settings.LowStockThreshold);

        return Success(new
        {
            totalRevenue = stats.TotalRevenue,
            completedOrders = stats.CompletedOrders,
            lowStockCount = stats.LowStockCount,
            totalProducts = stats.TotalProducts,
            currency = settings.CurrencyEn,
            generatedAt = DateTimeOffset.UtcNow
        });
    }

    private async Task<CommandExecutionResponse> SalesReportAsync(
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var (start, end) = ParseRange(payloadJson);
        cancellationToken.ThrowIfCancellationRequested();
        var bundle = await _analytics.BuildAsync(start, end);
        return Success(bundle);
    }

    private async Task<CommandExecutionResponse> UsersListAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var users = await _users.GetAllUsersAsync();
        return Success(users.Select(user => new
        {
            id = user.Id,
            username = user.Username,
            displayName = user.DisplayName,
            role = user.Role.ToString(),
            isActive = user.IsActive,
            createdAt = user.CreatedAt
        }));
    }

    private async Task<CommandExecutionResponse> SetUserActiveAsync(
        string payloadJson,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var userId = ReadString(payloadJson, "userId");
        if (string.IsNullOrWhiteSpace(userId))
            return Failure("The command payload must include a userId.");

        cancellationToken.ThrowIfCancellationRequested();
        var updated = await _users.SetActiveAsync(userId, isActive);
        if (!updated)
            return Failure("User not found.");

        return Success(new { userId, isActive });
    }

    private async Task<CommandExecutionResponse> InventoryListAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var settings = await _settings.GetSettingsAsync();
        var products = await _products.GetAllProductsAsync();

        return Success(products.Select(product => new
        {
            id = product.Id,
            nameAr = product.NameAr,
            nameEn = product.NameEn,
            category = product.Category,
            inventory = product.Inventory,
            maxInventory = product.MaxInventory,
            lowStock = product.Inventory <= settings.LowStockThreshold
        }));
    }

    private async Task<CommandExecutionResponse> AdjustInventoryAsync(
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var productId = ReadString(payloadJson, "productId");
        var delta = ReadInt32(payloadJson, "delta");

        if (string.IsNullOrWhiteSpace(productId))
            return Failure("The command payload must include a productId.");

        if (delta == 0)
            return Failure("The inventory delta cannot be zero.");

        cancellationToken.ThrowIfCancellationRequested();
        var product = await _products.GetProductByIdAsync(productId);
        if (product is null)
            return Failure("Product not found.");

        var previous = product.Inventory;
        var next = previous + delta;
        if (next < 0)
            return Failure($"Insufficient stock. Current inventory is {previous}.");

        var updated = await _products.UpdateInventoryAsync(productId, -delta);
        if (!updated)
            return Failure("The inventory changed before this command ran. Try again.");

        return Success(new
        {
            productId,
            previousInventory = previous,
            inventory = next,
            delta
        });
    }

    private static (DateTime? Start, DateTime? End) ParseRange(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return (null, null);

        using var document = JsonDocument.Parse(payloadJson);
        var root = document.RootElement;

        DateTime? start = TryGetDateTime(root, "start");
        DateTime? end = TryGetDateTime(root, "end");

        if (start is not null && end is not null && start > end)
            throw new ArgumentException("The report start date must be before the end date.");

        return (start, end);
    }

    private static DateTime? TryGetDateTime(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) ||
            element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
            throw new ArgumentException($"'{propertyName}' must be an ISO-8601 date.");

        if (!DateTimeOffset.TryParse(element.GetString(), out var value))
            throw new ArgumentException($"'{propertyName}' must be an ISO-8601 date.");

        return value.LocalDateTime;
    }

    private static string ReadString(string payloadJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return string.Empty;

        using var document = JsonDocument.Parse(payloadJson);
        return document.RootElement.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static int ReadInt32(string payloadJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException($"The command payload must include {propertyName}.");

        using var document = JsonDocument.Parse(payloadJson);
        if (!document.RootElement.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var result))
        {
            throw new ArgumentException($"The command payload must include an integer {propertyName}.");
        }

        return result;
    }

    private static CommandExecutionResponse Success(object value) =>
        new(true, JsonSerializer.Serialize(value, JsonOptions), null);

    private static CommandExecutionResponse Failure(string error) =>
        new(false, null, error);
}
