using DemaConsulting.Speech.Proofreader.Text;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Proofreader.Tests;

/// <summary>
///     Tests for <see cref="SentenceSplitter"/> and <see cref="MarkdownStripper"/>: what the listener hears must match
///     what the library synthesizes, and Markdown must reduce to plain prose.
/// </summary>
public class SentenceSplitterTests
{
    /// <summary>A paragraph rich in abbreviations, numbers, and decimals.</summary>
    private const string Paragraph =
        "Dr. Smith arrived at 5 p.m. on Jan. 3rd. The cost was $3.50 per unit. Is it 3.14 or 2.72? He said no!";

    /// <summary>
    ///     Validates that every sentence's offsets slice back to its text, in order and without overlap, and that
    ///     paragraphs separated by a blank line never merge.
    /// </summary>
    [Fact]
    public void SentenceSplitter_Split_AbbreviationsAndParagraphs_OffsetsSliceBackToSentences()
    {
        // Arrange: two paragraphs, the second without terminal punctuation (like a heading)
        var text = Paragraph + "\n\nA heading without punctuation\n\nThe end.";
        var splitter = new SentenceSplitter();

        // Act
        var sentences = splitter.Split(text, TestContext.Current.CancellationToken);

        // Assert: slices match, order is strictly increasing, and the unpunctuated paragraph stands alone
        Assert.True(sentences.Count >= 6);
        foreach (var sentence in sentences)
        {
            Assert.Equal(sentence.Text, text.Substring(sentence.Start, sentence.Length));
        }

        for (var i = 1; i < sentences.Count; i++)
        {
            Assert.Equal(i, sentences[i].Index);
            Assert.True(sentences[i].Start >= sentences[i - 1].Start + sentences[i - 1].Length);
        }

        Assert.Contains(sentences, sentence => sentence.Text == "A heading without punctuation");
        Assert.Equal("The end.", sentences[^1].Text);
    }

    /// <summary>
    ///     Validates that the sentences the tool plays are chunked exactly as the library chunks the whole paragraph,
    ///     so a played sentence is never split differently from how the library would speak it.
    /// </summary>
    [Fact]
    public void SentenceSplitter_Split_PerSentenceChunks_MatchLibraryChunks()
    {
        // Arrange: the library's view of the whole paragraph (what a model's capability profile chunks)
        var splitter = new SentenceSplitter();
        var whole = splitter.Analyzer.AnalyzeDetailed(Paragraph, CancellationToken.None);
        var libraryChunks = SentenceChunker.ChunkAligned(Paragraph, whole.Annotations, whole.SentenceBreaks)
            .Select(chunk => chunk.Text)
            .ToList();

        // Act: chunk each sentence the tool plays on its own, as a per-sentence synthesis request does
        var toolChunks = splitter.Split(Paragraph, TestContext.Current.CancellationToken)
            .SelectMany(sentence =>
            {
                var analysis = splitter.Analyzer.AnalyzeDetailed(sentence.Text, CancellationToken.None);
                return SentenceChunker.ChunkAligned(sentence.Text, analysis.Annotations, analysis.SentenceBreaks)
                    .Select(chunk => chunk.Text);
            })
            .ToList();

        // Assert: identical chunk texts in identical order
        Assert.Equal(libraryChunks, toolChunks);
    }

    /// <summary>
    ///     Validates that Markdown markers are removed while the spoken words are kept.
    /// </summary>
    [Fact]
    public void MarkdownStripper_Strip_SimpleMarkdown_KeepsOnlyProse()
    {
        // Arrange: a heading, emphasis, a link, an image, a code span, a fence, a list, and a quote
        const string markdown = """
            # The Title

            Some **bold** and *italic* text with a [link](https://example.com/x) and `code`.
            Continued line with snake_case_name intact.

            ```
            var skipped = 1;
            ```

            - first item
            - second item

            > quoted words ![alt text](img.png)
            """;

        // Act
        var plain = MarkdownStripper.Strip(markdown);

        // Assert: markers and targets are gone, words are kept, blocks are separate paragraphs, code is dropped
        Assert.Equal(
            "The Title\n\n"
            + "Some bold and italic text with a link and code.\nContinued line with snake_case_name intact.\n\n"
            + "first item\n\nsecond item\n\nquoted words alt text",
            plain);
    }
}
