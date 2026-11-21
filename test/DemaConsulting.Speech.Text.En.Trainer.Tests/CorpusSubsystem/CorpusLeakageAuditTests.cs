using System.Text;
using DemaConsulting.Speech.Text.En.Trainer.CorpusSubsystem;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.CorpusSubsystem;

/// <summary>
///     Unit tests for <see cref="CorpusLeakageAudit"/>: the exact, near and contains rules, attribution, the counts,
///     thread invariance, the removals file and the evaluation sentences of a data folder.
/// </summary>
public sealed class CorpusLeakageAuditTests
{
    /// <summary>A long evaluation sentence (12 words).</summary>
    private const string LongEval = "The old sailor mended the torn canvas sail beside the quiet harbor wall.";

    /// <summary>
    ///     Tokenizes bodies and audits them on one thread.
    /// </summary>
    /// <param name="evaluation">The evaluation sentences.</param>
    /// <param name="bodies">The bodies, named <c>gutenberg-1</c>, <c>gutenberg-2</c> and so on.</param>
    /// <returns>The removals.</returns>
    private static IReadOnlyList<CorpusRemoval> Audit(IReadOnlyList<EvaluationSentence> evaluation, params string[] bodies)
    {
        var corpus = CorpusReader.Tokenize([.. bodies.Select(static (body, index) => ($"gutenberg-{index + 1}", body))], 1, TestContext.Current.CancellationToken);
        return CorpusLeakageAudit.Run(corpus, evaluation, 1, TestContext.Current.CancellationToken);
    }

    /// <summary>Proves an exact repeat (case and punctuation aside) is removed with its location, rule and text hash.</summary>
    [Fact]
    public void CorpusLeakageAudit_Run_ExactRepeat_IsRemoved()
    {
        // Arrange
        EvaluationSentence[] evaluation = [new("dev.tsv", "d1", "The wind howled all night.")];

        // Act
        var removals = Audit(evaluation, "First paragraph.\n\nIt rained. THE WIND HOWLED, ALL NIGHT! Then calm.");

        // Assert
        var removal = Assert.Single(removals);
        Assert.Equal(("gutenberg-1", 1, 1, "exact", "dev.tsv", "d1"), (removal.SourceId, removal.Paragraph, removal.Sentence, removal.Rule, removal.EvalFile, removal.EvalId));
        Assert.Equal(GutenbergBody.Sha256Hex(Encoding.UTF8.GetBytes("THE WIND HOWLED, ALL NIGHT!")), removal.TextSha256);
    }

    /// <summary>Proves a near repeat of at least eight words is removed, and a short partial overlap is not.</summary>
    [Fact]
    public void CorpusLeakageAudit_Run_NearRepeat_IsRemoved()
    {
        // Arrange: the corpus sentence adds one word at the end of the 12-word sentence (8 of 9 5-grams shared)
        EvaluationSentence[] evaluation = [new("holdout-natural.tsv", "h1", LongEval)];

        // Act
        var removals = Audit(evaluation, "The old sailor mended the torn canvas sail beside the quiet harbor wall today. The old sailor mended it.");

        // Assert
        var removal = Assert.Single(removals);
        Assert.Equal(("near", 0), (removal.Rule, removal.Sentence));
    }

    /// <summary>Proves a long evaluation sentence quoted across two corpus sentences removes both.</summary>
    [Fact]
    public void CorpusLeakageAudit_Run_QuotedAcrossSentences_ContainsRemovesBoth()
    {
        // Arrange: a 16-word evaluation sentence quoted across two corpus sentences (a full stop splits it)
        EvaluationSentence[] evaluation = [new("holdout-independent.tsv", "i1", "Every evening the keeper climbed the long stair and lit the great lamp for the ships.")];

        // Act
        var removals = Audit(
            evaluation,
            "He wrote that every evening the keeper climbed the long stair. And lit the great lamp for the ships at sea, he said. Nothing else.");

        // Assert: the two sentences sharing 5-grams go, the unrelated one stays
        Assert.Equal([0, 1], removals.Select(static removal => removal.Sentence));
        Assert.All(removals, static removal => Assert.Equal("contains", removal.Rule));
    }

    /// <summary>Proves a short evaluation sentence (5 to 7 words) inside one corpus sentence is removed by the contains rule.</summary>
    [Fact]
    public void CorpusLeakageAudit_Run_ShortEvaluationInsideSentence_IsRemoved()
    {
        // Arrange
        EvaluationSentence[] evaluation = [new("regression.tsv", "reg-0001", "It was a deep muscle tear.")];

        // Act
        var removals = Audit(evaluation, "The doctor said it was a deep muscle tear in the thigh. It healed.");

        // Assert
        var removal = Assert.Single(removals);
        Assert.Equal(("contains", "regression.tsv", 0), (removal.Rule, removal.EvalFile, removal.Sentence));
    }

