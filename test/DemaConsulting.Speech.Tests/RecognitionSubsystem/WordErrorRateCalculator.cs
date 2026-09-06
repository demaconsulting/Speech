using System.Text;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Computes Word Error Rate (WER) between a reference (ground-truth) transcript and a
///     hypothesis (recognized) transcript, using the standard Levenshtein
///     edit-distance-over-words algorithm.
/// </summary>
/// <remarks>
///     This is test-only tooling, not a reusable production capability: it exists solely so
///     <c>SherpaOnnxRecognitionEngineAccuracyTests</c> can score a real recognizer's output
///     against a known-correct transcript with an objective, standard metric, closing a
///     confirmed test-coverage gap - no automated test anywhere in this project previously
///     asserted on real transcribed text content, only on buffer/bookkeeping behavior exercised
///     with silence and a synthesized tone (see that test class's own remarks). Because nothing
///     in <c>src/</c> depends on this type, it carries no production requirement of its own; the
///     requirement it supports
///     (<c>Speech-Recognition-RecognitionEngine-RealSpeechAccuracy</c>) is satisfied by the
///     accuracy test that consumes it, not by this calculation helper itself.
///     <para>
///     Both transcripts are independently normalized before comparison (lower-cased, punctuation
///     collapsed to whitespace, then split on whitespace) so that superficial differences in
///     casing or punctuation - which carry no information about whether the recognizer heard the
///     right words - are never counted as errors. WER is defined as the minimum number of word
///     insertions, deletions, and substitutions needed to turn the hypothesis into the reference,
///     divided by the reference word count - the same definition used throughout the speech
///     recognition literature.
///     </para>
/// </remarks>
internal static class WordErrorRateCalculator
{
    /// <summary>
    ///     Normalizes free-form text into a sequence of comparable words: lower-cased, with every
    ///     non-alphanumeric character (punctuation, symbols) treated as a word separator, and
    ///     runs of whitespace collapsed.
    /// </summary>
    /// <param name="text">The text to normalize. Must not be <see langword="null"/>.</param>
    /// <returns>
    ///     The normalized words, in original order, with no empty entries. Empty when
    ///     <paramref name="text"/> contains no alphanumeric characters.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     Punctuation is discarded rather than preserved as its own token because WER measures
    ///     whether the recognizer heard the right words, not whether it reproduced the ground
    ///     truth's exact punctuation or line breaks - a real STT model's raw output commonly omits
    ///     or differs on punctuation even when every word is correct.
    /// </remarks>
    public static string[] NormalizeAndSplit(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Lower-case first, then replace every non-alphanumeric character with a space so
        // adjacent punctuation (e.g. "sea," or "me!") never glues onto a neighboring word
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(c) ? c : ' ');
        }

        return builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    ///     Computes the Word Error Rate between a reference and hypothesis transcript, normalizing
    ///     both before comparison.
    /// </summary>
    /// <param name="reference">The ground-truth transcript. Must not be <see langword="null"/>.</param>
    /// <param name="hypothesis">The recognized transcript to score. Must not be <see langword="null"/>.</param>
    /// <returns>
    ///     The word error rate as a fraction (<c>0.0</c> = perfect match). May exceed <c>1.0</c>
    ///     when the hypothesis contains substantially more words than the reference (every extra
    ///     word is an insertion error). When <paramref name="reference"/> normalizes to zero
    ///     words, returns <c>0.0</c> if <paramref name="hypothesis"/> also normalizes to zero
    ///     words, otherwise <c>1.0</c> (a WER denominator of zero is otherwise undefined).
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="reference"/> or <paramref name="hypothesis"/> is <see langword="null"/>.
    /// </exception>
    public static double Compute(string reference, string hypothesis)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(hypothesis);

        return Compute(NormalizeAndSplit(reference), NormalizeAndSplit(hypothesis));
    }

    /// <summary>
    ///     Computes the Word Error Rate between an already-tokenized reference and hypothesis
    ///     word sequence, with no normalization applied.
    /// </summary>
    /// <param name="referenceWords">The ground-truth words, in order. Must not be <see langword="null"/>.</param>
    /// <param name="hypothesisWords">The recognized words to score, in order. Must not be <see langword="null"/>.</param>
    /// <returns>The word error rate as a fraction; see <see cref="Compute(string,string)"/> for the zero-reference edge case.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="referenceWords"/> or <paramref name="hypothesisWords"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Exposed separately from the string overload so this exact edit-distance math can be
    ///     unit tested against simple, hand-computed word arrays independent of any text
    ///     normalization concerns.
    /// </remarks>
    public static double Compute(IReadOnlyList<string> referenceWords, IReadOnlyList<string> hypothesisWords)
    {
        ArgumentNullException.ThrowIfNull(referenceWords);
        ArgumentNullException.ThrowIfNull(hypothesisWords);

        // A zero-length reference makes "errors divided by reference word count" undefined
        // (division by zero); define it directly instead as "no error only if nothing was heard
        // either", matching the intuitive limit of the metric
        if (referenceWords.Count == 0)
        {
            return hypothesisWords.Count == 0 ? 0.0 : 1.0;
        }

        var distance = EditDistance(referenceWords, hypothesisWords);
        return (double)distance / referenceWords.Count;
    }

    /// <summary>
    ///     Computes the Levenshtein edit distance between two word sequences: the minimum number
    ///     of single-word insertions, deletions, and substitutions needed to turn
    ///     <paramref name="hypothesis"/> into <paramref name="reference"/>.
    /// </summary>
    /// <param name="reference">The target word sequence.</param>
    /// <param name="hypothesis">The source word sequence being transformed.</param>
    /// <returns>The minimum edit distance, always non-negative.</returns>
    /// <remarks>
    ///     Standard dynamic-programming table: <c>table[i, j]</c> holds the edit distance between
    ///     the first <c>i</c> reference words and the first <c>j</c> hypothesis words, built up
    ///     from the trivial all-insertions/all-deletions base cases along row/column zero. Runs
    ///     in <c>O(reference.Count * hypothesis.Count)</c> time and space, which is fine for the
    ///     short (tens to low hundreds of words) transcripts this helper is designed to score.
    /// </remarks>
    private static int EditDistance(IReadOnlyList<string> reference, IReadOnlyList<string> hypothesis)
    {
        var rows = reference.Count + 1;
        var cols = hypothesis.Count + 1;
        var table = new int[rows, cols];

        // Base cases: turning an empty sequence into the first i/j words costs exactly i/j
        // insertions/deletions
        for (var i = 0; i < rows; i++)
        {
            table[i, 0] = i;
        }

        for (var j = 0; j < cols; j++)
        {
            table[0, j] = j;
        }

        // Fill the table left-to-right, top-to-bottom: each cell picks the cheapest of a
        // matching no-op carry-forward, a substitution, an insertion, or a deletion
        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < cols; j++)
            {
                if (reference[i - 1] == hypothesis[j - 1])
                {
                    table[i, j] = table[i - 1, j - 1];
                    continue;
                }

                var substitution = table[i - 1, j - 1] + 1;
                var insertion = table[i, j - 1] + 1;
                var deletion = table[i - 1, j] + 1;
                table[i, j] = Math.Min(substitution, Math.Min(insertion, deletion));
            }
        }

        return table[rows - 1, cols - 1];
    }
}
