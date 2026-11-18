namespace SmartCafeteria.Licensing.Crypto;

public static class LicenseKeys
{
    public const string PublicKeyBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEbGEeD+yNY5hWtLmtuyF7I/kIcggXiYDez3q7AYklLvCDSbYpVb5aSNq231lkskFx9gHRIQNuSL2pDE679ISh9w==";

    public static IReadOnlyList<byte[]> GetVerificationKeys()
    {
#if DEBUG
        var overrideValue = Environment.GetEnvironmentVariable("SMART_CAFETERIA_LICENSE_PUBLIC_KEY");
        if (!string.IsNullOrWhiteSpace(overrideValue))
        {
            return overrideValue
                .Split(new[] { ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(Convert.FromBase64String)
                .ToArray();
        }
#endif

        return new[] { Convert.FromBase64String(PublicKeyBase64) };
    }
}
