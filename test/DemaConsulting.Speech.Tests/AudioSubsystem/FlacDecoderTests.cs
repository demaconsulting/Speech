using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="FlacDecoder"/>, <see cref="FlacFrameDecoder"/>, <see cref="FlacBitReader"/> and
///     <see cref="FlacCrc"/>, driven through the public <see cref="AudioFileReader"/> where possible.
/// </summary>
public sealed class FlacDecoderTests
{
    /// <summary>The offset of the first frame when no extra metadata or ID3 tag is written.</summary>
    private const int FirstFrameOffset = 42;

    /// <summary>Proves real libFLAC output decodes bit-exactly to the lossless WAV made from the same samples.</summary>
    [Theory]
    [InlineData("mono-16-44100")]
    [InlineData("stereo-16-48000")]
    [InlineData("mono-24-48000")]
    [InlineData("mono-8-8000")]
    [InlineData("stereo-16-compression8")]
    [InlineData("stereo-correlated-16")]
    public void FlacDecoder_Decode_SoundfileFixture_MatchesWavBitExact(string name)
    {
        // Arrange
        var directory = Path.Combine(AppContext.BaseDirectory, "TestData", "flac");

        // Act
        var flac = AudioFileReader.Read(Path.Combine(directory, name + ".flac"));
        var wav = AudioFileReader.Read(Path.Combine(directory, name + ".wav"));

        // Assert
        Assert.Equal(wav.SampleRate, flac.SampleRate);
        Assert.Equal(wav.ChannelCount, flac.ChannelCount);
        Assert.Equal(wav.BitsPerSample, flac.BitsPerSample);
        Assert.True(flac.Samples.Length > 1000);
        Assert.Equal(wav.Samples, flac.Samples);
    }

    /// <summary>Proves the CRC-8 and CRC-16 match their published check values.</summary>
    [Fact]
    public void FlacCrc_Checksums_StandardCheckString_MatchPublishedValues()
    {
        // Arrange
        var data = "123456789"u8.ToArray();

        // Act / Assert
        Assert.Equal(0xF4, FlacCrc.Crc8(data));
        Assert.Equal(0xFEE8, FlacCrc.Crc16(data));
    }

    /// <summary>Proves a constant subframe expands to its value.</summary>
    [Fact]
    public void FlacDecoder_Decode_ConstantSubframe_RepeatsValue()
    {
        // Arrange
        var signal = new[] { Enumerable.Repeat(-1234, 300).ToArray() };
        var encoder = new TestFlacEncoder { Kind = FlacSubframeKind.Constant, BlockSize = 300 };

        // Act
        var clip = Decode(encoder.Encode(signal));

        // Assert
        AssertMatches(signal, clip, 16);
    }

    /// <summary>Proves verbatim subframes round-trip across several frames including a short last block.</summary>
    [Fact]
    public void FlacDecoder_Decode_VerbatimMultiFrame_RoundTrips()
    {
        // Arrange
        var signal = Signal(1, 1000, 16);
        var encoder = new TestFlacEncoder { Kind = FlacSubframeKind.Verbatim, BlockSize = 384 };

        // Act
        var clip = Decode(encoder.Encode(signal));

        // Assert
        AssertMatches(signal, clip, 16);
    }

    /// <summary>Proves every fixed-predictor order restores the original samples.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void FlacDecoder_Decode_FixedPredictor_RoundTrips(int order)
    {
        // Arrange
        var signal = Signal(1, 700, 16);
        var encoder = new TestFlacEncoder { Kind = FlacSubframeKind.Fixed, FixedOrder = order, BlockSize = 256 };

        // Act
        var clip = Decode(encoder.Encode(signal));

        // Assert
        AssertMatches(signal, clip, 16);
    }

