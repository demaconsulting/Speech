namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Cross-cutting constants every <see cref="ITextAnalyzer"/> implementation can rely on.
/// </summary>
/// <remarks>
///     Mirrors <see cref="SpeechParameterConventions"/>: the constants live on a static class
///     rather than on the analyzer interface so the contract has one obvious, documented home.
/// </remarks>
public static class TextAnalysisConventions
{
    /// <summary>
    ///     The character the library places in the text handed to an <see cref="ITextAnalyzer"/>
    ///     in place of each recognized Natural Language Audio Tag: the Unicode object replacement
    ///     character (code point 65532).
    /// </summary>
    /// <remarks>
    ///     One placeholder stands for exactly one audio tag. An analyzer MUST treat it as an
    ///     opaque, non-letter, non-word, whitespace-like inline object: it is neither a sentence
    ///     terminator nor part of any word. An analyzer SHOULD NOT cover a placeholder in an
    ///     annotation (such an annotation is dropped, never clipped), and MAY place a sentence
    ///     break immediately before or after it.
    /// </remarks>
    public const char TagPlaceholder = '\uFFFC';
}
