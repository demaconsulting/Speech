using System.Buffers.Binary;
using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="WavDecoder"/>, driven through <see cref="AudioFileReader"/>.
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
        var clip = AudioFileReader.Read(new MemoryStream(Wav(1, 8000, bits, 1, data.ToArray())));

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
        var clip = AudioFileReader.Read(new MemoryStream(Wav(3, 16000, 32, 2, data)));

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
        var clip = AudioFileReader.Read(new MemoryStream(image));

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
        var clip = AudioFileReader.Read(new MemoryStream(image));

        // Assert
        Assert.Equal([0f, 0.5f], clip.Samples);
    }

    /// <summary>Proves unsupported encodings raise <see cref="NotSupportedException"/>.</summary>
    [Theory]
    [InlineData(2, 16)]
    [InlineData(1, 12)]
    [InlineData(3, 64)]
    public void WavDecoder_Decode_UnsupportedEncoding_Throws(int tag, int bits) =>
        Assert.Throws<NotSupportedException>(() => AudioFileReader.Read(new MemoryStream(Wav(tag, 8000, bits, 1, new byte[8]))));

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
        var ex = Assert.Throws<InvalidDataException>(() => AudioFileReader.Read(new MemoryStream(image)));

        // Assert
        Assert.Contains(fragment, ex.Message);
    }

    /// <summary>Proves the sample cap is enforced.</summary>
    [Fact]
    public void WavDecoder_Decode_ExceedsMaxSamples_Throws() =>
        Assert.Throws<InvalidDataException>(() => AudioFileReader.Read(
            new MemoryStream(Wav(1, 8000, 16, 1, new byte[100])),
            new AudioFileReadOptions { MaxSamples = 10 }));

    /// <summary>Proves the cap is inclusive: exactly the cap decodes and one sample fewer than the data holds is rejected.</summary>
    [Fact]
    public void WavDecoder_Decode_ExactSampleCap_IsAcceptedAndOneUnderIsRejected()
    {
        // Arrange: 10 mono 16-bit samples
        var image = Wav(1, 8000, 16, 1, new byte[20]);

        // Act
        var atCap = AudioFileReader.Read(new MemoryStream(image), new AudioFileReadOptions { MaxSamples = 10 });

        // Assert
        Assert.Equal(10, atCap.Samples.Length);
        Assert.Throws<InvalidDataException>(() => AudioFileReader.Read(new MemoryStream(image), new AudioFileReadOptions { MaxSamples = 9 }));
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
        var clip = AudioFileReader.Read(new MemoryStream(Wav(1, 8000, bits, 1, sample)));

        // Assert
        Assert.Equal([expected], clip.Samples);
    }

    /// <summary>Proves a zero-length data chunk decodes to an empty clip.</summary>
    [Fact]
    public void WavDecoder_Decode_ZeroLengthData_YieldsEmptyClip()
    {
        // Act
        var clip = AudioFileReader.Read(new MemoryStream(Wav(1, 8000, 16, 1, [])));

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
        var clip = AudioFileReader.Read(new MemoryStream(Wav(1, 8000, 24, 2, data)));

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
        var clip = AudioFileReader.Read(new MemoryStream(Wav(3, 8000, 32, 1, data)));

        // Assert
        Assert.True(float.IsNaN(clip.Samples[0]));
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
