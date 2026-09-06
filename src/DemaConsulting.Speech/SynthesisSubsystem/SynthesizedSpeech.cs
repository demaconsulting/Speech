namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     One segment of synthesized audio produced by <see cref="ISpeechSynthesizer.SynthesizeStreamAsync"/>,
///     ready to be played back in order by <see cref="ISpeechSynthesizer.PlayStreamAsync"/>.
/// </summary>
/// <remarks>
///     This is the one Layer-2/engine output type architecture.md names directly in
///     <see cref="ISpeechSynthesizer.PlayStreamAsync"/>'s signature. It carries no sherpa-onnx
///     type, per architecture.md's "engine backend stays swappable at the public API surface"
///     decision. The record is immutable and safe to share across threads. Validation happens
///     eagerly in the constructor so an invalid instance is rejected the moment it is created.
/// </remarks>
public sealed record SynthesizedSpeech
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SynthesizedSpeech"/> record, validating
    ///     its invariants eagerly. See the type-level remarks for the exact rules enforced.
    /// </summary>
    /// <param name="Samples">
    ///     The synthesized mono samples, normalized to <c>[-1.0, 1.0]</c>. Never null; empty for a
    ///     pure-pause segment that carries only silence.
    /// </param>
    /// <param name="SampleRate">
    ///     The rate, in Hz, at which <paramref name="Samples"/> was produced. Meaningless (and
    ///     ignored by playback) when <paramref name="Samples"/> is empty.
    /// </param>
    /// <param name="PreSilence">Real silence to play immediately before <paramref name="Samples"/>.</param>
    /// <param name="PostSilence">Real silence to play immediately after <paramref name="Samples"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="Samples"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="SampleRate"/> is not positive while <paramref name="Samples"/>
    ///     is non-empty (a pure-pause segment carries no samples, so its sample rate is meaningless
    ///     and not checked), or when <paramref name="PreSilence"/> or <paramref name="PostSilence"/>
    ///     is negative.
    /// </exception>
    public SynthesizedSpeech(IReadOnlyList<float> Samples, int SampleRate, TimeSpan PreSilence, TimeSpan PostSilence)
    {
        ArgumentNullException.ThrowIfNull(Samples);

        // A non-empty samples buffer must carry a meaningful sample rate to be played back
        // correctly; an empty (pure-pause) segment's sample rate is documented as meaningless and
        // ignored by playback, so it is intentionally not checked here.
        if (Samples.Count > 0 && SampleRate <= 0)
        {
            throw new ArgumentException(
                $"SampleRate must be positive when Samples is non-empty, but was {SampleRate}.",
                nameof(SampleRate));
        }

        if (PreSilence < TimeSpan.Zero)
        {
            throw new ArgumentException(
                $"PreSilence must not be negative, but was {PreSilence}.",
                nameof(PreSilence));
        }

        if (PostSilence < TimeSpan.Zero)
        {
            throw new ArgumentException(
                $"PostSilence must not be negative, but was {PostSilence}.",
                nameof(PostSilence));
        }

        this.Samples = Samples;
        this.SampleRate = SampleRate;
        this.PreSilence = PreSilence;
        this.PostSilence = PostSilence;
    }

    /// <summary>
    ///     Gets the synthesized mono samples, normalized to <c>[-1.0, 1.0]</c>. Never null; empty
    ///     for a pure-pause segment that carries only silence.
    /// </summary>
    public IReadOnlyList<float> Samples { get; }

    /// <summary>
    ///     Gets the rate, in Hz, at which <see cref="Samples"/> was produced. Meaningless (and
    ///     ignored by playback) when <see cref="Samples"/> is empty.
    /// </summary>
    public int SampleRate { get; }

    /// <summary>Gets the real silence to play immediately before <see cref="Samples"/>.</summary>
    public TimeSpan PreSilence { get; }

    /// <summary>Gets the real silence to play immediately after <see cref="Samples"/>.</summary>
    public TimeSpan PostSilence { get; }
}
