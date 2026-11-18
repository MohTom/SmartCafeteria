namespace SmartCafeteria.Core.Validation;

/// <summary>Password rules shared by user creation and password changes. Existing
/// passwords stay valid until the user next changes them.</summary>
public static class PasswordPolicy
{
    public static bool IsValid(string? password) =>
        !string.IsNullOrEmpty(password);

    /// <summary>Arabic message suitable for the UI, or null when the password is valid.</summary>
    public static string? GetError(string? password) =>
        IsValid(password)
            ? null
            : "كلمة المرور مطلوبة";
}
