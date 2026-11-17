using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.Text.En.AnalysisSubsystem;

/// <summary>
///     Selects which annotation families <see cref="EnglishTextAnalyzer"/> produces.
/// </summary>
/// <remarks>
///     A <see cref="FlagsAttribute"/> enumeration so later releases can add families without
///     changing the meaning of existing values.
/// </remarks>
[Flags]
public enum TextEnAnnotationSelection
{
    /// <summary>No annotations; the analyzer then supplies only the tokenizer's sentence boundaries.</summary>
    None = 0,

    /// <summary>
    ///     One <see cref="TextEnAnnotations.PartOfSpeech"/> annotation per word token. Selecting
    ///     it is what makes the analyzer load the part-of-speech tagger.
    /// </summary>
    PartOfSpeech = 1,

    /// <summary>
    ///     <see cref="TextEnAnnotations.Normalized"/> annotations naming how spans such as
    ///     abbreviations, symbols and initialisms are spoken, and sentence boundaries reconciled
    ///     with them. It needs no tagger.
    /// </summary>
    Normalization = 2,

    /// <summary>
    ///     A <see cref="TextEnAnnotations.Reading"/> annotation and its
    ///     <see cref="TextEnAnnotations.ReadingConfidence"/> companion on each heteronym word (such as
    ///     <c>record</c>), always together and on the same span. The classifier is effective only when a
    ///     trained heteronym model is available; this release embeds none, so selecting it (it is part of
    ///     the default) produces readings only for words that carry a
    ///     <see cref="OverrideAction.Reading"/> override. The classifier needs the part-of-speech tagger
    ///     for its context but does not emit <see cref="TextEnAnnotations.PartOfSpeech"/> annotations
    ///     unless that family is selected as well.
    /// </summary>
    Reading = 4,
}

/// <summary>
///     Options for <see cref="EnglishTextAnalyzer"/>: which annotations to produce and an optional
///     counts-only diagnostics sink.
/// </summary>
/// <remarks>
///     The record is immutable after initialization and validated on every <c>init</c>, including
///     in <c>with</c> expressions. <see cref="EnglishTextAnalyzer"/> copies the values it needs
///     when it is constructed, so changing a later copy of the options never affects an existing
///     analyzer. The type is safe to share across threads provided the supplied diagnostics sink
///     is.
/// </remarks>
public sealed record EnglishTextAnalyzerOptions
{
    /// <summary>The selection that covers every annotation family this release defines.</summary>
    private const TextEnAnnotationSelection AllFamilies = TextEnAnnotationSelection.PartOfSpeech | TextEnAnnotationSelection.Normalization | TextEnAnnotationSelection.Reading;

    /// <summary>The default confidence below which a classified reading counts as low confidence.</summary>
    private const double DefaultLowConfidenceThreshold = 0.60;

    /// <summary>Backing field for <see cref="Annotations"/>.</summary>
    private readonly TextEnAnnotationSelection _annotations = AllFamilies;

    /// <summary>Backing field for <see cref="LowConfidenceThreshold"/>.</summary>
    private readonly double _lowConfidenceThreshold = DefaultLowConfidenceThreshold;

    /// <summary>
    ///     Gets the sink the analyzer reports aggregate-counts events to (one per analyzed request, plus
    ///     one heteronym event when readings were assigned and a model warning), or
    ///     <see langword="null"/> (the default) for no reporting.
    /// </summary>
    /// <remarks>
    ///     The library's diagnostics contract forbids sending text content to a sink, so the
    ///     analyzer reports only counts (sentences, tokens, annotations, unknown words,
    ///     normalizations, ambiguous abbreviations, numeric fallbacks, overrides applied and
    ///     overrides ignored; for readings: how many were classified, single-candidate, set by an
    ///     override and below the confidence threshold) and never a word or a reading id. A sink that
    ///     throws never interrupts an analysis.
    /// </remarks>
    public ISpeechDiagnostics? Diagnostics { get; init; }

    /// <summary>
    ///     Gets the host-supplied pronunciation overrides to apply, or <see langword="null"/> (the
    ///     default) for none.
    /// </summary>
    /// <remarks>
    ///     Overrides are applied by the normalization pass, so a non-empty dictionary requires
    ///     <see cref="TextEnAnnotationSelection.Normalization"/> in <see cref="Annotations"/>;
    ///     <see cref="EnglishTextAnalyzer"/> rejects the combination when it is constructed. The
    ///     dictionary is immutable, so the analyzer simply keeps the reference; an empty
    ///     dictionary is the same as none. Dictionary content is never reported to the diagnostics
    ///     sink.
    /// </remarks>
    public PronunciationOverrides? Overrides { get; init; }

    /// <summary>
    ///     Gets the annotation families to produce. Defaults to
    ///     <see cref="TextEnAnnotationSelection.PartOfSpeech"/>,
    ///     <see cref="TextEnAnnotationSelection.Normalization"/> and
    ///     <see cref="TextEnAnnotationSelection.Reading"/> combined; the reading family is effective
    ///     only when a heteronym model is available (none is embedded in this release).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     The value contains a flag this release does not define.
    /// </exception>
    public TextEnAnnotationSelection Annotations
    {
        get => _annotations;
        init
        {
            // Reject undefined bits so a typo or a value from a newer release fails loudly here
            // rather than being silently treated as "something selected".
            if ((value & ~AllFamilies) != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The annotation selection contains an undefined flag.");
            }

            _annotations = value;
        }
    }

    /// <summary>
    ///     Gets the confidence below which a classified reading is counted as low confidence in the
    ///     heteronym diagnostics event and flagged by <see cref="AnalyzedToken.IsLowConfidenceReading"/>;
    ///     0.60 by default.
    /// </summary>
    /// <remarks>
    ///     A low-confidence reading is still emitted: the threshold only counts and flags. The value is
    ///     validated on every <c>init</c>, including in a <c>with</c> expression, which replaces only the
    ///     property it names and re-validates it.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a finite number from 0 to 1.</exception>
    public double LowConfidenceThreshold
    {
        get => _lowConfidenceThreshold;
        init
        {
            // NaN and infinities fail the finite check, and everything else must lie in [0, 1].
            if (!double.IsFinite(value) || value < 0.0 || value > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "The low-confidence threshold must be a finite number from 0 to 1.");
            }

            _lowConfidenceThreshold = value;
        }
    }
}
