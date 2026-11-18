namespace Cafeteria.Domain;

public enum TenantRole
{
    Owner = 0,
    Admin = 1,
    Manager = 2,
    Viewer = 3
}

public enum CommandStatus
{
    Pending = 0,
    Dispatched = 1,
    Executing = 2,
    Completed = 3,
    Failed = 4,
    Expired = 5,
    Cancelled = 6
}

public enum AccessLevel
{
    View = 0,
    Manage = 1
}
