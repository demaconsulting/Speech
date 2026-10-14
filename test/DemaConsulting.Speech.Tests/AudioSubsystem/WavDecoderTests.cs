using System.Buffers.Binary;
using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="WavDecoder"/>, calling the decoder directly with in-memory WAV images.
/// </summary>
public sealed class WavDecoderTests
{
    /// <summary>Proves 8-, 16-, 24- and 32-bit PCM scale to [-1, 1).</summary>
    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void WavDecoder_Decode_PcmDepths_ScaleToUnitRange(int bits)
    {
        // Arrange: minimum, zero and one-quarter-scale positive samples
        var bytesPerSample = bits / 8;
        var data = new List<byte>();
        foreach (var fraction in new[] { -1.0, 0.0, 0.25 })
        {
            data.AddRange(PcmBytes(fraction, bits, bytesPerSample));
        }

        // Act
        var clip = WavDecoder.Decode(Wav(1, 8000, bits, 1, data.ToArray()), long.MaxValue);

        // Assert
        Assert.Equal([-1f, 0f, 0.25f], clip.Samples);
        Assert.Equal(bits, clip.BitsPerSample);
        Assert.Equal(8000, clip.SampleRate);
    }

    /// <summary>Proves 32-bit float samples pass through unchanged.</summary>
    [Fact]
    public void WavDecoder_Decode_Float32_PassesThrough()
    {
        // Arrange
        var data = new byte[8];
        BinaryPrimitives.WriteSingleLittleEndian(data, 0.5f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(4), -2f);

        // Act
        var clip = WavDecoder.Decode(Wav(3, 16000, 32, 2, data), long.MaxValue);

        // Assert
        Assert.Equal([0.5f, -2f], clip.Samples);
        Assert.Equal(2, clip.ChannelCount);
        Assert.Equal(1, clip.FrameCount);
    }

    /// <summary>Proves the extensible header form, unknown chunks and odd chunk padding are handled.</summary>
    [Fact]
    public void WavDecoder_Decode_ExtensibleWithExtraChunks_Decodes()
    {
        // Arrange
        var fmt = new byte[40];
        BinaryPrimitives.WriteUInt16LittleEndian(fmt, 0xFFFE);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(fmt.AsSpan(4), 22050);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(14), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(24), 1);
        var image = Riff(("LIST", new byte[] { 1, 2, 3 }), ("fmt ", fmt), ("data", new byte[] { 0x00, 0x40 }));

        // Act
        var clip = WavDecoder.Decode(image, long.MaxValue);

