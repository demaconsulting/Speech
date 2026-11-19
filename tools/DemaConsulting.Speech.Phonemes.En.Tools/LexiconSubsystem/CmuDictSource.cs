using System.Text.RegularExpressions;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.LexiconSubsystem;

/// <summary>
///     Reads the CMUdict source file (<c>cmudict.dict</c>) into the entries the lexicon image holds.
/// </summary>
/// <remarks>
///     <para>
///     The rules, which are what the lexicon's content is defined by: a line is <c>headword PHONE PHONE ...</c>
///     with an optional trailing <c># comment</c>; a headword <c>word(2)</c> is the second variant of <c>word</c>.
///     Only headwords consisting of the letters a to z and apostrophes are kept (hyphenated words, words with
///     periods and the like are dropped, as the text analysis hands the phonemizer plain words). Variants of
///     a word keep their numbered order, a variant repeated identically is dropped, and the entries are sorted
///     by ordinal comparison of the word, which is the order the image requires.
///     </para>
/// </remarks>
internal static partial class CmuDictSource
{
    /// <summary>
    ///     Parses the source lines.
    /// </summary>
    /// <param name="lines">The lines of <c>cmudict.dict</c>.</param>
    /// <returns>The parse result.</returns>
    /// <exception cref="InvalidDataException">Thrown when a kept line has a malformed headword or an unknown phone.</exception>
    public static CmuDictParseResult Parse(IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var variants = new Dictionary<string, List<(int Number, Pronunciation Pronunciation)>>(StringComparer.Ordinal);
        var lineCount = 0;
        var keptLines = 0;
        foreach (var line in lines)
        {
            if (line.Length == 0)
            {
                continue;
            }

            lineCount++;

            // Split "headword(2) PH PH # comment" into headword, variant number and phones
            var space = line.IndexOf(' ');
            if (space <= 0)
            {
                throw new InvalidDataException("Malformed CMUdict line: '" + line + "'.");
            }

            var match = HeadwordPattern().Match(line[..space]);
            if (!match.Success)
            {
                continue;
            }

            var comment = line.IndexOf('#', space);
            var phones = comment < 0 ? line[(space + 1)..] : line[(space + 1)..comment];
            var number = match.Groups["n"].Success ? int.Parse(match.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture) : 1;
            var word = match.Groups["w"].Value;
            if (!variants.TryGetValue(word, out var list))
            {
                list = [];
                variants[word] = list;
            }

            list.Add((number, Pronunciation.Parse(phones)));
            keptLines++;
        }

        // Order the variants by number (the sort is stable), drop identical repeats, and sort the words
        var entries = new List<LexiconEntry>(variants.Count);
        var repeats = 0;
        foreach (var (word, list) in variants.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var ordered = new List<Pronunciation>(list.Count);
            foreach (var (_, pronunciation) in list.OrderBy(v => v.Number))
            {
                if (ordered.Contains(pronunciation))
                {
                    repeats++;
                }
                else
                {
                    ordered.Add(pronunciation);
                }
            }

            entries.Add(new LexiconEntry(word, ordered));
        }

        return new CmuDictParseResult(entries, lineCount, keptLines, repeats);
    }

    /// <summary>
    ///     Gets the pattern of a kept headword: lower-case letters and apostrophes, optionally followed by <c>(n)</c>.
    /// </summary>
    /// <returns>The pattern.</returns>
    [GeneratedRegex(@"^(?<w>[a-z']+)(?:\((?<n>\d+)\))?$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadwordPattern();
}

/// <summary>
///     The result of parsing <c>cmudict.dict</c>.
/// </summary>
/// <param name="Entries">The kept entries, sorted by word (ordinal), each with its ordered, distinct variants.</param>
/// <param name="SourceLines">The number of non-empty source lines.</param>
/// <param name="KeptLines">The number of lines whose headword was kept.</param>
/// <param name="DroppedRepeats">The number of variants dropped because an identical variant of the same word came first.</param>
internal sealed record CmuDictParseResult(IReadOnlyList<LexiconEntry> Entries, int SourceLines, int KeptLines, int DroppedRepeats);
