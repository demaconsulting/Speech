namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Table-driven CRC-8 and CRC-16 checksums as used by FLAC frame headers and footers.
/// </summary>
/// <remarks>
///     CRC-8 uses polynomial <c>0x07</c> and CRC-16 uses <c>0x8005</c>; both start at zero, are computed
///     most-significant-bit first, and are not reflected. Stateless and thread-safe.
/// </remarks>
internal static class FlacCrc
{
    /// <summary>The CRC-8 lookup table.</summary>
    private static readonly byte[] Table8 = BuildTable8();

    /// <summary>The CRC-16 lookup table.</summary>
    private static readonly ushort[] Table16 = BuildTable16();

    /// <summary>Computes the CRC-8 of <paramref name="data"/>.</summary>
    /// <param name="data">The bytes to checksum.</param>
    /// <returns>The checksum.</returns>
    public static byte Crc8(ReadOnlySpan<byte> data)
    {
        byte crc = 0;
        foreach (var b in data)
        {
            crc = Table8[crc ^ b];
        }

        return crc;
    }

    /// <summary>Computes the CRC-16 of <paramref name="data"/>.</summary>
    /// <param name="data">The bytes to checksum.</param>
    /// <returns>The checksum.</returns>
    public static ushort Crc16(ReadOnlySpan<byte> data)
    {
        ushort crc = 0;
        foreach (var b in data)
        {
            crc = (ushort)((crc << 8) ^ Table16[(crc >> 8) ^ b]);
        }

        return crc;
    }

    /// <summary>Builds the CRC-8 table.</summary>
    private static byte[] BuildTable8()
    {
        var table = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            var value = i;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 0x80) != 0 ? (value << 1) ^ 0x07 : value << 1;
            }

            table[i] = (byte)value;
        }

        return table;
    }

    /// <summary>Builds the CRC-16 table.</summary>
    private static ushort[] BuildTable16()
    {
        var table = new ushort[256];
        for (var i = 0; i < 256; i++)
        {
            var value = i << 8;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 0x8000) != 0 ? (value << 1) ^ 0x8005 : value << 1;
            }

            table[i] = (ushort)value;
        }

        return table;
    }
}
