using DemaConsulting.Speech.Phonemes.En.LetterToSoundSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.LetterToSoundSubsystem;

/// <summary>
///     Aligns the letters of words with their phones, so that every letter owns a chunk of zero, one or two phones.
/// </summary>
/// <remarks>
///     <para>
///     The letter-to-sound network classifies letters, so training needs a label for every letter. The alignment is
///     learned, not authored: the probability of every (letter, chunk) pair is estimated by expectation-maximization
///     over all the training words (a forward-backward pass over the grid of letters against phones), starting from a
///     weak prior that vowel letters tend to carry vowel phones and consonant letters do not, and the final alignment
///     of each word is its most probable one (Viterbi). A silent letter gets the empty chunk, <c>x</c> typically
///     <c>K S</c>, and in <c>ph</c> one letter gets <c>F</c> and the other nothing. The procedure is deterministic: it
///     is sequential, double precision, and has no random numbers.
///     </para>
///     <para>
///     A chunk is encoded as <c>(first + 1) * 40 + (second + 1)</c> where <c>first</c> and <c>second</c> are base
///     phone ids or <c>-1</c> for none, so <c>0</c> is the empty chunk.
///     </para>
/// </remarks>
internal static class LtsAligner
{
    /// <summary>The number of chunk keys per letter.</summary>
    private const int KeyCount = 40 * 40;

    /// <summary>The number of expectation-maximization iterations.</summary>
    private const int Iterations = 8;

    /// <summary>The letters that normally carry a vowel sound.</summary>
    private static readonly bool[] VowelLetters = BuildVowelLetters();

    /// <summary>
    ///     Learns the alignment of a set of words.
    /// </summary>
    /// <param name="words">The words: letter ids and base phone ids.</param>
    /// <returns>For every word the chunk key of each of its letters, or <see langword="null"/> when the word cannot be aligned (more than two phones per letter).</returns>
    public static IReadOnlyList<int[]?> Align(IReadOnlyList<(int[] Letters, byte[] Phones)> words)
    {
        ArgumentNullException.ThrowIfNull(words);
        var probabilities = InitialProbabilities();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            probabilities = EstimateAgain(words, probabilities);
        }

        var result = new int[]?[words.Count];
        for (var i = 0; i < words.Count; i++)
        {
            result[i] = Viterbi(words[i].Letters, words[i].Phones, probabilities);
        }

