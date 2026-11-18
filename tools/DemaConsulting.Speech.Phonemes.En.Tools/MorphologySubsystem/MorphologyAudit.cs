using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using DemaConsulting.Speech.Phonemes.En.MorphologySubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.MorphologySubsystem;

/// <summary>
///     The agreement of the morphology rules with the lexicon for one ending.
/// </summary>
/// <param name="Kind">The ending.</param>
/// <param name="Candidates">Lexicon entries whose word is the stem (listed in the lexicon) plus this ending.</param>
/// <param name="NoStemPrefix">Candidates whose actual pronunciation does not start with any pronunciation of the stem: irregular or coincidental spellings (<c>seed</c> is not <c>see</c> plus an ending); excluded from the agreement rates.</param>
/// <param name="ExactAgree">Of the other candidates, those where a derived pronunciation equals an actual one.</param>
/// <param name="ClassAgree">Of the other candidates, those where the ending is the same up to the reduced vowel (<c>IH0 Z</c> and <c>AH0 Z</c> count as the same syllabic ending).</param>
/// <param name="Disagreements">The disagreement classes (derived ending, actual ending, or no stem prefix) by decreasing count.</param>
internal sealed record EndingReport(
    SuffixKind Kind,
    int Candidates,
    int NoStemPrefix,
    int ExactAgree,
    int ClassAgree,
    IReadOnlyList<DisagreementClass> Disagreements)
{
    /// <summary>Gets the number of candidates whose actual pronunciation extends a stem pronunciation.</summary>
    public int Compared => Candidates - NoStemPrefix;

    /// <summary>Gets the fraction of compared candidates that agree exactly; 1 when there are none.</summary>
    public double ExactRate => Compared == 0 ? 1 : (double)ExactAgree / Compared;

    /// <summary>Gets the fraction of compared candidates whose ending class agrees; 1 when there are none.</summary>
    public double ClassRate => Compared == 0 ? 1 : (double)ClassAgree / Compared;
}

/// <summary>
///     One class of disagreement between a derived and an actual pronunciation.
/// </summary>
/// <param name="Pattern">The class: <c>derived ending -> actual ending</c>, or <c>no stem prefix</c>.</param>
/// <param name="Count">The number of entries in the class.</param>
/// <param name="Examples">The first few words of the class, in lexicon order.</param>
internal sealed record DisagreementClass(string Pattern, int Count, IReadOnlyList<string> Examples);

/// <summary>
///     Measures the morphology rules on the lexicon itself: for every entry that is a stem listed in the
///     lexicon plus an ending, derive the pronunciation from the stem and compare it with the entry.
/// </summary>
/// <remarks>
///     <para>
///     The lexicon is the only ground truth available (the CMU Pronouncing Dictionary lists most regular
///     inflections by hand), so this is the evidence that the rules do what the dictionary's authors did. The
///     comparison is deliberately two-staged: an entry whose pronunciation does not even begin with a stem
///     pronunciation is a spelling coincidence or an irregular word, not a rule failure, and is counted and listed
///     separately; the rates are over the remaining entries. The class rate ignores the choice between the reduced
///     vowels <c>IH0</c> and <c>AH0</c> in a syllabic ending, which the dictionary itself uses interchangeably
///     (<c>IH0 Z</c> 328 times, <c>AH0 Z</c> 132 times for the plural), so it measures the real decision: voiceless,
///     voiced or syllabic.
///     </para>
///     <para>
///     Stems are accepted only when the lexicon lists them (no derived stems), so a stem's pronunciation is never
///     itself a guess.
///     </para>
/// </remarks>
internal static class MorphologyAudit
{
    /// <summary>The number of example words kept per disagreement class.</summary>
    private const int ExamplesPerClass = 6;

    /// <summary>
    ///     Runs the audit over every lexicon entry.
    /// </summary>
    /// <param name="lexicon">The lexicon, which is both the source of stems and the ground truth.</param>
    /// <returns>One report per ending, in <see cref="SuffixKind"/> order, for the endings that occurred.</returns>
    public static IReadOnlyList<EndingReport> Run(CmuLexicon lexicon)
    {
        ArgumentNullException.ThrowIfNull(lexicon);
        var perKind = new Dictionary<SuffixKind, Accumulator>();
        foreach (var entry in lexicon.Entries())
        {
            if (!Morphology.TryDerive(entry.Word, lexicon, out var derivation, allowDerivedStems: false))
            {
                continue;
            }

            if (!perKind.TryGetValue(derivation.Kind, out var accumulator))
            {
                accumulator = new Accumulator();
                perKind[derivation.Kind] = accumulator;
            }

            accumulator.Add(entry, derivation);
        }

        return [.. perKind.OrderBy(pair => pair.Key).Select(pair => pair.Value.ToReport(pair.Key))];
    }

