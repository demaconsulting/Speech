namespace DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;

/// <summary>
///     The result of phonemizing one text: its words and punctuation in spoken order, and the report of what the
///     phonemizer could not pronounce properly.
/// </summary>
/// <remarks>
///     Immutable and thread-safe. Nothing in the input is dropped silently: a word the dictionary and the rules
///     cannot pronounce is still an item (flagged <see cref="PhonemizedItem.Oov"/> and listed in
///     <see cref="OovWords"/>), and a token that cannot be pronounced at all is an
///     <see cref="PhonemizedItemKind.Unsupported"/> item listed in <see cref="UnsupportedTokens"/>. Whitespace and
///     the placeholder the core library puts in place of an audio tag carry no sound and are the only text that
///     produces no item.
/// </remarks>
public sealed class PhonemizedText
{
    /// <summary>
    ///     Initializes a result.
    /// </summary>
    /// <param name="items">The items in spoken order.</param>
    internal PhonemizedText(IReadOnlyList<PhonemizedItem> items)
    {
        Items = items;
        OovWords = Distinct(items.Where(i => i.Oov).Select(i => i.Text));
        UnsupportedTokens = Distinct(items.Where(i => i.Kind == PhonemizedItemKind.Unsupported).Select(i => i.Text));
    }

    /// <summary>Gets the words and punctuation marks in spoken order.</summary>
    public IReadOnlyList<PhonemizedItem> Items { get; }

    /// <summary>
    ///     Gets the words that were out of the vocabulary (spoken text, distinct, in order of first occurrence). The
    ///     letter-to-sound fallback pronounced them; a host can add them to its pronunciation overrides.
    /// </summary>
    public IReadOnlyList<string> OovWords { get; }

    /// <summary>Gets the tokens that could not be pronounced at all (distinct, in order of first occurrence).</summary>
    public IReadOnlyList<string> UnsupportedTokens { get; }

    /// <summary>
    ///     Returns the distinct values of a sequence in order of first occurrence, compared ignoring case.
    /// </summary>
    /// <param name="values">The values.</param>
    /// <returns>The distinct values.</returns>
    private static string[] Distinct(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return [.. values.Where(seen.Add)];
    }
}