    /// <summary>Proves linear predictors of several orders, shifts and precisions restore the original samples.</summary>
    [Theory]
    [InlineData(2, 0, 8)]
    [InlineData(8, 5, 10)]
    [InlineData(32, 9, 12)]
    public void FlacDecoder_Decode_LpcPredictor_RoundTrips(int order, int shift, int precision)
    {
        // Arrange: small, decaying coefficients so the prediction stays sensible
        var coefficients = Enumerable.Range(0, order).Select(i => i == 0 ? 1L << shift : AlternatingSign(i)).ToArray();
        var signal = Signal(1, 600, 16);
        var encoder = new TestFlacEncoder
        {
            Kind = FlacSubframeKind.Lpc,
            LpcCoefficients = order == 2 ? [2, -1] : coefficients,
            LpcShift = shift,
            LpcPrecision = precision,
            BlockSize = 300
        };

        // Act
        var clip = Decode(encoder.Encode(signal));

        // Assert
        AssertMatches(signal, clip, 16);
    }

    /// <summary>Proves both Rice methods, several partition orders and escape partitions decode correctly.</summary>
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 3, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 4, false)]
    [InlineData(0, 2, true)]
    [InlineData(1, 0, true)]
    public void FlacDecoder_Decode_ResidualCoding_RoundTrips(int method, int partitionOrder, bool escape)
    {
        // Arrange
        var signal = Signal(1, 512, 16);
        var encoder = new TestFlacEncoder
        {
            Kind = FlacSubframeKind.Fixed,
            FixedOrder = 2,
            RiceMethod = method,
            PartitionOrder = partitionOrder,
            EscapePartitions = escape,
            BlockSize = 512
        };

        // Act
        var clip = Decode(encoder.Encode(signal));

        // Assert
        AssertMatches(signal, clip, 16);
    }

    /// <summary>Proves wasted low bits are shifted back into the samples.</summary>
    [Fact]
    public void FlacDecoder_Decode_WastedBits_AreRestored()
    {
        // Arrange
        var signal = Signal(1, 400, 16).Select(c => c.Select(v => v & ~0x7).ToArray()).ToArray();
        var encoder = new TestFlacEncoder { Kind = FlacSubframeKind.Fixed, FixedOrder = 1, WastedBits = 3, BlockSize = 400 };

        // Act
        var clip = Decode(encoder.Encode(signal));

        // Assert
        AssertMatches(signal, clip, 16);
    }

    /// <summary>Proves all three stereo decorrelation modes, including a 33-bit side channel, are reversed.</summary>
    [Theory]
    [InlineData(FlacStereoMode.Independent, 16)]
    [InlineData(FlacStereoMode.LeftSide, 16)]
    [InlineData(FlacStereoMode.SideRight, 16)]
    [InlineData(FlacStereoMode.MidSide, 16)]
    [InlineData(FlacStereoMode.MidSide, 32)]
    [InlineData(FlacStereoMode.LeftSide, 24)]
    public void FlacDecoder_Decode_StereoModes_RoundTrip(FlacStereoMode mode, int bits)
    {
        // Arrange
        var signal = Signal(2, 500, bits);
        var encoder = new TestFlacEncoder { Stereo = mode, BitsPerSample = bits, Kind = FlacSubframeKind.Verbatim, BlockSize = 250 };

        // Act
        var clip = Decode(encoder.Encode(signal));

        // Assert
        Assert.Equal(2, clip.ChannelCount);
        AssertMatches(signal, clip, bits);
    }

    /// <summary>Proves each supported bit depth, with and without per-frame depth codes, decodes and is scaled to [-1, 1).</summary>
    [Theory]
    [InlineData(8, true)]
    [InlineData(12, true)]
    [InlineData(16, true)]
    [InlineData(20, true)]
    [InlineData(24, true)]
    [InlineData(32, true)]
    [InlineData(16, false)]
    [InlineData(4, false)]
    public void FlacDecoder_Decode_BitDepths_RoundTrip(int bits, bool explicitCodes)
    {
        // Arrange
        var signal = Signal(1, 300, bits);
        var encoder = new TestFlacEncoder
        {
            BitsPerSample = bits,
            ExplicitHeaderCodes = explicitCodes,
            Kind = FlacSubframeKind.Fixed,
            FixedOrder = 1,
            BlockSize = 300
        };

        // Act
        var clip = Decode(encoder.Encode(signal));

        // Assert
        Assert.Equal(bits, clip.BitsPerSample);
        Assert.All(clip.Samples, s => Assert.InRange(s, -1f, 1f));
        AssertMatches(signal, clip, bits);
    }

    /// <summary>Proves every sample-rate header form (table, kHz, Hz, tens of Hz, "as STREAMINFO") decodes.</summary>
    [Theory]
    [InlineData(44100, true)]
    [InlineData(8000, true)]
    [InlineData(50000, true)]
    [InlineData(12345, true)]
    [InlineData(100000, true)]
    [InlineData(37000, false)]
    public void FlacDecoder_Decode_SampleRateCodes_ReportRate(int rate, bool explicitCodes)
    {
        // Arrange
        var signal = Signal(1, 100, 16);
        var encoder = new TestFlacEncoder { SampleRate = rate, ExplicitHeaderCodes = explicitCodes, BlockSize = 100 };

        // Act
        var clip = Decode(encoder.Encode(signal));

        // Assert
        Assert.Equal(rate, clip.SampleRate);
        AssertMatches(signal, clip, 16);
    }

    /// <summary>Proves the fixed and trailing block-size header codes decode.</summary>
    [Theory]
    [InlineData(192)]
    [InlineData(576)]
    [InlineData(4096)]
    [InlineData(100)]
    [InlineData(1000)]
    public void FlacDecoder_Decode_BlockSizeCodes_RoundTrip(int blockSize)
    {
        // Arrange
        var signal = Signal(1, (blockSize * 2) + (blockSize / 2), 16);
        var encoder = new TestFlacEncoder { BlockSize = blockSize };

        // Act
        var clip = Decode(encoder.Encode(signal));

        // Assert
        AssertMatches(signal, clip, 16);
    }

    /// <summary>Proves frame numbers needing two and three UTF-8 bytes are parsed.</summary>
    [Fact]
    public void FlacDecoder_Decode_ManyFrames_ParsesMultiByteFrameNumbers()
    {
        // Arrange: one-sample blocks give 2100 frames
        var signal = Signal(1, 2100, 16);
        var encoder = new TestFlacEncoder { BlockSize = 1 };

        // Act
        var clip = Decode(encoder.Encode(signal));

        // Assert
        AssertMatches(signal, clip, 16);
    }

    /// <summary>Proves an ID3 tag, extra metadata blocks and many channels are handled.</summary>
    [Theory]
    [InlineData(1, true, true)]
    [InlineData(3, false, false)]
    [InlineData(8, true, false)]
    public void FlacDecoder_Decode_ContainerVariants_RoundTrip(int channels, bool id3, bool extraMetadata)
    {
        // Arrange
        var signal = Signal(channels, 300, 16);
        var encoder = new TestFlacEncoder { Id3Prefix = id3, ExtraMetadata = extraMetadata, BlockSize = 150 };

        // Act
        var clip = Decode(encoder.Encode(signal));

        // Assert
        Assert.Equal(channels, clip.ChannelCount);
        AssertMatches(signal, clip, 16);
    }

    /// <summary>Proves a stream with an unknown sample count decodes to its end and ignores trailing non-FLAC bytes.</summary>
    [Fact]
    public void FlacDecoder_Decode_UnknownTotal_DecodesUntilData_Ends()
    {
        // Arrange
        var signal = Signal(1, 700, 16);
        var encoder = new TestFlacEncoder { UnknownTotal = true, BlockSize = 256 };
        var bytes = encoder.Encode(signal).Concat("TAG trailing data"u8.ToArray()).ToArray();

        // Act
        var clip = Decode(bytes);

        // Assert
        AssertMatches(signal, clip, 16);
    }

    /// <summary>Proves a damaged frame header CRC and a damaged frame CRC are rejected, and tolerated when verification is off.</summary>
    [Fact]
    public void FlacDecoder_Decode_BadChecksums_ThrowUnlessVerificationDisabled()
    {
        // Arrange
        var signal = Signal(1, 100, 16);
        var good = new TestFlacEncoder { BlockSize = 100 }.Encode(signal);
        var badHeader = (byte[])good.Clone();
        badHeader[FirstFrameOffset + 6] ^= 0x55;
        var badFrame = (byte[])good.Clone();
        badFrame[^1] ^= 0x55;
        var lenient = new AudioFileReadOptions { VerifyChecksums = false };

        // Act / Assert
        Assert.Contains("CRC-8", Assert.Throws<InvalidDataException>(() => Decode(badHeader)).Message);
        Assert.Contains("CRC-16", Assert.Throws<InvalidDataException>(() => Decode(badFrame)).Message);
        AssertMatches(signal, Decode(badHeader, lenient), 16);
        AssertMatches(signal, Decode(badFrame, lenient), 16);
    }

    /// <summary>Proves truncation anywhere in the stream is reported.</summary>
    [Theory]
    [InlineData(30)]
    [InlineData(45)]
    [InlineData(120)]
    public void FlacDecoder_Decode_Truncated_Throws(int keep)
    {
        // Arrange
        var bytes = new TestFlacEncoder { BlockSize = 100 }.Encode(Signal(1, 300, 16));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => Decode(bytes[..keep]));
    }

    /// <summary>Proves a stream that ends before its declared sample count is reported.</summary>
    [Fact]
    public void FlacDecoder_Decode_MissingFrames_Throws()
    {
        // Arrange: drop the last frame of a three-frame stream
        var bytes = new TestFlacEncoder { BlockSize = 100 }.Encode(Signal(1, 300, 16));
        var frameLength = (bytes.Length - FirstFrameOffset) / 3;

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => Decode(bytes[..^frameLength]));
    }

    /// <summary>Proves malformed STREAMINFO and frame fields are rejected with <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void FlacDecoder_Decode_InvalidFields_Throw()
    {
        // Arrange
        var lenient = new AudioFileReadOptions { VerifyChecksums = false };
        var good = new TestFlacEncoder { BlockSize = 100 }.Encode(Signal(1, 100, 16));

        byte[] Tamper(int offset, byte value)
        {
            var copy = (byte[])good.Clone();
            copy[offset] = value;
            return copy;
        }

        // Act / Assert: STREAMINFO not first, damaged sync, reserved codes, mismatches
        Assert.Throws<InvalidDataException>(() => Decode(Tamper(4, 0x84)));
        Assert.Throws<InvalidDataException>(() => Decode(Tamper(FirstFrameOffset, 0x00), lenient));
        Assert.Throws<InvalidDataException>(() => Decode(Tamper(FirstFrameOffset + 1, 0xFA), lenient));
        Assert.Throws<InvalidDataException>(() => Decode(Tamper(FirstFrameOffset + 2, 0x09), lenient));
        Assert.Throws<InvalidDataException>(() => Decode(Tamper(FirstFrameOffset + 2, 0x6F), lenient));
        Assert.Throws<InvalidDataException>(() => Decode(Tamper(FirstFrameOffset + 2, 0x6A), lenient));
        Assert.Throws<InvalidDataException>(() => Decode(Tamper(FirstFrameOffset + 3, 0x10), lenient));
        Assert.Throws<InvalidDataException>(() => Decode(Tamper(FirstFrameOffset + 3, 0xB0), lenient));
        Assert.Throws<InvalidDataException>(() => Decode(Tamper(FirstFrameOffset + 3, 0x0C), lenient));
        Assert.Throws<InvalidDataException>(() => Decode(Tamper(FirstFrameOffset + 3, 0x01), lenient));
        Assert.Throws<InvalidDataException>(() => Decode(Tamper(FirstFrameOffset + 4, 0xFF), lenient));
    }

    /// <summary>Proves a frame larger than the STREAMINFO maximum and a damaged ID3 tag are rejected.</summary>
    [Fact]
    public void FlacDecoder_Decode_BlockLargerThanMaximum_Throws()
    {
        // Arrange
        var bytes = new TestFlacEncoder { BlockSize = 256, StreamInfoMaxBlockSize = 64 }.Encode(Signal(1, 256, 16));
        var damagedId3 = "ID3\u0004\0\0\0\0\u007F\u007F"u8.ToArray().Concat("fLaC"u8.ToArray()).ToArray();

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => Decode(bytes));
        Assert.Throws<InvalidDataException>(() => Decode(damagedId3));
    }

    /// <summary>Proves invalid predictor and residual parameters are rejected.</summary>
    [Fact]
    public void FlacDecoder_Decode_InvalidPredictorParameters_Throw()
    {
        // Arrange
        var signal = Signal(1, 64, 16);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => Decode(new TestFlacEncoder
        {
            Kind = FlacSubframeKind.Lpc,
            LpcShift = -1,
            BlockSize = 64
        }.Encode(signal)));
        Assert.Throws<InvalidDataException>(() => Decode(new TestFlacEncoder
        {
            Kind = FlacSubframeKind.Lpc,
            LpcPrecision = 16,
            BlockSize = 64
        }.Encode(signal)));
        Assert.Throws<InvalidDataException>(() => Decode(new TestFlacEncoder
        {
            Kind = FlacSubframeKind.Fixed,
            RiceMethod = 2,
            BlockSize = 64
        }.Encode(signal)));
        Assert.Throws<InvalidDataException>(() => Decode(new TestFlacEncoder
        {
            Kind = FlacSubframeKind.Fixed,
            PartitionOrder = 7,
            BlockSize = 64
        }.Encode(signal)));
    }

    /// <summary>Proves the sample cap rejects an oversized declared or unknown-length stream.</summary>
    [Fact]
    public void FlacDecoder_Decode_ExceedsMaxSamples_Throws()
    {
        // Arrange
        var known = new TestFlacEncoder { BlockSize = 100 }.Encode(Signal(1, 300, 16));
        var unknown = new TestFlacEncoder { BlockSize = 100, UnknownTotal = true }.Encode(Signal(1, 300, 16));
        var options = new AudioFileReadOptions { MaxSamples = 150 };

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => Decode(known, options));
        Assert.Throws<InvalidDataException>(() => Decode(unknown, options));
    }

    /// <summary>Returns +1 for even and -1 for odd indices.</summary>
    private static long AlternatingSign(int i) => i % 2 == 0 ? 1L : -1L;

    /// <summary>Decodes an image through the public reader.</summary>
    private static AudioClip Decode(byte[] bytes, AudioFileReadOptions? options = null) =>
        AudioFileReader.Read(new MemoryStream(bytes), options);

    /// <summary>Asserts the clip equals the (interleaved, scaled) signal exactly.</summary>
    private static void AssertMatches(int[][] signal, AudioClip clip, int bits)
    {
        var scale = Math.Pow(2, bits - 1);
        var frames = signal[0].Length;
        Assert.Equal(frames * signal.Length, clip.Samples.Length);
        for (var i = 0; i < frames; i++)
        {
            for (var c = 0; c < signal.Length; c++)
            {
                Assert.Equal((float)(signal[c][i] / scale), clip.Samples[(i * signal.Length) + c]);
            }
        }
    }

    /// <summary>Builds a deterministic smooth-plus-noise signal within the bit depth.</summary>
    private static int[][] Signal(int channels, int frames, int bits)
    {
        var random = new Random(1234 + channels);
        var peak = Math.Pow(2, bits - 1) - 1;
        return [.. Enumerable.Range(0, channels).Select(channel => Enumerable.Range(0, frames).Select(i =>
        {
            var value = (0.5 * Math.Sin(2 * Math.PI * (220 + (110 * channel)) * i / 8000.0)) + (0.05 * ((random.NextDouble() * 2) - 1));
            return (int)Math.Clamp(Math.Round(value * peak), -peak - 1, peak);
        }).ToArray())];
    }
}
