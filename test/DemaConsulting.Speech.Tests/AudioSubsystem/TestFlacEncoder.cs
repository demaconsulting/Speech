using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>How the encoder codes each subframe.</summary>
public enum FlacSubframeKind
{
    /// <summary>A constant subframe (the first sample of each block, repeated).</summary>
    Constant,

    /// <summary>A verbatim subframe.</summary>
    Verbatim,

    /// <summary>A fixed-predictor subframe.</summary>
    Fixed,

    /// <summary>A linear-predictor subframe.</summary>
    Lpc
}

/// <summary>How a stereo pair is coded.</summary>
public enum FlacStereoMode
{
    /// <summary>Independent channels.</summary>
    Independent,

    /// <summary>Left and side.</summary>
    LeftSide,

    /// <summary>Side and right.</summary>
    SideRight,

    /// <summary>Mid and side.</summary>
    MidSide
}

/// <summary>
///     A deterministic test-only FLAC writer able to emit every subframe type, residual coding method, stereo mode
///     and header code the decoder supports, so each decoder branch is covered even where libFLAC's own encoder
///     would never choose it. It favors clarity over compression.
/// </summary>
public sealed class TestFlacEncoder
{
    /// <summary>Gets or sets the sample rate in Hz.</summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>Gets or sets the bit depth.</summary>
    public int BitsPerSample { get; set; } = 16;

    /// <summary>Gets or sets the block size in samples per channel.</summary>
    public int BlockSize { get; set; } = 256;

    /// <summary>Gets or sets the subframe type.</summary>
    public FlacSubframeKind Kind { get; set; } = FlacSubframeKind.Verbatim;

    /// <summary>Gets or sets the predictor order for <see cref="FlacSubframeKind.Fixed"/> (0-4).</summary>
    public int FixedOrder { get; set; } = 2;

    /// <summary>Gets or sets the LPC coefficients for <see cref="FlacSubframeKind.Lpc"/>.</summary>
    public long[] LpcCoefficients { get; set; } = [2, -1];

    /// <summary>Gets or sets the LPC quantization shift.</summary>
    public int LpcShift { get; set; }

    /// <summary>Gets or sets the LPC coefficient precision in bits.</summary>
    public int LpcPrecision { get; set; } = 8;

    /// <summary>Gets or sets the residual coding method (0 = 4-bit parameters, 1 = 5-bit parameters).</summary>
    public int RiceMethod { get; set; }

    /// <summary>Gets or sets the residual partition order.</summary>
    public int PartitionOrder { get; set; }

    /// <summary>Gets or sets a value indicating whether partitions are coded with the escape code.</summary>
    public bool EscapePartitions { get; set; }

    /// <summary>Gets or sets the stereo coding for two-channel input.</summary>
    public FlacStereoMode Stereo { get; set; } = FlacStereoMode.Independent;

    /// <summary>Gets or sets the number of wasted (always-zero) low bits per sample.</summary>
    public int WastedBits { get; set; }

    /// <summary>Gets or sets a value indicating whether an ID3v2 tag is written first.</summary>
    public bool Id3Prefix { get; set; }

    /// <summary>Gets or sets a value indicating whether the STREAMINFO total sample count is left as zero (unknown).</summary>
    public bool UnknownTotal { get; set; }

