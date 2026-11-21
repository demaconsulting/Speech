using System.Text;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ClusteringSubsystem;

/// <summary>
///     Unit tests for <see cref="ClusterCorpusReader"/>: paragraphs, the runtime normalization and its self-check,
///     eligibility, the integer counts, the vocabulary order and limits, skipped sentences and thread invariance.
/// </summary>
public sealed class ClusterCorpusReaderTests
{
    /// <summary>
    ///     Tokenizes bodies on one thread.
    /// </summary>
    /// <param name="bodies">The bodies, named <c>gutenberg-1</c>, <c>gutenberg-2</c> and so on.</param>
    /// <returns>The tokenized corpus.</returns>
    private static TokenizedCorpus Tokenize(params string[] bodies) =>
        ClusterCorpusReader.Tokenize([.. bodies.Select(static (body, index) => ($"gutenberg-{index + 1}", body))], 1, TestContext.Current.CancellationToken);

    /// <summary>
    ///     Counts a corpus with no skipped sentences.
    /// </summary>
    /// <param name="corpus">The corpus.</param>
    /// <param name="minCount">The minimum count.</param>
    /// <param name="maxWords">The vocabulary limit.</param>
    /// <returns>The counts.</returns>
    private static CorpusCounts Count(TokenizedCorpus corpus, int minCount = 1, int maxWords = 0) =>
        ClusterCorpusReader.Count(corpus, [], new ReaderSettings(minCount, maxWords), 1, TestContext.Current.CancellationToken);

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

    /// <summary>Proves words and adjacent pairs are counted as integers and a sentence boundary breaks adjacency.</summary>
    [Fact]
    public void ClusterCorpusReader_Count_SimpleCorpus_CountsWordsAndBigrams()
    {
        // Arrange: two sentences in one paragraph
        var corpus = Tokenize("The dog runs. The cat runs.");

        // Act
        var counts = Count(corpus);

        // Assert: the words by count, then ordinal; pairs only inside sentences (runs-. twice)
        Assert.Equal([".", "runs", "the", "cat", "dog"], counts.Words);
        Assert.Equal([2L, 2L, 2L, 1L, 1L], counts.Counts);
        Assert.Equal(8, counts.Tokens);
        Assert.Equal(2, counts.Sentences);
        Assert.Equal(1, counts.Paragraphs);
        Assert.Equal(6, counts.Bigrams.Total);
        Assert.Equal(5, counts.Bigrams.PairTypes);
        Assert.Equal(5, counts.Types);
    }

    /// <summary>Proves an ineligible token is not a word and breaks adjacency.</summary>
    [Fact]
    public void ClusterCorpusReader_Count_IneligibleToken_BreaksAdjacency()
    {
        // Arrange: a Greek word between two English ones
        var corpus = Tokenize("cat \u03b3\u03ac\u03c4\u03b1 dog");

        // Act
        var counts = Count(corpus);

        // Assert
        Assert.Equal(["cat", "dog"], counts.Words);
        Assert.Equal(0, counts.Bigrams.Total);
        Assert.Equal(3, counts.Tokens);
    }

    /// <summary>Proves the minimum count and the vocabulary limit cut the vocabulary and the cut words break adjacency.</summary>
    [Fact]
    public void ClusterCorpusReader_Count_MinCountAndMaxWords_CutVocabulary()
    {
        // Arrange
        var corpus = Tokenize("a a a b b c a b");

        // Act
        var minimum = Count(corpus, minCount: 2);
        var limited = Count(corpus, maxWords: 1);

        // Assert
        Assert.Equal(["a", "b"], minimum.Words);
        Assert.Equal(["a"], limited.Words);
        Assert.Equal(2, limited.Bigrams.Total);
        Assert.Equal(1L, minimum.FormCounts["c"]);
    }

