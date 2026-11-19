using System.Text.RegularExpressions;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

namespace DemaConsulting.Speech.Proofreader.Text;

/// <summary>
///     Splits long-form text into the sentences the Proofreader plays, using the same English
///     sentence analysis the synthesis models use so the boundaries the listener hears match what
///     the library speaks.
/// </summary>
/// <remarks>
///     Blank lines are hard paragraph boundaries; inside a paragraph the sentences are exactly the
///     <see cref="EnglishTextAnalysis.SentenceBreaks"/> of the library's analyzer. The analyzer is
///     configured like the one the Kokoro and Piper models share (normalization and readings) and
///     is independent of the model the user selects, so sentence indices - which flags store - never
///     shift when the model changes. Not thread-safe (the analyzer is shared by the instance's calls).
/// </remarks>
internal sealed partial class SentenceSplitter
{
    /// <summary>The analyzer that decides sentence boundaries.</summary>
    private readonly EnglishTextAnalyzer _analyzer;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SentenceSplitter"/> class.
    /// </summary>
    /// <param name="analyzer">
    ///     The analyzer to use, or <see langword="null"/> to create one configured like the shared
    ///     Kokoro and Piper analyzer.
    /// </param>
    public SentenceSplitter(EnglishTextAnalyzer? analyzer = null)
    {
        _analyzer = analyzer ?? CreateAnalyzer();
    }

    /// <summary>
    ///     Gets the analyzer this splitter uses, so diagnostics can fall back to it for models that
    ///     bring no analyzer of their own.
    /// </summary>
    public EnglishTextAnalyzer Analyzer => _analyzer;

    /// <summary>
    ///     Creates an analyzer configured like the one the Kokoro and Piper models share.
    /// </summary>
    /// <returns>A new analyzer with normalization and reading annotations enabled.</returns>
    public static EnglishTextAnalyzer CreateAnalyzer() => new(new EnglishTextAnalyzerOptions
    {
        Annotations = TextEnAnnotationSelection.Normalization | TextEnAnnotationSelection.Reading,
    });

    /// <summary>
    ///     Splits <paramref name="text"/> into sentences with source offsets.
    /// </summary>
    /// <param name="text">The plain text to split. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the analysis of a very long text.</param>
    /// <returns>
    ///     The sentences in order. Fragments without a letter or digit (for example a stray rule
    ///     of dashes) are dropped because there is nothing to speak or to proofread.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public IReadOnlyList<ProofreadSentence> Split(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        var sentences = new List<ProofreadSentence>();
        var paragraphStart = 0;

        // Each blank-line-delimited paragraph is analyzed on its own, so a heading without
        // terminal punctuation can never merge with the paragraph that follows it.
        foreach (var separator in ParagraphSeparatorRegex().EnumerateMatches(text))
        {
            AddParagraph(text, paragraphStart, separator.Index, sentences, cancellationToken);
            paragraphStart = separator.Index + separator.Length;
        }

        AddParagraph(text, paragraphStart, text.Length, sentences, cancellationToken);
        return sentences;
    }

    /// <summary>
    ///     Analyzes one paragraph and appends its sentences.
    /// </summary>
    /// <param name="text">The whole text.</param>
    /// <param name="start">The paragraph's first offset in <paramref name="text"/>.</param>
    /// <param name="end">The offset just past the paragraph's last character.</param>
    /// <param name="sentences">The list the sentences are appended to; indices continue from its count.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    private void AddParagraph(string text, int start, int end, List<ProofreadSentence> sentences, CancellationToken cancellationToken)
    {
        // Trim the paragraph so the first sentence starts at its first visible character.
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }

        if (end <= start)
        {
            return;
        }

        // The analyzer's sentence breaks are offsets of the first token of every sentence after
        // the first, relative to the analyzed paragraph text.
        var paragraph = text[start..end];
        var breaks = _analyzer.AnalyzeDetailed(paragraph, cancellationToken).SentenceBreaks
            .Where(offset => offset > 0 && offset < paragraph.Length)
            .Distinct()
            .Order()
            .ToList();

        var sentenceStart = 0;
        foreach (var sentenceEnd in breaks.Append(paragraph.Length))
        {
            AddSentence(paragraph, sentenceStart, sentenceEnd, start, sentences);
            sentenceStart = sentenceEnd;
        }
    }

    /// <summary>
    ///     Trims one candidate sentence and appends it when it contains anything speakable.
    /// </summary>
    /// <param name="paragraph">The paragraph text.</param>
    /// <param name="from">The candidate's first offset within the paragraph.</param>
    /// <param name="to">The offset just past the candidate's last character within the paragraph.</param>
    /// <param name="paragraphOffset">The paragraph's offset in the whole text.</param>
    /// <param name="sentences">The list the sentence is appended to.</param>
    private static void AddSentence(string paragraph, int from, int to, int paragraphOffset, List<ProofreadSentence> sentences)
    {
        while (from < to && char.IsWhiteSpace(paragraph[from]))
        {
            from++;
        }

        while (to > from && char.IsWhiteSpace(paragraph[to - 1]))
        {
            to--;
        }

        var sentence = paragraph[from..to];
        if (!sentence.Any(char.IsLetterOrDigit))
        {
            return;
        }

        sentences.Add(new ProofreadSentence(sentences.Count, paragraphOffset + from, to - from, sentence));
    }

    /// <summary>Matches a paragraph separator: a line break followed by at least one blank line.</summary>
    [GeneratedRegex(@"\r?\n[ \t]*(\r?\n[ \t]*)+")]
    private static partial Regex ParagraphSeparatorRegex();
}