    /// <summary>Gets or sets a value indicating whether frames repeat the rate/depth instead of coding "as STREAMINFO".</summary>
    public bool ExplicitHeaderCodes { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether a PADDING and a VORBIS_COMMENT block precede the audio.</summary>
    public bool ExtraMetadata { get; set; }

    /// <summary>Gets or sets the maximum block size written to STREAMINFO (zero = <see cref="BlockSize"/>).</summary>
    public int StreamInfoMaxBlockSize { get; set; }

    /// <summary>
    ///     Encodes the channels (each an array of samples within the bit depth) into a FLAC image.
    /// </summary>
    /// <param name="channels">The per-channel samples; all arrays have the same length.</param>
    /// <returns>The FLAC bytes.</returns>
    public byte[] Encode(int[][] channels)
    {
        var output = new List<byte>();
        if (Id3Prefix)
        {
            output.AddRange("ID3\u0004\0\0\0\0\0\u000A"u8.ToArray());
            output.AddRange(new byte[10]);
        }

        output.AddRange("fLaC"u8.ToArray());
        WriteMetadata(output, channels.Length, channels[0].Length);

        var total = channels[0].Length;
        var frameIndex = 0;
        for (var start = 0; start < total; start += BlockSize)
        {
            var length = Math.Min(BlockSize, total - start);
            var block = channels.Select(c => c.Skip(start).Take(length).Select(v => (long)v).ToArray()).ToArray();
            output.AddRange(EncodeFrame(block, frameIndex++));
        }

        return [.. output];
    }

    /// <summary>Writes STREAMINFO (and optional filler blocks), flagging the final block.</summary>
    private void WriteMetadata(List<byte> output, int channelCount, int total)
    {
        var info = new TestBitWriter();
        info.Write((ulong)Math.Min(BlockSize, 16), 16);
        info.Write((ulong)(StreamInfoMaxBlockSize == 0 ? BlockSize : StreamInfoMaxBlockSize), 16);
        info.Write(0, 24);
        info.Write(0, 24);
        info.Write((ulong)SampleRate, 20);
        info.Write((ulong)(channelCount - 1), 3);
        info.Write((ulong)(BitsPerSample - 1), 5);
        info.Write(UnknownTotal ? 0UL : (ulong)total, 36);
        info.Write(0, 64);
        info.Write(0, 64);

        // STREAMINFO (type 0) must come first; PADDING (1) and VORBIS_COMMENT (4) are skipped by length
        var blocks = new List<(int Type, byte[] Body)> { (0, info.ToArray()) };
        if (ExtraMetadata)
        {
            blocks.Add((1, new byte[4]));
            blocks.Add((4, [1, 2, 3, 4, 5, 6, 7, 8]));
        }

        for (var i = 0; i < blocks.Count; i++)
        {
            var (type, body) = blocks[i];
            output.Add((byte)((i == blocks.Count - 1 ? 0x80 : 0) | type));
            output.AddRange([(byte)(body.Length >> 16), (byte)(body.Length >> 8), (byte)body.Length]);
            output.AddRange(body);
        }
    }
    /// <summary>Encodes one frame.</summary>
    private byte[] EncodeFrame(long[][] block, int frameIndex)
    {
        var writer = new TestBitWriter();
        var length = block[0].Length;
        var assignment = block.Length - 1;
        var coded = block;
        if (block.Length == 2 && Stereo != FlacStereoMode.Independent)
        {
            (assignment, coded) = Decorrelate(block);
        }

        WriteHeader(writer, length, assignment, frameIndex);
        for (var channel = 0; channel < coded.Length; channel++)
        {
            var bits = BitsPerSample + (IsSide(assignment, channel) ? 1 : 0);
            WriteSubframe(writer, coded[channel], bits);
        }

        writer.Align();
        var bytes = writer.ToArray().ToList();
        var crc = FlacCrc.Crc16(bytes.ToArray());
        bytes.Add((byte)(crc >> 8));
        bytes.Add((byte)crc);
        return [.. bytes];
    }

    /// <summary>Returns the channel assignment and coded channels for the configured stereo mode.</summary>
    private (int Assignment, long[][] Coded) Decorrelate(long[][] block)
    {
        var left = block[0];
        var right = block[1];
        var side = left.Zip(right, (l, r) => l - r).ToArray();
        return Stereo switch
        {
            FlacStereoMode.LeftSide => (8, [left, side]),
            FlacStereoMode.SideRight => (9, [side, right]),
            _ => (10, [left.Zip(right, (l, r) => (l + r) >> 1).ToArray(), side])
        };
    }

    /// <summary>Returns whether the coded channel is the side signal.</summary>
    private static bool IsSide(int assignment, int channel) =>
        (assignment == 8 && channel == 1) || (assignment == 9 && channel == 0) || (assignment == 10 && channel == 1);

    /// <summary>Writes a frame header including its CRC-8.</summary>
    private void WriteHeader(TestBitWriter writer, int length, int assignment, int frameIndex)
    {
        writer.Write(0x3FFE, 14);
        writer.Write(0, 2);

        var blockCode = BlockSizeCode(length);
        writer.Write((ulong)blockCode, 4);
        var rateCode = ExplicitHeaderCodes ? SampleRateCode() : 0;
        writer.Write((ulong)rateCode, 4);
        writer.Write((ulong)assignment, 4);
        writer.Write((ulong)(ExplicitHeaderCodes ? BitDepthCode() : 0), 3);
        writer.Write(0, 1);
        WriteUtf8(writer, frameIndex);
        if (blockCode == 6)
        {
            writer.Write((ulong)(length - 1), 8);
        }
        else if (blockCode == 7)
        {
            writer.Write((ulong)(length - 1), 16);
        }

        switch (rateCode)
        {
            case 12:
                writer.Write((ulong)(SampleRate / 1000), 8);
                break;
            case 13:
                writer.Write((ulong)SampleRate, 16);
                break;
            case 14:
                writer.Write((ulong)(SampleRate / 10), 16);
                break;
        }

        writer.Write(FlacCrc.Crc8(writer.ToArray()), 8);
    }

    /// <summary>Picks the header block size code.</summary>
    private static int BlockSizeCode(int length) => length switch
    {
        192 => 1,
        576 => 2,
        1152 => 3,
        2304 => 4,
        4608 => 5,
        256 => 8,
        512 => 9,
        1024 => 10,
        2048 => 11,
        4096 => 12,
        8192 => 13,
        16384 => 14,
        32768 => 15,
        <= 256 => 6,
        _ => 7
    };

    /// <summary>Picks the header sample rate code.</summary>
    private int SampleRateCode()
    {
        int[] table = [88200, 176400, 192000, 8000, 16000, 22050, 24000, 32000, 44100, 48000, 96000];
        var index = Array.IndexOf(table, SampleRate);
        if (index >= 0)
        {
            return index + 1;
        }

        if (SampleRate % 1000 == 0 && SampleRate / 1000 <= 255)
        {
            return 12;
        }

        return SampleRate <= 65535 ? 13 : 14;
    }

    /// <summary>Picks the header bit depth code (zero when the depth has no code).</summary>
    private int BitDepthCode() => BitsPerSample switch { 8 => 1, 12 => 2, 16 => 4, 20 => 5, 24 => 6, 32 => 7, _ => 0 };

    /// <summary>Writes a UTF-8 style number.</summary>
    private static void WriteUtf8(TestBitWriter writer, int value)
    {
        if (value < 0x80)
        {
            writer.Write((ulong)value, 8);
        }
        else if (value < 0x800)
        {
            writer.Write((ulong)(0xC0 | (value >> 6)), 8);
            writer.Write((ulong)(0x80 | (value & 0x3F)), 8);
        }
        else
        {
            writer.Write((ulong)(0xE0 | (value >> 12)), 8);
            writer.Write((ulong)(0x80 | ((value >> 6) & 0x3F)), 8);
            writer.Write((ulong)(0x80 | (value & 0x3F)), 8);
        }
    }

    /// <summary>Writes one subframe.</summary>
    private void WriteSubframe(TestBitWriter writer, long[] samples, int bits)
    {
        var effective = bits - WastedBits;
        var data = WastedBits == 0 ? samples : samples.Select(s => s >> WastedBits).ToArray();

        writer.Write(0, 1);
        var type = Kind switch
        {
            FlacSubframeKind.Constant => 0,
            FlacSubframeKind.Verbatim => 1,
            FlacSubframeKind.Fixed => 8 + FixedOrder,
            _ => 32 + LpcCoefficients.Length - 1
        };
        writer.Write((ulong)type, 6);
        if (WastedBits > 0)
        {
            writer.Write(1, 1);
            writer.Write(1, WastedBits);
        }
        else
        {
            writer.Write(0, 1);
        }

        switch (Kind)
        {
            case FlacSubframeKind.Constant:
                writer.WriteSigned(data[0], effective);
                break;
            case FlacSubframeKind.Verbatim:
                foreach (var sample in data)
                {
                    writer.WriteSigned(sample, effective);
                }

                break;
            case FlacSubframeKind.Fixed:
                WriteFixed(writer, data, effective);
                break;
            default:
                WriteLpc(writer, data, effective);
                break;
        }
    }

    /// <summary>Writes a fixed-predictor body.</summary>
    private void WriteFixed(TestBitWriter writer, long[] data, int bits)
    {
        long[][] coefficients = [[], [1], [2, -1], [3, -3, 1], [4, -6, 4, -1]];
        for (var i = 0; i < FixedOrder; i++)
        {
            writer.WriteSigned(data[i], bits);
        }

        var residual = new long[data.Length - FixedOrder];
        for (var i = FixedOrder; i < data.Length; i++)
        {
            long prediction = 0;
            for (var j = 0; j < FixedOrder; j++)
            {
                prediction += coefficients[FixedOrder][j] * data[i - 1 - j];
            }

            residual[i - FixedOrder] = data[i] - prediction;
        }

        WriteResidual(writer, residual, data.Length);
    }

    /// <summary>Writes a linear-predictor body.</summary>
    private void WriteLpc(TestBitWriter writer, long[] data, int bits)
    {
        var order = LpcCoefficients.Length;
        for (var i = 0; i < order; i++)
        {
            writer.WriteSigned(data[i], bits);
        }

        writer.Write((ulong)(LpcPrecision - 1), 4);
        writer.WriteSigned(LpcShift, 5);
        foreach (var coefficient in LpcCoefficients)
        {
            writer.WriteSigned(coefficient, LpcPrecision);
        }

        var residual = new long[data.Length - order];
        for (var i = order; i < data.Length; i++)
        {
            long sum = 0;
            for (var j = 0; j < order; j++)
            {
                sum += LpcCoefficients[j] * data[i - 1 - j];
            }

            residual[i - order] = data[i] - (sum >> LpcShift);
        }

        WriteResidual(writer, residual, data.Length);
    }

    /// <summary>Writes a Rice-coded residual section.</summary>
    private void WriteResidual(TestBitWriter writer, long[] residual, int blockSize)
    {
        writer.Write((ulong)RiceMethod, 2);
        writer.Write((ulong)PartitionOrder, 4);
        var parameterBits = RiceMethod == 0 ? 4 : 5;
        var partitions = 1 << PartitionOrder;
        var partitionSize = blockSize >> PartitionOrder;
        var predictorOrder = blockSize - residual.Length;
        var index = 0;
        for (var partition = 0; partition < partitions; partition++)
        {
            var count = partitionSize - (partition == 0 ? predictorOrder : 0);
            var slice = residual.Skip(index).Take(count).ToArray();
            index += count;
            if (EscapePartitions)
            {
                var width = slice.Length == 0 ? 0 : slice.Max(v => SignedWidth(v));
                writer.Write((ulong)((1 << parameterBits) - 1), parameterBits);
                writer.Write((ulong)width, 5);
                foreach (var value in slice)
                {
                    writer.WriteSigned(value, width);
                }
            }
            else
            {
                WriteRicePartition(writer, slice, parameterBits);
            }
        }
    }

    /// <summary>Writes one Rice partition with the cheapest parameter.</summary>
    private static void WriteRicePartition(TestBitWriter writer, long[] slice, int parameterBits)
    {
        var folded = slice.Select(v => (ulong)((v << 1) ^ (v >> 63))).ToArray();
        var maxParameter = (1 << parameterBits) - 2;
        var best = 0;
        var bestCost = long.MaxValue;
        for (var k = 0; k <= maxParameter; k++)
        {
            var cost = folded.Sum(u => (long)(u >> k) + 1 + k);
            if (cost < bestCost)
            {
                bestCost = cost;
                best = k;
            }
        }

        writer.Write((ulong)best, parameterBits);
        foreach (var u in folded)
        {
            writer.Write(0, (int)(u >> best));
            writer.Write(1, 1);
            writer.Write(u & ((1UL << best) - 1), best);
        }
    }

    /// <summary>Returns the minimum two's-complement width that holds the value.</summary>
    private static int SignedWidth(long value)
    {
        var width = 1;
        while (value < -(1L << (width - 1)) || value > (1L << (width - 1)) - 1)
        {
            width++;
        }

        return width;
    }
}
