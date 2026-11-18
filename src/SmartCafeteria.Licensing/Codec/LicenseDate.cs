using System.Buffers.Binary;

namespace SmartCafeteria.Licensing.Codec;

internal static class LicenseDate
{
    private static readonly DateTimeOffset Epoch = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static int ToDays(DateTimeOffset value) =>
        checked((int)(value.UtcDateTime.Date - Epoch.UtcDateTime.Date).TotalDays);

    public static DateTimeOffset FromDays(int days) =>
        Epoch.AddDays(days);

    public static void Write(DateTimeOffset value, Span<byte> destination) =>
        BinaryPrimitives.WriteInt32LittleEndian(destination, ToDays(value));

    public static void WriteNullable(DateTimeOffset? value, Span<byte> destination) =>
        BinaryPrimitives.WriteInt32LittleEndian(destination, value.HasValue ? ToDays(value.Value) : 0);

    public static DateTimeOffset? ReadNullable(ReadOnlySpan<byte> source)
    {
        var days = BinaryPrimitives.ReadInt32LittleEndian(source);
        return days == 0 ? null : FromDays(days);
    }

    public static DateTimeOffset Read(ReadOnlySpan<byte> source) =>
        FromDays(BinaryPrimitives.ReadInt32LittleEndian(source));
}
