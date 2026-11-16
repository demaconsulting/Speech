using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.Text.En.AnalysisSubsystem;

/// <summary>
///     Selects which annotation families <see cref="EnglishTextAnalyzer"/> produces.
/// </summary>
/// <remarks>
///     A <see cref="FlagsAttribute"/> enumeration so later releases can add families (for
///     example heteronym readings) without changing the meaning of existing values.
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
    private const TextEnAnnotationSelection AllFamilies = TextEnAnnotationSelection.PartOfSpeech | TextEnAnnotationSelection.Normalization;

    /// <summary>Backing field for <see cref="Annotations"/>.</summary>
    private readonly TextEnAnnotationSelection _annotations = AllFamilies;

    /// <summary>
    ///     Gets the sink the analyzer reports one aggregate-counts event per analyzed request to,
    ///     or <see langword="null"/> (the default) for no reporting.
    /// </summary>
    /// <remarks>
    ///     The library's diagnostics contract forbids sending text content to a sink, so the
    ///     analyzer reports only counts (sentences, tokens, annotations, unknown words,
    ///     normalizations, ambiguous abbreviations) and never a word. A sink that throws never
    ///     interrupts an analysis.
    /// </remarks>
    public ISpeechDiagnostics? Diagnostics { get; init; }

    /// <summary>
    ///     Gets the annotation families to produce. Defaults to
    ///     <see cref="TextEnAnnotationSelection.PartOfSpeech"/> combined with
    ///     <see cref="TextEnAnnotationSelection.Normalization"/>.
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
}
