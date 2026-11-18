using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingDataSubsystem;

/// <summary>
///     Unit tests for <see cref="SchemaChecker"/>: positive controls for valid data and, per rule family, one fixture
///     that carries several independent defects, each of which must be reported by its own finding.
/// </summary>
public sealed class SchemaCheckerTests
{
    /// <summary>The check name the schema checker reports under.</summary>
    private const string Schema = "schema";

    /// <summary>
    ///     Asserts that the schema check reported a finding for every expected message part.
    /// </summary>
    /// <param name="report">The report.</param>
    /// <param name="messages">The message parts that must each be found.</param>
    private static void AssertFindings(CheckReport report, params string[] messages)
    {
        foreach (var message in messages)
        {
            Assert.True(DatasetFixture.Has(report, Schema, message), $"No schema finding contains '{message}'.");
        }
    }

    /// <summary>Proves the valid fixture has no schema findings and the run adds the informational summary lines.</summary>
    [Fact]
    public void SchemaChecker_Run_ValidFixture_ReportsNoFindings()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Empty(report.Findings);
        Assert.Contains("extension decisions 2 (add: 1, defer: 1)", report.Info);
        Assert.Contains("readings 3 lemmas 2 mapped WHD ids 3 forms 3", report.Info);
        Assert.Contains("rows: dev-natural: 1, dev-synthetic: 1, holdout-independent: 3, holdout-natural: 1, train-natural: 1, train-synthetic: 2", report.Info);
        Assert.Contains("readings with a holdout-independent row: 3; with fewer than 5: [lead.noun, lead.verb, wind.noun]", report.Info);
    }

    /// <summary>Proves the per-reading counts are computed from the rows.</summary>
    [Fact]
    public void ReadingCounts_Build_ValidFixture_CountsRowsPerReading()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        var data = TextEnDataSet.Load(fixture.Root, new CheckReport());

        // Act
        var counts = ReadingCounts.Build(data);

        // Assert
        var leadVerb = Assert.Single(counts.Items, item => item.ReadingId == "lead.verb");
        Assert.Equal(new ReadingCount("lead.verb", 1, 0, 1, 0, 1), leadVerb);
        Assert.Equal(39, leadVerb.NeedSynthetic);
        Assert.Equal(9, leadVerb.NeedNatural);
    }

    /// <summary>Proves the missing counts never go below zero.</summary>
    [Theory]
    [InlineData(0, 0, 40, 10)]
    [InlineData(11, 29, 0, 0)]
    public void ReadingCount_Needs_AreClampedAtZero(int natural, int synthetic, int needSynthetic, int needNatural)
    {
        // Arrange
        var count = new ReadingCount("x.y", natural, synthetic, 0, 0, 0);

        // Act / Assert
        Assert.Equal(needSynthetic, count.NeedSynthetic);
        Assert.Equal(needNatural, count.NeedNatural);
    }

    /// <summary>Proves the row-level rules: unique id, split column, reading, lemma, source, integer offsets and a whole-word span.</summary>
    [Fact]
    public void SchemaChecker_Run_RowFieldDefects_ReportsEachFinding()
    {
        // Arrange: one defect per row; the span row keeps "lead" at 9..13 of "She will lead the team today." and moves it by one
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "syn-00001", ("reading_id", "ghost.noun"));
        fixture.EditRow("train.tsv", "syn-00002", ("lemma", "zinc"));
        fixture.EditRow("train.tsv", "harvest-00001", ("start", "10"), ("end", "13"));
        fixture.EditRow("dev.tsv", "syn-00003", ("id", "syn-00001"));
        fixture.EditRow("dev.tsv", "harvest-00003", ("source", "scraped-web"));
        fixture.EditRow("holdout-independent.tsv", "hind-1", ("split", "holdout-independent"));
        fixture.EditRow("holdout-independent.tsv", "hind-2", ("start", "x"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        AssertFindings(
            report,
            "unknown reading 'ghost.noun'",
            "lemma 'zinc' is not 'wind'",
            "syn-00001: duplicate id (also in train.tsv)",
            "unknown source 'scraped-web'",
            "split column 'holdout-independent' (expected 'test')",
            "start and end must be integers");
        Assert.Contains(report.Findings, finding => finding.Location == "train.tsv:2" && finding.Message.Contains("span:"));
    }

    /// <summary>Proves the reading inventory rules: identifier grammar and uniqueness, lemma prefix, closed tokens, descriptions and IPA.</summary>
    [Fact]
    public void SchemaChecker_Run_ReadingInventoryDefects_ReportsEachFinding()
    {
        // Arrange: edits first (they address rows by identifier), then the appended rows
        using var fixture = new DatasetFixture();
        fixture.EditRow("readings.tsv", "lead.noun", ("lemma", "breeze"), ("pos_hint", "NOUN|XYZ"), ("status", "retired"));
        fixture.EditRow("readings.tsv", "lead.verb", ("description", "Verb use of \u02C8lead."));
        fixture.EditRow("readings.tsv", "wind.noun", ("description", "Noun use of \"lead\", the heavy metal, as opposed to the verb."));
        fixture.AppendRow("readings.tsv", "Wind.noun", "wind", "NOUN", "Another description.", "active");
        fixture.AppendRow("readings.tsv", "wind.noun", "wind", "NOUN", "A second, different description.", "active");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        AssertFindings(
            report,
            "does not match lemma.discriminator",
            "lemma 'breeze' is not the prefix of 'lead.noun'",
            "duplicate reading id 'wind.noun'",
            "pos_hint 'NOUN|XYZ'",
            "status 'retired'",
            "description of 'wind.noun' is empty or duplicates",
            "IPA symbol");
    }

    /// <summary>Proves the references between mapping, forms and readings: repeated, unknown, unmapped and form-less readings.</summary>
    [Fact]
    public void SchemaChecker_Run_ReferentialDefects_ReportsEachFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.AppendRow("whd-mapping.tsv", "lead_vrb", "wind.noun");
        fixture.AppendRow("whd-mapping.tsv", "ghost_nou", "ghost.noun");
        fixture.RemoveRow("whd-mapping.tsv", "lead_nou");
        fixture.AppendRow("forms.tsv", "ghost", "ghost.verb", "base");
        fixture.RemoveRow("forms.tsv", "wind");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        AssertFindings(
            report,
            "duplicate whd_wordid 'lead_vrb'",
            "unknown reading 'ghost.noun'",
            "reading 'lead.noun' has no entry in whd-mapping.tsv",
            "unknown reading 'ghost.verb'",
            "reading 'wind.noun' has no form in forms.tsv");
    }

    /// <summary>Proves a reading added as a new reading by an extension decision needs no mapping.</summary>
    [Fact]
    public void SchemaChecker_Run_ExtensionAddedReadingWithoutMapping_ReportsNothing()
    {
        // Arrange: wind.noun is now a brand-new reading the evaluation sets never knew
        using var fixture = new DatasetFixture();
        fixture.RemoveRow("whd-mapping.tsv", "wind_nou");
        fixture.EditRow("extension-decisions.tsv", "1", ("kind", "new-reading"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Empty(report.Findings);
    }

    /// <summary>Proves the extension decision rules: only add or defer, added readings exist, deferred lemmas list none.</summary>
    [Fact]
    public void SchemaChecker_Run_ExtensionDecisionDefects_ReportsEachFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("extension-decisions.tsv", "1", ("readings", "ghost.noun"));
        fixture.EditRow("extension-decisions.tsv", "2", ("decision", "maybe"));
        fixture.AppendRow("extension-decisions.tsv", "3", "fog", "fog", "new-lemma", "R", "defer", "deferred", "none", "wind.noun", "0");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        AssertFindings(
            report,
            "'wind' adds an unknown reading",
            "decision 'maybe' of 'bow' is not add or defer",
            "deferred lemma 'fog' must not list readings");
    }

    /// <summary>Proves the provenance rules: a record for every harvested and synthetic row, none repeated, none without a row.</summary>
    [Fact]
    public void SchemaChecker_Run_ProvenanceDefects_ReportsEachFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.RemoveRow("harvest-provenance.tsv", "harvest-00003");
        fixture.RemoveRow("synthetic-provenance.tsv", "syn-00003");
        fixture.AppendRow("harvest-provenance.tsv", "harvest-99999", "masc:x", "masc", "masc:x#1", "upos");
        fixture.AppendRow("synthetic-provenance.tsv", "syn-00001", "s1", "lead", "the _ pipe was");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "dev.tsv:3" && finding.Message.Contains("no entry in harvest-provenance.tsv"));
        AssertFindings(
            report,
            "syn-00003: no entry in synthetic-provenance.tsv",
            "does not match any data row",
            "duplicate provenance entry for 'syn-00001'");
    }

    /// <summary>Proves a synthetic row renamed to look like an independent holdout row neither escapes provenance nor keeps its id.</summary>
    [Fact]
    public void SchemaChecker_Run_SyntheticRowRenamedToHoldoutId_ReportsIdAndMissingProvenance()
    {
        // Arrange: the synthetic train row is renamed hind-x, which used to exempt it from every provenance check
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "syn-00001", ("id", "hind-x"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "train.tsv:3" && finding.Message.Contains("hind-x: id of a 'synthetic' row must start with 'syn'"));
        Assert.Contains(report.Findings, finding => finding.Location == "train.tsv:3" && finding.Message.Contains("hind-x: no entry in synthetic-provenance.tsv"));
        Assert.Contains(report.Findings, finding => finding.Location == "synthetic-provenance.tsv:2" && finding.Message.Contains("'syn-00001' does not match any data row"));
    }

    /// <summary>Proves every row of the independent holdout file must have the independent holdout source and still needs its own provenance.</summary>
    [Fact]
    public void SchemaChecker_Run_HoldoutIndependentRowWithSyntheticSource_ReportsFindings()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("holdout-independent.tsv", "hind-1", ("source", "synthetic"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(
            report.Findings,
            finding => finding.Location == "holdout-independent.tsv:2" && finding.Message.Contains("rows of holdout-independent.tsv must have source 'independent-holdout' but this one has 'synthetic'"));
        Assert.Contains(report.Findings, finding => finding.Location == "holdout-independent.tsv:2" && finding.Message.Contains("hind-1: no entry in synthetic-provenance.tsv"));
    }

    /// <summary>Proves the source, identifier and file of a row must agree: independent holdout outside its file, and the id prefix of each family.</summary>
    [Fact]
    public void SchemaChecker_Run_IdentifierAndSourceMismatches_ReportsEachFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.AppendDataRow("train.tsv", "hind-9", "lead.verb", "Please lead the long way home.", "lead", "independent-holdout", "CC0-1.0");
        fixture.EditRow("train.tsv", "syn-00002", ("id", "gen-00002"));
        fixture.EditRow("train.tsv", "harvest-00001", ("id", "syn2-harvest"));
        fixture.EditRow("holdout-natural.tsv", "harvest-u00001", ("id", "hind-harvest"));
        fixture.EditRow("holdout-independent.tsv", "hind-3", ("id", "other-3"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        AssertFindings(
            report,
            "hind-9: source 'independent-holdout' is only allowed in holdout-independent.tsv",
            "gen-00002: id of a 'synthetic' row must start with 'syn'",
            "syn2-harvest: id of a 'harvest-masc' row must not start with 'syn' or 'hind-'",
            "hind-harvest: id of a 'harvest-masc-llm' row must not start with 'syn' or 'hind-'",
            "other-3: id of a 'independent-holdout' row must start with 'hind-'");
    }

    /// <summary>Positive control: every real identifier family with its matching source and provenance table passes.</summary>
    [Fact]
    public void SchemaChecker_Run_AllIdentifierFamiliesWithMatchingSources_ReportsNoFindings()
    {
        // Arrange: rename rows to the families of the real data; counts are unchanged because only ids and sources change
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "harvest-00001", ("id", "ext-00001"));
        fixture.EditRow("harvest-provenance.tsv", "harvest-00001", ("id", "ext-00001"));
        fixture.EditRow("dev.tsv", "harvest-00003", ("id", "cand-00003"));
        fixture.EditRow("harvest-provenance.tsv", "harvest-00003", ("id", "cand-00003"));
        fixture.EditRow("train.tsv", "syn-00001", ("id", "syn2-00001"), ("source", "synthetic-v2"));
        fixture.EditRow("synthetic-provenance.tsv", "syn-00001", ("id", "syn2-00001"));
        fixture.EditRow("train.tsv", "syn-00002", ("id", "syn3-00002"), ("source", "synthetic-v3"));
        fixture.EditRow("synthetic-provenance.tsv", "syn-00002", ("id", "syn3-00002"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Empty(report.Findings);
    }

    /// <summary>Proves the gap table must equal the counts of the rows: stale, missing, unknown and repeated rows are reported.</summary>
    [Fact]
    public void SchemaChecker_Run_GapTableDefects_ReportsEachFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("gaps-v4.tsv", "lead.verb", ("train_natural", "5"));
        fixture.RemoveRow("gaps-v4.tsv", "wind.noun");
        fixture.AppendRow("gaps-v4.tsv", "ghost.noun", "0", "0", "0", "0", "0", "40", "10");
        fixture.AppendRow("gaps-v4.tsv", "lead.verb", "1", "0", "1", "0", "1", "39", "9");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "gaps-v4.tsv:2" && finding.Message.Contains("is stale"));
        AssertFindings(
            report,
            "reading 'wind.noun' has no row",
            "'ghost.noun' is not a reading of readings.tsv",
            "duplicate entry for 'lead.verb'");
    }
}
