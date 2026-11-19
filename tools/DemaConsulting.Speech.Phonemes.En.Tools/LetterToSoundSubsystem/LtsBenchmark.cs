using System.Diagnostics;
using System.Globalization;
using DemaConsulting.Speech.Phonemes.En.LetterToSoundSubsystem;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.LetterToSoundSubsystem;

/// <summary>
///     The metrics of one candidate on the held-out words.
/// </summary>
/// <param name="Candidate">The candidate's name.</param>
/// <param name="Words">The number of test words.</param>
/// <param name="PerWithStress">The phoneme error rate in percent when the stress digit of a vowel is part of the phone.</param>
/// <param name="PerWithoutStress">The phoneme error rate in percent on the phones without stress.</param>
/// <param name="WordAccuracyWithStress">The percentage of words whose pronunciation equals a dictionary variant exactly.</param>
/// <param name="WordAccuracyWithoutStress">The percentage of words that equal a variant when stress is ignored.</param>
/// <param name="PrimaryStressAll">The percentage of words whose primary-stress syllable (vowel index) is that of some variant.</param>
/// <param name="PrimaryStressMulti">The same on words of two or more syllables.</param>
/// <param name="MillisecondsPerWord">The mean time to pronounce a word on this machine, single-threaded (not committed).</param>
internal sealed record LtsScore(
    string Candidate,
    int Words,
    double PerWithStress,
    double PerWithoutStress,
    double WordAccuracyWithStress,
    double WordAccuracyWithoutStress,
    double PrimaryStressAll,
    double PrimaryStressMulti,
    double MillisecondsPerWord);

/// <summary>
///     Scores letter-to-sound candidates against the held-out CMUdict words.
/// </summary>
/// <remarks>
///     <para>
///     <b>Metrics.</b> A word may have several dictionary variants; the candidate is scored against the best-matching
///     one, as is usual for dictionary G2P evaluation. <em>PER</em> (phoneme error rate) is the Levenshtein distance
///     between the hypothesis and the variant at the smallest distance, summed over all words and divided by the
///     summed lengths of those variants; it is given with stress (a vowel with a different stress digit is an error)
///     and without (the best variant is chosen separately for each). <em>Word accuracy</em> counts a word right when
///     the hypothesis equals any variant. <em>Primary-stress position</em> counts a word right when the index of its
///     primary-stressed vowel (or "none") equals that of any variant.
///     </para>
///     <para>
///     Candidates are functions from a word to a pronunciation, so a rules system, the shipped model's integer
///     runtime and the outputs of an external system read from a file are all scored by the same code. Scoring is a
///     pure function of the words and the hypotheses; only the timing column depends on the machine.
///     </para>
/// </remarks>
internal static class LtsBenchmark
{
    /// <summary>
    ///     Scores a candidate on a set of entries.
    /// </summary>
    /// <param name="name">The candidate's name.</param>
    /// <param name="entries">The test entries.</param>
    /// <param name="candidate">The candidate; it is called once per word, sequentially.</param>
    /// <returns>The score.</returns>
    public static LtsScore Score(string name, IReadOnlyList<LexiconEntry> entries, Func<string, Pronunciation> candidate)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(candidate);

        // Timing: a single-threaded pass that also produces the hypotheses
        var hypotheses = new Pronunciation[entries.Count];
        var clock = Stopwatch.StartNew();
        for (var i = 0; i < entries.Count; i++)
        {
            hypotheses[i] = candidate(entries[i].Word);
        }

        clock.Stop();

        long errorsWith = 0, errorsWithout = 0, lengthWith = 0, lengthWithout = 0, exactWith = 0, exactWithout = 0;
        long stressAll = 0, stressMulti = 0, multi = 0;
        for (var i = 0; i < entries.Count; i++)
        {
            var hypothesis = hypotheses[i];
            var variants = entries[i].Pronunciations;

            // Best variant by distance, with and without stress
            var bestWith = int.MaxValue;
            var bestWithLength = 0;
            var bestWithout = int.MaxValue;
            var bestWithoutLength = 0;
            var stressRight = false;
            var maxVowels = 0;
            foreach (var variant in variants)
            {
                var with = Distance(hypothesis, variant, withStress: true);
                if (with < bestWith)
                {
                    bestWith = with;
                    bestWithLength = variant.Count;
                }

                var without = Distance(hypothesis, variant, withStress: false);
                if (without < bestWithout)
                {
                    bestWithout = without;
                    bestWithoutLength = variant.Count;
                }

                stressRight |= PrimaryIndex(hypothesis) == PrimaryIndex(variant);
                maxVowels = Math.Max(maxVowels, VowelCount(variant));
            }

            errorsWith += bestWith;
            lengthWith += bestWithLength;
            errorsWithout += bestWithout;
            lengthWithout += bestWithoutLength;
            exactWith += bestWith == 0 ? 1 : 0;
            exactWithout += bestWithout == 0 ? 1 : 0;
            stressAll += stressRight ? 1 : 0;
            if (maxVowels >= 2)
            {
                multi++;
                stressMulti += stressRight ? 1 : 0;
            }
        }

