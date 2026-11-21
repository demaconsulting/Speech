using System.Globalization;
using DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.ClusteringSubsystem;

/// <summary>
///     Unit tests for <see cref="ClusterInducer"/> and its input <see cref="BigramGraph"/>: the fixed-input golden result
///     that CI compares across operating systems, thread-count invariance, a brute-force loss oracle, tie-breaking and
///     argument checks.
/// </summary>
public sealed class ClusterInducerTests
{
    /// <summary>The content words of the golden corpus: animals, verbs and places (the function words are added per sentence).</summary>
    private static readonly string[][] GoldenGroups =
    [
        ["dog", "cat", "horse", "cow", "sheep", "goat", "pig", "mule"],
        ["runs", "eats", "sleeps", "jumps", "walks", "rests", "waits"],
        ["field", "barn", "garden", "river", "hill", "road", "yard", "wood"],
    ];

    /// <summary>The expected paths of the golden corpus, as <c>word TAB path</c> lines in vocabulary-rank order.</summary>
    private static readonly string[] GoldenPaths =
    [
        "the\t00", ".\t010", "a\t00", "near\t010", "on\t010", "walks\t011",
        "in\t010", "field\t1", "pig\t1", "runs\t011", "sleeps\t011", "barn\t1",
        "dog\t1", "mule\t1", "river\t1", "yard\t1", "waits\t011", "cow\t1",
        "goat\t1", "sheep\t1", "garden\t1", "hill\t1", "horse\t1", "rests\t011",
        "road\t1", "jumps\t011", "cat\t1", "eats\t011", "wood\t1",
    ];

    /// <summary>The expected merges of the golden corpus, as <c>step word a b loss</c> lines (loss in round-trip form).</summary>
    private static readonly string[] GoldenMerges =
    [
        "0 4 1 3 0", "1 5 0 2 0.29845064591896886",
        "2 6 2 4 0.1317577018045597", "3 7 1 2 6.215180798216974",
        "4 8 2 4 9.282248679955352", "5 9 3 4 1.0791691261684662",
        "6 10 3 4 0.32800487147068935", "7 11 2 4 3.350123781002601",
        "8 12 2 4 7.849784487065634", "9 13 2 4 5.582574317786779",
        "10 14 2 4 5.548493153536711", "11 15 2 4 4.37306992218312",
        "12 16 3 4 0.7699766027702992", "13 17 2 4 5.5195978173496485",
        "14 18 2 4 3.7229604720293423", "15 19 2 4 3.394901905432306",
        "16 20 2 4 3.8606331709934807", "17 21 2 4 3.489405141510275",
        "18 22 2 4 3.495294888991424", "19 23 3 4 0.02216011196408374",
        "20 24 2 4 3.7802923711878975", "21 25 3 4 0.01474709432294219",
        "22 26 2 4 2.885361077567353", "23 27 3 4 -2.842170943040401E-14",
        "24 28 2 4 2.1179072957513654", "25 -1 1 3 114.57255029306661",
        "26 -1 0 1 218.13809637570768", "27 -1 0 2 62.78977725174548",
    ];

    /// <summary>
    ///     Proves the fixed-input clustering gives exactly the recorded paths and merge sequence, losses included to the
    ///     last bit; because this runs in every CI job, it compares the arithmetic of Windows, Linux and macOS.
    /// </summary>
    [Fact]
    public void ClusterInducer_Induce_FixedInput_MatchesGoldenPathsAndMerges()
    {
        // Arrange: 60 sentences over a 29-word vocabulary, four clusters
        var (words, graph) = GoldenCorpus();

        // Act
        var result = ClusterInducer.Induce(graph, new InducerOptions(4, 1), TestContext.Current.CancellationToken);

        // Assert: the exact paths and the exact merge sequence
        Assert.Equal(string.Concat(GoldenPaths.Select(static line => line + "\n")), PathText(words, result));
        Assert.Equal(string.Concat(GoldenMerges.Select(static line => line + "\n")), MergeText(result));
    }

    /// <summary>Proves the output is identical for 1, 2, 3 and 8 worker threads, losses included.</summary>
    [Fact]
    public void ClusterInducer_Induce_DifferentThreadCounts_IdenticalOutput()
    {
        // Arrange: a larger structured corpus so every phase runs with many rows
        var graph = SyntheticGraph(words: 160, sentences: 3000, seed: 11);

        // Act
        var results = new[] { 1, 2, 3, 8 }.Select(threads => ClusterInducer.Induce(graph, new InducerOptions(12, threads), TestContext.Current.CancellationToken)).ToList();

        // Assert: every run equals the single-thread run
        foreach (var result in results.Skip(1))
        {
            Assert.Equal(results[0].Paths, result.Paths);
            Assert.Equal(results[0].Merges, result.Merges);
        }
    }

