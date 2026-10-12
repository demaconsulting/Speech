using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

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
///     <para>
///     <b>Moods</b>: a voice may also carry named mood clips (for example <c>sad</c>, <c>anger</c>, see
///     <see cref="ReferenceVoiceInfo.KnownMoods"/>) of the same speaker; they are read lazily, are used only when
///     the text is spoken in that mood, and a mood the voice lacks falls back to the base clip.
///     </para>
/// </remarks>
public sealed class ChatterboxTurboReferenceVoice
{
    /// <summary>The sample rate, in Hz, that every stored clip is normalized to.</summary>
    public const int SampleRate = 24000;

    /// <summary>The shortest accepted clip, in seconds.</summary>
    public const double MinimumDurationSeconds = 1.0;

    /// <summary>The longest retained clip, in seconds; longer clips are trimmed to this length.</summary>
    public const double MaximumDurationSeconds = 30.0;

    /// <summary>The normalized mono 24 kHz samples, produced eagerly or on first access.</summary>
    private readonly Lazy<float[]> _samples;

    /// <summary>The lazily normalized mood clips by lower-case mood name; empty when the voice has none.</summary>
    private readonly IReadOnlyDictionary<string, Lazy<float[]>> _moodSamples;

    /// <summary>Initializes a new instance of the <see cref="ChatterboxTurboReferenceVoice"/> class.</summary>
    private ChatterboxTurboReferenceVoice(
        string id,
        string displayName,
        Lazy<float[]> samples,
        IReadOnlyDictionary<string, Lazy<float[]>>? moodSamples = null)
    {
        Id = id;
        DisplayName = displayName;
        _samples = samples;
        _moodSamples = moodSamples ?? new Dictionary<string, Lazy<float[]>>();
    }

    /// <summary>Gets the stable identifier used as the voice choice value.</summary>
    public string Id { get; }

    /// <summary>Gets the human-readable name shown for the voice.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the normalized mono 24 kHz samples (shared; callers must not modify them).</summary>
    internal ReadOnlyMemory<float> Samples => _samples.Value;

    /// <summary>
    ///     Gets the names of the mood clips this voice has (lower case, ordinal order); empty for a plain voice.
    /// </summary>
    public IReadOnlyList<string> Moods => [.. _moodSamples.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    ///     Returns a copy of this voice that also has the given mood clips, each read and normalized the first
    ///     time a mood is spoken. The base clip is shared with this instance.
    /// </summary>
    /// <param name="moodFiles">Mood name (case-insensitive) to <c>.wav</c>/<c>.flac</c> path; null or empty adds none.</param>
    /// <returns>The voice with mood clips. A damaged or too-short mood file fails at first use with an <see cref="InvalidDataException"/> naming the file.</returns>
    /// <exception cref="ArgumentException">Thrown when a mood name or path is blank.</exception>
    public ChatterboxTurboReferenceVoice WithMoodFiles(IReadOnlyDictionary<string, string>? moodFiles)
    {
        var moods = new Dictionary<string, Lazy<float[]>>(_moodSamples, StringComparer.Ordinal);
        foreach (var (mood, path) in moodFiles ?? new Dictionary<string, string>())
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(mood);
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            var name = mood.Trim().ToLowerInvariant();
            moods[name] = new Lazy<float[]>(() => LoadNormalized($"{Id}/{name}", path), LazyThreadSafetyMode.PublicationOnly);
        }

        return new ChatterboxTurboReferenceVoice(Id, DisplayName, _samples, moods);
    }

    /// <summary>Returns a copy of this voice with one already-decoded mood clip (used by tests).</summary>
    internal ChatterboxTurboReferenceVoice WithMoodSamples(string mood, ReadOnlySpan<float> samples, int sampleRate, int channelCount = 1)
    {
        var moods = new Dictionary<string, Lazy<float[]>>(_moodSamples, StringComparer.Ordinal)
        {
            [mood.Trim().ToLowerInvariant()] = new Lazy<float[]>(Normalize(samples, sampleRate, channelCount)),
        };
        return new ChatterboxTurboReferenceVoice(Id, DisplayName, _samples, moods);
    }

    /// <summary>
    ///     Gets the samples of a mood clip, or reports that the voice has none so the caller falls back to the base clip.
    /// </summary>
    /// <param name="mood">The mood name; matched case-insensitively after trimming. Null or blank never matches.</param>
    /// <param name="samples">The normalized samples when found.</param>
    /// <returns>True when the voice has a clip for the mood.</returns>
    internal bool TryGetMoodSamples(string? mood, out ReadOnlyMemory<float> samples)
    {
        if (NormalizeMoodName(mood) is { } name && _moodSamples.TryGetValue(name, out var lazy))
        {
            samples = lazy.Value;
            return true;
        }

        samples = default;
        return false;
    }

