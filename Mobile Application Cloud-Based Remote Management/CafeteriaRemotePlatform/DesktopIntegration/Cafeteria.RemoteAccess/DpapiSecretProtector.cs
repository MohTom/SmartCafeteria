using System.Security.Cryptography;
using System.Text;

namespace Cafeteria.RemoteAccess;

public sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes(
        "SmartCafeteria.RemoteAccess.v1");

    public byte[] Protect(byte[] data) =>
        ProtectedData.Protect(data, Entropy, DataProtectionScope.LocalMachine);

    public byte[] Unprotect(byte[] data) =>
        ProtectedData.Unprotect(data, Entropy, DataProtectionScope.LocalMachine);
}
