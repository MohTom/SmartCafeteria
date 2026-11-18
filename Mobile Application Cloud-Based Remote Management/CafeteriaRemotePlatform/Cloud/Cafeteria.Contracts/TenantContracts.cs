namespace Cafeteria.Contracts;

public sealed record InviteMemberRequest(
    string Email,
    string Role,
    DateTimeOffset? ExpiresAt = null);

public sealed record InvitationCreated(
    Guid Id,
    string Email,
    string Role,
    string InviteCode,
    DateTimeOffset ExpiresAt);

public sealed record AcceptInvitationRequest(string InviteCode);

public sealed record TenantMemberDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt);
