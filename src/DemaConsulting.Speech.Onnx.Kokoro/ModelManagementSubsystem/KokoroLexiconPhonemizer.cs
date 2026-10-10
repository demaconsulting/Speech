using System.IO.Compression;
using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;

/// <summary>
///     Converts English text into Kokoro v1.0's own IPA phoneme string by looking each word up in
///     a verified, precomputed word -&gt; phoneme lexicon, rather than reimplementing
///     grapheme-to-phoneme (G2P) linguistics by hand.
/// </summary>
/// <remarks>
///     <b>Why a lexicon lookup, not a hand-written G2P</b>: Kokoro v1.0's reference pipeline uses
///     <c>misaki.en.G2P</c> (the same phonemizer <c>hexgrad/kokoro</c> itself depends on), whose
///     phoneme encoding packs several common diphthongs and affricates into single, non-obvious
///     Unicode characters confirmed empirically in this project's development sandbox - for
///     example <c>I</c> (not lowercase <c>i</c>) represents the diphthong <c>/aɪ/</c> as in
///     "life", and <c>O</c> represents <c>/oʊ/</c> as in "know". Hand-reimplementing these rules
///     in C# without a native-speaker-verifiable ground truth would risk silently wrong
///     pronunciation that is hard to catch in review. Instead, this package embeds a lexicon
///     precomputed by running the real <c>misaki.en.G2P</c> tool, once, offline, over every
///     single-pronunciation, purely-alphabetic word in the public-domain CMUdict word list (see
///     <c>docs/build_notes/</c> for the exact script and provenance) - so every phoneme string
///     this class ever emits was produced by the same tool Kokoro's own authors use, not by this
///     library's own guesswork.
///     <para>
///     <b>Known Stage-1 limitation (out-of-vocabulary words)</b>: text is first passed through
///     the internal <c>KokoroTextNormalizer</c> helper (numbers, currency, times, years, ordinals,
///     markdown, URLs, e-mail addresses, abbreviations and diacritics). Each word then resolves by
///     lexicon hit, possessive, camelCase split, all-caps spelling by letter name, or a spelling
///     correction (an apostrophe-less contraction, or the most frequent lexicon word within a
///     Damerau-Levenshtein distance of 1 or 2). A corrected or near-match word is spoken and is
///     not reported. Only a word with no near match at all (for example an uncommon proper noun
///     or a neologism) is omitted from the synthesized phoneme stream and reported in the
///     <see cref="Phonemize"/> return value so a caller can detect it. This is a deliberate,
///     honestly-documented simplification, not a hidden defect: guessing a pronunciation for a
///     word with no near match risks producing confidently wrong speech, which is worse than
///     omitting the word. A later pass may add a real fallback G2P (for example invoking
///     <c>espeak-ng</c> the same way misaki itself optionally does) without changing this class's
///     public contract.
///     </para>
///     <para>
///     <b>Known Stage-1 limitation (homographs)</b>: the lexicon maps one spelling to exactly one
///     pronunciation, computed with no surrounding sentence context. A handful of English words
///     (for example "read" or "lead") are pronounced differently depending on grammatical context;
///     this class always emits the one pronunciation misaki's isolated, no-context G2P call chose
///     for that spelling. This is the same simplification every static-dictionary G2P (including
///     CMUdict-based systems generally) makes, not specific to this implementation.
///     </para>
/// </remarks>
internal sealed class KokoroLexiconPhonemizer
{
    /// <summary>
    ///     The embedded, gzip-compressed resource name of the verified word -&gt; phoneme lexicon,
    ///     relative to this assembly's default namespace.
    /// </summary>
    private const string ResourceName = "DemaConsulting.Speech.Onnx.Kokoro.Resources.kokoro-en-lexicon.tsv.gz";

    /// <summary>
    ///     Matches one word (letters and internal apostrophes, e.g. "don't"), one run of
    ///     whitespace, or one other single character (typically punctuation) - covering every
    ///     character of an input string exactly once, in order.
    /// </summary>
    private static readonly Regex TokenPattern = new(
        @"\uE001[a-z]|[A-Za-z]+(?:['\u2019][A-Za-z]+)*|\s+|.",
        RegexOptions.Compiled | RegexOptions.Singleline,
        TimeSpan.FromSeconds(1));