    /// <summary>
    ///     Proves the incrementally maintained loss of every pair equals a fresh brute-force recomputation of the total
    ///     mutual information before and after the merge, and that each chosen merge is the brute-force minimum.
    /// </summary>
    [Fact]
    public void ClusterInducer_Induce_SmallInput_LossesMatchBruteForceOracle()
    {
        // Arrange: eight words, three clusters, every state observed
        var graph = SyntheticGraph(words: 8, sentences: 60, seed: 5);
        var snapshots = new List<InducerSnapshot>();

        // Act
        var result = ClusterInducer.Induce(graph, new InducerOptions(3, 1, Observer: snapshots.Add), TestContext.Current.CancellationToken);

        // Assert: one snapshot per merge, every maintained loss close to the oracle, every choice a minimum
        Assert.Equal(result.Merges.Count, snapshots.Count);
        for (var step = 0; step < snapshots.Count; step++)
        {
            var oracle = snapshots[step].Losses.Keys.ToDictionary(static pair => pair, pair => OracleLoss(graph, snapshots[step].Members, pair.A, pair.B));
            foreach (var (pair, loss) in snapshots[step].Losses)
            {
                Assert.True(Close(oracle[pair], loss), $"step {step} pair {pair}: maintained {loss} oracle {oracle[pair]}");
            }

            var chosen = result.Merges[step];
            Assert.True(Close(oracle.Values.Min(), oracle[(chosen.SlotA, chosen.SlotB)]), $"step {step} did not choose a minimum");
        }
    }

    /// <summary>Proves equal losses are broken by the lowest slots: with no bigrams every loss is zero.</summary>
    [Fact]
    public void ClusterInducer_Induce_AllLossesEqual_MergesLowestSlotsFirst()
    {
        // Arrange: four words that never occur next to each other
        var graph = BigramGraph.FromPairs(4, []);

        // Act
        var result = ClusterInducer.Induce(graph, new InducerOptions(2, 1), TestContext.Current.CancellationToken);

        // Assert: word 2 enters slot 2 and (0,1) merge; word 3 enters slot 1 and (0,1) merge; then (0,2)
        Assert.Equal(
            [(2, 0, 1), (3, 0, 1), (-1, 0, 2)],
            result.Merges.Select(static merge => (merge.Word, merge.SlotA, merge.SlotB)));
        Assert.Equal(["0", "0", "1", "0"], result.Paths);
    }

    /// <summary>Proves a vocabulary smaller than the cluster count keeps one cluster per word and only builds the hierarchy.</summary>
    [Fact]
    public void ClusterInducer_Induce_FewerWordsThanClusters_OnePathPerWord()
    {
        // Arrange
        var graph = SyntheticGraph(words: 5, sentences: 30, seed: 3);

        // Act
        var result = ClusterInducer.Induce(graph, new InducerOptions(512, 2), TestContext.Current.CancellationToken);

        // Assert: five distinct prefix-free paths built by four final merges
        Assert.Equal(5, result.Clusters);
        Assert.Equal(5, result.Paths.Distinct(StringComparer.Ordinal).Count());
        Assert.All(result.Merges, static merge => Assert.Equal(-1, merge.Word));
        Assert.Equal(4, result.Merges.Count);
        AssertPrefixFree(result.Paths);
    }

    /// <summary>Proves the paths are bit strings, prefix-free, and number at most the requested clusters.</summary>
    [Fact]
    public void ClusterInducer_Induce_SyntheticInput_PathsArePrefixFreeAndBounded()
    {
        // Arrange
        var graph = SyntheticGraph(words: 60, sentences: 800, seed: 9);

        // Act
        var result = ClusterInducer.Induce(graph, new InducerOptions(7, 2), TestContext.Current.CancellationToken);

        // Assert
        var distinct = result.Paths.Distinct(StringComparer.Ordinal).ToList();
        Assert.Equal(7, distinct.Count);
        Assert.All(result.Paths, static path => Assert.Matches("^[01]+$", path));
        AssertPrefixFree(result.Paths);
    }

    /// <summary>Proves progress is reported for the words and the final hierarchy.</summary>
    [Fact]
    public void ClusterInducer_Induce_WithProgress_ReportsWordsAndFinalMerges()
    {
        // Arrange
        var graph = SyntheticGraph(words: 30, sentences: 200, seed: 4);
        var reports = new List<InducerProgress>();

        // Act
        ClusterInducer.Induce(graph, new InducerOptions(4, 1, reports.Add, ProgressEvery: 10), TestContext.Current.CancellationToken);

        // Assert: the words reach the vocabulary size and the final merges reach C - 1
        Assert.Contains(reports, static report => report is { WordsAdded: 30, FinalMerges: 0 });
        Assert.Contains(reports, static report => report is { WordsAdded: 30, FinalMerges: 3, FinalMergesTotal: 3 });
    }

