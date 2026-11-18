namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     The kind of one <see cref="PhonemizedItem"/>.
/// </summary>
public enum PhonemizedItemKind
{
    /// <summary>A spoken word with a pronunciation.</summary>
    Word = 0,

    /// <summary>A punctuation mark (a run of the same mark is one item); it carries no phones but shapes phrasing.</summary>
    Punctuation = 1,

    /// <summary>A token the phonemizer cannot pronounce (a symbol, an emoji, a letter outside the Latin alphabets). It is kept, with no phones, and listed in <see cref="PhonemizedText.UnsupportedTokens"/>, never dropped silently.</summary>
    Unsupported = 2,
}
