using System.Security.Cryptography;

var outDirectory = GetArgument("--out") ?? Path.Combine(Environment.CurrentDirectory, "Activator tool", "SmartCafeteria.Activator", "keys");
var password = Environment.GetEnvironmentVariable("SMART_CAFETERIA_KEY_PASSWORD") ?? GetArgument("--password");

if (string.IsNullOrWhiteSpace(password))
{
    Console.Error.WriteLine("Set SMART_CAFETERIA_KEY_PASSWORD or pass --password.");
    return 1;
}

Directory.CreateDirectory(outDirectory);

using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
var publicKey = key.ExportSubjectPublicKeyInfo();
var privateKey = key.ExportEncryptedPkcs8PrivateKey(
    password,
    new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000));

var privatePath = Path.Combine(outDirectory, "activator-private-key.pk8");
var publicPath = Path.Combine(outDirectory, "license-public-key.spki");

File.WriteAllText(privatePath, Convert.ToBase64String(privateKey));
File.WriteAllText(publicPath, Convert.ToBase64String(publicKey));

var fingerprint = Convert.ToHexString(SHA256.HashData(publicKey));
Console.WriteLine($"Private key: {privatePath}");
Console.WriteLine($"Public key:  {publicPath}");
Console.WriteLine($"Public key Base64: {Convert.ToBase64String(publicKey)}");
Console.WriteLine($"Fingerprint: {fingerprint}");
return 0;

string? GetArgument(string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    }

    return null;
}
