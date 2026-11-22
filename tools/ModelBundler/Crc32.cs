namespace DemaConsulting.Speech.ModelBundler;

/// <summary>
///     Computes the CRC-32 checksum (the IEEE 802.3 checksum, reflected polynomial <c>0xEDB88320</c>) that every zip
///     entry header records.
/// </summary>
/// <remarks>
///     A small table-driven implementation, so the tool needs no extra package for the one checksum the zip format
///     requires. The algorithm is fixed by the zip specification, so its output is identical on every platform.
///     Stateless (the table is immutable) and thread-safe.
/// </remarks>
internal static class Crc32
{
    /// <summary>The reflected CRC-32 polynomial used by the zip format.</summary>
    private const uint Polynomial = 0xEDB88320u;

    /// <summary>The initial and final XOR value of the CRC-32 register.</summary>
    private const uint Seed = 0xFFFFFFFFu;

    /// <summary>The 256-entry lookup table: the register update for each possible low byte.</summary>
    private static readonly uint[] Table = CreateTable();

    /// <summary>Gets the register value to start a new checksum with.</summary>
    public static uint Initial => Seed;

    /// <summary>Feeds bytes into a running CRC-32 register.</summary>
    /// <param name="register">The running register, starting at <see cref="Initial"/>.</param>
    /// <param name="data">The next bytes.</param>
    /// <returns>The updated register.</returns>
    public static uint Append(uint register, ReadOnlySpan<byte> data)
    {
        foreach (var value in data)
        {
            register = Table[(register ^ value) & 0xFF] ^ (register >> 8);
        }

        return register;
    }

    /// <summary>Converts a running register into the final checksum.</summary>
    /// <param name="register">The register after the last <see cref="Append"/>.</param>
    /// <returns>The CRC-32 checksum.</returns>
    public static uint Finish(uint register) => register ^ Seed;

    /// <summary>Computes the CRC-32 checksum of a whole buffer.</summary>
    /// <param name="data">The bytes to checksum.</param>
    /// <returns>The CRC-32 checksum.</returns>
    public static uint Compute(ReadOnlySpan<byte> data) => Finish(Append(Initial, data));

    /// <summary>Builds the lookup table for <see cref="Polynomial"/>.</summary>
    /// <returns>The 256-entry table.</returns>
    private static uint[] CreateTable()
    {
        var table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            // Shift the byte through the register eight times, applying the polynomial on each carried-out 1 bit
            var value = index;
            for (var bit = 0; bit < 8; bit++)
            {
                value = (value & 1) != 0 ? Polynomial ^ (value >> 1) : value >> 1;
            }

            table[index] = value;
        }

        return table;
    }
}
