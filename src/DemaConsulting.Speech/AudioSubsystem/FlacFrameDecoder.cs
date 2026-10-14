namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Decodes one FLAC frame (header, subframes, footer) into per-channel sample buffers.
/// </summary>
/// <remarks>
///     Implements RFC 9639 sections on frame headers, the constant/verbatim/fixed/LPC subframes, Rice-coded
///     residuals (methods 0 and 1, including escape partitions), wasted bits and the three stereo decorrelation
///     modes. All prediction and stereo arithmetic is performed in <see cref="long"/> so 24/32-bit streams with a
///     side channel and 32-coefficient predictors cannot overflow. Stateless and thread-safe.
/// </remarks>
internal static class FlacFrameDecoder
{
    /// <summary>The block sizes for header codes 8-15 are <c>256 &lt;&lt; (code - 8)</c>.</summary>
    private const int FirstPowerOfTwoBlockCode = 8;

    /// <summary>The sample rates, in Hz, for header codes 1-11.</summary>
    private static readonly int[] CodedSampleRates =
        [88200, 176400, 192000, 8000, 16000, 22050, 24000, 32000, 44100, 48000, 96000];

    /// <summary>The bit depths for header codes 1-7 (zero marks a reserved code or "from STREAMINFO").</summary>
    private static readonly int[] CodedBitDepths = [0, 8, 12, 0, 16, 20, 24, 32];

    /// <summary>The fixed-predictor coefficients by order, applied to the previous samples (newest first).</summary>
    private static readonly long[][] FixedCoefficients = [[], [1], [2, -1], [3, -3, 1], [4, -6, 4, -1]];

    /// <summary>
    ///     Decodes the frame starting at the reader's (byte-aligned) position.
    /// </summary>
    /// <param name="reader">The bit reader, positioned on the frame sync code.</param>
    /// <param name="info">The stream parameters.</param>
    /// <param name="verify">Whether the CRC-8 and CRC-16 are verified.</param>
    /// <param name="buffers">One buffer per channel, each at least the stream's maximum block size long.</param>
    /// <returns>The block size, in samples per channel, written to <paramref name="buffers"/>.</returns>
    /// <exception cref="InvalidDataException">Thrown when the frame is malformed, truncated or inconsistent with <paramref name="info"/>.</exception>
    public static int Decode(FlacBitReader reader, FlacStreamInfo info, bool verify, long[][] buffers)
    {
        var data = reader.Data;
        var start = reader.BytePosition;
        var header = ReadHeader(reader, info, verify);

        for (var channel = 0; channel < info.ChannelCount; channel++)
        {
            // The side channel of a stereo pair carries one extra bit of precision
            var bits = header.BitsPerSample + (IsSideChannel(header.ChannelAssignment, channel) ? 1 : 0);
            DecodeSubframe(reader, bits, header.BlockSize, buffers[channel]);
        }

        reader.AlignToByte();
        var footerStart = reader.BytePosition;
        var expected = reader.ReadBits(16);
        if (verify && FlacCrc.Crc16(data.AsSpan(start, footerStart - start)) != expected)
        {
            throw new InvalidDataException($"The FLAC frame at byte {start} failed its CRC-16 check.");
        }

        Decorrelate(header.ChannelAssignment, buffers, header.BlockSize);
        return header.BlockSize;
    }

    /// <summary>Parses and validates a frame header, including its CRC-8.</summary>
    private static FrameHeader ReadHeader(FlacBitReader reader, FlacStreamInfo info, bool verify)
    {
        var start = reader.BytePosition;
        var sync = reader.ReadBits(16);
        if ((sync >> 2) != 0x3FFE || (sync & 0x02) != 0)
        {
            throw new InvalidDataException($"No FLAC frame sync code at byte {start}.");
        }

        var blockCode = (int)reader.ReadBits(4);
        var rateCode = (int)reader.ReadBits(4);
        var assignment = (int)reader.ReadBits(4);
        var sizeCode = (int)reader.ReadBits(3);
        if (reader.ReadBits(1) != 0)
        {
            throw new InvalidDataException($"The FLAC frame at byte {start} uses a reserved header bit.");
        }

        reader.ReadUtf8Number();
        var blockSize = ReadBlockSize(reader, blockCode, start);
        var rate = ReadSampleRate(reader, rateCode, info, start);
        var bits = ReadBitDepth(sizeCode, info, start);

        var headerEnd = reader.BytePosition;
        var expectedCrc = (byte)reader.ReadBits(8);
        if (verify && FlacCrc.Crc8(reader.Data.AsSpan(start, headerEnd - start)) != expectedCrc)
        {
            throw new InvalidDataException($"The FLAC frame header at byte {start} failed its CRC-8 check.");
        }

        ValidateFrame(blockSize, rate, assignment, info, start);
        return new FrameHeader(blockSize, assignment, bits);
    }