    /// <summary>Proves invalid arguments are refused.</summary>
    [Fact]
    public void ClusterInducer_Induce_InvalidArguments_Throw()
    {
        // Arrange
        var graph = SyntheticGraph(words: 6, sentences: 20, seed: 1);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => ClusterInducer.Induce(BigramGraph.FromPairs(1, []), new InducerOptions(2, 1), TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ClusterInducer.Induce(graph, new InducerOptions(1, 1), TestContext.Current.CancellationToken));
        Assert.Throws<ArgumentException>(() => ClusterInducer.Induce(graph, new InducerOptions(2, 0), TestContext.Current.CancellationToken));
    }

    /// <summary>Proves the graph adds repeated pairs and computes the adjacency lists and marginals.</summary>
    [Fact]
    public void BigramGraph_FromPairs_RepeatedPairs_AddsCountsAndMarginals()
    {
        // Arrange / Act
        var graph = BigramGraph.FromPairs(3, [(0, 1, 2), (1, 2, 3), (0, 1, 1), (2, 0, 4)]);

        // Assert
        Assert.Equal(3, graph.PairTypes);
        Assert.Equal(10, graph.Total);
        Assert.Equal([3L, 3L, 4L], graph.LeftMarginal);
        Assert.Equal([4L, 3L, 3L], graph.RightMarginal);
        Assert.Equal([0, 1, 2, 3], graph.RightStart);
        Assert.Equal([1, 2, 0], graph.RightWord);
        Assert.Equal([2, 0, 1], graph.LeftWord);
    }

