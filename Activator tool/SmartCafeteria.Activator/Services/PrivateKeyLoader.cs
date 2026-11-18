using System.IO;
using System.Security.Cryptography;

namespace SmartCafeteria.Activator.Services;

public static class PrivateKeyLoader
{
    public static ECDsa? Load(string path, string password, out string error)
    {
        error = string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = "لم يتم العثور على ملف المفتاح الخاص.";
                return null;
            }

            if (string.IsNullOrEmpty(password))
            {
                error = "أدخل كلمة مرور المفتاح الخاص.";
                return null;
            }

            var encoded = File.ReadAllText(path).Trim();
            var bytes = Convert.FromBase64String(encoded);
            var key = ECDsa.Create();
            key.ImportEncryptedPkcs8PrivateKey(password, bytes, out _);
            return key;
        }
        catch (Exception exception)
        {
            error = $"تعذر فتح المفتاح الخاص: {exception.Message}";
            return null;
        }
    }
}
