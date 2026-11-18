using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tools.PhonemizerSubsystem;

/// <summary>
///     One case of the readings golden corpus: a sentence with a heteronym and the reviewed pronunciation of it.
/// </summary>
/// <param name="ReadingId">The reading id the target word has in the sentence.</param>
/// <param name="Mode">
///     <c>e2e</c> when the real text analysis labels the target with <paramref name="ReadingId"/> (the test runs the
///     whole pipeline), or <c>forced</c> when it does not (the test hands the phonemizer a synthetic reading
///     annotation instead); empty before the first <c>regenerate-goldens</c>.
/// </param>
/// <param name="Sentence">The sentence.</param>
/// <param name="Target">The word of the sentence that carries the reading, as written there.</param>
/// <param name="Expected">The reviewed ARPAbet of the target word; empty until drafted.</param>
internal sealed record ReadingCase(string ReadingId, string Mode, string Sentence, string Target, string Expected);

/// <summary>
///     One case of the sentences golden corpus: a sentence and the expected word stream.
/// </summary>
/// <param name="Id">The case id.</param>
/// <param name="Sentence">The sentence.</param>
/// <param name="Expected">The expected word stream in the format of <see cref="GoldenCorpus.Format"/>; empty until generated.</param>
internal sealed record SentenceCase(string Id, string Sentence, string Expected);

/// <summary>
///     One case of a renderer golden corpus: a sentence of the sentences corpus and the phoneme string a renderer
///     produces for it, with the token ids of that string.
/// </summary>
/// <param name="Id">The case id (that of the sentence).</param>
/// <param name="Sentence">The sentence.</param>
/// <param name="Phonemes">The rendered phoneme string; empty until generated.</param>
/// <param name="Ids">The model's token id of every character of the phoneme string, separated by spaces; empty until generated.</param>
internal sealed record RenderedCase(string Id, string Sentence, string Phonemes, string Ids);

/// <summary>
///     The shared machinery of the phonemizer golden corpora: running the whole pipeline (real text analysis, then
///     the phonemizer), the one-line text form of a result, locating a target word, and reading and writing the
///     corpus files.
/// </summary>
/// <remarks>
///     This file is compiled into both the tools project and the test project, so the command that regenerates a
///     golden and the test that verifies it cannot disagree about the format or the pipeline. It uses only the
///     public API of the library.
/// </remarks>
internal static class GoldenCorpus
{
    /// <summary>The kind of the reading annotation, as the text analysis names it.</summary>
    private const string ReadingKind = TextEnAnnotations.Reading;

    /// <summary>The shared analyzer (thread-safe).</summary>
    private static readonly EnglishTextAnalyzer Analyzer = new();

    /// <summary>The shared phonemizer (thread-safe).</summary>
    private static readonly EnglishPhonemizer Phonemizer = new();

    /// <summary>
    ///     Runs the whole pipeline on a sentence: the real English text analysis, then the phonemizer with its annotations.
    /// </summary>
    /// <param name="sentence">The sentence; it is one segment, so the annotations apply to it directly.</param>
    /// <returns>The phonemized text.</returns>
    public static PhonemizedText RunEndToEnd(string sentence) =>
        Phonemizer.Phonemize(sentence, Analyzer.Analyze(sentence, CancellationToken.None).Annotations);

    /// <summary>
    ///     Runs the phonemizer on a sentence with one synthetic reading annotation on the target word.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="target">The target word.</param>
    /// <param name="readingId">The reading id to force.</param>
    /// <returns>The phonemized text.</returns>
    public static PhonemizedText RunForced(string sentence, string target, string readingId)
    {
        var (start, length) = Locate(sentence, target);
        return Phonemizer.Phonemize(sentence, [new SegmentAnnotation(start, length, ReadingKind, readingId)]);
    }

    /// <summary>
    ///     Gets the reading id the real text analysis gives the target word of a sentence.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="target">The target word.</param>
    /// <returns>The reading id, or <see langword="null"/> when the analysis gives the word none.</returns>
    public static string? ClassifiedReading(string sentence, string target)
    {
        var (start, length) = Locate(sentence, target);
        foreach (var annotation in Analyzer.Analyze(sentence, CancellationToken.None).Annotations)
        {
            if (annotation.Start == start && annotation.Length == length && TextEnAnnotations.TryReadReading(annotation, out var id))
            {
                return id;
            }
        }

        return null;
    }

    /// <summary>
    ///     Finds a target word in a sentence as a whole word.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="target">The word.</param>
    /// <returns>The start and length of its first occurrence.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the word does not occur as a whole word.</exception>
    public static (int Start, int Length) Locate(string sentence, string target)
    {
        var match = Regex.Match(sentence, @"(?<![A-Za-z'])" + Regex.Escape(target) + @"(?![A-Za-z'])", RegexOptions.None, TimeSpan.FromSeconds(1));
        if (!match.Success)
        {
            throw new InvalidOperationException($"The target '{target}' is not a whole word of: {sentence}");
        }

        return (match.Index, match.Length);
    }