    /// <summary>
    ///     Collects the comparison results of one ending.
    /// </summary>
    private sealed class Accumulator
    {
        /// <summary>
        ///     Normalizes the first phone of an ending so the reduced vowels <c>IH0</c> and <c>AH0</c> compare equal.
        /// </summary>
        /// <param name="ending">The ending's phones.</param>
        /// <returns>The ending's text with a reduced first vowel written as <c>@</c>.</returns>
        private static string NormalizeEnding(ReadOnlySpan<Phone> ending)
        {
            var text = new Pronunciation(ending).ToString();
            return text.StartsWith("IH0", StringComparison.Ordinal) || text.StartsWith("AH0", StringComparison.Ordinal) ? "@" + text[3..] : text;
        }

        /// <summary>
        ///     Gets the part of an actual pronunciation after a stem pronunciation, as text.
        /// </summary>
        /// <param name="actual">The actual pronunciation, which starts with <paramref name="stem"/>.</param>
        /// <param name="stem">The stem pronunciation.</param>
        /// <returns>The remaining phones as text.</returns>
        private static string Ending(Pronunciation actual, Pronunciation stem) => new Pronunciation(actual.Phones[stem.Count..]).ToString();

        /// <summary>The number of candidates.</summary>
        private int _candidates;

        /// <summary>The number of candidates without a stem prefix.</summary>
        private int _noPrefix;

        /// <summary>The number of exact agreements.</summary>
        private int _exact;

        /// <summary>The number of class agreements.</summary>
        private int _class;

        /// <summary>The disagreement counts by pattern.</summary>
        private readonly Dictionary<string, (int Count, List<string> Examples)> _classes = new(StringComparer.Ordinal);

        /// <summary>
        ///     Compares one entry with its derivation.
        /// </summary>
        /// <param name="entry">The lexicon entry.</param>
        /// <param name="derivation">The derivation of the entry's word.</param>
        public void Add(LexiconEntry entry, Derivation derivation)
        {
            _candidates++;

            // Find a stem pronunciation that the actual pronunciation extends
            var prefixed = false;
            var exact = false;
            var sameClass = false;
            var derivedEnding = string.Empty;
            var actualEnding = string.Empty;
            for (var i = 0; i < derivation.Pronunciations.Count; i++)
            {
                var stem = derivation.StemPronunciations[i];
                var derived = derivation.Pronunciations[i];
                foreach (var actual in entry.Pronunciations)
                {
                    if (!actual.StartsWith(stem))
                    {
                        continue;
                    }

                    // The first prefix-matched pair names the disagreement if no pair agrees
                    if (!prefixed)
                    {
                        derivedEnding = derived.StartsWith(stem) ? Ending(derived, stem) : derived.ToString();
                        actualEnding = Ending(actual, stem);
                    }

                    prefixed = true;
                    exact |= derived.Equals(actual);
                    sameClass |= derived.StartsWith(stem) && NormalizeEnding(derived.Phones[stem.Count..]) == NormalizeEnding(actual.Phones[stem.Count..]);
                }
            }

            if (!prefixed)
            {
                _noPrefix++;
                Count("no stem prefix", entry.Word);
                return;
            }

            _exact += exact ? 1 : 0;
            _class += sameClass ? 1 : 0;
            if (!exact)
            {
                Count(derivedEnding + " -> " + actualEnding + (sameClass ? " (same class)" : string.Empty), entry.Word);
            }
        }

        /// <summary>
        ///     Produces the report.
        /// </summary>
        /// <param name="kind">The ending.</param>
        /// <returns>The report.</returns>
        public EndingReport ToReport(SuffixKind kind) => new(
            kind,
            _candidates,
            _noPrefix,
            _exact,
            _class,
            [.. _classes.OrderByDescending(pair => pair.Value.Count).ThenBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new DisagreementClass(pair.Key, pair.Value.Count, pair.Value.Examples))]);

        /// <summary>
        ///     Counts one entry in a disagreement class.
        /// </summary>
        /// <param name="pattern">The class.</param>
        /// <param name="word">The entry's word.</param>
        private void Count(string pattern, string word)
        {
            var (count, examples) = _classes.TryGetValue(pattern, out var existing) ? existing : (0, []);
            if (examples.Count < ExamplesPerClass)
            {
                examples.Add(word);
            }

            _classes[pattern] = (count + 1, examples);
        }
    }
}
