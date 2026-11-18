namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     Where the pronunciation of a <see cref="PhonemizedItem"/> came from; the phonemizer tries the sources in
///     the order of this list, strongest first.
/// </summary>
/// <remarks>
///     The order of the members is the priority of the phonemizer and also the order of "how far down the list
///     did this word have to fall": a larger value is a weaker source. <see cref="LetterToSound"/> is the only
///     value that marks a word the dictionary and the rules could not pronounce (see
///     <see cref="PhonemizedItem.Oov"/>).
/// </remarks>
public enum PronunciationSource
{
    /// <summary>The item has no pronunciation: it is punctuation, or a token the phonemizer cannot handle.</summary>
    None = 0,

    /// <summary>A host-supplied pronunciation (the <c>en.pron</c> annotation of the English text analysis).</summary>
    Override = 1,

    /// <summary>The pronunciation of the heteronym reading the text analysis chose (the <c>en.reading</c> annotation).</summary>
    Reading = 2,

    /// <summary>The first variant of the word in the CMU Pronouncing Dictionary lexicon.</summary>
    Lexicon = 3,

    /// <summary>Derived from a listed stem by the morphology rules (<c>cats</c>, <c>unfollowed</c>, <c>zebra's</c>, a contraction).</summary>
    Morphology = 4,

    /// <summary>The name of a letter: an initialism letter the text analysis asked to be spelled, or a letter that has no pronunciation of its own.</summary>
    Spelled = 5,

    /// <summary>A bare digit spoken as the name of the digit (a safe fallback; numbers normally arrive already written out).</summary>
    Digits = 6,

    /// <summary>The word is out of the vocabulary: neither the dictionary nor the morphology knows it, so the letter-to-sound fallback pronounced it.</summary>
    LetterToSound = 7,
}
