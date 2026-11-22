using System.Text;

namespace DemaConsulting.Speech.SttEvaluation;

/// <summary>
///     The word-level alignment of a hypothesis (recognized) transcript against a reference
///     (ground-truth) transcript: the edit operations of a minimum-edit-distance alignment and
///     how many of the inserted words were invented after the end of the reference.
/// </summary>
/// <remarks>
///     Kept as plain counts (not the aligned word pairs) so per-file alignments can be summed into
///     a corpus result with <see cref="Add"/>, which is how corpus word error rate is defined: the
///     total number of errors divided by the total number of reference words, not the mean of the
///     per-file rates. Immutable and therefore thread-safe.
/// </remarks>
/// <param name="ReferenceWords">The number of reference words (the word error rate denominator).</param>
/// <param name="HypothesisWords">The number of hypothesis words.</param>
/// <param name="Substitutions">Reference words recognized as a different word.</param>
/// <param name="Deletions">Reference words missing from the hypothesis.</param>
/// <param name="Insertions">Hypothesis words with no reference counterpart, wherever they occur.</param>
/// <param name="InventedWords">
///     The insertions that follow the last reference word the hypothesis aligned to (by a match or
///     a substitution): words the recognizer added after the speech ended, for example a tail
///     hallucination produced while flushing. Always less than or equal to
///     <paramref name="Insertions"/>.
/// </param>
internal sealed record WordAlignment(
    int ReferenceWords,
    int HypothesisWords,
    int Substitutions,
    int Deletions,
    int Insertions,
    int InventedWords)
{
    /// <summary>
    ///     Gets the empty alignment (no words, no errors), the starting value when summing per-file
    ///     alignments into a corpus result.
    /// </summary>
    public static WordAlignment Empty { get; } = new(0, 0, 0, 0, 0, 0);

    /// <summary>Gets the total number of word errors (substitutions, deletions and insertions).</summary>
    public int Errors => Substitutions + Deletions + Insertions;

    /// <summary>
    ///     Gets the word error rate as a fraction (<c>0.0</c> = perfect match): <see cref="Errors"/>
    ///     divided by <see cref="ReferenceWords"/>. May exceed <c>1.0</c> when the hypothesis has
    ///     many more words than the reference. When there are no reference words the rate is
    ///     <c>0.0</c> if the hypothesis is also empty and <c>1.0</c> otherwise, because the ratio is
    ///     undefined there.
    /// </summary>
    public double WordErrorRate
    {
        get
        {
            if (ReferenceWords != 0)
            {
                return (double)Errors / ReferenceWords;
            }

            return HypothesisWords == 0 ? 0.0 : 1.0;
        }
    }

    /// <summary>
    ///     Sums this alignment with another, producing the alignment of the two transcripts taken
    ///     together (the corpus aggregation of word error rate).
    /// </summary>
    /// <param name="other">The alignment to add. Must not be <see langword="null"/>.</param>
    /// <returns>A new alignment whose every count is the sum of the two.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is <see langword="null"/>.</exception>
    public WordAlignment Add(WordAlignment other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new WordAlignment(
            ReferenceWords + other.ReferenceWords,
            HypothesisWords + other.HypothesisWords,
            Substitutions + other.Substitutions,
            Deletions + other.Deletions,
            Insertions + other.Insertions,
            InventedWords + other.InventedWords);
    }
}

