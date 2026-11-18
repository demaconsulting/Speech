using System.Text;
using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingDataSubsystem;

/// <summary>
///     Unit tests for <see cref="LeakageChecker"/>: split separation inside the data
///     and the optional comparison with external evaluation sets.
/// </summary>
public sealed class LeakageCheckerTests
{
    /// <summary>Runs the external check of a fixture against the evaluation sets written to its evaluation folder.</summary>
    /// <param name="fixture">The fixture.</param>
    /// <returns>The report and the report rows.</returns>
    private static (CheckReport Report, IReadOnlyList<ExternalLeakageRow> Rows) RunExternal(DatasetFixture fixture)
    {
        var report = new CheckReport(1000);
        var checker = new LeakageChecker(TextEnDataSet.Load(fixture.Root, report), report);
        var rows = checker.CheckExternal(LeakageChecker.LoadEvaluationSets(fixture.EvaluationDirectory));
        return (report, rows);
    }

    /// <summary>Proves a sentence that differs only in punctuation and case still collides across two files.</summary>
    [Fact]
    public void LeakageChecker_CheckInternal_SentenceInTwoFiles_ReportsFinding()
    {
        // Arrange: the dev row is the train sentence with other punctuation
        using var fixture = new DatasetFixture();
        fixture.EditRow("dev.tsv", "syn-00003", ("sentence", "the lead PIPE was heavy!"), ("start", "4"), ("end", "8"));

        // Act
        var report = fixture.CheckLeakage();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "dev.tsv:2" && finding.Message.Contains("sentence also in train.tsv (syn-00001)"));
    }

    /// <summary>Proves repeating a sentence inside one file is not a split violation.</summary>
    [Fact]
    public void LeakageChecker_CheckInternal_SentenceTwiceInOneFile_IsAllowed()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.AppendDataRow("train.tsv", "syn-00004", "lead.noun", "The lead pipe was heavy.", "lead", "synthetic", "CC0-1.0");

        // Act
        var report = fixture.CheckLeakage();

        // Assert
        Assert.Empty(report.Findings);
    }

    /// <summary>Proves one natural source document in two splits is reported.</summary>
    [Fact]
    public void LeakageChecker_CheckInternal_DocumentInTwoSplits_ReportsFinding()
    {
        // Arrange: the dev row now comes from the same document as the train row
        using var fixture = new DatasetFixture();
        fixture.EditRow("harvest-provenance.tsv", "harvest-00003", ("source_id", "masc:written/blog/one"));

        // Act
        var report = fixture.CheckLeakage();

        // Assert
        Assert.Contains(
            report.Findings,
            finding => finding.Location == "dev.tsv:3" && finding.Message.Contains("document masc:written/blog/one in train and dev"));
    }

    /// <summary>Proves several rows of one document inside one split are allowed.</summary>
    [Fact]
    public void LeakageChecker_CheckInternal_DocumentTwiceInOneSplit_IsAllowed()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.AppendDataRow("train.tsv", "harvest-00004", "lead.verb", "Another sentence about how to lead.", "lead", "harvest-masc", "CC-BY-3.0-US");
        fixture.AppendRow("harvest-provenance.tsv", "harvest-00004", "masc:written/blog/one", "masc", "masc:written/blog/one#2", "upos");

        // Act
        var report = fixture.CheckLeakage();

        // Assert
        Assert.Empty(report.Findings);
    }

    /// <summary>Proves one synthetic (reading, template) pair in two files is reported.</summary>
    [Fact]
    public void LeakageChecker_CheckInternal_TemplateInTwoFiles_ReportsFinding()
    {
        // Arrange: a dev row of the same reading generated from the template of a train row
        using var fixture = new DatasetFixture();
        fixture.AppendDataRow("dev.tsv", "syn-00004", "lead.noun", "The lead pipe was rusty.", "lead", "synthetic", "CC0-1.0");
        fixture.AppendRow("synthetic-provenance.tsv", "syn-00004", "s4", "lead", "the _ pipe was");

        // Act
        var report = fixture.CheckLeakage();

        // Assert
        Assert.Contains(
            report.Findings,
            finding => finding.Location == "dev.tsv:4" && finding.Message.Contains("synthetic template 'the _ pipe was' for lead.noun in train and dev"));
    }

    /// <summary>Proves a synthetic row renamed to look like an independent holdout row is still checked for template reuse.</summary>
    [Fact]
    public void LeakageChecker_CheckInternal_RenamedSyntheticRowReusingTemplate_ReportsFinding()
    {
        // Arrange: the dev row is synthetic by source but carries the id of an independent holdout row
        using var fixture = new DatasetFixture();
        fixture.AppendDataRow("dev.tsv", "hind-x", "lead.noun", "The lead pipe was rusty.", "lead", "synthetic", "CC0-1.0");
        fixture.AppendRow("synthetic-provenance.tsv", "hind-x", "s4", "lead", "the _ pipe was");

        // Act
        var report = fixture.CheckLeakage();

        // Assert
        Assert.Contains(
            report.Findings,
            finding => finding.Location == "dev.tsv:4" && finding.Message.Contains("synthetic template 'the _ pipe was' for lead.noun in train and dev"));
    }

    /// <summary>Proves a harvested row renamed to look like a synthetic row is still checked for document reuse.</summary>
    [Fact]
    public void LeakageChecker_CheckInternal_RenamedHarvestRowReusingDocument_ReportsFinding()
    {
        // Arrange: the dev row is harvested by source but carries a synthetic-looking id, from the document of a train row
        using var fixture = new DatasetFixture();
        fixture.EditRow("dev.tsv", "harvest-00003", ("id", "syn-renamed"));
        fixture.EditRow("harvest-provenance.tsv", "harvest-00003", ("id", "syn-renamed"), ("source_id", "masc:written/blog/one"));

        // Act
        var report = fixture.CheckLeakage();

        // Assert
        Assert.Contains(
            report.Findings,
            finding => finding.Location == "dev.tsv:3" && finding.Message.Contains("document masc:written/blog/one in train and dev"));
    }

    /// <summary>Proves the same template for a different reading is not a violation.</summary>
    [Fact]
    public void LeakageChecker_CheckInternal_TemplateOfAnotherReading_IsAllowed()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.AppendDataRow("dev.tsv", "syn-00004", "wind.noun", "The wind pipe was rusty.", "wind", "synthetic", "CC0-1.0");
        fixture.AppendRow("synthetic-provenance.tsv", "syn-00004", "s4", "wind", "the _ pipe was");

        // Act
        var report = fixture.CheckLeakage();

        // Assert
        Assert.Empty(report.Findings);
    }

    /// <summary>Proves an exact normalized match with an evaluation sentence is reported and counted.</summary>
    [Fact]
    public void LeakageChecker_CheckExternal_ExactMatch_ReportsFindingAndCounts()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteEvaluationSet("gold", "the LEAD pipe -- was heavy");

        // Act
        var (report, rows) = RunExternal(fixture);

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "train.tsv:3" && finding.Message.Contains("exact match with evaluation set gold"));
        var train = Assert.Single(rows, row => row.Split == "train");
        Assert.Equal(new ExternalLeakageRow("gold", 1, "train", 3, 1, 0, 1), train);
    }

    /// <summary>Proves a long sentence whose 5-grams are contained in an evaluation sentence is a near duplicate but not an exact match.</summary>
    [Fact]
    public void LeakageChecker_CheckExternal_NearDuplicate_ReportsFinding()
    {
        // Arrange: ten words, all six 5-grams appear in the longer evaluation sentence
        using var fixture = new DatasetFixture();
        fixture.AppendDataRow("train.tsv", "syn-00004", "lead.noun", "The old lead pipe in the cellar was very heavy", "lead", "synthetic", "CC0-1.0");
        fixture.WriteEvaluationSet("gold", "the old lead pipe in the cellar was very heavy and rusty");

        // Act
        var (report, rows) = RunExternal(fixture);

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "train.tsv:5" && finding.Message.Contains("near duplicate of evaluation set(s) gold"));
        Assert.DoesNotContain(report.Findings, finding => finding.Message.Contains("exact match"));
        var train = Assert.Single(rows, row => row.Split == "train");
        Assert.Equal(1, train.NearDuplicate);
        Assert.Equal(1, train.Share5Gram);
    }

    /// <summary>Proves rows below the 80 percent containment, or of fewer than eight words, are only counted as sharing a 5-gram.</summary>
    [Fact]
    public void LeakageChecker_CheckExternal_BelowContainmentThreshold_OnlyCountsSharedGram()
    {
        // Arrange: of the six 5-grams of the long row only the first is shared; the five-word row is fully contained in a longer evaluation sentence
        using var fixture = new DatasetFixture();
        fixture.AppendDataRow("train.tsv", "syn-00004", "lead.noun", "The old lead pipe in the cellar was very heavy", "lead", "synthetic", "CC0-1.0");
        fixture.WriteEvaluationSet("gold", "the old lead pipe in some other place entirely", "The lead pipe was heavy and old");

        // Act
        var (report, rows) = RunExternal(fixture);

        // Assert
        Assert.Empty(report.Findings);
        var train = Assert.Single(rows, row => row.Split == "train");
        Assert.Equal(0, train.NearDuplicate);
        Assert.Equal(2, train.Share5Gram);
    }

    /// <summary>Proves the report has one row per evaluation set and split, with sets sorted by name.</summary>
    [Fact]
    public void LeakageChecker_CheckExternal_TwoSets_ReturnsRowPerSetAndSplitSortedByName()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.WriteEvaluationSet("zeta", "nothing in common here at all");
        fixture.WriteEvaluationSet("alpha", "completely different words entirely now");

        // Act
        var (report, rows) = RunExternal(fixture);

        // Assert
        Assert.Empty(report.Findings);
        Assert.Equal(8, rows.Count);
        Assert.Equal(["alpha", "alpha", "alpha", "alpha", "zeta", "zeta", "zeta", "zeta"], rows.Select(row => row.EvalSet));
        Assert.Equal(["train", "dev", "holdout-natural", "holdout-independent"], rows.Take(4).Select(row => row.Split));
        Assert.Contains("evaluation sets 2 sentences 2 distinct keys 2", report.Info);
    }

    /// <summary>Proves a TSV evaluation set reads its <c>sentence</c> column without quoting and falls back to <c>text</c>.</summary>
    [Fact]
    public void LeakageChecker_LoadEvaluationSets_TsvSets_ReadSentenceOrTextColumn()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        File.WriteAllText(Path.Combine(fixture.EvaluationDirectory, "one.tsv"), "id\tsentence\n1\t\"quoted\" lead pipe\n2\tsecond\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(fixture.EvaluationDirectory, "two.tsv"), "id\ttext\n1\tonly text column\n", new UTF8Encoding(false));
        fixture.WriteEvaluationSet("three", "plain line", string.Empty, "another line");

        // Act
        var sets = LeakageChecker.LoadEvaluationSets(fixture.EvaluationDirectory);

        // Assert
        Assert.Equal(["one", "three", "two"], sets.Select(set => set.Name));
        Assert.Equal(["\"quoted\" lead pipe", "second"], sets[0].Sentences);
        Assert.Equal(["plain line", "another line"], sets[1].Sentences);
        Assert.Equal(["only text column"], sets[2].Sentences);
    }

    /// <summary>Proves a TSV evaluation set without a sentence or text column is rejected.</summary>
    [Fact]
    public void LeakageChecker_LoadEvaluationSets_TsvWithoutSentenceColumn_ThrowsInvalidDataException()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        File.WriteAllText(Path.Combine(fixture.EvaluationDirectory, "bad.tsv"), "id\tother\n1\tx\n", new UTF8Encoding(false));

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => LeakageChecker.LoadEvaluationSets(fixture.EvaluationDirectory));
    }

    /// <summary>Proves a missing evaluation folder is an error, not a silent pass.</summary>
    [Fact]
    public void LeakageChecker_LoadEvaluationSets_MissingDirectory_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act / Assert
        Assert.Throws<DirectoryNotFoundException>(() => LeakageChecker.LoadEvaluationSets(Path.Combine(fixture.BaseDirectory, "nope")));
    }

    /// <summary>Proves an evaluation folder without any set is an error, not a silent pass.</summary>
    [Fact]
    public void LeakageChecker_LoadEvaluationSets_EmptyDirectory_ThrowsInvalidDataException()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => LeakageChecker.LoadEvaluationSets(fixture.EvaluationDirectory));
    }

    /// <summary>Proves a file that holds no sentence is rejected by name, whatever its format, even next to good sets.</summary>
    [Theory]
    [InlineData("empty.txt", "")]
    [InlineData("spaces.txt", "   \n\t\n  \r\n")]
    [InlineData("blankcells.tsv", "id\tsentence\n1\t\n2\t   \n")]
    public void LeakageChecker_LoadEvaluationSets_FileWithoutSentences_ThrowsNamingTheFile(string fileName, string content)
    {
        // Arrange: a good set sits next to the empty file, which must still reject the whole folder
        using var fixture = new DatasetFixture();
        fixture.WriteEvaluationSet("good", "a real sentence");
        File.WriteAllText(Path.Combine(fixture.EvaluationDirectory, fileName), content, new UTF8Encoding(false));

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => LeakageChecker.LoadEvaluationSets(fixture.EvaluationDirectory));

        // Assert
        Assert.Contains(fileName, exception.Message);
        Assert.Contains("has no sentences", exception.Message);
    }

    /// <summary>Positive control: blank lines are skipped and the original text of every other sentence is kept.</summary>
    [Fact]
    public void LeakageChecker_LoadEvaluationSets_BlankLinesAmongSentences_AreSkippedAndTextIsKept()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        File.WriteAllText(Path.Combine(fixture.EvaluationDirectory, "gold.txt"), "  padded sentence  \n   \n\nsecond one\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(fixture.EvaluationDirectory, "table.tsv"), "id\tsentence\n1\t \n2\t kept as is \n", new UTF8Encoding(false));

        // Act
        var sets = LeakageChecker.LoadEvaluationSets(fixture.EvaluationDirectory);

        // Assert
        Assert.Equal(["  padded sentence  ", "second one"], sets[0].Sentences);
        Assert.Equal([" kept as is "], sets[1].Sentences);
    }

    /// <summary>Proves the external check refuses sets that hold no sentence at all, however they were built.</summary>
    [Fact]
    public void LeakageChecker_CheckExternal_SetsWithoutAnySentence_ThrowsInvalidDataException()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        var report = new CheckReport(1000);
        var checker = new LeakageChecker(TextEnDataSet.Load(fixture.Root, report), report);

        // Act / Assert
        Assert.Throws<InvalidDataException>(() => checker.CheckExternal([]));
        Assert.Throws<InvalidDataException>(() => checker.CheckExternal([new EvaluationSet("hollow", [])]));
    }

    /// <summary>Proves the inside-the-data-folder test.</summary>
    [Theory]
    [InlineData("data/text-en", "data/text-en", true)]
    [InlineData("data/text-en/eval", "data/text-en", true)]
    [InlineData("data/text-en-eval", "data/text-en", false)]
    public void LeakageChecker_IsInside_ComparesFullPaths(string directory, string root, bool expected)
    {
        // Arrange
        var baseDirectory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "texten-inside"));

        // Act
        var inside = LeakageChecker.IsInside(Path.Combine(baseDirectory, directory), Path.Combine(baseDirectory, root));

        // Assert
        Assert.Equal(expected, inside);
    }

    /// <summary>Proves a link outside the root that points into it counts as inside, and a link inside the root that points out counts as outside.</summary>
    [Fact]
    public void LeakageChecker_IsInside_LinkedPaths_UseRealLocations()
    {
        // Arrange: a root with a real folder, an outside folder, a link out to the root and a link in to the outside folder
        var baseDirectory = Path.Combine(Path.GetTempPath(), "texten-links-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(baseDirectory, "root");
        var outside = Path.Combine(baseDirectory, "outside");
        Directory.CreateDirectory(Path.Combine(root, "real"));
        Directory.CreateDirectory(outside);
        try
        {
            var linkIn = Path.Combine(baseDirectory, "link-to-root");
            var linkOut = Path.Combine(root, "link-to-outside");
            Assert.SkipUnless(DirectoryLink.TryCreate(linkIn, root) && DirectoryLink.TryCreate(linkOut, outside), DirectoryLink.SkipReason);

            // Act
            var insideViaLinkIn = LeakageChecker.IsInside(Path.Combine(linkIn, "real"), root);
            var newFolderViaLink = LeakageChecker.IsInside(Path.Combine(linkIn, "not-yet", "created"), root);
            var insideViaLink = LeakageChecker.IsInside(Path.Combine(linkOut, "x"), root);
            var plainOutside = LeakageChecker.IsInside(Path.Combine(outside, "x"), root);

            // Assert
            Assert.True(insideViaLinkIn);
            Assert.True(newFolderViaLink);
            Assert.False(insideViaLink);
            Assert.False(plainOutside);
        }
        finally
        {
            // Remove the links themselves first so the recursive delete never meets them
            foreach (var link in new[] { Path.Combine(baseDirectory, "link-to-root"), Path.Combine(root, "link-to-outside") })
            {
                if (Directory.Exists(link))
                {
                    Directory.Delete(link, false);
                }
            }

            Directory.Delete(baseDirectory, true);
        }
    }

    /// <summary>Proves the report is written as UTF-8 without byte order mark, LF line endings and the documented columns.</summary>
    [Fact]
    public void LeakageChecker_WriteReport_Rows_WritesDocumentedFormat()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        var path = Path.Combine(fixture.EvaluationDirectory, "report.tsv");
        var rows = new[] { new ExternalLeakageRow("gold", 500, "train", 19865, 0, 1, 39) };

        // Act
        LeakageChecker.WriteReport(path, rows);

        // Assert
        var bytes = File.ReadAllBytes(path);
        Assert.NotEqual(0xEF, bytes[0]);
        Assert.Equal(
            "eval_set\teval_sentences\tsplit\trows\texact\tnear_duplicate\tshare_5gram\ngold\t500\ttrain\t19865\t0\t1\t39\n",
            Encoding.UTF8.GetString(bytes));
    }
}
