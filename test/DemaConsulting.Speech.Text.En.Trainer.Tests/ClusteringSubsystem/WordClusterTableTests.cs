using System.Text;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ClusteringSubsystem;

/// <summary>
///     Unit tests for <see cref="WordClusterTable"/>: the shared-hash drop-all rule, eligibility, the fixed text form and
///     every parse rule.
/// </summary>
public sealed class WordClusterTableTests
{
    /// <summary>Two eligible words with the same 32-bit lookup hash, found by a deterministic search.</summary>
    private static readonly string[] Colliding = FindCollision();

    /// <summary>
    ///     Finds the first two words of the form <c>w</c> followed by a decimal number whose lookup hashes are equal.
    /// </summary>
    /// <remarks>A 32-bit hash has its first collision after about 80,000 such words, so the search is fast and always gives the same pair.</remarks>
    /// <returns>The two words in ordinal order.</returns>
    private static string[] FindCollision()
    {
        var seen = new Dictionary<uint, string>();
        var index = 0;
        while (true)
        {
            var word = "w" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!seen.TryAdd(FeatureHashing.Fnv1a(word), word))
            {
                return [.. new[] { seen[FeatureHashing.Fnv1a(word)], word }.Order(StringComparer.Ordinal)];
            }

            index++;
        }
    }

    /// <summary>
    ///     Parses table text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maximumPaths">The largest number of paths.</param>
    /// <returns>The rows.</returns>
    private static IReadOnlyList<WordClusterEntry> Parse(string text, int maximumPaths = 512) =>
        WordClusterTable.Parse(Encoding.UTF8.GetBytes(text), maximumPaths, 1);

    /// <summary>Proves every word of a lookup hash shared by two words is left out and counted, and the rest kept.</summary>
    [Fact]
    public void WordClusterTable_Build_SharedHash_DropsEveryWordOfThatHash()
    {
        // Arrange: the two colliding words and two ordinary ones
        Assert.Equal(FeatureHashing.Fnv1a(Colliding[0]), FeatureHashing.Fnv1a(Colliding[1]));
        string[] words = ["the", Colliding[0], "cat", Colliding[1]];

        // Act
        var entries = WordClusterTable.Build(words, ["0", "10", "11", "10"], [9, 8, 7, 6], out var collisions, out var ineligible);

        // Assert: neither colliding word survives, so neither can return the other's cluster
        Assert.Equal(2, collisions);
        Assert.Equal(0, ineligible);
        Assert.Equal([new WordClusterEntry("cat", "11", 7), new WordClusterEntry("the", "0", 9)], entries);
    }

    /// <summary>Proves a word with an ineligible character is left out and counted.</summary>
    [Fact]
    public void WordClusterTable_Build_IneligibleWord_IsDroppedAndCounted()
    {
        // Act
        var entries = WordClusterTable.Build(["dog", "\u0101b"], ["0", "1"], [3, 2], out var collisions, out var ineligible);

        // Assert
        Assert.Equal(["dog"], entries.Select(static entry => entry.Word));
        Assert.Equal((0, 1), (collisions, ineligible));
    }

    /// <summary>Proves lists of different length or repeated words are refused.</summary>
    [Fact]
    public void WordClusterTable_Build_InconsistentInput_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => WordClusterTable.Build(["a", "b"], ["0"], [1, 1], out _, out _));
        Assert.Throws<ArgumentException>(() => WordClusterTable.Build(["a", "a"], ["0", "1"], [1, 1], out _, out _));
    }

    /// <summary>Proves the text form is the fixed header plus ordinal rows with LF endings, and it parses back.</summary>
    [Fact]
    public void WordClusterTable_WriteAndParse_RoundTrip()
    {
        // Arrange
        var entries = WordClusterTable.Build(["the", "<num>", "Zebra", "don't"], ["00", "01", "10", "11"], [40, 30, 20, 10], out _, out _);

        // Act
        var bytes = WordClusterTable.Write(entries);

        // Assert
        Assert.Equal("word\tpath\tcount\n<num>\t01\t30\nZebra\t10\t20\ndon't\t11\t10\nthe\t00\t40\n", Encoding.UTF8.GetString(bytes));
        Assert.Equal(entries, WordClusterTable.Parse(bytes, 4, 10));
    }

    /// <summary>Proves each table rule is enforced when parsing.</summary>
    [Fact]
    public void WordClusterTable_Parse_BrokenTables_Throw()
    {
        // Act / Assert: header, order, path alphabet, prefix, path count, count minimum, shared hash, CR, eligibility
        Assert.Throws<InvalidDataException>(() => Parse("word\tcluster\tcount\n"));
        Assert.Throws<InvalidDataException>(() => Parse("word\tpath\tcount\nb\t0\t1\na\t1\t1\n"));
        Assert.Throws<InvalidDataException>(() => Parse("word\tpath\tcount\na\t02\t1\n"));
        Assert.Throws<InvalidDataException>(() => Parse("word\tpath\tcount\na\t0\t1\nb\t01\t1\n"));
        Assert.Throws<InvalidDataException>(() => Parse("word\tpath\tcount\na\t0\t1\nb\t1\t1\n", maximumPaths: 1));
        Assert.Throws<InvalidDataException>(() => Parse("word\tpath\tcount\na\t0\t0\n"));
        Assert.Throws<InvalidDataException>(() => Parse($"word\tpath\tcount\n{Colliding[0]}\t0\t1\n{Colliding[1]}\t1\t1\n"));
        Assert.Throws<InvalidDataException>(() => Parse("word\tpath\tcount\r\na\t0\t1\r\n"));
        Assert.Throws<InvalidDataException>(() => Parse("word\tpath\tcount\n\u0101\t0\t1\n"));
    }
}