        return result;
    }

    /// <summary>
    ///     Gets the key of a chunk.
    /// </summary>
    /// <param name="first">The first base phone id or <c>-1</c>.</param>
    /// <param name="second">The second base phone id or <c>-1</c>.</param>
    /// <returns>The key.</returns>
    public static int Key(int first, int second) => ((first + 1) * 40) + second + 1;

    /// <summary>
    ///     Gets the base phone ids of a chunk.
    /// </summary>
    /// <param name="key">The chunk key.</param>
    /// <returns>Zero, one or two base phone ids.</returns>
    public static byte[] Phones(int key)
    {
        var first = (key / 40) - 1;
        var second = (key % 40) - 1;
        if (first < 0)
        {
            return [];
        }

        return second < 0 ? [(byte)first] : [(byte)first, (byte)second];
    }

    /// <summary>
    ///     Builds the prior: every chunk possible, vowel letters favoring vowel phones and consonant letters avoiding them.
    /// </summary>
    /// <returns>The normalized probabilities, indexed by <c>letter * KeyCount + key</c>.</returns>
    private static double[] InitialProbabilities()
    {
        var p = new double[LtsModel.LetterIdCount * KeyCount];
        for (var letter = 1; letter < LtsModel.LetterIdCount; letter++)
        {
            var total = 0.0;
            for (var key = 0; key < KeyCount; key++)
            {
                var first = (key / 40) - 1;
                var second = (key % 40) - 1;
                if ((first < 0 && second >= 0) || first >= LtsModel.BasePhoneCount || second >= LtsModel.BasePhoneCount)
                {
                    continue;
                }

                var length = 0;
                if (first >= 0)
                {
                    length = second < 0 ? 1 : 2;
                }

                var weight = 1.0;
                if (length == 0)
                {
                    weight = 0.15;
                }
                else if (length == 2)
                {
                    weight = 0.1;
                }
                var vowels = (first >= LtsModel.FirstVowelId ? 1 : 0) + (second >= LtsModel.FirstVowelId ? 1 : 0);
                if (VowelLetters[letter])
                {
                    weight *= vowels > 0 ? 4.0 : 1.0;
                }
                else if (vowels > 0)
                {
                    weight *= 0.2;
                }

                p[(letter * KeyCount) + key] = weight;
                total += weight;
            }

            for (var key = 0; key < KeyCount; key++)
            {
                p[(letter * KeyCount) + key] /= total;
            }
        }

        return p;
    }

    /// <summary>
    ///     One expectation-maximization step.
    /// </summary>
    /// <param name="words">The words.</param>
    /// <param name="p">The current probabilities.</param>
    /// <returns>The re-estimated probabilities.</returns>
    private static double[] EstimateAgain(IReadOnlyList<(int[] Letters, byte[] Phones)> words, double[] p)
    {
        var counts = new double[p.Length];
        foreach (var (letters, phones) in words)
        {
            int n = letters.Length, m = phones.Length;
            if (m > 2 * n)
            {
                continue;
            }

            // Forward and backward probabilities over the grid (letters consumed, phones consumed)
            var alpha = new double[n + 1, m + 1];
            var beta = new double[n + 1, m + 1];
            alpha[0, 0] = 1;
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j <= m; j++)
                {
                    var a = alpha[i, j];
                    if (a == 0)
                    {
                        continue;
                    }

                    for (var l = 0; l <= 2 && j + l <= m; l++)
                    {
                        alpha[i + 1, j + l] += a * p[(letters[i] * KeyCount) + ChunkKey(phones, j, l)];
                    }
                }
            }

            var total = alpha[n, m];
            if (total <= 0)
            {
                continue;
            }

            beta[n, m] = 1;
            for (var i = n - 1; i >= 0; i--)
            {
                for (var j = 0; j <= m; j++)
                {
                    var b = 0.0;
                    for (var l = 0; l <= 2 && j + l <= m; l++)
                    {
                        b += p[(letters[i] * KeyCount) + ChunkKey(phones, j, l)] * beta[i + 1, j + l];
                    }

                    beta[i, j] = b;
                }
            }

            // Expected counts of every (letter, chunk) used
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j <= m; j++)
                {
                    var a = alpha[i, j];
                    if (a == 0)
                    {
                        continue;
                    }

                    for (var l = 0; l <= 2 && j + l <= m; l++)
                    {
                        var index = (letters[i] * KeyCount) + ChunkKey(phones, j, l);
                        counts[index] += a * p[index] * beta[i + 1, j + l] / total;
                    }
                }
            }
        }

        // Normalize per letter; a letter never seen keeps its prior
        var result = new double[p.Length];
        for (var letter = 1; letter < LtsModel.LetterIdCount; letter++)
        {
            var sum = 0.0;
            for (var key = 0; key < KeyCount; key++)
            {
                sum += counts[(letter * KeyCount) + key];
            }

            for (var key = 0; key < KeyCount; key++)
            {
                var index = (letter * KeyCount) + key;
                result[index] = sum > 0 ? counts[index] / sum : p[index];
            }
        }

        return result;
    }

    /// <summary>
    ///     Finds the most probable alignment of a word.
    /// </summary>
    /// <param name="letters">The letter ids.</param>
    /// <param name="phones">The base phone ids.</param>
    /// <param name="p">The probabilities.</param>
    /// <returns>The chunk key of every letter, or <see langword="null"/> when no alignment exists.</returns>
    private static int[]? Viterbi(int[] letters, byte[] phones, double[] p)
    {
        int n = letters.Length, m = phones.Length;
        if (m > 2 * n)
        {
            return null;
        }

        var best = new double[n + 1, m + 1];
        var back = new sbyte[n + 1, m + 1];
        best[0, 0] = 1;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j <= m; j++)
            {
                var a = best[i, j];
                if (a == 0)
                {
                    continue;
                }

                for (var l = 0; l <= 2 && j + l <= m; l++)
                {
                    var score = a * p[(letters[i] * KeyCount) + ChunkKey(phones, j, l)];

                    // Strictly better only, so ties keep the shorter chunk deterministically
                    if (score > best[i + 1, j + l])
                    {
                        best[i + 1, j + l] = score;
                        back[i + 1, j + l] = (sbyte)l;
                    }
                }
            }
        }

        if (best[n, m] <= 0)
        {
            return null;
        }

        var keys = new int[n];
        var column = m;
        for (var i = n; i > 0; i--)
        {
            var l = back[i, column];
            keys[i - 1] = ChunkKey(phones, column - l, l);
            column -= l;
        }

        return keys;
    }

    /// <summary>
    ///     Gets the key of the chunk of <paramref name="length"/> phones starting at <paramref name="start"/>.
    /// </summary>
    /// <param name="phones">The phones.</param>
    /// <param name="start">The first phone of the chunk.</param>
    /// <param name="length">The chunk length, 0 to 2.</param>
    /// <returns>The key.</returns>
    private static int ChunkKey(byte[] phones, int start, int length) => length switch
    {
        0 => 0,
        1 => Key(phones[start], -1),
        _ => Key(phones[start], phones[start + 1]),
    };

    /// <summary>
    ///     Marks the letters that normally carry a vowel sound (<c>a e i o u y</c>).
    /// </summary>
    /// <returns>A table indexed by letter id.</returns>
    private static bool[] BuildVowelLetters()
    {
        var table = new bool[LtsModel.LetterIdCount];
        foreach (var letter in "aeiouy")
        {
            table[LtsModel.LetterId(letter)] = true;
        }

        return table;
    }
}