        var count = entries.Count;
        return new LtsScore(
            name,
            count,
            100.0 * errorsWith / lengthWith,
            100.0 * errorsWithout / lengthWithout,
            100.0 * exactWith / count,
            100.0 * exactWithout / count,
            100.0 * stressAll / count,
            100.0 * stressMulti / multi,
            clock.Elapsed.TotalMilliseconds / count);
    }

    /// <summary>
    ///     Writes the metrics as the committed tab-separated table.
    /// </summary>
    /// <param name="scores">The scores, one row each.</param>
    /// <param name="sizes">The artifact size in bytes of each candidate by name (<c>0</c> when it has none).</param>
    /// <returns>The file text (header, rows, final newline); timing is deliberately not included, so the file is the same on every machine.</returns>
    public static string ToTsv(IEnumerable<LtsScore> scores, IReadOnlyDictionary<string, long> sizes)
    {
        var lines = new List<string>
        {
            "candidate\ttest_words\tper_with_stress_pct\tper_without_stress_pct\tword_accuracy_pct\tword_accuracy_no_stress_pct\tprimary_stress_position_pct\tprimary_stress_position_two_plus_syllables_pct\tartifact_bytes",
        };
        foreach (var s in scores)
        {
            lines.Add(string.Join('\t', s.Candidate, s.Words.ToString(CultureInfo.InvariantCulture), F(s.PerWithStress), F(s.PerWithoutStress), F(s.WordAccuracyWithStress), F(s.WordAccuracyWithoutStress), F(s.PrimaryStressAll), F(s.PrimaryStressMulti), sizes.GetValueOrDefault(s.Candidate).ToString(CultureInfo.InvariantCulture)));
        }

        return string.Join('\n', lines) + "\n";
    }

    /// <summary>
    ///     Formats a percentage with two decimals, culture-independent.
    /// </summary>
    /// <param name="value">The percentage.</param>
    /// <returns>The text.</returns>
    private static string F(double value) => value.ToString("F2", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Computes the Levenshtein distance between two pronunciations.
    /// </summary>
    /// <param name="a">The first pronunciation.</param>
    /// <param name="b">The second pronunciation.</param>
    /// <param name="withStress">Whether the stress digit is part of a vowel; otherwise vowels compare by base symbol.</param>
    /// <returns>The minimum number of phone insertions, deletions and substitutions.</returns>
    public static int Distance(Pronunciation a, Pronunciation b, bool withStress)
    {
        var x = Codes(a, withStress);
        var y = Codes(b, withStress);
        var previous = new int[y.Length + 1];
        var current = new int[y.Length + 1];
        for (var j = 0; j <= y.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= x.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= y.Length; j++)
            {
                var substitution = previous[j - 1] + (x[i - 1] == y[j - 1] ? 0 : 1);
                current[j] = Math.Min(substitution, Math.Min(previous[j] + 1, current[j - 1] + 1));
            }

            (previous, current) = (current, previous);
        }

        return previous[y.Length];
    }

    /// <summary>
    ///     Gets the comparison codes of a pronunciation.
    /// </summary>
    /// <param name="p">The pronunciation.</param>
    /// <param name="withStress">Whether stress is part of the code.</param>
    /// <returns>One code per phone.</returns>
    private static int[] Codes(Pronunciation p, bool withStress)
    {
        var codes = new int[p.Count];
        for (var i = 0; i < codes.Length; i++)
        {
            codes[i] = withStress ? p[i].Code : LtsModel.BaseId(p[i]);
        }

        return codes;
    }

    /// <summary>
    ///     Gets the index among the vowels of the vowel with primary stress.
    /// </summary>
    /// <param name="p">The pronunciation.</param>
    /// <returns>The vowel index, or <c>-1</c> when no vowel has primary stress.</returns>
    private static int PrimaryIndex(Pronunciation p)
    {
        var vowel = 0;
        for (var i = 0; i < p.Count; i++)
        {
            if (!p[i].IsVowel)
            {
                continue;
            }

            if (p[i].Stress == 1)
            {
                return vowel;
            }

            vowel++;
        }

        return -1;
    }

    /// <summary>
    ///     Counts the vowels of a pronunciation.
    /// </summary>
    /// <param name="p">The pronunciation.</param>
    /// <returns>The number of vowel phones.</returns>
    private static int VowelCount(Pronunciation p)
    {
        var count = 0;
        for (var i = 0; i < p.Count; i++)
        {
            count += p[i].IsVowel ? 1 : 0;
        }

        return count;
    }
}
