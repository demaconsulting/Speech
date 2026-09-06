using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Turns a streaming recognizer's UPPERCASE, unpunctuated, apostrophe-free raw output into
///     readable prose, by restoring a closed, conservative set of contractions, capitalizing the
///     first letter and the standalone word "I", and ensuring terminal punctuation.
/// </summary>
/// <remarks>
///     <para>
///     <strong>Why this exists.</strong> Some sherpa-onnx streaming transducer models (see
///     <see cref="SherpaOnnxZipformerEnRecognitionModel"/>) are trained on normalized
///     transcripts, so their raw output reads as an unbroken shout ("AND EVERYTHING IS UPPERCASE
///     LIKE IM YELLING"). This helper undoes exactly that raw-output <em>style</em> - it is not
///     specific to any one model's architecture, so any model whose raw output empirically
///     matches this style can reuse it via its own <see cref="IRecognitionModel.NormalizeText"/>
///     override.
///     </para>
///     <para>
///     <strong>Conservative heuristics only.</strong> This class deliberately implements only
///     heuristic restoration: lowercase, restore a closed set of unambiguous contractions
///     (deliberately leaving genuinely ambiguous forms alone - see
///     <see cref="DeliberatelyAmbiguous"/>), capitalize, and ensure terminal punctuation. It does
///     not perform any glossary/vocabulary-aware casing (this generic library has no doc-set or
///     glossary concept to hook such a stage to) and does not integrate any trained punctuation
///     model (no such model is being integrated in this phase). Both omissions match the
///     shipping default of the reference implementation this class was ported from, which itself
///     ships with heuristics-only as its default configuration.
///     </para>
///     <para>
///     <strong>Finalized vs. provisional.</strong> <see cref="RestoreFinal"/> runs the full
///     pipeline and is intended for a committed, finalized recognition result.
///     <see cref="RestoreProvisional"/> applies only cheap casing (lowercase, then capitalize the
///     first letter and standalone "I") with no contraction restoration and no terminal
///     punctuation, since a provisional hypothesis may still be revised or superseded many times
///     before it settles - the visible provisional-to-final transition this implies is expected
///     and acceptable.
///     </para>
///     <para>
///     <strong>Thread safety / robustness.</strong> This class is stateless: every pattern is a
///     compiled, immutable <see cref="Regex"/> held in a <see langword="static readonly"/> field,
///     there is no mutable shared state, and every member is a pure function of its input. Both
///     entry points are safe to call concurrently from any number of threads.
///     </para>
/// </remarks>
internal static class UppercaseTranscriptRestorer
{
    /// <summary>
    ///     The closed, conservative contraction rule set, keyed by the recognizer's
    ///     apostrophe-free lowercase form. Every entry is a form whose non-contraction reading is
    ///     rare in ordinary prose; genuinely ambiguous forms are deliberately absent (see
    ///     <see cref="DeliberatelyAmbiguous"/>). Ported verbatim from the reference
    ///     <c>HiArc.AI.Speech.DictationRestorer</c> implementation's contraction table.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Contractions =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["im"] = "I'm",
            ["ive"] = "I've",
            ["dont"] = "don't",
            ["cant"] = "can't",
            ["wont"] = "won't",
            ["didnt"] = "didn't",
            ["doesnt"] = "doesn't",
            ["isnt"] = "isn't",
            ["arent"] = "aren't",
            ["wasnt"] = "wasn't",
            ["werent"] = "weren't",
            ["havent"] = "haven't",
            ["hasnt"] = "hasn't",
            ["hadnt"] = "hadn't",
            ["wouldnt"] = "wouldn't",
            ["couldnt"] = "couldn't",
            ["shouldnt"] = "shouldn't",
            ["thats"] = "that's",
            ["whats"] = "what's",
            ["theres"] = "there's",
            ["heres"] = "here's",
            ["youre"] = "you're",
            ["youll"] = "you'll",
            ["youve"] = "you've",
            ["theyre"] = "they're",
            ["theyll"] = "they'll",
            ["theyve"] = "they've",
            ["weve"] = "we've",
        };

    /// <summary>
    ///     Forms that are <em>deliberately left alone</em> because their non-contraction reading
    ///     is common in ordinary prose, so silently rewriting them would damage otherwise-correct
    ///     text: <c>were</c> (past-tense "be" vs. we're), <c>well</c> (the noun/adverb vs.
    ///     we'll), <c>ill</c> (sick vs. I'll), <c>its</c> (possessive vs. it's), <c>id</c>
    ///     (identifier vs. I'd), <c>lets</c> (allows vs. let's), and
    ///     <c>shell</c>/<c>shed</c>/<c>hell</c>/<c>hed</c>/<c>wed</c>/<c>whos</c>
    ///     (she'll/she'd/he'll/he'd/we'd/who's). Possessive <c>its</c> in particular is extremely
    ///     common, so it is never rewritten. Ported verbatim from the reference implementation's
    ///     exclusion list.
    /// </summary>
    private static readonly IReadOnlyList<string> DeliberatelyAmbiguous =
    [
        "were", "well", "ill", "its", "id", "lets", "shell", "shed", "hell", "hed", "wed", "whos",
    ];

    /// <summary>Single case-insensitive alternation matching any contraction key as a whole word.</summary>
    private static readonly Regex ContractionPattern = new(
        @"\b(" + string.Join('|', Contractions.Keys) + @")\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>Matches the standalone word "i" so it can be capitalized to "I".</summary>
    private static readonly Regex StandaloneIPattern = new(
        @"\bi\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>Collapses runs of interior whitespace to a single space.</summary>
    private static readonly Regex InteriorWhitespacePattern = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    ///     Enumerates the contraction forms this restorer deliberately leaves unchanged because
    ///     their non-contraction reading is common in ordinary prose.
    /// </summary>
    /// <returns>The forms left alone, lowercase and apostrophe-free.</returns>
    public static IReadOnlyList<string> DeliberatelyAmbiguousForms() => DeliberatelyAmbiguous;

    /// <summary>
    ///     Restores casing, contractions, and terminal punctuation on a <em>finalized</em>
    ///     recognition result.
    /// </summary>
    /// <param name="rawText">
    ///     The recognizer's committed result text (typically UPPERCASE and without punctuation).
    ///     May be empty; may already carry casing/punctuation, in which case the pipeline is
    ///     effectively idempotent.
    /// </param>
    /// <returns>The restored, readable prose for the text, or the empty string for empty input.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rawText"/> is null.</exception>
    public static string RestoreFinal(string rawText)
    {
        ArgumentNullException.ThrowIfNull(rawText);

        var normalized = InteriorWhitespacePattern.Replace(rawText, " ").Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        // Stage 1: lowercase the baseline, which never damages a correctly cased word.
        var working = normalized.ToLowerInvariant();

        // Stage 2: conservative contraction restoration (whole word, case-insensitive).
        working = ContractionPattern.Replace(
            working,
            match => Contractions[match.Value.ToLowerInvariant()]);

        // Stage 3: capitalize the standalone word "I" and the first alphabetic character.
        working = StandaloneIPattern.Replace(working, "I");
        working = CapitalizeFirstLetter(working);

        // Stage 4: ensure the text ends with terminal punctuation so it reads as a sentence.
        return EnsureTerminalPunctuation(working);
    }

    /// <summary>
    ///     Applies only cheap, non-damaging casing to a <em>provisional</em> (still-forming)
    ///     recognition result so the live display is readable while the recognizer may still
    ///     revise it.
    /// </summary>
    /// <remarks>
    ///     Deliberately does no contraction restoration or terminal-punctuation insertion:
    ///     provisional text may be revised many times before it settles, and the visible
    ///     transition from this cheap casing to the fully restored final text is expected and
    ///     acceptable.
    /// </remarks>
    /// <param name="rawText">The provisional hypothesis text (typically UPPERCASE).</param>
    /// <returns>
    ///     The provisional text lowercased with its first letter and standalone "I" capitalized,
    ///     or the empty string for empty input.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rawText"/> is null.</exception>
    public static string RestoreProvisional(string rawText)
    {
        ArgumentNullException.ThrowIfNull(rawText);

        var normalized = InteriorWhitespacePattern.Replace(rawText, " ").Trim();
        if (normalized.Length == 0)
        {
            return string.Empty;
        }

        var working = normalized.ToLowerInvariant();
        working = StandaloneIPattern.Replace(working, "I");
        return CapitalizeFirstLetter(working);
    }

    /// <summary>Capitalizes the first alphabetic character of the text, leaving the rest untouched.</summary>
    private static string CapitalizeFirstLetter(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsLetter(text[i]))
            {
                continue;
            }

            if (char.IsUpper(text[i]))
            {
                return text;
            }

            var builder = new StringBuilder(text);
            builder[i] = char.ToUpper(text[i], CultureInfo.InvariantCulture);
            return builder.ToString();
        }

        return text;
    }

    /// <summary>
    ///     Appends a terminal period when the text does not already end with sentence-terminal
    ///     punctuation, so a finalized result reads as a sentence.
    /// </summary>
    private static string EnsureTerminalPunctuation(string text)
    {
        var last = text[^1];
        return last is '.' or '?' or '!' ? text : text + ".";
    }
}
