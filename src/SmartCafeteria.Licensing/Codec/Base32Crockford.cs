using System.Text;

namespace SmartCafeteria.Licensing.Codec;

public static class Base32Crockford
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
    private static readonly int[] Lookup = BuildLookup();

    public static string Encode(ReadOnlySpan<byte> data)
    {
        if (data.IsEmpty)
            return string.Empty;

        var output = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;

        foreach (var value in data)
        {
            buffer = (buffer << 8) | value;
            bits += 8;

            while (bits >= 5)
            {
                bits -= 5;
                output.Append(Alphabet[(buffer >> bits) & 0x1F]);
            }
        }

        if (bits > 0)
            output.Append(Alphabet[(buffer << (5 - bits)) & 0x1F]);

        return output.ToString();
    }

    public static string Format(string value)
    {
        var compact = Normalize(value);
        var builder = new StringBuilder(compact.Length + compact.Length / 5);

        for (var i = 0; i < compact.Length; i++)
        {
            if (i > 0 && i % 5 == 0)
                builder.Append('-');

            builder.Append(compact[i]);
        }

        return builder.ToString();
    }

    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var character in value)
        {
            if (character is ' ' or '-' or '\r' or '\n' or '\t')
                continue;

            var normalized = char.ToUpperInvariant(character);
            normalized = normalized switch
            {
                'O' => '0',
                'I' or 'L' => '1',
                _ => normalized
            };

            builder.Append(normalized);
        }

        return builder.ToString();
    }

    public static bool TryDecode(string value, out byte[] result)
    {
        result = Array.Empty<byte>();
        var normalized = Normalize(value);

        if (normalized.Length == 0)
            return false;

        var output = new List<byte>(normalized.Length * 5 / 8);
        var buffer = 0;
        var bits = 0;

        foreach (var character in normalized)
        {
            if (character >= Lookup.Length || Lookup[character] < 0)
                return false;

            buffer = (buffer << 5) | Lookup[character];
            bits += 5;

            if (bits >= 8)
            {
                bits -= 8;
                output.Add((byte)((buffer >> bits) & 0xFF));
            }
        }

        if (bits > 0 && (buffer & ((1 << bits) - 1)) != 0)
            return false;

        result = output.ToArray();
        return true;
    }

    private static int[] BuildLookup()
    {
        var lookup = new int[128];
        Array.Fill(lookup, -1);

        for (var i = 0; i < Alphabet.Length; i++)
            lookup[Alphabet[i]] = i;

        return lookup;
    }
}
