using Cafeteria.Api.Authentication;
using Cafeteria.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Cafeteria.Api.Hubs;

[Authorize]
public sealed class UserHub : Hub
{
    private readonly IAppDbContext _db;

    public UserHub(IAppDbContext db)
    {
        _db = db;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.GetUserId();
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.User(userId));

        var tenantIds = await _db.TenantMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.IsActive && m.Tenant.IsActive)
            .Select(m => m.TenantId)
            .ToListAsync(Context.ConnectionAborted);

        foreach (var tenantId in tenantIds)
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupNames.Tenant(tenantId));

        await base.OnConnectedAsync();
    }
}

public static class GroupNames
{
    public static string User(Guid userId) => $"user:{userId}";
    public static string Tenant(Guid tenantId) => $"tenant:{tenantId}";
    public static string Installation(Guid installationId) => $"installation:{installationId}";
}
