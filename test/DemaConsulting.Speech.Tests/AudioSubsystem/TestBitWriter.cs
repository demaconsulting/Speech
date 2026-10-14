using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>A most-significant-bit-first bit writer used by <see cref="TestFlacEncoder"/>.</summary>
public sealed class TestBitWriter
{
    /// <summary>The completed bytes.</summary>
    private readonly List<byte> _bytes = [];

    /// <summary>The bits of the byte in progress, left aligned.</summary>
    private int _current;

    /// <summary>The number of bits in <see cref="_current"/>.</summary>
    private int _count;

    /// <summary>Writes the low <paramref name="bits"/> bits of <paramref name="value"/>.</summary>
    /// <param name="value">The value.</param>
    /// <param name="bits">The bit count, 0-64.</param>
    public void Write(ulong value, int bits)
    {
        for (var i = bits - 1; i >= 0; i--)
        {
            _current = (_current << 1) | (int)((value >> i) & 1);
            if (++_count == 8)
            {
                _bytes.Add((byte)_current);
                _current = 0;
                _count = 0;
            }
        }
    }

    /// <summary>Writes a two's-complement value in <paramref name="bits"/> bits.</summary>
    /// <param name="value">The value.</param>
    /// <param name="bits">The bit count.</param>
    public void WriteSigned(long value, int bits)
    {
        var mask = bits >= 64 ? ulong.MaxValue : (1UL << bits) - 1;
        Write((ulong)value & mask, bits);
    }

    /// <summary>Pads with zero bits to the next byte boundary.</summary>
    public void Align()
    {
        while (_count != 0)
        {
            Write(0, 1);
        }
    }

    /// <summary>Returns the bytes written so far (the byte in progress is excluded unless aligned).</summary>
    /// <returns>The bytes.</returns>
    public byte[] ToArray() => [.. _bytes];
}
