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
///     <b>Known Stage-1 limitation (out-of-vocabulary words)</b>: a word not present in the
///     embedded lexicon (for example an uncommon proper noun, a neologism, or a word CMUdict
///     itself does not list) is silently dropped from the synthesized phoneme stream rather than
///     guessed at - see <see cref="Phonemize"/>'s return value for how a caller can detect this
///     happened. This is a deliberate, honestly-documented simplification for the first ONNX-model
///     pass, not a hidden defect: the alternative of guessing a pronunciation for an unknown word
///     risks producing confidently wrong speech, which is worse than omitting the word. A later
///     pass may add a real fallback G2P (for example invoking <c>espeak-ng</c> the same way misaki
///     itself optionally does) without changing this class's public contract.
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
        @"[A-Za-z]+(?:'[A-Za-z]+)*|\s+|.",
        RegexOptions.Compiled | RegexOptions.Singleline,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     The exact set of non-alphabetic vocabulary characters Kokoro v1.0's tokenizer declares
    ///     (see <see cref="KokoroPhonemeVocabulary"/>'s remarks) that this class treats as
    ///     passthrough punctuation when encountered directly in the input text.
    /// </summary>
    private static readonly HashSet<char> PassthroughPunctuation =
        [';', ':', ',', '.', '!', '?', '\u2014', '\u2026', '"', '(', ')', '\u201c', '\u201d'];

    private readonly IReadOnlyDictionary<string, string> _lexicon;

    /// <summary>Initializes a new instance, loading and parsing the embedded lexicon immediately.</summary>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the embedded resource is missing - this indicates this assembly was built
    ///     incorrectly, never a runtime/environment condition a caller could recover from.
    /// </exception>
    public KokoroLexiconPhonemizer()
    {
        _lexicon = LoadLexicon();
    }

    private static IReadOnlyDictionary<string, string> LoadLexicon()
    {
        using var stream = typeof(KokoroLexiconPhonemizer).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{ResourceName}' is missing - this assembly was built incorrectly.");

        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, System.Text.Encoding.UTF8);

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var tab = line.IndexOf('\t');
            if (tab > 0 && tab < line.Length - 1)
            {
                result[line[..tab]] = line[(tab + 1)..];
            }
        }

        return result;
    }

    /// <summary>
    ///     Converts <paramref name="text"/> into Kokoro v1.0's IPA phoneme string.
    /// </summary>
    /// <param name="text">The English text to phonemize. Must not be null.</param>
    /// <returns>
    ///     The phoneme string to pass to <see cref="KokoroPhonemeVocabulary.ToTokenIds"/>, and the
    ///     distinct, lower-cased words from <paramref name="text"/> that were not found in the
    ///     embedded lexicon and were therefore omitted from the phoneme string (empty when every
    ///     word was recognized).
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public (string Phonemes, IReadOnlyList<string> UnknownWords) Phonemize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var phonemes = new System.Text.StringBuilder();
        var unknownWords = new List<string>();

        foreach (var token in TokenPattern.Matches(text).Select(match => match.Value))
        {
            if (token.Length == 0)
            {
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
                var lower = token.ToLowerInvariant();
                if (_lexicon.TryGetValue(lower, out var wordPhonemes))
                {
                    phonemes.Append(wordPhonemes);
                }
                else
                {
                    unknownWords.Add(lower);
                }

                continue;
            }

            // A single non-letter, non-whitespace character: pass it through only when Kokoro's
            // own vocabulary actually declares it, so a character the model has no embedding for
            // is dropped rather than silently fed through to a token-id lookup that would drop it
            // anyway (see KokoroPhonemeVocabulary.ToTokenIds), keeping this class's own log of
            // "what did I actually skip" limited to meaningful omissions (unknown words), not
            // every unsupported punctuation mark too.
            if (token.Length == 1 && PassthroughPunctuation.Contains(token[0]))
            {
                phonemes.Append(token[0]);
            }
        }

        return (phonemes.ToString(), unknownWords);
    }
}
