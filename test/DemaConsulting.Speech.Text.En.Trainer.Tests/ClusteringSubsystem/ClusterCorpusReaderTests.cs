using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ClusteringSubsystem;

/// <summary>
///     Unit tests for <see cref="ClusterCorpusReader"/>: paragraphs, sentences and tokens, the runtime normalization,
///     eligibility and thread invariance.
/// </summary>
public sealed class ClusterCorpusReaderTests
{
    /// <summary>Proves paragraphs split on blank lines and their lines are joined by single spaces.</summary>
    [Fact]
    public void ClusterCorpusReader_Paragraphs_BlankLines_SplitAndJoin()
    {
        // Act
        var paragraphs = ClusterCorpusReader.Paragraphs("One line\nand more.\n\nSecond.\n");

        // Assert
        Assert.Equal(["One line and more.", "Second."], paragraphs);
    }

    /// <summary>Proves the runtime rule: curly apostrophes, ASCII-only lower-casing and the number form.</summary>
    [Fact]
    public void ClusterCorpusReader_Normalize_RuntimeRule()
    {
        // Act / Assert
        Assert.Equal("don't", ClusterCorpusReader.Normalize("Don\u2019t"));
        Assert.Equal("\u00c9cole", ClusterCorpusReader.Normalize("\u00c9COLE".AsSpan(0, 1).ToString() + "cole"));
        Assert.Equal("<num>", ClusterCorpusReader.Normalize("1,234"));
        Assert.Equal("4th", ClusterCorpusReader.Normalize("4TH"));
    }

    /// <summary>Proves an over-long form or one with an ineligible character is not eligible, and the boundary characters are.</summary>
    [Fact]
    public void ClusterCorpusReader_NormalizeEligible_Boundaries()
    {
        // Act / Assert
        Assert.Null(ClusterCorpusReader.NormalizeEligible(new string('a', ClusterCorpusReader.MaximumFormLength + 1)));
        Assert.Equal(new string('a', ClusterCorpusReader.MaximumFormLength), ClusterCorpusReader.NormalizeEligible(new string('a', ClusterCorpusReader.MaximumFormLength)));
        Assert.Null(ClusterCorpusReader.NormalizeEligible("\u0101"));
        Assert.Null(ClusterCorpusReader.NormalizeEligible("\u00a0"));
        Assert.Equal("\u00ff\u2010\u2027!~", ClusterCorpusReader.NormalizeEligible("\u00ff\u2010\u2027!~"));
    }

    /// <summary>Proves the analyzer's tokenizer splits a paragraph into sentences and token spans, the same for any thread count.</summary>
    [Fact]
    public void ClusterCorpusReader_Tokenize_DifferentThreadCounts_SameBooks()
    {
        // Arrange: several books of two-sentence paragraphs
        var bodies = Enumerable.Range(0, 8)
            .Select(static book => string.Join("\n\n", Enumerable.Range(0, 5).Select(i => $"The w{(i * (book + 3)) % 17} runs. A w{i % 5} sleeps.")))
            .Select(static (body, index) => ($"gutenberg-{index + 1}", body))
            .ToList();

        // Act
        var one = ClusterCorpusReader.Tokenize(bodies, 1, TestContext.Current.CancellationToken);
        var four = ClusterCorpusReader.Tokenize(bodies, 4, TestContext.Current.CancellationToken);

        // Assert: books in input order, two sentences of four tokens per paragraph, identical spans on both runs
        Assert.Equal(bodies.Select(static body => body.Item1), one.Books.Select(static book => book.SourceId));
        var paragraph = one.Books[0].Paragraphs[0];
        Assert.Equal(["The w0 runs.", "A w0 sleeps."], [paragraph.SentenceText(0), paragraph.SentenceText(1)]);
        Assert.Equal(8 * 5 * 8, one.Tokens);
        Assert.Equal(one.Tokens, four.Tokens);
        for (var book = 0; book < one.Books.Count; book++)
        {
            Assert.Equal(one.Books[book].Paragraphs.Select(static p => p.Text), four.Books[book].Paragraphs.Select(static p => p.Text));
            Assert.Equal(
                one.Books[book].Paragraphs.SelectMany(static p => p.Sentences.SelectMany(static s => s)),
                four.Books[book].Paragraphs.SelectMany(static p => p.Sentences.SelectMany(static s => s)));
        }
    }
}
