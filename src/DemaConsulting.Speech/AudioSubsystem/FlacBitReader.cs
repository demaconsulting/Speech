namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Most-significant-bit-first bit reader over an in-memory FLAC image.
/// </summary>
/// <remarks>
///     Reads past the end of the image throw <see cref="InvalidDataException"/> so a truncated stream is always
///     reported, never silently padded. Not thread-safe; one instance per decode.
/// </remarks>
internal sealed class FlacBitReader
{
    /// <summary>The image being read.</summary>
    private readonly byte[] _data;

    /// <summary>Bits already consumed from the current byte (0-7).</summary>
    private int _bit;

    /// <summary>Initializes a new instance of the <see cref="FlacBitReader"/> class.</summary>
    /// <param name="data">The image. Must not be null.</param>
    /// <param name="position">The first byte to read.</param>
    public FlacBitReader(byte[] data, int position)
    {
        _data = data;
        BytePosition = position;
    }

    /// <summary>Gets the index of the byte currently being read (the next whole byte when byte-aligned).</summary>
    public int BytePosition { get; private set; }

    /// <summary>Gets the image being read (shared; callers must not modify it).</summary>
    public byte[] Data => _data;

    /// <summary>Peeks whether the next two (byte-aligned) bytes carry the 14-bit frame sync code.</summary>
    /// <returns><see langword="true"/> when a frame sync code is next.</returns>
    public bool PeekFrameSync() =>
        _bit == 0 && BytePosition + 1 < _data.Length && _data[BytePosition] == 0xFF && (_data[BytePosition + 1] & 0xFC) == 0xF8;

    /// <summary>Reads up to 32 bits as an unsigned value.</summary>
    /// <param name="count">The number of bits, 0-32.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">Thrown when the image ends first.</exception>
    public uint ReadBits(int count) => (uint)ReadBitsLong(count);

    /// <summary>Reads up to 56 bits as an unsigned value.</summary>
    /// <param name="count">The number of bits, 0-56.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">Thrown when the image ends first.</exception>
    public ulong ReadBitsLong(int count)
    {
        ulong value = 0;
        while (count > 0)
        {
            if (BytePosition >= _data.Length)
            {
                throw Truncated();
            }

            var available = 8 - _bit;
            var take = Math.Min(available, count);
            var chunk = (_data[BytePosition] >> (available - take)) & ((1 << take) - 1);
            value = (value << take) | (uint)chunk;
            _bit += take;
            count -= take;
            if (_bit == 8)
            {
                _bit = 0;
                BytePosition++;
            }
        }

        return value;
    }

    /// <summary>Reads a two's-complement signed value of up to 56 bits.</summary>
    /// <param name="count">The number of bits, 0-56.</param>
    /// <returns>The sign-extended value.</returns>
    public long ReadSigned(int count)
    {
        if (count == 0)
        {
            return 0;
        }

        var shift = 64 - count;
        return (long)(ReadBitsLong(count) << shift) >> shift;
    }

    /// <summary>Reads a unary-coded value: the number of zero bits before the first one bit.</summary>
    /// <returns>The count of zero bits.</returns>
    public long ReadUnary()
    {
        long zeros = 0;
        while (ReadBits(1) == 0)
        {
            zeros++;
        }

        return zeros;
    }

    /// <summary>Reads a FLAC frame/sample number: UTF-8 style, one to seven bytes.</summary>
    /// <returns>The number.</returns>
    /// <exception cref="InvalidDataException">Thrown when the prefix or a continuation byte is invalid.</exception>
    public long ReadUtf8Number()
    {
        var first = (int)ReadBits(8);

        // The count of leading one bits gives the sequence length (zero means a plain 7-bit value)
        var leading = 0;
        while (leading < 8 && (first & (0x80 >> leading)) != 0)
        {
            leading++;
        }

        if (leading == 0)
        {
            return first;
        }

        if (leading is 1 or 8)
        {
            throw new InvalidDataException($"Invalid FLAC frame number prefix at byte {BytePosition - 1}.");
        }

        var extra = leading - 1;
        long value = first & (0x7F >> leading);
        for (var i = 0; i < extra; i++)
        {
            var next = (int)ReadBits(8);
            if ((next & 0xC0) != 0x80)
            {
                throw new InvalidDataException($"Invalid FLAC frame number continuation at byte {BytePosition - 1}.");
            }

            value = (value << 6) | (long)(next & 0x3F);
        }

        return value;
    }

    /// <summary>Skips to the next byte boundary.</summary>
    public void AlignToByte()
    {
        if (_bit != 0)
        {
            _bit = 0;
            BytePosition++;
        }
    }

    /// <summary>Creates the exception reported for a truncated image.</summary>
    private InvalidDataException Truncated() =>
        new($"The FLAC stream is truncated (data ends at byte {_data.Length}).");
}