    private static readonly Regex SpaceBeforePunctuationPattern = new(
        @" ([;:,.!?\u2026\u2014])", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Regex RepeatedPausePattern = new(
        @"([,;:!?])\1+", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private static readonly Regex CamelBoundaryPattern = new(
        @"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])|(?<=[A-Za-z])(?=\d)|(?<=\d)(?=[A-Za-z])",
        RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    /// <summary>The phoneme string for each letter's name, used to spell out acronyms and "P M".</summary>
    private static readonly Dictionary<char, string> LetterPhonemes = new()
    {
        ['a'] = "\u02C8A",
        ['b'] = "b\u02C8i",
        ['c'] = "s\u02C8i",
        ['d'] = "d\u02C8i",
        ['e'] = "\u02C8i",
        ['f'] = "\u02C8\u025Bf",
        ['g'] = "\u02A4\u02C8i",
        ['h'] = "\u02C8A\u02A7",
        ['i'] = "\u02C8I",
        ['j'] = "\u02A4\u02C8A",
        ['k'] = "k\u02C8A",
        ['l'] = "\u02C8\u025Bl",
        ['m'] = "\u02C8\u025Bm",
        ['n'] = "\u02C8\u025Bn",
        ['o'] = "\u02C8O",
        ['p'] = "p\u02C8i",
        ['q'] = "kj\u02C8u",
        ['r'] = "\u02C8\u0251\u0279",
        ['s'] = "\u02C8\u025Bs",
        ['t'] = "t\u02C8i",
        ['u'] = "j\u02C8u",
        ['v'] = "v\u02C8i",
        ['w'] = "d\u02C8\u028Cb\u0259lju",
        ['x'] = "\u02C8\u025Bks",
        ['y'] = "w\u02C8I",
        ['z'] = "z\u02C8i",
    };

    /// <summary>
    ///     The exact set of non-alphabetic vocabulary characters Kokoro v1.0's tokenizer declares
    ///     (see <see cref="KokoroPhonemeVocabulary"/>'s remarks) that this class treats as
    ///     passthrough punctuation when encountered directly in the input text.
    /// </summary>
    private static readonly HashSet<char> PassthroughPunctuation =
        [';', ':', ',', '.', '!', '?', '\u2014', '\u2026', '"', '(', ')', '\u201c', '\u201d'];

    private readonly IReadOnlyDictionary<string, string> _lexicon;
    private readonly IReadOnlyDictionary<string, int> _frequency;
    private readonly Dictionary<int, List<string>> _keysByLength;
    private readonly Dictionary<string, string> _apostropheLess;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string?> _corrections = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance, loading and parsing the embedded lexicon immediately.</summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the embedded resource is missing - this indicates this assembly was built
    ///     incorrectly, never a runtime/environment condition a caller could recover from.
    /// </exception>
    public KokoroLexiconPhonemizer()
    {
        (_lexicon, _frequency) = LoadLexicon();

        _keysByLength = [];
        _apostropheLess = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in _lexicon.Keys)
        {
            if (!_keysByLength.TryGetValue(key.Length, out var bucket))
            {
                _keysByLength[key.Length] = bucket = [];
            }

            bucket.Add(key);

            // "dont" -> "don't": index each contraction by its apostrophe-less spelling, unless
            // that spelling is itself a real word (its, were, well) which must stay unchanged.
            if (key.Contains('\'', StringComparison.Ordinal))
            {
                var stripped = key.Replace("'", string.Empty, StringComparison.Ordinal);
                if (!_lexicon.ContainsKey(stripped) &&
                    (!_apostropheLess.TryGetValue(stripped, out var existing) || FrequencyOf(key) > FrequencyOf(existing)))
                {
                    _apostropheLess[stripped] = key;
                }
            }
        }
    }

    private static (IReadOnlyDictionary<string, string> Lexicon, IReadOnlyDictionary<string, int> Frequency) LoadLexicon()
    {
        using var stream = typeof(KokoroLexiconPhonemizer).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' is missing - this assembly was built incorrectly.");

        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, System.Text.Encoding.UTF8);

        var lexicon = new Dictionary<string, string>(StringComparer.Ordinal);
        var frequency = new Dictionary<string, int>(StringComparer.Ordinal);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            // word <TAB> phonemes [<TAB> zipf-frequency x 100]
            var tab = line.IndexOf('\t');
            if (tab <= 0 || tab >= line.Length - 1)
            {
                continue;
            }

            var secondTab = line.IndexOf('\t', tab + 1);
            var word = line[..tab];
            if (secondTab < 0)
            {
                lexicon[word] = line[(tab + 1)..];
            }
            else
            {
                lexicon[word] = line[(tab + 1)..secondTab];
                if (int.TryParse(line.AsSpan(secondTab + 1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var zipf))
                {
                    frequency[word] = zipf;
                }
            }
        }

        return (lexicon, frequency);
    }

    private int FrequencyOf(string word) => _frequency.TryGetValue(word, out var f) ? f : 0;

    /// <summary>
    ///     Finds the most likely intended word for a misspelling: a missing-apostrophe contraction,
    ///     or the most frequent lexicon word within a small edit distance (a transposition counts
    ///     as one edit), preferring candidates that share the first letter.
    /// </summary>
    private string? Correct(string word)
    {
        if (word.Length < 3 || word.Contains('\'', StringComparison.Ordinal))
        {
            return null;
        }

        return _corrections.GetOrAdd(word, FindCorrection);
    }

    private string? FindCorrection(string word)
    {
        if (_apostropheLess.TryGetValue(word, out var contraction))
        {
            return contraction;
        }

        var maxDistance = word.Length <= 8 ? 1 : 2;
        string? best = null;
        var bestScore = (Distance: int.MaxValue, FirstLetterMismatch: 1, NegativeFrequency: 0);

        for (var length = word.Length - maxDistance; length <= word.Length + maxDistance; length++)
        {
            if (!_keysByLength.TryGetValue(length, out var bucket))
            {
                continue;
            }

            foreach (var candidate in bucket)
            {
                var distance = BoundedEditDistance(word, candidate, maxDistance);
                if (distance > maxDistance || candidate.Contains('\'', StringComparison.Ordinal))
                {
                    continue;
                }

                var score = (distance, candidate[0] == word[0] ? 0 : 1, -FrequencyOf(candidate));
                if (best is null || score.CompareTo(bestScore) < 0 ||
                    (score.CompareTo(bestScore) == 0 && string.CompareOrdinal(candidate, best) < 0))
                {
                    best = candidate;
                    bestScore = score;
                }
            }
        }

        return best;
    }

    /// <summary>
    ///     Optimal-string-alignment (Damerau-Levenshtein with adjacent transposition) distance,
    ///     returning <paramref name="limit"/> + 1 as soon as it is known to exceed the limit.
    /// </summary>
    private static int BoundedEditDistance(string a, string b, int limit)
    {
        if (Math.Abs(a.Length - b.Length) > limit)
        {
            return limit + 1;
        }

        var previousPrevious = new int[b.Length + 1];
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMinimum = current[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var value = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    value = Math.Min(value, previousPrevious[j - 2] + 1);
                }

                current[j] = value;
                rowMinimum = Math.Min(rowMinimum, value);
            }

            if (rowMinimum > limit)
            {
                return limit + 1;
            }

            (previousPrevious, previous, current) = (previous, current, previousPrevious);
        }

