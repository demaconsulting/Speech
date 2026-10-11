namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     The fields of a FLAC STREAMINFO metadata block that the decoder needs.
/// </summary>
/// <param name="MaxBlockSize">The largest block size, in samples per channel, of any frame.</param>
/// <param name="SampleRate">The sample rate in Hz.</param>
/// <param name="ChannelCount">The channel count, 1-8.</param>
/// <param name="BitsPerSample">The bit depth, 4-32.</param>
/// <param name="TotalSamples">The samples per channel, or zero when unknown.</param>
internal sealed record FlacStreamInfo(int MaxBlockSize, int SampleRate, int ChannelCount, int BitsPerSample, long TotalSamples);

/// <summary>
///     Decodes FLAC native-framed streams (RFC 9639) into <see cref="AudioClip"/> values.
/// </summary>
/// <remarks>
///     Handles an optional leading ID3v2 tag, skips every metadata block except STREAMINFO, then decodes frames
///     with <see cref="FlacFrameDecoder"/> until the declared sample count is reached (or the data ends, when
///     the count is unknown). Failures are reported as <see cref="InvalidDataException"/> with the byte offset;
///     damaged frames are never skipped. The MD5 signature is not verified, but the per-frame CRC-8 and CRC-16
///     are (unless disabled). Samples are scaled by <c>2^(bits-1)</c> into [-1, 1). Stateless and thread-safe.
/// </remarks>
internal static class FlacDecoder
{
    /// <summary>The size of a STREAMINFO block body.</summary>
    private const int StreamInfoLength = 34;

    /// <summary>Gets a value indicating whether <paramref name="data"/> is a FLAC stream, optionally behind an ID3v2 tag.</summary>
    /// <param name="data">The file bytes.</param>
    /// <returns><see langword="true"/> when the <c>fLaC</c> marker is found.</returns>
    public static bool IsFlac(ReadOnlySpan<byte> data)
    {
        var start = SkipId3(data);
        return start >= 0 && data.Length >= start + 4 && data.Slice(start, 4).SequenceEqual("fLaC"u8);
    }

    /// <summary>
    ///     Decodes a FLAC stream.
    /// </summary>
    /// <param name="data">The file bytes.</param>
    /// <param name="verifyChecksums">Whether frame CRCs are verified.</param>
    /// <param name="maxSamples">The most samples (across channels) the clip may contain.</param>
    /// <returns>The decoded clip.</returns>
    /// <exception cref="InvalidDataException">Thrown when the stream is malformed, truncated or too large.</exception>
    public static AudioClip Decode(byte[] data, bool verifyChecksums, long maxSamples)
    {
        var position = SkipId3(data);
        if (position < 0 || !IsFlac(data))
        {
            throw new InvalidDataException("The data is not a FLAC stream.");
        }

        var info = ReadMetadata(data, ref position);
        if (info.TotalSamples * info.ChannelCount > maxSamples)
        {
            throw new InvalidDataException(
                $"The FLAC stream declares {info.TotalSamples * info.ChannelCount} samples, more than the allowed {maxSamples}.");
        }

        var samples = DecodeFrames(data, position, info, verifyChecksums, maxSamples);
        return new AudioClip(samples, info.SampleRate, info.ChannelCount, info.BitsPerSample);
    }

    /// <summary>Returns the offset just past a leading ID3v2 tag (zero when there is none), or -1 when the tag is damaged.</summary>
    private static int SkipId3(ReadOnlySpan<byte> data)
    {
        if (data.Length < 10 || !data[..3].SequenceEqual("ID3"u8))
        {
            return 0;
        }

        // The tag size is four 7-bit "syncsafe" bytes; a footer adds ten more bytes when flagged
        var size = (data[6] << 21) | (data[7] << 14) | (data[8] << 7) | data[9];
        var footer = (data[5] & 0x10) != 0 ? 10 : 0;
        var end = 10L + size + footer;
        return end <= data.Length ? (int)end : -1;
    }