    /// <summary>Proves unsorted or out-of-range keys are refused.</summary>
    [Fact]
    public void BigramGraph_FromSortedKeys_InvalidKeys_Throw()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => BigramGraph.FromSortedKeys(3, [(1L << 32) | 2, 1], [1, 1]));
        Assert.Throws<ArgumentException>(() => BigramGraph.FromSortedKeys(2, [(1L << 32) | 2], [1]));
        Assert.Throws<ArgumentException>(() => BigramGraph.FromSortedKeys(2, [1], [0]));
    }

    /// <summary>
    ///     Builds the golden corpus: 60 sentences of the pattern determiner, animal, verb, preposition, determiner, place,
    ///     full stop, with the words chosen by a fixed integer pseudo-random generator.
    /// </summary>
    /// <returns>The vocabulary by rank and the graph.</returns>
    internal static (IReadOnlyList<string> Words, BigramGraph Graph) GoldenCorpus()
    {
        var state = 20261120u;
        string Pick(string[] group)
        {
            state = unchecked((state * 1664525u) + 1013904223u);
            return group[(state >> 16) % (uint)group.Length];
        }

        var sentences = new List<string[]>();
        for (var i = 0; i < 60; i++)
        {
            string[] determiners = ["the", "a"];
            string[] prepositions = ["in", "on", "near"];
            sentences.Add([Pick(determiners), Pick(GoldenGroups[0]), Pick(GoldenGroups[1]), Pick(prepositions), Pick(determiners), Pick(GoldenGroups[2]), "."]);
        }

        return GraphOf(sentences);
    }

    /// <summary>
    ///     Builds a graph from word sentences with the reader's vocabulary order (count descending, then ordinal).
    /// </summary>
    /// <param name="sentences">The sentences.</param>
    /// <returns>The vocabulary by rank and the graph.</returns>
    internal static (IReadOnlyList<string> Words, BigramGraph Graph) GraphOf(IReadOnlyList<string[]> sentences)
    {
        var counts = sentences.SelectMany(static sentence => sentence).GroupBy(static word => word, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.LongCount(), StringComparer.Ordinal);
        var words = counts.Keys.OrderByDescending(word => counts[word]).ThenBy(static word => word, StringComparer.Ordinal).ToList();
        var rank = words.Select(static (word, index) => (word, index)).ToDictionary(static pair => pair.word, static pair => pair.index, StringComparer.Ordinal);
        var pairs = sentences.SelectMany(sentence => sentence.Zip(sentence.Skip(1), (left, right) => (rank[left], rank[right], 1L)));
        return (words, BigramGraph.FromPairs(words.Count, pairs));
    }

    /// <summary>
    ///     Builds a structured synthetic graph: words fall into a few hidden classes and each class prefers the next one.
    /// </summary>
    /// <param name="words">The number of words.</param>
    /// <param name="sentences">The number of sentences.</param>
    /// <param name="seed">The generator seed.</param>
    /// <returns>The graph (words are ranks).</returns>
    internal static BigramGraph SyntheticGraph(int words, int sentences, uint seed)
    {
        var state = seed;
        int Next(int bound)
        {
            state = unchecked((state * 1664525u) + 1013904223u);
            return (int)((state >> 8) % (uint)bound);
        }

        var classes = Math.Max(2, words / 8);
        var pairs = new List<(int, int, long)>();
        for (var s = 0; s < sentences; s++)
        {
            var length = 3 + Next(6);
            var word = Next(Math.Min(words, 1 + Next(words)));
            for (var i = 1; i < length; i++)
            {
                // Skewed choice within the next class so low ranks are frequent
                var nextClass = ((word % classes) + 1 + Next(2)) % classes;
                var members = Enumerable.Range(0, words).Where(w => w % classes == nextClass).ToList();
                var next = members[Math.Min(members.Count - 1, Next(1 + Next(members.Count)))];
                pairs.Add((word, next, 1));
                word = next;
            }
        }

        return BigramGraph.FromPairs(words, pairs);
    }

    /// <summary>
    ///     Renders the paths as <c>word TAB path</c> lines in rank order.
    /// </summary>
    /// <param name="words">The vocabulary.</param>
    /// <param name="result">The result.</param>
    /// <returns>The text.</returns>
    internal static string PathText(IReadOnlyList<string> words, InducedClusters result) =>
        string.Concat(words.Select((word, rank) => $"{word}\t{result.Paths[rank]}\n"));

    /// <summary>
    ///     Renders the merges as <c>step word a b loss</c> lines with the loss in round-trip form.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The text.</returns>
    internal static string MergeText(InducedClusters result) =>
        string.Concat(result.Merges.Select(static merge => string.Create(
            CultureInfo.InvariantCulture,
            $"{merge.Step} {merge.Word} {merge.SlotA} {merge.SlotB} {merge.Loss:R}\n")));

    /// <summary>
    ///     Computes the loss of merging two clusters from scratch: total mutual information before minus after.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="members">The words of each occupied slot.</param>
    /// <param name="a">The lower slot.</param>
    /// <param name="b">The higher slot.</param>
    /// <returns>The loss.</returns>
    private static double OracleLoss(BigramGraph graph, IReadOnlyDictionary<int, IReadOnlyList<int>> members, int a, int b)
    {
        var before = members.ToDictionary(static pair => pair.Key, static pair => pair.Value.ToList());
        var after = members.Where(pair => pair.Key != b).ToDictionary(static pair => pair.Key, static pair => pair.Value.ToList());
        after[a] = [.. members[a], .. members[b]];
        return Information(graph, before) - Information(graph, after);
    }

    /// <summary>
    ///     Computes the clustering's total of <c>n (ln n + ln N - ln nL - ln nR)</c> with the library logarithm.
    /// </summary>
    /// <param name="graph">The graph.</param>
    /// <param name="clusters">The words of each cluster.</param>
    /// <returns>The total.</returns>
    private static double Information(BigramGraph graph, Dictionary<int, List<int>> clusters)
    {
        var clusterOf = new Dictionary<int, int>();
        foreach (var (slot, words) in clusters)
        {
            foreach (var word in words)
            {
                clusterOf[word] = slot;
            }
        }

        var counts = new Dictionary<(int, int), long>();
        for (var u = 0; u < graph.WordCount; u++)
        {
            for (var e = graph.RightStart[u]; e < graph.RightStart[u + 1]; e++)
            {
                if (clusterOf.TryGetValue(u, out var c) && clusterOf.TryGetValue(graph.RightWord[e], out var d))
                {
                    counts[(c, d)] = counts.GetValueOrDefault((c, d)) + graph.RightCount[e];
                }
            }
        }

        var total = 0.0;
        foreach (var ((c, d), n) in counts)
        {
            var left = clusters[c].Sum(word => graph.LeftMarginal[word]);
            var right = clusters[d].Sum(word => graph.RightMarginal[word]);
            total += n * (Math.Log(n) + Math.Log(graph.Total) - Math.Log(left) - Math.Log(right));
        }

        return total;
    }

    /// <summary>
    ///     Compares two losses with a relative tolerance of 1e-9 (absolute near zero).
    /// </summary>
    /// <param name="expected">The oracle value.</param>
    /// <param name="actual">The maintained value.</param>
    /// <returns><see langword="true"/> when they agree.</returns>
    private static bool Close(double expected, double actual) =>
        Math.Abs(expected - actual) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(expected), Math.Abs(actual)));

    /// <summary>
    ///     Asserts that no path is a proper prefix of another.
    /// </summary>
    /// <param name="paths">The paths.</param>
    private static void AssertPrefixFree(IEnumerable<string> paths)
    {
        var distinct = paths.Distinct(StringComparer.Ordinal).ToList();
        foreach (var path in distinct)
        {
            Assert.DoesNotContain(distinct, other => other.Length > path.Length && other.StartsWith(path, StringComparison.Ordinal));
        }
    }
}
