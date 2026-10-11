using System.Buffers.Binary;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

/// <summary>
///     An immutable reference voice: a short clip of the target speaker, normalized to mono 24 kHz, from which
///     Chatterbox Turbo clones the voice it speaks with.
/// </summary>
/// <remarks>
///     <b>Why caller-supplied</b>: the upstream model repository ships no default voice and this package bundles
///     no audio, so every voice comes from the host application through
///     <see cref="ChatterboxTurboModelOptions"/>. Clips longer than <see cref="MaximumDurationSeconds"/> are
///     trimmed (the model only conditions on a short prefix) and clips shorter than
///     <see cref="MinimumDurationSeconds"/> are rejected because they cannot characterize a speaker.
///     Instances are immutable and safe to share across threads.
/// </remarks>
public sealed class ChatterboxTurboReferenceVoice
{
    /// <summary>The sample rate, in Hz, that every stored clip is normalized to.</summary>
    public const int SampleRate = 24000;

    /// <summary>The shortest accepted clip, in seconds.</summary>
    public const double MinimumDurationSeconds = 1.0;

    /// <summary>The longest retained clip, in seconds; longer clips are trimmed to this length.</summary>
    public const double MaximumDurationSeconds = 30.0;

    /// <summary>The normalized mono 24 kHz samples.</summary>
    private readonly float[] _samples;

    /// <summary>Initializes a new instance of the <see cref="ChatterboxTurboReferenceVoice"/> class.</summary>
    private ChatterboxTurboReferenceVoice(string id, string displayName, float[] samples)
    {
        Id = id;
        DisplayName = displayName;
        _samples = samples;
    }

    /// <summary>Gets the stable identifier used as the voice choice value.</summary>
    public string Id { get; }

    /// <summary>Gets the human-readable name shown for the voice.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the normalized mono 24 kHz samples (shared; callers must not modify them).</summary>
    internal ReadOnlyMemory<float> Samples => _samples;

    /// <summary>
    ///     Creates a voice from in-memory audio, down-mixing and resampling it to mono 24 kHz.
    /// </summary>
    /// <param name="id">A non-empty identifier unique among the supplied voices.</param>
    /// <param name="samples">Interleaved samples in the range [-1, 1].</param>
    /// <param name="sampleRate">The sample rate of <paramref name="samples"/> in Hz; must be positive.</param>
    /// <param name="channelCount">The interleaved channel count; must be at least one.</param>
    /// <param name="displayName">An optional display name; defaults to <paramref name="id"/>.</param>
    /// <returns>The voice.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is null/blank or the clip is too short.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the rate or channel count is invalid.</exception>
    public static ChatterboxTurboReferenceVoice FromSamples(
        string id,
        ReadOnlySpan<float> samples,
        int sampleRate,
        int channelCount = 1,
        string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(channelCount, 1);

        // Normalize to the encoder's expected mono 24 kHz stream
        var normalized = ReferenceAudioResampler.Resample(
            ReferenceAudioResampler.DownMix(samples, channelCount),
            sampleRate,
            SampleRate);

        if (normalized.Length < MinimumDurationSeconds * SampleRate)
        {
            throw new ArgumentException(
                $"A reference voice needs at least {MinimumDurationSeconds:0.#} second(s) of audio.",
                nameof(samples));
        }

        // Only a bounded prefix is useful for conditioning and the encoder cost grows with length
        var maximumSamples = (int)(MaximumDurationSeconds * SampleRate);
        if (normalized.Length > maximumSamples)
        {
            normalized = normalized[..maximumSamples];
        }

        return new ChatterboxTurboReferenceVoice(id, displayName ?? id, normalized);
    }

    /// <summary>
    ///     Creates a voice from an uncompressed 16-bit PCM WAV file (any channel count and sample rate).
    /// </summary>
    /// <param name="id">A non-empty identifier unique among the supplied voices.</param>
    /// <param name="wavPath">The path of the WAV file.</param>
    /// <param name="displayName">An optional display name; defaults to <paramref name="id"/>.</param>
    /// <returns>The voice.</returns>
    /// <exception cref="ArgumentException">Thrown when an argument is blank or the clip is too short.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="NotSupportedException">Thrown when the file is not 16-bit PCM.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a well-formed WAV file.</exception>
    public static ChatterboxTurboReferenceVoice FromFile(string id, string wavPath, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(wavPath);

        if (!File.Exists(wavPath))
        {
            throw new FileNotFoundException($"Reference voice file '{wavPath}' was not found.", wavPath);
        }

        var (samples, sampleRate, channelCount) = ReadPcm16Wav(File.ReadAllBytes(wavPath), wavPath);
        return FromSamples(id, samples, sampleRate, channelCount, displayName);
    }

    /// <summary>Parses a RIFF/WAVE image holding 16-bit PCM into float samples.</summary>
    private static (float[] Samples, int SampleRate, int ChannelCount) ReadPcm16Wav(byte[] bytes, string path)
    {
        // Validate the RIFF/WAVE container header
        if (bytes.Length < 12 || !bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) || !bytes.AsSpan(8, 4).SequenceEqual("WAVE"u8))
        {
            throw new InvalidDataException($"'{path}' is not a RIFF/WAVE file.");
        }

        // Walk the chunks, capturing the format and the PCM data
        int format = 0, channels = 0, rate = 0, bits = 0;
        var offset = 12;
        while (offset + 8 <= bytes.Length)
        {
            var size = (int)Math.Min(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset + 4, 4)), int.MaxValue);
            var body = offset + 8;
            var chunkId = bytes.AsSpan(offset, 4);

            if (chunkId.SequenceEqual("fmt "u8) && size >= 16 && body + 16 <= bytes.Length)
            {
                format = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body, 2));
                channels = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body + 2, 2));
                rate = (int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(body + 4, 4));
                bits = BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(body + 14, 2));
            }
            else if (chunkId.SequenceEqual("data"u8))
            {
                if (channels == 0)
                {
                    throw new InvalidDataException($"'{path}' has a 'data' chunk before its 'fmt ' chunk.");
                }

                if (format != 1 || bits != 16)
                {
                    throw new NotSupportedException($"'{path}' is not 16-bit PCM; only 16-bit PCM WAV is supported.");
                }

                // Tolerate a data size that overstates the file (streamed writers) by clamping to what is present
                var available = Math.Min(size, bytes.Length - body);
                var count = available / 2;
                var samples = new float[count];
                for (var i = 0; i < count; i++)
                {
                    samples[i] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(body + (i * 2), 2)) / 32768f;
                }

                return (samples, rate, channels);
            }

            // Chunks are word aligned
            var next = (long)body + size + (size & 1);
            if (next > bytes.Length)
            {
                break;
            }

            offset = (int)next;
        }

        throw new InvalidDataException($"'{path}' has no 'data' chunk.");
    }
}
