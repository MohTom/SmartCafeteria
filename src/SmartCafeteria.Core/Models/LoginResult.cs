namespace SmartCafeteria.Core.Models;

public enum LoginFailureReason
{
    None = 0,
    InvalidCredentials = 1,
    LockedOut = 2,
    Inactive = 3
}

/// <summary>Outcome of an authentication attempt, including the reason for a
/// failure so the UI can report a lockout with the remaining time.</summary>
public sealed class LoginResult
{
    public User? User { get; init; }
    public LoginFailureReason FailureReason { get; init; }
    public TimeSpan LockoutRemaining { get; init; }

    public bool Succeeded => User is not null;

    public static LoginResult Success(User user) => new() { User = user };

    public static LoginResult Failed(LoginFailureReason reason, TimeSpan lockoutRemaining = default)
        => new() { FailureReason = reason, LockoutRemaining = lockoutRemaining };
}
