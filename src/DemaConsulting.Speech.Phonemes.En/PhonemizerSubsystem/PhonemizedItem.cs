using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     One word or punctuation mark of a <see cref="PhonemizedText"/>, with the ARPAbet phones of a word and the
///     facts a renderer or a quality report needs about where they came from.
/// </summary>
/// <remarks>
///     Immutable and thread-safe. The phones are ARPAbet symbols as the CMU Pronouncing Dictionary writes them:
///     consonants (<c>K</c>, <c>SH</c>) and vowels with a stress digit (<c>AE1</c> primary, <c>AH0</c> none,
///     <c>EY2</c> secondary).
/// </remarks>
public sealed class PhonemizedItem
{
    /// <summary>The phones, one symbol each; empty for an item that is not a word.</summary>
    private readonly string[] _phones;

    /// <summary>
    ///     Initializes an item.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="text">The spoken text of the item.</param>
    /// <param name="pronunciation">The pronunciation, or <see langword="null"/> for an item without phones.</param>
    /// <param name="source">Where the pronunciation came from.</param>
    /// <param name="readingId">The reading id the text analysis requested for the word, if any.</param>
    /// <param name="ipaOverride">A host-supplied IPA pronunciation for the word, if any.</param>
    /// <param name="elementStarts">The indexes of the first phone of each element after the first of a hyphenated compound pronounced part by part, or <see langword="null"/>.</param>
    /// <param name="partOfSpeech">The part of speech the text analysis tagged the word with (a Universal Dependencies tag), or <see langword="null"/>.</param>
    internal PhonemizedItem(PhonemizedItemKind kind, string text, Pronunciation? pronunciation, PronunciationSource source, string? readingId = null, string? ipaOverride = null, IReadOnlyList<int>? elementStarts = null, string? partOfSpeech = null)
    {
        PartOfSpeech = partOfSpeech;
        Kind = kind;
        Text = text;
        Pronunciation = pronunciation ?? Pronunciation.Empty;
        Source = source;
        ReadingId = readingId;
        IpaOverride = ipaOverride;
        ElementStarts = elementStarts ?? [];
        _phones = [.. Pronunciation.Phones.ToArray().Select(p => p.Symbol)];
    }

    /// <summary>Gets the kind of the item.</summary>
    public PhonemizedItemKind Kind { get; }

    /// <summary>
    ///     Gets the text of the item as it is spoken: the word as written, or its replacement when the text analysis
    ///     replaced it (<c>Dr.</c> is <c>Doctor</c>); for punctuation the mark itself.
    /// </summary>
    public string Text { get; }

    /// <summary>Gets the ARPAbet phones, one symbol each (for example <c>K</c>, <c>AE1</c>, <c>T</c>); empty for punctuation and unsupported tokens.</summary>
    public IReadOnlyList<string> Phones => _phones;

    /// <summary>Gets the phones as one string, separated by single spaces (<c>K AE1 T</c>); empty when there are none.</summary>
    public string Arpabet => string.Join(' ', _phones);

    /// <summary>Gets where the pronunciation came from; <see cref="PronunciationSource.None"/> for an item without phones.</summary>
    public PronunciationSource Source { get; }

    /// <summary>Gets a value indicating whether the pronunciation is that of the heteronym reading the text analysis chose.</summary>
    public bool FromReading => Source == PronunciationSource.Reading;

    /// <summary>Gets a value indicating whether the word is out of the vocabulary and was pronounced by the letter-to-sound fallback.</summary>
    public bool Oov => Source == PronunciationSource.LetterToSound;

    /// <summary>
    ///     Gets the reading id the text analysis asked for on this word, or <see langword="null"/>. It is set
    ///     even when it could not be applied (<see cref="FromReading"/> is then false and the word fell back to its
    ///     default pronunciation), so an unapplied request is visible.
    /// </summary>
    public string? ReadingId { get; }

    /// <summary>
    ///     Gets the IPA pronunciation a host supplied for this word, or <see langword="null"/>. An IPA override
    ///     cannot be turned into ARPAbet, so <see cref="Source"/> is <see cref="PronunciationSource.Override"/> and a
    ///     renderer translates the IPA into its voice's symbols. When the voice cannot represent the IPA completely the
    ///     renderer speaks the word's normal pronunciation instead, which is why <see cref="Phones"/> then holds the
    ///     phones the word has without the override (empty only for a word the phonemizer cannot pronounce).
    /// </summary>
    public string? IpaOverride { get; }

    /// <summary>
    ///     Gets the part of speech the text analysis tagged the word with (a Universal Dependencies tag such as
    ///     <c>NOUN</c> or <c>AUX</c>), or <see langword="null"/> when the text was not analyzed or the word was replaced.
    ///     A renderer uses it to tell a function word from the content word of the same spelling (<i>my will</i>).
    /// </summary>
    internal string? PartOfSpeech { get; }

    /// <summary>Gets the pronunciation as the internal value type; empty when there are no phones.</summary>
    internal Pronunciation Pronunciation { get; }

    /// <summary>
    ///     Gets the index of the first phone of each element after the first of a hyphenated compound that was
    ///     pronounced part by part (empty for any other word). A sound rule that looks at neighboring phones (the vowel
    ///     before an <c>r</c>) must not look across such a boundary.
    /// </summary>
    internal IReadOnlyList<int> ElementStarts { get; }

    /// <summary>
    ///     Gets the index of the first phone of the second part of a hyphenated compound that was pronounced part by
    ///     part, or -1 for any other word. A renderer for a voice that stresses only
    ///     the first part of such a compound can demote the primary stresses from this phone on.
    /// </summary>
    internal int CompoundBreak => ElementStarts.Count > 0 ? ElementStarts[0] : -1;

    /// <summary>
    ///     Gets a copy of this item with a part of speech.
    /// </summary>
    /// <param name="partOfSpeech">The part of speech, or <see langword="null"/>.</param>
    /// <returns>The copy; every other property is unchanged.</returns>
    internal PhonemizedItem WithPartOfSpeech(string? partOfSpeech) =>
        new(Kind, Text, Pronunciation, Source, ReadingId, IpaOverride, ElementStarts, partOfSpeech);

    /// <summary>
    ///     Returns a readable form: the word and its phones, or the punctuation mark.
    /// </summary>
    /// <returns>For example <c>cat [K AE1 T]</c>.</returns>
    public override string ToString() => Kind == PhonemizedItemKind.Word ? $"{Text} [{Arpabet}]" : Text;
}

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
