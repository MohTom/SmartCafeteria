using Cafeteria.Domain;

namespace Cafeteria.Application;

public static class Permissions
{
    public const string ReportsRead = "reports.read";
    public const string UsersManage = "users.manage";
    public const string InventoryAdjust = "inventory.adjust";
    public const string InstallationRead = "installation.read";
}

public sealed record CommandDefinition(
    string Type,
    string Permission,
    bool RequiresOnline,
    bool QueueWhenOffline,
    TimeSpan Lifetime);

public static class CommandCatalog
{
    private static readonly Dictionary<string, CommandDefinition> Definitions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["dashboard.snapshot"] = new(
                "dashboard.snapshot", Permissions.ReportsRead, true, false, TimeSpan.FromMinutes(2)),
            ["reports.sales"] = new(
                "reports.sales", Permissions.ReportsRead, true, false, TimeSpan.FromMinutes(5)),
            ["users.list"] = new(
                "users.list", Permissions.UsersManage, true, false, TimeSpan.FromMinutes(2)),
            ["users.lock"] = new(
                "users.lock", Permissions.UsersManage, true, false, TimeSpan.FromMinutes(5)),
            ["users.unlock"] = new(
                "users.unlock", Permissions.UsersManage, true, false, TimeSpan.FromMinutes(5)),
            ["inventory.list"] = new(
                "inventory.list", Permissions.ReportsRead, true, false, TimeSpan.FromMinutes(2)),
            ["inventory.adjust"] = new(
                "inventory.adjust", Permissions.InventoryAdjust, false, true, TimeSpan.FromMinutes(30))
        };

    public static CommandDefinition? Find(string type) =>
        Definitions.TryGetValue(type, out var definition) ? definition : null;

    public static IReadOnlyCollection<CommandDefinition> All => Definitions.Values;
}

public static class RolePermissions
{
    public static bool Allows(TenantRole role, string permission)
    {
        if (role is TenantRole.Owner or TenantRole.Admin)
            return true;

        return permission switch
        {
            Permissions.ReportsRead => role is TenantRole.Manager or TenantRole.Viewer,
            Permissions.UsersManage => role is TenantRole.Manager,
            Permissions.InventoryAdjust => role is TenantRole.Manager,
            Permissions.InstallationRead => true,
            _ => false
        };
    }
}
