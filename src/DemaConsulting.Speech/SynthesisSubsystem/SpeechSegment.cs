namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     One ordered element of a <see cref="SpeechPlan"/>: a single synthesis call's worth of text
///     (possibly empty, for a pure-pause segment) plus any real inserted silence and per-model
///     parameter overrides that apply only while this segment is spoken.
/// </summary>
/// <param name="Text">
///     The text to synthesize for this segment. May be empty, in which case this segment carries
///     only silence (<paramref name="PreSilenceMs"/>/<paramref name="PostSilenceMs"/>) and no
///     synthesis call is made for it.
/// </param>
/// <param name="PreSilenceMs">
///     Real silence, in milliseconds, to insert immediately before this segment's audio. Must not
///     be negative.
/// </param>
/// <param name="PostSilenceMs">
///     Real silence, in milliseconds, to insert immediately after this segment's audio. Must not
///     be negative.
/// </param>
/// <param name="ParameterOverrides">
///     A model-declared parameter id to overridden value bag that applies only to this segment
///     (for example a <c>ParameterMapped</c> tag raising a declared tempo parameter), or
///     <see langword="null"/> when no override applies. Never contains a key the model does not
///     declare in <see cref="DemaConsulting.Speech.ModelManagementSubsystem.ISpeechModel.Parameters"/>.
/// </param>
/// <remarks>
///     Per architecture.md's "two-layer tag rendering" decision, pauses always render as real
///     inserted silence regardless of model capability, so
///     <see cref="NaturalLanguageAudioTag.ShortPause"/>/<see cref="NaturalLanguageAudioTag.LongPause"/>
///     always produce silence here rather than a parameter override or passthrough text. This
///     record is immutable and safe to share across threads.
/// </remarks>
internal sealed record SpeechSegment(
    string Text,
    int PreSilenceMs,
    int PostSilenceMs,
    IReadOnlyDictionary<string, object>? ParameterOverrides);
