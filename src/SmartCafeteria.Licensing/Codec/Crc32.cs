namespace SmartCafeteria.Licensing.Codec;

public static class Crc32
{
    private const uint Polynomial = 0xEDB88320;
    private static readonly uint[] Table = BuildTable();

    public static uint Compute(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var value in data)
            crc = Table[(crc ^ value) & 0xFF] ^ (crc >> 8);

        return crc ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];

        for (var i = 0; i < table.Length; i++)
        {
            var value = (uint)i;
            for (var bit = 0; bit < 8; bit++)
                value = (value & 1) == 1 ? (value >> 1) ^ Polynomial : value >> 1;

            table[i] = value;
        }

        return table;
    }
}