    /// <summary>Checks the parsed header against STREAMINFO.</summary>
    private static void ValidateFrame(int blockSize, int rate, int assignment, FlacStreamInfo info, int start)
    {
        var channels = assignment < 8 ? assignment + 1 : 2;
        if (assignment > 10)
        {
            throw new InvalidDataException($"The FLAC frame at byte {start} uses a reserved channel assignment.");
        }

        if (channels != info.ChannelCount || rate != info.SampleRate)
        {
            throw new InvalidDataException(
                $"The FLAC frame at byte {start} disagrees with STREAMINFO ({channels} channel(s) at {rate} Hz).");
        }

        if (blockSize > info.MaxBlockSize)
        {
            throw new InvalidDataException(
                $"The FLAC frame at byte {start} has {blockSize} samples, more than the stream maximum {info.MaxBlockSize}.");
        }
    }

    /// <summary>Decodes the block size from its header code.</summary>
    private static int ReadBlockSize(FlacBitReader reader, int code, int start) => code switch
    {
        0 => throw new InvalidDataException($"The FLAC frame at byte {start} uses a reserved block size code."),
        1 => 192,
        >= 2 and <= 5 => 576 << (code - 2),
        6 => (int)reader.ReadBits(8) + 1,
        7 => (int)reader.ReadBits(16) + 1,
        _ => 256 << (code - FirstPowerOfTwoBlockCode)
    };

    /// <summary>Decodes the sample rate from its header code (zero means "as in STREAMINFO").</summary>
    private static int ReadSampleRate(FlacBitReader reader, int code, FlacStreamInfo info, int start) => code switch
    {
        0 => info.SampleRate,
        <= 11 => CodedSampleRates[code - 1],
        12 => (int)reader.ReadBits(8) * 1000,
        13 => (int)reader.ReadBits(16),
        14 => (int)reader.ReadBits(16) * 10,
        _ => throw new InvalidDataException($"The FLAC frame at byte {start} uses a reserved sample rate code.")
    };

    /// <summary>Decodes the bit depth from its header code (zero means "as in STREAMINFO").</summary>
    private static int ReadBitDepth(int code, FlacStreamInfo info, int start)
    {
        if (code == 0)
        {
            return info.BitsPerSample;
        }

        var bits = CodedBitDepths[code];
        if (bits == 0 || bits != info.BitsPerSample)
        {
            throw new InvalidDataException(
                $"The FLAC frame at byte {start} has a reserved or inconsistent bit depth (stream is {info.BitsPerSample}-bit).");
        }

        return bits;
    }

    /// <summary>Returns whether the channel carries the stereo "side" signal.</summary>
    private static bool IsSideChannel(int assignment, int channel) =>
        (assignment == 8 && channel == 1) || (assignment == 9 && channel == 0) || (assignment == 10 && channel == 1);

    /// <summary>Decodes one subframe into <paramref name="output"/>.</summary>
    private static void DecodeSubframe(FlacBitReader reader, int bits, int blockSize, long[] output)
    {
        if (reader.ReadBits(1) != 0)
        {
            throw new InvalidDataException($"A FLAC subframe at byte {reader.BytePosition} has a non-zero padding bit.");
        }

        var type = (int)reader.ReadBits(6);
        var wasted = 0;
        if (reader.ReadBits(1) == 1)
        {
            // Range-checked before narrowing to int: a unary run longer than the sample's bit depth
            // is malformed, and an absurdly long run (over int.MaxValue zero bits) must not wrap
            var wastedRun = reader.ReadUnary();
            if (wastedRun >= bits)
            {
                throw new InvalidDataException($"A FLAC subframe at byte {reader.BytePosition} wastes more bits than the sample has.");
            }

            wasted = (int)wastedRun + 1;
        }

        var effectiveBits = bits - wasted;
        if (effectiveBits < 1 || effectiveBits > 33)
        {
            throw new InvalidDataException($"A FLAC subframe at byte {reader.BytePosition} wastes more bits than the sample has.");
        }

        DecodeSubframeBody(reader, type, effectiveBits, blockSize, output);
        if (wasted > 0)
        {
            for (var i = 0; i < blockSize; i++)
            {
                output[i] <<= wasted;
            }
        }
    }

    /// <summary>Dispatches on the subframe type.</summary>
    private static void DecodeSubframeBody(FlacBitReader reader, int type, int bits, int blockSize, long[] output)
    {
        if (type == 0)
        {
            Array.Fill(output, reader.ReadSigned(bits), 0, blockSize);
        }
        else if (type == 1)
        {
            for (var i = 0; i < blockSize; i++)
            {
                output[i] = reader.ReadSigned(bits);
            }
        }
        else if (type is >= 8 and <= 12)
        {
            DecodeFixed(reader, type - 8, bits, blockSize, output);
        }
        else if (type >= 32)
        {
            DecodeLpc(reader, type - 31, bits, blockSize, output);
        }
        else
        {
            throw new InvalidDataException($"A FLAC subframe at byte {reader.BytePosition} has a reserved type {type}.");
        }
    }

    /// <summary>Decodes a fixed-predictor subframe of the given order.</summary>
    private static void DecodeFixed(FlacBitReader reader, int order, int bits, int blockSize, long[] output)
    {
        ReadWarmUp(reader, order, bits, blockSize, output);
        DecodeResidual(reader, blockSize, order, output);
        var coefficients = FixedCoefficients[order];
        for (var i = order; i < blockSize; i++)
        {
            long prediction = 0;
            for (var j = 0; j < order; j++)
            {
                prediction += coefficients[j] * output[i - 1 - j];
            }

            output[i] += prediction;
        }
    }