    /// <summary>Walks the metadata blocks, returning STREAMINFO and leaving <paramref name="position"/> at the first frame.</summary>
    private static FlacStreamInfo ReadMetadata(byte[] data, ref int position)
    {
        position += 4;
        FlacStreamInfo? info = null;
        var isLast = false;
        while (!isLast)
        {
            if (position + 4 > data.Length)
            {
                throw new InvalidDataException($"The FLAC stream is truncated in its metadata (byte {position}).");
            }

            isLast = (data[position] & 0x80) != 0;
            var type = data[position] & 0x7F;
            var length = (data[position + 1] << 16) | (data[position + 2] << 8) | data[position + 3];
            var body = position + 4;
            if (body + length > data.Length)
            {
                throw new InvalidDataException($"The FLAC metadata block at byte {position} runs past the end of the data.");
            }

            if (info is null)
            {
                // STREAMINFO must come first; every other block type is skipped by length
                if (type != 0 || length != StreamInfoLength)
                {
                    throw new InvalidDataException("The FLAC stream does not start with a STREAMINFO block.");
                }

                info = ParseStreamInfo(new FlacBitReader(data, body));
            }

            position = body + length;
        }

        return info ?? throw new InvalidDataException("The FLAC stream has no STREAMINFO block.");
    }

    /// <summary>Parses and validates a STREAMINFO block body.</summary>
    private static FlacStreamInfo ParseStreamInfo(FlacBitReader reader)
    {
        reader.ReadBits(16);
        var maxBlock = (int)reader.ReadBits(16);
        reader.ReadBits(24);
        reader.ReadBits(24);
        var rate = (int)reader.ReadBits(20);
        var channels = (int)reader.ReadBits(3) + 1;
        var bits = (int)reader.ReadBits(5) + 1;
        var total = ((long)reader.ReadBits(4) << 32) | reader.ReadBits(32);

        if (rate is < 1 or > 655350 || bits < 4 || maxBlock < 1)
        {
            throw new InvalidDataException(
                $"The FLAC STREAMINFO is invalid (rate {rate} Hz, {bits} bits, maximum block size {maxBlock}).");
        }

        return new FlacStreamInfo(maxBlock, rate, channels, bits, total);
    }

    /// <summary>Decodes all frames into one interleaved float array.</summary>
    private static float[] DecodeFrames(byte[] data, int position, FlacStreamInfo info, bool verify, long maxSamples)
    {
        var buffers = new long[info.ChannelCount][];
        for (var channel = 0; channel < buffers.Length; channel++)
        {
            buffers[channel] = new long[info.MaxBlockSize];
        }

        // The declared length only decides when to stop; the buffer grows on demand so a hostile header cannot force a huge allocation
        var known = info.TotalSamples > 0;
        var target = known ? info.TotalSamples * info.ChannelCount : long.MaxValue;
        var output = new float[Math.Min(target, 1 << 16)];
        var written = 0L;
        var reader = new FlacBitReader(data, position);
        var scale = Math.Pow(2, info.BitsPerSample - 1);

        while (written < target && (known || reader.PeekFrameSync()))
        {
            var blockSize = FlacFrameDecoder.Decode(reader, info, verify, buffers);
            var frames = (int)Math.Min(blockSize, (target - written) / info.ChannelCount);
            var needed = written + ((long)frames * info.ChannelCount);
            if (needed > maxSamples)
            {
                throw new InvalidDataException($"The FLAC stream holds more than the allowed {maxSamples} samples.");
            }

            if (needed > output.Length)
            {
                Array.Resize(ref output, (int)Math.Min(Math.Max(needed, output.Length * 2L), maxSamples));
            }

            written = Interleave(buffers, frames, scale, output, written);
        }

        if (written != output.Length)
        {
            Array.Resize(ref output, (int)written);
        }

        return output;
    }

    /// <summary>Writes the first <paramref name="frames"/> decoded sample frames, interleaved, and returns the new write position.</summary>
    private static long Interleave(long[][] buffers, int frames, double scale, float[] output, long written)
    {
        for (var i = 0; i < frames; i++)
        {
            foreach (var channel in buffers)
            {
                output[written++] = (float)(channel[i] / scale);
            }
        }

        return written;
    }
}