/// <summary>
///     Computes Word Error Rate (WER) and the word alignment between a reference (ground-truth)
///     transcript and a hypothesis (recognized) transcript, using the standard Levenshtein
///     edit-distance-over-words algorithm.
/// </summary>
/// <remarks>
///     This is evaluation tooling, not a shipped capability: it is owned by the non-shipped
///     <c>DemaConsulting.Speech.SttEvaluation</c> tool and compiled (linked) into the test projects
///     that score real recognizers against known transcripts, so the tool and every accuracy test
///     measure with one implementation. Nothing in <c>src/</c> depends on it.
///     <para>
///     Both transcripts are independently normalized before comparison (lower-cased, punctuation
///     collapsed to whitespace except apostrophes inside a word, then split on whitespace) so that
///     superficial differences in casing or punctuation - which carry no information about whether
///     the recognizer heard the right words - are never counted as errors, while contractions and
///     possessives ("don't", "luther's") stay one word as the LibriSpeech transcripts write them.
///     WER is defined as the minimum number of word insertions, deletions, and substitutions needed
///     to turn the hypothesis into the reference, divided by the reference word count - the same
///     definition used throughout the speech recognition literature.
///     </para>
///     <para>
///     Stateless and thread-safe.
///     </para>
/// </remarks>
internal static class WordErrorRateCalculator
{
    /// <summary>
    ///     Normalizes free-form text into a sequence of comparable words: lower-cased, with every
    ///     character other than a letter or digit treated as a word separator - except an
    ///     apostrophe (straight or typographic) between two letters or digits, which is kept as a
    ///     straight apostrophe - and runs of whitespace collapsed.
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
    ///     truth's exact punctuation or line breaks. A word-internal apostrophe is kept because it
    ///     distinguishes words ("its" and "it's"); a leading or trailing one is a quotation mark and
    ///     is dropped.
    /// </remarks>
    public static string[] NormalizeAndSplit(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Lower-case first, then replace every separator character with a space so adjacent
        // punctuation (e.g. "sea," or "me!") never glues onto a neighboring word
        var lower = text.ToLowerInvariant();
        var builder = new StringBuilder(lower.Length);
        for (var i = 0; i < lower.Length; i++)
        {
            var c = lower[i];
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (IsApostrophe(c) && IsWordInternal(lower, i))
            {
                builder.Append('\'');
            }
            else
            {
                builder.Append(' ');
            }
        }

        return builder.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    ///     Splits text into words on whitespace only, keeping casing and punctuation, for the
    ///     optional punctuated (verbatim) word error rate.
    /// </summary>
    /// <param name="text">The text to split. Must not be <see langword="null"/>.</param>
    /// <returns>The whitespace-separated tokens, in order, with no empty entries.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <remarks>
    ///     A token here is a word with its attached punctuation ("star," or "Pilot"), so a
    ///     verbatim comparison counts a casing or punctuation difference as a substitution. It is
    ///     only meaningful against a reference that is itself punctuated and cased.
    /// </remarks>
    public static string[] SplitVerbatim(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
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
    public static double Compute(string reference, string hypothesis) =>
        Align(reference, hypothesis).WordErrorRate;

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
    public static double Compute(IReadOnlyList<string> referenceWords, IReadOnlyList<string> hypothesisWords) =>
        Align(referenceWords, hypothesisWords).WordErrorRate;

    /// <summary>
    ///     Aligns a hypothesis transcript against a reference transcript after normalizing both
    ///     with <see cref="NormalizeAndSplit"/>.
    /// </summary>
    /// <param name="reference">The ground-truth transcript. Must not be <see langword="null"/>.</param>
    /// <param name="hypothesis">The recognized transcript to align. Must not be <see langword="null"/>.</param>
    /// <returns>The alignment counts, including the invented (tail-inserted) words.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="reference"/> or <paramref name="hypothesis"/> is <see langword="null"/>.
    /// </exception>
    public static WordAlignment Align(string reference, string hypothesis)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(hypothesis);

        return Align(NormalizeAndSplit(reference), NormalizeAndSplit(hypothesis));
    }

    /// <summary>
    ///     Aligns an already-tokenized hypothesis word sequence against a reference word sequence,
    ///     with no normalization applied, and classifies every edit.
    /// </summary>
    /// <param name="referenceWords">The ground-truth words, in order. Must not be <see langword="null"/>.</param>
    /// <param name="hypothesisWords">The recognized words, in order. Must not be <see langword="null"/>.</param>
    /// <returns>
    ///     The counts of one minimum-edit-distance alignment. The sum of its substitutions,
    ///     deletions and insertions is always the edit distance.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="referenceWords"/> or <paramref name="hypothesisWords"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Several alignments can share the minimum edit distance. The backtrace starts at the end
    ///     and prefers an insertion whenever it ties with another operation, so extra words are
    ///     placed as late as the minimum allows: a hypothesis that repeats its last word ("call for
    ///     me me") reports the repetition as an invented word rather than hiding it before the last
    ///     reference word. A word inserted between two reference words that both aligned is never
    ///     invented, because a later reference word still aligns after it.
    /// </remarks>
    public static WordAlignment Align(IReadOnlyList<string> referenceWords, IReadOnlyList<string> hypothesisWords)
    {
        ArgumentNullException.ThrowIfNull(referenceWords);
        ArgumentNullException.ThrowIfNull(hypothesisWords);

        var table = BuildDistanceTable(referenceWords, hypothesisWords);

        // Walk back from the bottom-right corner, classifying each step. Insertions met before
        // (that is, later in the transcript than) the first matched or substituted reference word
        // of the backtrace are the tail of the hypothesis: the invented words.
        var i = referenceWords.Count;
        var j = hypothesisWords.Count;
        var substitutions = 0;
        var deletions = 0;
        var insertions = 0;
        var invented = 0;
        var alignedWordSeen = false;
        while (i > 0 || j > 0)
        {
            // Prefer an insertion on a tie so extra words land at the end (see remarks)
            if (j > 0 && table[i, j] == table[i, j - 1] + 1)
            {
                insertions++;
                invented += alignedWordSeen ? 0 : 1;
                j--;
                continue;
            }

            // A diagonal step aligns a reference word with a hypothesis word: a match costs
            // nothing, a substitution costs one
            if (i > 0 && j > 0)
            {
                var same = referenceWords[i - 1] == hypothesisWords[j - 1];
                if (table[i, j] == table[i - 1, j - 1] + (same ? 0 : 1))
                {
                    substitutions += same ? 0 : 1;
                    alignedWordSeen = true;
                    i--;
                    j--;
                    continue;
                }
            }

            // Otherwise the reference word was not heard at all
            deletions++;
            i--;
        }

        return new WordAlignment(
            referenceWords.Count,
            hypothesisWords.Count,
            substitutions,
            deletions,
            insertions,
            invented);
    }

    /// <summary>
    ///     Builds the Levenshtein dynamic-programming table between two word sequences:
    ///     <c>table[i, j]</c> holds the edit distance between the first <c>i</c> reference words
    ///     and the first <c>j</c> hypothesis words.
    /// </summary>
    /// <param name="reference">The target word sequence.</param>
    /// <param name="hypothesis">The source word sequence being transformed.</param>
    /// <returns>The filled table of size <c>(reference.Count + 1) x (hypothesis.Count + 1)</c>.</returns>
    /// <remarks>
    ///     Built up from the trivial all-insertions/all-deletions base cases along row/column zero.
    ///     Runs in <c>O(reference.Count * hypothesis.Count)</c> time and space, which is fine for
    ///     the transcripts this helper scores (at most a few hundred words each).
    /// </remarks>
    private static int[,] BuildDistanceTable(IReadOnlyList<string> reference, IReadOnlyList<string> hypothesis)
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

        return table;
    }

    /// <summary>Returns whether a character is a straight or typographic apostrophe.</summary>
    /// <param name="c">The character to test.</param>
    /// <returns><see langword="true"/> for <c>'</c> and <c>U+2019</c>.</returns>
    private static bool IsApostrophe(char c) => c is '\'' or '\u2019';

    /// <summary>
    ///     Returns whether the character at <paramref name="index"/> sits between two letters or
    ///     digits, that is, inside a word.
    /// </summary>
    /// <param name="text">The text being normalized.</param>
    /// <param name="index">The position of the character to test.</param>
    /// <returns><see langword="true"/> when both neighbors exist and are letters or digits.</returns>
    private static bool IsWordInternal(string text, int index) =>
        index > 0 &&
        index < text.Length - 1 &&
        char.IsLetterOrDigit(text[index - 1]) &&
        char.IsLetterOrDigit(text[index + 1]);
}