    /// <summary>Decodes a linear-predictor subframe of the given order.</summary>
    private static void DecodeLpc(FlacBitReader reader, int order, int bits, int blockSize, long[] output)
    {
        ReadWarmUp(reader, order, bits, blockSize, output);

        var precisionCode = (int)reader.ReadBits(4);
        if (precisionCode == 15)
        {
            throw new InvalidDataException($"A FLAC subframe at byte {reader.BytePosition} has an invalid predictor precision.");
        }

        // RFC 9639 section 9.2.6: the right shift is coded signed, but "MUST NOT be negative" (see Appendix B.4)
        var shift = (int)reader.ReadSigned(5);
        if (shift < 0)
        {
            throw new InvalidDataException($"A FLAC subframe at byte {reader.BytePosition} has a negative predictor shift.");
        }

        var coefficients = new long[order];
        for (var j = 0; j < order; j++)
        {
            coefficients[j] = reader.ReadSigned(precisionCode + 1);
        }

        DecodeResidual(reader, blockSize, order, output);
        for (var i = order; i < blockSize; i++)
        {
            long sum = 0;
            for (var j = 0; j < order; j++)
            {
                sum += coefficients[j] * output[i - 1 - j];
            }

            output[i] += sum >> shift;
        }
    }

    /// <summary>Reads the verbatim warm-up samples of a predictive subframe.</summary>
    private static void ReadWarmUp(FlacBitReader reader, int order, int bits, int blockSize, long[] output)
    {
        if (order > blockSize)
        {
            throw new InvalidDataException(
                $"A FLAC subframe at byte {reader.BytePosition} has predictor order {order} above its block size {blockSize}.");
        }

        for (var i = 0; i < order; i++)
        {
            output[i] = reader.ReadSigned(bits);
        }
    }

    /// <summary>Decodes the Rice-coded residual into <c>output[predictorOrder..blockSize)</c>.</summary>
    private static void DecodeResidual(FlacBitReader reader, int blockSize, int predictorOrder, long[] output)
    {
        var method = (int)reader.ReadBits(2);
        if (method > 1)
        {
            throw new InvalidDataException($"A FLAC residual at byte {reader.BytePosition} uses a reserved coding method.");
        }

        var parameterBits = method == 0 ? 4 : 5;
        var escape = (1 << parameterBits) - 1;
        var partitionOrder = (int)reader.ReadBits(4);
        var partitions = 1 << partitionOrder;
        var partitionSize = blockSize >> partitionOrder;
        if ((blockSize & (partitions - 1)) != 0 || partitionSize < predictorOrder)
        {
            throw new InvalidDataException(
                $"A FLAC residual at byte {reader.BytePosition} has partition order {partitionOrder} unsuited to {blockSize} samples.");
        }

        var index = predictorOrder;
        for (var partition = 0; partition < partitions; partition++)
        {
            var end = (partition + 1) * partitionSize;
            var parameter = (int)reader.ReadBits(parameterBits);
            if (parameter == escape)
            {
                ReadEscapedPartition(reader, index, end, output);
            }
            else
            {
                ReadRicePartition(reader, parameter, index, end, output);
            }

            index = end;
        }
    }

    /// <summary>Reads a partition of raw, fixed-width residuals.</summary>
    private static void ReadEscapedPartition(FlacBitReader reader, int from, int to, long[] output)
    {
        var width = (int)reader.ReadBits(5);
        for (var i = from; i < to; i++)
        {
            output[i] = reader.ReadSigned(width);
        }
    }

    /// <summary>Reads a partition of Rice-coded residuals with the given parameter.</summary>
    private static void ReadRicePartition(FlacBitReader reader, int parameter, int from, int to, long[] output)
    {
        for (var i = from; i < to; i++)
        {
            var quotient = reader.ReadUnary();
            var folded = (quotient << parameter) | reader.ReadBits(parameter);
            output[i] = (folded >> 1) ^ -(folded & 1);
        }
    }

    /// <summary>Reverses left/side, side/right or mid/side stereo coding in place.</summary>
    private static void Decorrelate(int assignment, long[][] buffers, int blockSize)
    {
        if (assignment < 8)
        {
            return;
        }

        var first = buffers[0];
        var second = buffers[1];
        for (var i = 0; i < blockSize; i++)
        {
            switch (assignment)
            {
                case 8:
                    second[i] = first[i] - second[i];
                    break;
                case 9:
                    first[i] += second[i];
                    break;
                default:
                    var side = second[i];
                    var mid = (first[i] << 1) | (side & 1);
                    first[i] = (mid + side) >> 1;
                    second[i] = (mid - side) >> 1;
                    break;
            }
        }
    }

    /// <summary>The validated frame header fields the subframe decoder needs.</summary>
    private sealed record FrameHeader(int BlockSize, int ChannelAssignment, int BitsPerSample);
}
