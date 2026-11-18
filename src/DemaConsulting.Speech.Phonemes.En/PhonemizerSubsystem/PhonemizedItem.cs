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
    internal PhonemizedItem(PhonemizedItemKind kind, string text, Pronunciation? pronunciation, PronunciationSource source, string? readingId = null, string? ipaOverride = null)
    {
        Kind = kind;
        Text = text;
        Pronunciation = pronunciation ?? Pronunciation.Empty;
        Source = source;
        ReadingId = readingId;
        IpaOverride = ipaOverride;
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
    ///     cannot be turned into ARPAbet, so the item then has no <see cref="Phones"/> and
    ///     <see cref="Source"/> is <see cref="PronunciationSource.Override"/>; a renderer passes the IPA through.
    /// </summary>
    public string? IpaOverride { get; }

    /// <summary>Gets the pronunciation as the internal value type; empty when there are no phones.</summary>
    internal Pronunciation Pronunciation { get; }

    /// <summary>
    ///     Returns a readable form: the word and its phones, or the punctuation mark.
    /// </summary>
    /// <returns>For example <c>cat [K AE1 T]</c>.</returns>
    public override string ToString() => Kind == PhonemizedItemKind.Word ? $"{Text} [{Arpabet}]" : Text;
}