    /// <summary>
    ///     Gets the ARPAbet of the first word item of a result whose text equals a target, ignoring case.
    /// </summary>
    /// <param name="result">The phonemized text.</param>
    /// <param name="target">The target word as written.</param>
    /// <returns>The item, or <see langword="null"/>.</returns>
    public static PhonemizedItem? FindWord(PhonemizedText result, string target) =>
        result.Items.FirstOrDefault(i => i.Kind == PhonemizedItemKind.Word && string.Equals(i.Text, target, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    ///     Writes a result as one line: items separated by <c> | </c>, a word as <c>text=PHONES</c> (marked
    ///     <c>!oov</c> when out of vocabulary), punctuation as itself and an unsupported token as <c>&lt;text&gt;</c>.
    /// </summary>
    /// <param name="result">The phonemized text.</param>
    /// <returns>The line.</returns>
    public static string Format(PhonemizedText result) => string.Join(" | ", result.Items.Select(FormatItem));

    /// <summary>
    ///     Reads the sentences corpus.
    /// </summary>
    /// <param name="path">The file: a header line, then <c>id</c>, <c>sentence</c> and optionally <c>expected</c>.</param>
    /// <returns>The cases.</returns>
    public static IReadOnlyList<SentenceCase> ReadSentences(string path) =>
        [.. Rows(path).Select(f => new SentenceCase(f[0], Field(f, 1), Field(f, 2)))];

    /// <summary>
    ///     Reads the readings corpus.
    /// </summary>
    /// <param name="path">The file: a header line, then <c>reading_id</c>, <c>mode</c>, <c>sentence</c>, <c>target</c> and <c>expected</c>; a three-column draft (reading id, sentence, target) is accepted.</param>
    /// <returns>The cases.</returns>
    public static IReadOnlyList<ReadingCase> ReadReadings(string path) =>
        [.. Rows(path).Select(ToReadingCase)];

    /// <summary>
    ///     Writes the sentences corpus.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="cases">The cases.</param>
    public static void WriteSentences(string path, IEnumerable<SentenceCase> cases) =>
        File.WriteAllText(path, "id\tsentence\texpected\n" + string.Concat(cases.Select(c => $"{c.Id}\t{c.Sentence}\t{c.Expected}\n")), new UTF8Encoding(false));

    /// <summary>
    ///     Reads a renderer golden corpus.
    /// </summary>
    /// <param name="path">The file: a header line, then <c>id</c>, <c>sentence</c>, <c>phonemes</c> and <c>ids</c>.</param>
    /// <returns>The cases.</returns>
    public static IReadOnlyList<RenderedCase> ReadRendered(string path) =>
        [.. Rows(path).Select(f => new RenderedCase(f[0], Field(f, 1), Field(f, 2), Field(f, 3)))];

    /// <summary>
    ///     Writes a renderer golden corpus.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="cases">The cases.</param>
    public static void WriteRendered(string path, IEnumerable<RenderedCase> cases) =>
        File.WriteAllText(path, "id\tsentence\tphonemes\tids\n" + string.Concat(cases.Select(c => $"{c.Id}\t{c.Sentence}\t{c.Phonemes}\t{c.Ids}\n")), new UTF8Encoding(false));

    /// <summary>
    ///     Writes the readings corpus.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="cases">The cases.</param>
    public static void WriteReadings(string path, IEnumerable<ReadingCase> cases) =>
        File.WriteAllText(path, "reading_id\tmode\tsentence\ttarget\texpected\n" + string.Concat(cases.Select(c => $"{c.ReadingId}\t{c.Mode}\t{c.Sentence}\t{c.Target}\t{c.Expected}\n")), new UTF8Encoding(false));

    /// <summary>
    ///     Formats one item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The text form.</returns>
    private static string FormatItem(PhonemizedItem item)
    {
        switch (item.Kind)
        {
            case PhonemizedItemKind.Punctuation:
                return item.Text;
            case PhonemizedItemKind.Unsupported:
                return "<" + item.Text + ">";
            default:
                var phones = item.IpaOverride is null ? item.Arpabet : "ipa:" + item.IpaOverride;
                return item.Text + "=" + phones + (item.Oov ? " !oov" : string.Empty);
        }
    }

    /// <summary>
    ///     Converts the fields of a readings row.
    /// </summary>
    /// <param name="fields">The fields.</param>
    /// <returns>The case.</returns>
    private static ReadingCase ToReadingCase(string[] fields) =>
        fields.Length is 3
            ? new ReadingCase(fields[0], string.Empty, fields[1], fields[2], string.Empty)
            : new ReadingCase(fields[0], Field(fields, 1), Field(fields, 2), Field(fields, 3), Field(fields, 4));

    /// <summary>
    ///     Gets a field or an empty string when the row is shorter.
    /// </summary>
    /// <param name="fields">The fields.</param>
    /// <param name="index">The field index.</param>
    /// <returns>The field.</returns>
    private static string Field(string[] fields, int index) => index < fields.Length ? fields[index] : string.Empty;

    /// <summary>
    ///     Reads the non-blank data rows of a tab-separated file after its header.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <returns>The fields of each row.</returns>
    private static IEnumerable<string[]> Rows(string path) =>
        File.ReadLines(path).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t'));
}
