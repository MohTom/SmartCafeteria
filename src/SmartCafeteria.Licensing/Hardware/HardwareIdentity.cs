using System.Text;

namespace SmartCafeteria.Licensing.Hardware;

public sealed record HardwareIdentity(
    byte[] IdBytes,
    string DeviceId,
    string MachineGuid,
    string MacAddress,
    byte[] MacBytes);

public interface IHardwareIdProvider
{
    HardwareIdentity GetIdentity();
}