    /// <summary>Returns true when the voice has a clip for the mood (case-insensitive, trimmed; null or blank is false).</summary>
    internal bool HasMood(string? mood) => NormalizeMoodName(mood) is { } name && _moodSamples.ContainsKey(name);

    /// <summary>Normalizes a mood name (trimmed, lower case) for lookup; null when it is null or blank.</summary>
    private static string? NormalizeMoodName(string? mood) =>
        string.IsNullOrWhiteSpace(mood) ? null : mood.Trim().ToLowerInvariant();

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
        var normalized = Normalize(samples, sampleRate, channelCount);
        return new ChatterboxTurboReferenceVoice(id, displayName ?? id, new Lazy<float[]>(normalized));
    }

    /// <summary>Down-mixes, resamples, validates and trims a clip to the stored mono 24 kHz form.</summary>
    private static float[] Normalize(ReadOnlySpan<float> samples, int sampleRate, int channelCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(channelCount, 1);

        // Normalize to the encoder's expected mono 24 kHz stream
        var normalized = AudioResampler.Resample(
            AudioResampler.DownMix(samples, channelCount),
            sampleRate,
            SampleRate);

        if (normalized.Length < MinimumDurationSeconds * SampleRate)
        {
            throw new ArgumentException(
                $"A reference voice needs at least {MinimumDurationSeconds:0.#} second(s) of audio.");
        }

        // Only a bounded prefix is useful for conditioning and the encoder cost grows with length
        var maximumSamples = (int)(MaximumDurationSeconds * SampleRate);
        if (normalized.Length > maximumSamples)
        {
            normalized = normalized[..maximumSamples];
        }

        return normalized;
    }

    /// <summary>
    ///     Creates a voice from a WAV or FLAC file (any channel count and sample rate).
    /// </summary>
    /// <param name="id">A non-empty identifier unique among the supplied voices.</param>
    /// <param name="path">The path of the <c>.wav</c> or <c>.flac</c> file; the format is detected from the content.</param>
    /// <param name="displayName">An optional display name; defaults to <paramref name="id"/>.</param>
    /// <returns>The voice.</returns>
    /// <exception cref="ArgumentException">Thrown when an argument is blank or the clip is too short.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="NotSupportedException">Thrown when a WAV file uses an unsupported encoding.</exception>
    /// <exception cref="InvalidDataException">Thrown when the file is not a well-formed WAV or FLAC file.</exception>
    public static ChatterboxTurboReferenceVoice FromFile(string id, string path, string? displayName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // The core reader detects WAV vs FLAC by content and reports a missing file with FileNotFoundException
        var clip = AudioFileReader.Read(path);
        return FromSamples(id, clip.Samples, clip.SampleRate, clip.ChannelCount, displayName);
    }

    /// <summary>
    ///     Creates a voice that reads and normalizes its file the first time its samples are needed, so listing
    ///     many voices (for example an installed reference-voice pack) costs nothing until one is used.
    /// </summary>
    /// <param name="id">A non-empty identifier unique among the supplied voices.</param>
    /// <param name="path">The path of the <c>.wav</c> or <c>.flac</c> file.</param>
    /// <param name="displayName">An optional display name; defaults to <paramref name="id"/>.</param>
    /// <param name="moodClips">Optional mood name to file path map, also read lazily (see <see cref="WithMoodFiles"/>).</param>
    /// <returns>The voice. A damaged or too-short file fails at first use with an <see cref="InvalidDataException"/> naming the file.</returns>
    internal static ChatterboxTurboReferenceVoice FromFileLazy(
        string id,
        string path,
        string? displayName = null,
        IReadOnlyDictionary<string, string>? moodClips = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // PublicationOnly does not cache a failure, so replacing a bad file takes effect without a restart
        var samples = new Lazy<float[]>(() => LoadNormalized(id, path), LazyThreadSafetyMode.PublicationOnly);
        var voice = new ChatterboxTurboReferenceVoice(id, displayName ?? id, samples);
        return moodClips is { Count: > 0 } ? voice.WithMoodFiles(moodClips) : voice;
    }

    /// <summary>Reads and normalizes a file, naming it in any failure.</summary>
    private static float[] LoadNormalized(string id, string path)
    {
        try
        {
            var clip = AudioFileReader.Read(path);
            return Normalize(clip.Samples, clip.SampleRate, clip.ChannelCount);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or NotSupportedException or IOException or UnauthorizedAccessException)
        {
            throw new InvalidDataException($"Reference voice '{id}' ('{path}') could not be loaded: {ex.Message}", ex);
        }
    }
}
