using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartCafeteria.Core.Enums;
using SmartCafeteria.Core.Interfaces;

namespace Cafeteria.RemoteAccess;

public sealed class DesktopEventCollector
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly IOrderService _orders;
    private readonly IUserService _users;
    private readonly IProductService _products;
    private readonly ISettingsService _settings;
    private readonly IEventOutbox _outbox;
    private readonly string _statePath;
    private readonly ILogger<DesktopEventCollector> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private EventCollectorState? _state;

    public DesktopEventCollector(
        IOrderService orders,
        IUserService users,
        IProductService products,
        ISettingsService settings,
        IEventOutbox outbox,
        string statePath,
        ILogger<DesktopEventCollector>? logger = null)
    {
        _orders = orders;
        _users = users;
        _products = products;
        _settings = settings;
        _outbox = outbox;
        _statePath = statePath;
        _logger = logger ?? NullLogger<DesktopEventCollector>.Instance;
    }

    public async Task PollAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var state = await LoadStateAsync(cancellationToken);
            var settings = await _settings.GetSettingsAsync();
            var orders = (await _orders.GetAllOrdersAsync()).ToList();
            var users = (await _users.GetAllUsersAsync()).ToList();
            var products = (await _products.GetAllProductsAsync()).ToList();
            var events = new List<OutboxEvent>();

            CollectOrders(orders, state, events);
            CollectUsers(users, state, events);
            CollectProducts(products, settings.LowStockThreshold, state, events);

            if (!state.Initialized)
            {
                state.Initialized = true;
                await SaveStateAsync(state, cancellationToken);
                return;
            }

            if (events.Count == 0)
                return;

            await _outbox.EnqueueAsync(events, cancellationToken);
            await SaveStateAsync(state, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Desktop event collection failed.");
        }
        finally
        {
            _gate.Release();
        }
    }

    private static void CollectOrders(
        IReadOnlyList<SmartCafeteria.Core.Models.Order> orders,
        EventCollectorState state,
        List<OutboxEvent> events)
    {
        foreach (var order in orders)
        {
            var key = $"order:{order.Id}";
            var value = order.Status.ToString();

            if (!state.Orders.TryGetValue(key, out var snapshot))
            {
                snapshot = new EntitySnapshot { Value = value, Version = 1 };
                state.Orders[key] = snapshot;

                if (state.Initialized)
                {
                    events.Add(CreateEvent(
                        "order.created",
                        order.Id,
                        snapshot.Version,
                        value,
                        new
                        {
                            orderId = order.Id,
                            code = order.Code,
                            total = order.Total,
                            status = order.Status.ToString(),
                            itemCount = order.Items.Sum(item => item.Quantity),
                            cashierName = order.CashierName,
                            timestamp = order.Timestamp
                        }));
                }

                continue;
            }

            if (string.Equals(snapshot.Value, value, StringComparison.Ordinal))
                continue;

            snapshot.Value = value;
            snapshot.Version++;

            if (order.Status == OrderStatus.Cancelled)
            {
                events.Add(CreateEvent(
                    "order.cancelled",
                    order.Id,
                    snapshot.Version,
                    value,
                    new
                    {
                        orderId = order.Id,
                        code = order.Code,
                        total = order.Total,
                        status = order.Status.ToString(),
                        timestamp = order.Timestamp
                    }));
            }
        }
    }

    private static void CollectUsers(
        IReadOnlyList<SmartCafeteria.Core.Models.User> users,
        EventCollectorState state,
        List<OutboxEvent> events)
    {
        foreach (var user in users)
        {
            var key = $"user:{user.Id}";
            var value = user.IsActive ? "active" : "inactive";

            if (!state.Users.TryGetValue(key, out var snapshot))
            {
                state.Users[key] = new EntitySnapshot { Value = value, Version = 1 };

                if (state.Initialized)
                {
                    events.Add(CreateEvent(
                        "user.created",
                        user.Id,
                        state.Users[key].Version,
                        value,
                        new
                        {
                            userId = user.Id,
                            username = user.Username,
                            displayName = user.DisplayName,
                            role = user.Role.ToString(),
                            isActive = user.IsActive
                        }));
                }

                continue;
            }

            if (string.Equals(snapshot.Value, value, StringComparison.Ordinal))
                continue;

            snapshot.Value = value;
            snapshot.Version++;

            events.Add(CreateEvent(
                "user.status_changed",
                user.Id,
                snapshot.Version,
                value,
                new
                {
                    userId = user.Id,
                    username = user.Username,
                    displayName = user.DisplayName,
                    role = user.Role.ToString(),
                    isActive = user.IsActive
                }));
        }
    }

    private static void CollectProducts(
        IReadOnlyList<SmartCafeteria.Core.Models.Product> products,
        int lowStockThreshold,
        EventCollectorState state,
        List<OutboxEvent> events)
    {
        foreach (var product in products)
        {
            var key = $"product:{product.Id}";
            var value = product.Inventory.ToString();

            if (!state.Products.TryGetValue(key, out var snapshot))
            {
                state.Products[key] = new EntitySnapshot { Value = value, Version = 1 };
                continue;
            }

            if (string.Equals(snapshot.Value, value, StringComparison.Ordinal))
                continue;

            var previous = int.TryParse(snapshot.Value, out var parsed) ? parsed : 0;
            snapshot.Value = value;
            snapshot.Version++;

            events.Add(CreateEvent(
                "inventory.changed",
                product.Id,
                snapshot.Version,
                value,
                new
                {
                    productId = product.Id,
                    nameAr = product.NameAr,
                    nameEn = product.NameEn,
                    previousInventory = previous,
                    inventory = product.Inventory,
                    lowStock = product.Inventory <= lowStockThreshold
                }));
        }
    }

    private static OutboxEvent CreateEvent(
        string type,
        string entityId,
        int version,
        string value,
        object payload)
    {
        return new OutboxEvent
        {
            EventId = CreateEventId(type, entityId, version, value),
            Type = type,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private static string CreateEventId(
        string type,
        string entityId,
        int version,
        string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{type}|{entityId}|{version}|{value}"));

        return Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
    }

    private async Task<EventCollectorState> LoadStateAsync(
        CancellationToken cancellationToken)
    {
        if (_state is not null)
            return _state;

        if (!File.Exists(_statePath))
        {
            _state = new EventCollectorState();
            return _state;
        }

        try
        {
            var json = await File.ReadAllTextAsync(_statePath, cancellationToken);
            _state = JsonSerializer.Deserialize<EventCollectorState>(json, JsonOptions)
                ?? new EventCollectorState();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Event collector state was invalid; reseeding.");
            _state = new EventCollectorState();
        }

        return _state;
    }

    private async Task SaveStateAsync(
        EventCollectorState state,
        CancellationToken cancellationToken)
    {
        RemoteStateDirectory.Ensure(Path.GetDirectoryName(_statePath)!);
        var json = JsonSerializer.Serialize(state, JsonOptions);
        var tempPath = _statePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, _statePath, true);
    }

    private sealed class EventCollectorState
    {
        public bool Initialized { get; set; }
        public Dictionary<string, EntitySnapshot> Orders { get; set; } = new();
        public Dictionary<string, EntitySnapshot> Users { get; set; } = new();
        public Dictionary<string, EntitySnapshot> Products { get; set; } = new();
    }

    private sealed class EntitySnapshot
    {
        public string Value { get; set; } = string.Empty;
        public int Version { get; set; }
    }
}