        return previous[b.Length];
    }

    /// <summary>
    ///     Resolves one word to phonemes: lexicon hit, possessive, camelCase parts, spelled-out
    ///     acronym, or a corrected misspelling - or <see langword="null"/> when nothing applies.
    /// </summary>
    private string? ResolveWord(string original, int depth = 0)
    {
        var lower = original.ToLowerInvariant().Replace('\u2019', '\'');
        if (_lexicon.TryGetValue(lower, out var direct))
        {
            return direct;
        }

        if (lower.EndsWith("'s", StringComparison.Ordinal) && _lexicon.TryGetValue(lower[..^2], out var owner))
        {
            return owner + PossessiveSuffix(owner);
        }

        var isAllCaps = original.Length >= 2 && original.All(char.IsAsciiLetterUpper);
        if (isAllCaps)
        {
            return string.Join(' ', lower.Select(c => LetterPhonemes[c]));
        }

        if (depth == 0)
        {
            var parts = CamelBoundaryPattern.Split(original).Where(p => p.Length > 0).ToArray();
            if (parts.Length > 1)
            {
                var resolved = parts.Select(p => p.All(char.IsAsciiDigit) ? null : ResolveWord(p, depth + 1)).ToArray();
                if (resolved.All(r => r is not null))
                {
                    return string.Join(' ', resolved);
                }
            }
        }

        var corrected = Correct(lower);
        return corrected is not null ? _lexicon[corrected] : null;
    }

    private static string PossessiveSuffix(string phonemes) => phonemes[^1] switch
    {
        'p' or 't' or 'k' or 'f' or '\u03B8' => "s",
        's' or 'z' or '\u0283' or '\u0292' or '\u02A7' or '\u02A4' => "\u026Az",
        _ => "z",
    };

    /// <summary>
    ///     Converts <paramref name="text"/> into Kokoro v1.0's IPA phoneme string.
    /// </summary>
    /// <param name="text">The English text to phonemize. Must not be null.</param>
    /// <returns>
    ///     The phoneme string to pass to <see cref="KokoroPhonemeVocabulary.ToTokenIds"/>, and the
    ///     distinct, lower-cased words from <paramref name="text"/> that were not found in the
    ///     embedded lexicon and were therefore omitted from the phoneme string (empty when every
    ///     word was recognized). Distinct by ordinal, case-insensitive (post-lower-casing)
    ///     comparison - a word repeated in <paramref name="text"/> is reported only once, the
    ///     first time it is encountered reading left to right, so the list preserves first-seen
    ///     order rather than listing every repeated occurrence.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public (string Phonemes, IReadOnlyList<string> UnknownWords) Phonemize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Kokoro was trained on the single ellipsis character; three consecutive periods make the
        // model swallow the preceding words into a long silence, so collapse them (as misaki does).
        text = KokoroTextNormalizer.Normalize(text.Replace("...", "\u2026", StringComparison.Ordinal));

        var phonemes = new System.Text.StringBuilder();
        var unknownWords = new List<string>();
        var seenUnknownWords = new HashSet<string>(StringComparer.Ordinal);

        foreach (var token in TokenPattern.Matches(text).Select(match => match.Value))
        {
            if (token.Length == 0)
            {
                continue;
            }

            if (token[0] == KokoroTextNormalizer.LetterMarker)
            {
                phonemes.Append(' ').Append(LetterPhonemes[token[1]]).Append(' ');
                continue;
            }

            if (char.IsWhiteSpace(token[0]))
            {
                // Collapse any run of whitespace to a single vocabulary space character, mirroring
                // misaki's own normalization of the input sentence.
                phonemes.Append(' ');
                continue;
            }

            if (char.IsLetter(token[0]))
            {
                var wordPhonemes = ResolveWord(token);
                if (wordPhonemes is not null)
                {
                    phonemes.Append(' ').Append(wordPhonemes).Append(' ');
                }
                else
                {
                    var lower = token.ToLowerInvariant().Replace('\u2019', '\'');
                    if (seenUnknownWords.Add(lower))
                    {
                        unknownWords.Add(lower);
                    }

                    phonemes.Append(' ');
                }

                continue;
            }

            // A single non-letter, non-whitespace character: pass it through only when Kokoro's
            // own vocabulary declares it. Any other character is dropped but still acts as a word
            // separator so neighbors ("cat@dog", "well-known") do not run together.
            if (token.Length == 1 && PassthroughPunctuation.Contains(token[0]))
            {
                phonemes.Append(token[0]);
            }
            else
            {
                phonemes.Append(' ');
            }
        }

        // Omitted words and separators leave doubled or edge whitespace; normalize so an utterance
        // with no recognized words yields an empty string (and Generate's zero-token fast path).
        var joined = string.Join(' ', phonemes.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        var normalized = SpaceBeforePunctuationPattern.Replace(joined, "$1");
        normalized = RepeatedPausePattern.Replace(normalized, "$1");
        return (normalized, unknownWords);
    }
}