    /// <summary>Proves a sentence matching several evaluation sentences names the first file, then the lowest id.</summary>
    [Fact]
    public void CorpusLeakageAudit_Run_SeveralMatches_FirstFileThenLowestId()
    {
        // Arrange: the same sentence in the generalization set, twice in dev (ids out of order) and in regression
        EvaluationSentence[] evaluation =
        [
            new("generalization.tsv", "gen-0001", "The wind howled all night."),
            new("dev.tsv", "d9", "The wind howled all night."),
            new("dev.tsv", "d10", "The wind howled all night!"),
            new("regression.tsv", "reg-0001", "The wind howled all night."),
        ];

        // Act
        var removal = Assert.Single(Audit(evaluation, "The wind howled all night."));

        // Assert: dev comes first and "d10" precedes "d9" ordinally
        Assert.Equal(("dev.tsv", "d10"), (removal.EvalFile, removal.EvalId));
    }

    /// <summary>Proves the removals and their order do not depend on the number of threads.</summary>
    [Fact]
    public void CorpusLeakageAudit_Run_DifferentThreadCounts_SameRemovals()
    {
        // Arrange
        EvaluationSentence[] evaluation = [new("dev.tsv", "d1", "The wind howled all night."), new("holdout-natural.tsv", "h1", LongEval)];
        var bodies = Enumerable.Range(0, 6).Select(static book => $"Book {book}. The wind howled all night.\n\n{LongEval} More text.").ToArray();
        var corpus = CorpusReader.Tokenize([.. bodies.Select(static (body, index) => ($"gutenberg-{index + 1}", body))], 3, TestContext.Current.CancellationToken);

        // Act
        var one = CorpusLeakageAudit.Run(corpus, evaluation, 1, TestContext.Current.CancellationToken);
        var four = CorpusLeakageAudit.Run(corpus, evaluation, 4, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(12, one.Count);
        Assert.Equal(one, four);
    }

    /// <summary>Proves the counts are given per evaluation file and rule with distinct evaluation sentences and books.</summary>
    [Fact]
    public void CorpusLeakageAudit_Counts_PerFileAndRule()
    {
        // Arrange
        var hash = new string('a', 64);
        CorpusRemoval[] removals =
        [
            new("gutenberg-1", 0, 0, "exact", "dev.tsv", "d1", hash),
            new("gutenberg-1", 0, 1, "exact", "dev.tsv", "d1", hash),
            new("gutenberg-2", 3, 0, "exact", "dev.tsv", "d2", hash),
            new("gutenberg-2", 4, 0, "contains", "regression.tsv", "reg-0001", hash),
        ];

        // Act
        var counts = CorpusLeakageAudit.Counts(removals);

        // Assert: five files times three rules, with the two non-empty cells
        Assert.Equal(15, counts.Count);
        Assert.Contains(new AuditCount("dev.tsv", "exact", 3, 2, 2), counts);
        Assert.Contains(new AuditCount("regression.tsv", "contains", 1, 1, 1), counts);
        Assert.Equal(4, counts.Sum(static count => count.RemovedSentences));
    }

    /// <summary>Proves the removals file round-trips and malformed or unsorted files are refused.</summary>
    [Fact]
    public void CorpusLeakageAudit_WriteAndParse_RoundTripAndValidation()
    {
        // Arrange
        var hash = new string('f', 64);
        CorpusRemoval[] removals =
        [
            new("gutenberg-10", 2, 1, "near", "holdout-natural.tsv", "h1", hash),
            new("gutenberg-9", 0, 0, "exact", "dev.tsv", "d1", hash),
        ];

        // Act
        var bytes = CorpusLeakageAudit.Write(removals);

        // Assert
        Assert.StartsWith(CorpusLeakageAudit.Header + "\ngutenberg-10\t2\t1\tnear\t", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
        Assert.Equal(removals, CorpusLeakageAudit.Parse(bytes));
        Assert.Throws<InvalidDataException>(() => CorpusLeakageAudit.Parse(CorpusLeakageAudit.Write([removals[1], removals[0]])));
        Assert.Throws<InvalidDataException>(() => CorpusLeakageAudit.Parse(CorpusLeakageAudit.Write([removals[0] with { Rule = "fuzzy" }])));
        Assert.Throws<InvalidDataException>(() => CorpusLeakageAudit.Parse(CorpusLeakageAudit.Write([removals[0] with { EvalFile = "train.tsv" }])));
        Assert.Throws<InvalidDataException>(() => CorpusLeakageAudit.Parse(Encoding.UTF8.GetBytes("wrong header\n")));
    }

    /// <summary>Proves the evaluation sentences of a data folder are every row of the five evaluation files and none of the training file.</summary>
    [Fact]
    public void CorpusLeakageAudit_LoadEvaluationSentences_FixtureData_FiveFilesNoTraining()
    {
        // Arrange
        using var data = new DatasetFixture();

        // Act
        var sentences = CorpusLeakageAudit.LoadEvaluationSentences(data.Root);

        // Assert: 2 dev, 1 natural, 3 independent, 2 regression
        Assert.Equal(8, sentences.Count);
        Assert.Equal(["dev.tsv", "dev.tsv", "holdout-natural.tsv"], sentences.Take(3).Select(static sentence => sentence.File));
        Assert.DoesNotContain(sentences, static sentence => sentence.Id == "harvest-00001");
    }
}
