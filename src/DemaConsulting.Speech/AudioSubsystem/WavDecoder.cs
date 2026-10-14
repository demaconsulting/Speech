using System.Buffers.Binary;

namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Decodes RIFF/WAVE images (PCM 8/16/24/32-bit, IEEE float 32-bit, and the same encodings inside
///     <c>WAVE_FORMAT_EXTENSIBLE</c>) into <see cref="AudioClip"/> values.
/// </summary>
/// <remarks>
///     A small chunk walker: unknown chunks (for example <c>LIST</c>) are skipped, chunks are word aligned, and a
///     <c>data</c> size that overstates the file (streamed writers, including 0xFFFFFFFF) is clamped to the bytes
///     actually present; when several <c>fmt </c> chunks appear the last one before <c>data</c> wins.
///     8-bit PCM is unsigned per the WAV convention. 32-bit float samples are passed through as stored, so a
///     NaN or out-of-range value in the file reaches the clip unchanged. Stateless and thread-safe.
/// </remarks>
internal static class WavDecoder
{
    /// <summary>The <c>WAVE_FORMAT_PCM</c> format tag.</summary>
    private const int FormatPcm = 1;

    /// <summary>The <c>WAVE_FORMAT_IEEE_FLOAT</c> format tag.</summary>
    private const int FormatFloat = 3;

    /// <summary>The <c>WAVE_FORMAT_EXTENSIBLE</c> format tag.</summary>
    private const int FormatExtensible = 0xFFFE;

    /// <summary>The last twelve bytes shared by the canonical PCM and IEEE float sub-format GUIDs.</summary>
    private static ReadOnlySpan<byte> SubFormatGuidTail => [0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71];

    /// <summary>Gets a value indicating whether <paramref name="data"/> starts with a RIFF/WAVE header.</summary>
    /// <param name="data">The file bytes.</param>
    /// <returns><see langword="true"/> when the header is present.</returns>
    public static bool IsWav(ReadOnlySpan<byte> data) =>
        data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WAVE"u8);

    /// <summary>
    ///     Decodes a WAV image.
    /// </summary>
    /// <param name="data">The file bytes.</param>
    /// <param name="maxSamples">The most samples (across channels) the clip may contain.</param>
    /// <returns>The decoded clip.</returns>
    /// <exception cref="InvalidDataException">Thrown when the image is malformed or too large.</exception>
    /// <exception cref="NotSupportedException">Thrown when the encoding is not supported.</exception>
    public static AudioClip Decode(byte[] data, long maxSamples)
    {
        if (!IsWav(data))
        {
            throw new InvalidDataException("The data is not a RIFF/WAVE file.");
        }

        WavFormat? format = null;
        var offset = 12;
        while (offset + 8 <= data.Length)
        {
            var size = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset + 4, 4)), int.MaxValue);
            var body = offset + 8;
            var chunkId = data.AsSpan(offset, 4);

            if (chunkId.SequenceEqual("fmt "u8))
            {
                format = ParseFormat(data, body, size);
            }
            else if (chunkId.SequenceEqual("data"u8))
            {
                if (format is null)
                {
                    throw new InvalidDataException("The WAV file has a 'data' chunk before its 'fmt ' chunk.");
                }

                return DecodeSamples(data, body, Math.Min(size, data.Length - body), format, maxSamples);
            }

            // Chunks are word aligned
            var next = (long)body + size + (size & 1);
            if (next > data.Length)
            {
                break;
            }

            offset = (int)next;
        }

        throw new InvalidDataException("The WAV file has no 'data' chunk.");
    }

    /// <summary>Parses and validates the <c>fmt </c> chunk.</summary>
    /// <param name="data">The file bytes.</param>
    /// <param name="body">The offset of the chunk body.</param>
    /// <param name="size">The declared chunk size (clamped to <see cref="int.MaxValue"/>).</param>
    /// <returns>
    ///     The validated format. Fields beyond the format tag, channels, rate and bit depth (byte rate, block align,
    ///     extensible channel mask) are ignored.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the chunk is truncated (an extensible chunk must supply all 40 bytes) or declares no
    ///     channels or an unusable sample rate.
    /// </exception>
    /// <exception cref="NotSupportedException">Thrown when the format tag, extensible sub-format GUID or bit depth is not supported.</exception>
    private static WavFormat ParseFormat(byte[] data, int body, int size)
    {
        if (size < 16 || body + 16 > data.Length)
        {
            throw new InvalidDataException("The WAV file has a truncated 'fmt ' chunk.");
        }

        var tag = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(body, 2));
        var channels = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(body + 2, 2));
        var rate = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(body + 4, 4));
        var bits = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(body + 14, 2));

        // The extensible form carries the real tag in the first two bytes of its sub-format GUID
        if (tag == FormatExtensible)
        {
            if (size < 40 || body + 40 > data.Length)
            {
                throw new InvalidDataException("The WAV file has a truncated extensible 'fmt ' chunk.");
            }

            // Only the canonical KSDATAFORMAT_SUBTYPE_PCM / _IEEE_FLOAT GUIDs are accepted: the 4-byte
            // little-endian format tag followed by 00 00 10 00 80 00 00 AA 00 38 9B 71
            var subFormat = data.AsSpan(body + 24, 16);
            if (!subFormat[4..].SequenceEqual(SubFormatGuidTail) || subFormat[2] != 0 || subFormat[3] != 0)
            {
                throw new NotSupportedException("The WAV extensible sub-format is not a canonical PCM or IEEE float GUID.");
            }

            tag = BinaryPrimitives.ReadUInt16LittleEndian(subFormat[..2]);
        }

        if (channels == 0 || rate == 0)
        {
            throw new InvalidDataException("The WAV file declares no channels or no sample rate.");
        }

        if (rate > int.MaxValue)
        {
            throw new InvalidDataException(
                $"The WAV file declares a sample rate of {rate} Hz, above the supported maximum of {int.MaxValue} Hz.");
        }

        var supported = (tag == FormatPcm && bits is 8 or 16 or 24 or 32) || (tag == FormatFloat && bits == 32);
        if (!supported)
        {
            throw new NotSupportedException(
                $"WAV format tag {tag} with {bits} bits per sample is not supported; " +
                "use PCM (8/16/24/32-bit) or 32-bit float.");
        }

        return new WavFormat(tag == FormatFloat, channels, (int)rate, bits);
    }

    /// <summary>Converts the <c>data</c> chunk bytes to float samples.</summary>
    /// <param name="data">The file bytes.</param>
    /// <param name="start">The offset of the first sample byte.</param>
    /// <param name="available">The number of data bytes present (already clamped to the image).</param>
    /// <param name="format">The validated format.</param>
    /// <param name="maxSamples">The most samples (across channels) the clip may contain.</param>
    /// <returns>The decoded clip; a trailing partial frame is dropped.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the data holds more than <paramref name="maxSamples"/> samples.
    /// </exception>
    private static AudioClip DecodeSamples(byte[] data, int start, int available, WavFormat format, long maxSamples)
    {
        var bytesPerSample = format.Bits / 8;
        var count = available / bytesPerSample;
        count -= count % format.Channels;
        if (count > maxSamples)
        {
            throw new InvalidDataException($"The WAV file holds {count} samples, more than the allowed {maxSamples}.");
        }

        var samples = new float[count];
        var span = data.AsSpan(start, count * bytesPerSample);
        for (var i = 0; i < count; i++)
        {
            samples[i] = ReadSample(span.Slice(i * bytesPerSample, bytesPerSample), format);
        }

        return new AudioClip(samples, format.SampleRate, format.Channels, format.Bits);
    }

    /// <summary>Converts one little-endian sample.</summary>
    /// <param name="bytes">The sample bytes (exactly one sample wide).</param>
    /// <param name="format">The validated format.</param>
    /// <returns>The sample scaled to [-1, 1) (integer PCM) or passed through (float).</returns>
    private static float ReadSample(ReadOnlySpan<byte> bytes, WavFormat format) => format.Bits switch
    {
        8 => (bytes[0] - 128) / 128f,
        16 => BinaryPrimitives.ReadInt16LittleEndian(bytes) / 32768f,
        24 => (((bytes[0] | (bytes[1] << 8) | (bytes[2] << 16)) << 8) >> 8) / 8388608f,
        _ when format.IsFloat => BinaryPrimitives.ReadSingleLittleEndian(bytes),
        _ => BinaryPrimitives.ReadInt32LittleEndian(bytes) / 2147483648f
    };

    /// <summary>The validated <c>fmt </c> chunk content.</summary>
    /// <param name="IsFloat">Whether samples are IEEE float rather than integer PCM.</param>
    /// <param name="Channels">The channel count (at least 1).</param>
    /// <param name="SampleRate">The sample rate in Hz (1 to <see cref="int.MaxValue"/>).</param>
    /// <param name="Bits">The bits per sample.</param>
    private sealed record WavFormat(bool IsFloat, int Channels, int SampleRate, int Bits);
}