        // Assert
        Assert.Equal([0.5f], clip.Samples);
        Assert.Equal(22050, clip.SampleRate);
    }

    /// <summary>Proves an overstated data size is clamped to the bytes present and a ragged final frame is dropped.</summary>
    [Fact]
    public void WavDecoder_Decode_OverstatedDataSize_IsClamped()
    {
        // Arrange: 6 data bytes of stereo 16-bit audio is one whole frame (4 bytes) plus 2 stray bytes
        var image = Wav(1, 8000, 16, 2, new byte[] { 0, 0, 0, 0x40, 0, 0x20 });
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(40), 1_000_000);

        // Act
        var clip = WavDecoder.Decode(image, long.MaxValue);

        // Assert
        Assert.Equal([0f, 0.5f], clip.Samples);
    }

    /// <summary>Proves unsupported encodings raise <see cref="NotSupportedException"/>.</summary>
    [Theory]
    [InlineData(2, 16)]
    [InlineData(1, 12)]
    [InlineData(3, 64)]
    public void WavDecoder_Decode_UnsupportedEncoding_Throws(int tag, int bits)
    {
        // Arrange
        var image = Wav(tag, 8000, bits, 1, new byte[8]);

        // Act
        var error = Record.Exception(() => WavDecoder.Decode(image, long.MaxValue));

        // Assert
        Assert.IsType<NotSupportedException>(error);
    }

    /// <summary>Proves malformed images raise <see cref="InvalidDataException"/> naming the defect.</summary>
    [Theory]
    [InlineData("NoData", "no 'data' chunk")]
    [InlineData("DataBeforeFmt", "before its 'fmt ' chunk")]
    [InlineData("ShortFmt", "truncated 'fmt ' chunk")]
    [InlineData("NoRate", "no channels or no sample rate")]
    [InlineData("NoChannels", "no channels or no sample rate")]
    [InlineData("TruncatedFile", "truncated 'fmt ' chunk")]
    [InlineData("TruncatedExtensible", "truncated extensible 'fmt ' chunk")]
    public void WavDecoder_Decode_MalformedImages_Throw(string name, string fragment)
    {
        // Arrange
        var image = name switch
        {
            "NoData" => Riff(("fmt ", Format(1, 8000, 16, 1))),
            "DataBeforeFmt" => Riff(("data", new byte[2]), ("fmt ", Format(1, 8000, 16, 1))),
            "ShortFmt" => Riff(("fmt ", new byte[8]), ("data", new byte[2])),
            "NoRate" => Riff(("fmt ", Format(1, 0, 16, 1)), ("data", new byte[2])),
            "NoChannels" => Riff(("fmt ", Format(1, 8000, 16, 0)), ("data", new byte[2])),
            "TruncatedFile" => Wav(1, 8000, 16, 1, new byte[4])[..30],
            _ => Riff(("fmt ", Format(0xFFFE, 8000, 16, 1)), ("data", new byte[2]))
        };

        // Act
        var ex = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(image, long.MaxValue));

        // Assert
        Assert.Contains(fragment, ex.Message);
    }

    /// <summary>Proves the sample cap is enforced.</summary>
    [Fact]
    public void WavDecoder_Decode_ExceedsMaxSamples_Throws()
    {
        // Arrange
        var image = Wav(1, 8000, 16, 1, new byte[100]);

        // Act
        var error = Record.Exception(() => WavDecoder.Decode(image, 10));

        // Assert
        Assert.IsType<InvalidDataException>(error);
    }

    /// <summary>Proves the cap is inclusive: exactly the cap decodes and one sample fewer than the data holds is rejected.</summary>
    [Fact]
    public void WavDecoder_Decode_ExactSampleCap_IsAcceptedAndOneUnderIsRejected()
    {
        // Arrange: 10 mono 16-bit samples
        var image = Wav(1, 8000, 16, 1, new byte[20]);

        // Act
        var atCap = WavDecoder.Decode(image, 10);

        // Assert
        Assert.Equal(10, atCap.Samples.Length);
        Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(image, 9));
    }

    /// <summary>Proves the largest positive PCM value of each depth stays below full scale and the extreme negative is exactly -1.</summary>
    [Theory]
    [InlineData(16, new byte[] { 0xFF, 0x7F }, 32767f / 32768f)]
    [InlineData(24, new byte[] { 0xFF, 0xFF, 0x7F }, 8388607f / 8388608f)]
    [InlineData(16, new byte[] { 0x00, 0x80 }, -1f)]
    [InlineData(24, new byte[] { 0x00, 0x00, 0x80 }, -1f)]
    public void WavDecoder_Decode_ExtremePcmValues_ScaleAsExpected(int bits, byte[] sample, float expected)
    {
        // Act
        var clip = WavDecoder.Decode(Wav(1, 8000, bits, 1, sample), long.MaxValue);

        // Assert
        Assert.Equal([expected], clip.Samples);
    }

    /// <summary>Proves a zero-length data chunk decodes to an empty clip.</summary>
    [Fact]
    public void WavDecoder_Decode_ZeroLengthData_YieldsEmptyClip()
    {
        // Act
        var clip = WavDecoder.Decode(Wav(1, 8000, 16, 1, []), long.MaxValue);

        // Assert
        Assert.Empty(clip.Samples);
        Assert.Equal(8000, clip.SampleRate);
    }

    /// <summary>Proves 24-bit multichannel samples stay interleaved in channel order.</summary>
    [Fact]
    public void WavDecoder_Decode_TwentyFourBitStereo_KeepsChannelOrder()
    {
        // Arrange: left = +0.5, right = -0.5 (0x400000 and 0xC00000)
        var data = new byte[] { 0x00, 0x00, 0x40, 0x00, 0x00, 0xC0 };

        // Act
        var clip = WavDecoder.Decode(Wav(1, 8000, 24, 2, data), long.MaxValue);

        // Assert
        Assert.Equal([0.5f, -0.5f], clip.Samples);
        Assert.Equal(2, clip.ChannelCount);
    }

    /// <summary>Proves a NaN float sample reaches the clip unchanged, as documented.</summary>
    [Fact]
    public void WavDecoder_Decode_FloatNaN_PassesThroughUnchanged()
    {
        // Arrange
        var data = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(data, float.NaN);

        // Act
        var clip = WavDecoder.Decode(Wav(3, 8000, 32, 1, data), long.MaxValue);

        // Assert
        Assert.True(float.IsNaN(clip.Samples[0]));
    }

    /// <summary>Proves a sample rate above <see cref="int.MaxValue"/> is rejected as an invalid rate.</summary>
    [Fact]
    public void WavDecoder_Decode_SampleRateAboveIntMax_Throws()
    {
        // Arrange
        var fmt = Format(1, 8000, 16, 1);
        BinaryPrimitives.WriteUInt32LittleEndian(fmt.AsSpan(4), 0x80000000u);

        // Act
        var ex = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(Riff(("fmt ", fmt), ("data", new byte[2])), long.MaxValue));

        // Assert
        Assert.Contains("above the supported maximum", ex.Message);
        Assert.DoesNotContain("no channels", ex.Message);
    }

    /// <summary>Proves a data size of 0xFFFFFFFF (streaming writers) is clamped to the bytes present.</summary>
    [Fact]
    public void WavDecoder_Decode_MaxDataSize_IsClampedToBytesPresent()
    {
        // Arrange: two mono 16-bit samples behind a data size of 0xFFFFFFFF
        var image = Wav(1, 8000, 16, 1, new byte[] { 0, 0, 0, 0x40 });
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(40), 0xFFFFFFFFu);

        // Act
        var clip = WavDecoder.Decode(image, long.MaxValue);

        // Assert
        Assert.Equal([0f, 0.5f], clip.Samples);
    }

    /// <summary>Proves the 8-bit unsigned extremes map to -1 (0) and just below 1 (255).</summary>
    [Theory]
    [InlineData(0, -1f)]
    [InlineData(128, 0f)]
    [InlineData(255, 127f / 128f)]
    public void WavDecoder_Decode_EightBitExtremes_ScaleAsExpected(int value, float expected)
    {
        // Act
        var clip = WavDecoder.Decode(Wav(1, 8000, 8, 1, new[] { (byte)value }), long.MaxValue);

        // Assert
        Assert.Equal([expected], clip.Samples);
    }

    /// <summary>Proves that when a second fmt chunk appears before data, the last one wins.</summary>
    [Fact]
    public void WavDecoder_Decode_DuplicateFmtChunks_LastOneWins()
    {
        // Arrange: a 16-bit fmt followed by an 8-bit fmt, then one byte of data
        var image = Riff(("fmt ", Format(1, 8000, 16, 1)), ("fmt ", Format(1, 44100, 8, 1)), ("data", new byte[] { 255 }));

        // Act
        var clip = WavDecoder.Decode(image, long.MaxValue);

        // Assert
        Assert.Equal(8, clip.BitsPerSample);
        Assert.Equal(44100, clip.SampleRate);
        Assert.Equal([127f / 128f], clip.Samples);
    }

    /// <summary>Proves images shorter than the 12-byte RIFF/WAVE header are rejected as not WAV.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(11)]
    public void WavDecoder_Decode_ImageShorterThanHeader_Throws(int length)
    {
        // Arrange
        var image = "RIFF\0\0\0\0WAVE"u8[..length].ToArray();

        // Act
        var ex = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(image, long.MaxValue));

        // Assert
        Assert.Contains("not a RIFF/WAVE", ex.Message, StringComparison.Ordinal);
        Assert.False(WavDecoder.IsWav(image));
    }

    /// <summary>Proves a non-data chunk whose padded body runs past the end ends the walk with "no data chunk".</summary>
    [Fact]
    public void WavDecoder_Decode_ChunkRunsPastEnd_ReportsNoDataChunk()
    {
        // Arrange: a LIST chunk declaring 100 bytes with only 2 present
        var image = Riff(("fmt ", Format(1, 8000, 16, 1))).Concat("LIST"u8.ToArray()).Concat(BitConverter.GetBytes(100u)).Concat(new byte[2]).ToArray();

        // Act
        var ex = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(image, long.MaxValue));

        // Assert
        Assert.Contains("no 'data' chunk", ex.Message);
    }

    /// <summary>Proves an extensible header with a float sub-format decodes as float.</summary>
    [Fact]
    public void WavDecoder_Decode_ExtensibleFloatSubFormat_DecodesAsFloat()
    {
        // Arrange
        var data = new byte[4];
        BinaryPrimitives.WriteSingleLittleEndian(data, -0.25f);

        // Act
        var clip = WavDecoder.Decode(Riff(("fmt ", Extensible(3, 32, 1)), ("data", data)), long.MaxValue);

        // Assert
        Assert.Equal([-0.25f], clip.Samples);
        Assert.Equal(32, clip.BitsPerSample);
    }

    /// <summary>Proves an unsupported extensible sub-format or bit depth raises <see cref="NotSupportedException"/>.</summary>
    [Theory]
    [InlineData(2, 16)]
    [InlineData(1, 12)]
    [InlineData(3, 64)]
    public void WavDecoder_Decode_ExtensibleUnsupportedEncoding_Throws(int subFormat, int bits)
    {
        // Arrange
        var image = Riff(("fmt ", Extensible(subFormat, bits, 1)), ("data", new byte[8]));

        // Act
        var error = Record.Exception(() => WavDecoder.Decode(image, long.MaxValue));

        // Assert
        Assert.IsType<NotSupportedException>(error);
    }

    /// <summary>Proves a ragged final frame is dropped when the declared data size is exact.</summary>
    [Fact]
    public void WavDecoder_Decode_TrailingPartialFrame_IsDropped()
    {
        // Arrange: stereo 16-bit, 6 data bytes with an accurate declared size (one frame plus 2 stray bytes)
        var image = Wav(1, 8000, 16, 2, new byte[] { 0, 0, 0, 0x40, 0, 0x20 });

        // Act
        var clip = WavDecoder.Decode(image, long.MaxValue);

        // Assert
        Assert.Equal([0f, 0.5f], clip.Samples);
        Assert.Equal(1, clip.FrameCount);
    }

    /// <summary>Proves the decoder itself rejects data that lacks a RIFF/WAVE header.</summary>
    [Fact]
    public void WavDecoder_Decode_NonWavHeader_Throws()
    {
        // Act
        var ex = Assert.Throws<InvalidDataException>(() => WavDecoder.Decode(new byte[20], long.MaxValue));

        // Assert
        Assert.Contains("not a RIFF/WAVE", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>Builds a 40-byte <c>WAVE_FORMAT_EXTENSIBLE</c> <c>fmt </c> body with the given sub-format tag.</summary>
    private static byte[] Extensible(int subFormat, int bits, int channels)
    {
        var fmt = new byte[40];
        BinaryPrimitives.WriteUInt16LittleEndian(fmt, 0xFFFE);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(2), (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(fmt.AsSpan(4), 8000);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(14), (ushort)bits);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(24), (ushort)subFormat);
        return fmt;
    }

    /// <summary>Encodes a fractional full-scale value as little-endian PCM.</summary>
    private static byte[] PcmBytes(double fraction, int bits, int bytesPerSample)
    {
        if (bits == 8)
        {
            return [(byte)(128 + (fraction * 128))];
        }

        var value = (long)(fraction * Math.Pow(2, bits - 1));
        var bytes = new byte[bytesPerSample];
        for (var i = 0; i < bytesPerSample; i++)
        {
            bytes[i] = (byte)(value >> (8 * i));
        }

        return bytes;
    }

    /// <summary>Builds a 16-byte <c>fmt </c> body.</summary>
    private static byte[] Format(int tag, int rate, int bits, int channels)
    {
        var fmt = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(fmt, (ushort)tag);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(2), (ushort)channels);
        BinaryPrimitives.WriteUInt32LittleEndian(fmt.AsSpan(4), (uint)rate);
        BinaryPrimitives.WriteUInt16LittleEndian(fmt.AsSpan(14), (ushort)bits);
        return fmt;
    }

    /// <summary>Builds a canonical WAV image.</summary>
    private static byte[] Wav(int tag, int rate, int bits, int channels, byte[] data) =>
        Riff(("fmt ", Format(tag, rate, bits, channels)), ("data", data));

    /// <summary>Builds a RIFF/WAVE image from chunks, padding odd chunks.</summary>
    private static byte[] Riff(params (string Id, byte[] Body)[] chunks)
    {
        var output = new List<byte>();
        output.AddRange("RIFF\0\0\0\0WAVE"u8.ToArray());
        foreach (var (id, body) in chunks)
        {
            output.AddRange(System.Text.Encoding.ASCII.GetBytes(id));
            output.AddRange(BitConverter.GetBytes((uint)body.Length));
            output.AddRange(body);
            if ((body.Length & 1) == 1)
            {
                output.Add(0);
            }
        }

        var image = output.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(4), (uint)(image.Length - 8));
        return image;
    }
}