    /// <summary>Proves a skipped sentence is not counted and is reported as removed.</summary>
    [Fact]
    public void ClusterCorpusReader_Count_SkippedSentence_IsNotCounted()
    {
        // Arrange
        var corpus = Tokenize("Keep this one. Skip that one.");
        var text = corpus.Books[0].Paragraphs[0].SentenceText(1);
        var skipped = new SkippedSentence("gutenberg-1", 0, 1, GutenbergBody.Sha256Hex(Encoding.UTF8.GetBytes(text)));

        // Act
        var counts = ClusterCorpusReader.Count(corpus, [skipped], new ReaderSettings(1, 0), 1, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Skip that one.", text);
        Assert.DoesNotContain("skip", counts.Words);
        Assert.Equal(1, counts.RemovedSentences);
        Assert.Equal(4, counts.RemovedTokens);
        Assert.Equal(1, counts.Sentences);
    }

    /// <summary>Proves a skipped sentence whose text changed, that does not exist or that names an unknown book is refused as stale.</summary>
    [Fact]
    public void ClusterCorpusReader_Count_StaleSkippedSentence_Throws()
    {
        // Arrange
        var corpus = Tokenize("Keep this one. Skip that one.");
        var settings = new ReaderSettings(1, 0);
        var token = TestContext.Current.CancellationToken;

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => ClusterCorpusReader.Count(corpus, [new SkippedSentence("gutenberg-1", 0, 1, new string('0', 64))], settings, 1, token));
        Assert.Throws<InvalidDataException>(() => ClusterCorpusReader.Count(corpus, [new SkippedSentence("gutenberg-1", 3, 0, new string('0', 64))], settings, 1, token));
        Assert.Throws<InvalidDataException>(() => ClusterCorpusReader.Count(corpus, [new SkippedSentence("gutenberg-9", 0, 0, new string('0', 64))], settings, 1, token));
    }

    /// <summary>Proves the counts do not depend on the number of threads.</summary>
    [Fact]
    public void ClusterCorpusReader_Count_DifferentThreadCounts_SameCounts()
    {
        // Arrange: several books
        var bodies = Enumerable.Range(0, 8).Select(static book => string.Join(' ', Enumerable.Range(0, 40).Select(i => $"w{(i * (book + 3)) % 17} w{i % 5}."))).ToArray();
        var corpus = Tokenize(bodies);
        var parallel = ClusterCorpusReader.Tokenize([.. bodies.Select(static (body, index) => ($"gutenberg-{index + 1}", body))], 4, TestContext.Current.CancellationToken);

        // Act
        var one = Count(corpus);
        var four = ClusterCorpusReader.Count(parallel, [], new ReaderSettings(1, 0), 4, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(one.Words, four.Words);
        Assert.Equal(one.Counts, four.Counts);
        Assert.Equal(one.Bigrams.RightWord, four.Bigrams.RightWord);
        Assert.Equal(one.Bigrams.RightCount, four.Bigrams.RightCount);
    }

    /// <summary>Proves every vocabulary word hashes to the runtime's lower-case key of itself (the normalization is idempotent).</summary>
    [Fact]
    public void ClusterCorpusReader_Count_Words_HashToRuntimeKey()
    {
        // Arrange
        var corpus = Tokenize("\u201CDon\u2019t,\u201D said O\u2019NEIL at 5 o\u2019clock in 1850 \u2014 R\u00e9sum\u00e9 & CO.");

        // Act
        var counts = Count(corpus);

        // Assert
        Assert.Contains(counts.Words, static word => word.Contains('\''));
        Assert.Contains("<num>", counts.Words);
        Assert.All(counts.Words, static word => Assert.Equal(FeatureHashing.Fnv1a(word), HeteronymContext.Create(word, 0, word.Length, null, 0).LowerHash));
    }

    /// <summary>Proves the coverage counts the tokens of the first words in units of one ten-thousandth.</summary>
    [Fact]
    public void ClusterCorpusReader_CoveragePerTenThousand_FirstWords()
    {
        // Arrange: a (3 of 6 tokens), b (2), c (1)
        var counts = Count(Tokenize("a a a b b c"));

        // Act / Assert
        Assert.Equal(5000, counts.CoveragePerTenThousand(1));
        Assert.Equal(8333, counts.CoveragePerTenThousand(2));
        Assert.Equal(10000, counts.CoveragePerTenThousand(100));
    }
}
