using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingDataSubsystem;

/// <summary>
///     Unit tests for <see cref="SchemaChecker"/>: every schema rule has a passing case and a negative case.
/// </summary>
public sealed class SchemaCheckerTests
{
    /// <summary>The check name the schema checker reports under.</summary>
    private const string Schema = "schema";

    /// <summary>Proves the valid fixture has no schema findings.</summary>
    [Fact]
    public void SchemaChecker_Run_ValidFixture_ReportsNoFindings()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Empty(report.Findings);
    }

    /// <summary>Proves the run adds the informational summary lines.</summary>
    [Fact]
    public void SchemaChecker_Run_ValidFixture_AddsSummaryInfo()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains("extension decisions 2 (add: 1, defer: 1)", report.Info);
        Assert.Contains("readings 3 lemmas 2 mapped WHD ids 3 forms 3", report.Info);
        Assert.Contains("rows: dev-natural: 1, dev-synthetic: 1, holdout-independent: 3, holdout-natural: 1, train-natural: 1, train-synthetic: 2", report.Info);
        Assert.Contains("readings with a train row: 3 of 3; with a dev row: 2; with a holdout-natural row: 1", report.Info);
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
    [InlineData(2, 70, 0, 8)]
    public void ReadingCount_Needs_AreClampedAtZero(int natural, int synthetic, int needSynthetic, int needNatural)
    {
        // Arrange
        var count = new ReadingCount("x.y", natural, synthetic, 0, 0, 0);

        // Act / Assert
        Assert.Equal(needSynthetic, count.NeedSynthetic);
        Assert.Equal(needNatural, count.NeedNatural);
    }

    /// <summary>Proves the per-reading table is written with a header and one line per reading.</summary>
    [Fact]
    public void ReadingCounts_WriteTable_ValidFixture_WritesHeaderAndOneLinePerReading()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        var counts = ReadingCounts.Build(TextEnDataSet.Load(fixture.Root, new CheckReport()));
        var writer = new StringWriter();

        // Act
        counts.WriteTable(writer);

        // Assert
        var lines = writer.ToString().TrimEnd().Split(Environment.NewLine);
        Assert.Equal(4, lines.Length);
        Assert.Equal("lead.verb\t1\t0\t1\t0\t1\t39\t9", lines[1]);
    }

    /// <summary>Proves a row naming a reading that does not exist is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_UnknownReading_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "syn-00001", ("reading_id", "ghost.noun"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "train.tsv:3" && finding.Message.Contains("unknown reading 'ghost.noun'"));
    }

    /// <summary>Proves an id used in two data files is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_DuplicateIdAcrossFiles_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("dev.tsv", "syn-00003", ("id", "syn-00001"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "syn-00001: duplicate id (also in train.tsv)"));
    }

    /// <summary>Proves a split column that does not match the file is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_WrongSplitColumn_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("holdout-independent.tsv", "hind-1", ("split", "holdout-independent"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "split column 'holdout-independent' (expected 'test')"));
    }

    /// <summary>Proves a lemma that differs from the reading's lemma is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_LemmaMismatch_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "syn-00001", ("lemma", "zinc"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "lemma 'zinc' is not 'lead'"));
    }

    /// <summary>Proves a source outside the closed vocabulary is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_UnknownSource_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "syn-00001", ("source", "scraped-web"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "unknown source 'scraped-web'"));
    }

    /// <summary>Proves offsets that are not integers are reported.</summary>
    [Fact]
    public void SchemaChecker_Run_NonIntegerOffsets_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "syn-00001", ("start", "x"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "start and end must be integers"));
    }

    /// <summary>Proves a span moved by one code unit is reported.</summary>
    [Theory]
    [InlineData("10", "13")]
    [InlineData("8", "13")]
    [InlineData("9", "12")]
    [InlineData("9", "14")]
    public void SchemaChecker_Run_SpanOffByOne_ReportsFinding(string start, string end)
    {
        // Arrange: in "She will lead the team today." the word is at 9..13
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "harvest-00001", ("start", start), ("end", end));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "train.tsv:2" && finding.Message.Contains("span:"));
    }

    /// <summary>Proves a span that is only the beginning of a longer word is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_SpanInsideLongerWord_ReportsFinding()
    {
        // Arrange: "lead" is a listed form but here it is the start of "leadership"
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "harvest-00001", ("sentence", "She will leadership today."));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "followed by a letter or digit"));
    }

    /// <summary>Proves an offset inside a surrogate pair is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_SpanSplitsSurrogatePair_ReportsFinding()
    {
        // Arrange: the emoji occupies code units 0 and 1; start 1 is inside it
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "harvest-00001", ("sentence", "\U0001F600lead the team"), ("start", "1"), ("end", "6"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "split a surrogate pair"));
    }

    /// <summary>Proves an end offset beyond the sentence is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_SpanBeyondSentence_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "harvest-00001", ("end", "999"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "out of range"));
    }

    /// <summary>Proves a reading id that breaks the grammar is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_ReadingIdGrammar_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("readings.tsv", "wind.noun", ("reading_id", "Wind.noun"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "does not match lemma.discriminator"));
    }

    /// <summary>Proves a lemma that is not the id prefix is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_ReadingLemmaNotIdPrefix_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("readings.tsv", "wind.noun", ("lemma", "breeze"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "lemma 'breeze' is not the prefix of 'wind.noun'"));
    }

    /// <summary>Proves a duplicate reading id is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_DuplicateReadingId_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.AppendRow("readings.tsv", "wind.noun", "wind", "NOUN", "A second, different description.", "active");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "duplicate reading id 'wind.noun'"));
    }

    /// <summary>Proves an unknown part-of-speech token or status is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_BadPosTokenAndStatus_ReportsFindings()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("readings.tsv", "wind.noun", ("pos_hint", "NOUN|XYZ"), ("status", "retired"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "pos_hint 'NOUN|XYZ'"));
        Assert.True(DatasetFixture.Has(report, Schema, "status 'retired'"));
    }

    /// <summary>Proves an empty or duplicated description is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_DuplicateDescription_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        var description = "Verb use of \"lead\", to guide, as opposed to the metal.";
        fixture.EditRow("readings.tsv", "wind.noun", ("description", description));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "description of 'wind.noun' is empty or duplicates"));
    }

    /// <summary>Proves a phonetic symbol in the inventory is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_IpaSymbolInDescription_ReportsFinding()
    {
        // Arrange: U+02C8 is the IPA primary stress mark
        using var fixture = new DatasetFixture();
        fixture.EditRow("readings.tsv", "wind.noun", ("description", "Moving air, said \u02C8wind."));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "IPA symbol"));
    }

    /// <summary>Proves a duplicate Whd word id is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_DuplicateWordId_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.AppendRow("whd-mapping.tsv", "lead_vrb", "lead.noun");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "duplicate whd_wordid 'lead_vrb'"));
    }

    /// <summary>Proves a mapping to an unknown reading is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_MappingToUnknownReading_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.AppendRow("whd-mapping.tsv", "ghost_nou", "ghost.noun");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "whd-mapping.tsv:5" && finding.Message.Contains("unknown reading 'ghost.noun'"));
    }

    /// <summary>Proves a reading without a mapping is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_UnmappedReading_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.RemoveRow("whd-mapping.tsv", "lead_nou");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "reading 'lead.noun' has no entry in whd-mapping.tsv"));
    }

    /// <summary>Proves a reading added by an extension as a new reading needs no mapping.</summary>
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

    /// <summary>Proves a forms row for an unknown reading is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_FormOfUnknownReading_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.AppendRow("forms.tsv", "ghost", "ghost.noun", "base");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "unknown reading 'ghost.noun'"));
    }

    /// <summary>Proves a reading without any form is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_ReadingWithoutForm_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.RemoveRow("forms.tsv", "wind");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "reading 'wind.noun' has no form in forms.tsv"));
    }

    /// <summary>Proves a decision other than add or defer is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_InvalidDecision_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("extension-decisions.tsv", "2", ("decision", "maybe"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "decision 'maybe' of 'bow' is not add or defer"));
    }

    /// <summary>Proves an add decision naming an unknown reading is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_AddDecisionWithUnknownReading_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("extension-decisions.tsv", "1", ("readings", "ghost.noun"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "'wind' adds an unknown reading"));
    }

    /// <summary>Proves a deferred lemma that lists readings is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_DeferDecisionWithReadings_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("extension-decisions.tsv", "2", ("readings", "wind.noun"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "deferred lemma 'bow' must not list readings"));
    }

    /// <summary>Proves a harvested row without a provenance entry is reported at the row.</summary>
    [Fact]
    public void SchemaChecker_Run_HarvestRowWithoutProvenance_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.RemoveRow("harvest-provenance.tsv", "harvest-00003");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "dev.tsv:3" && finding.Message.Contains("no entry in harvest-provenance.tsv"));
    }

    /// <summary>Proves a provenance entry that matches no data row is reported at the entry.</summary>
    [Fact]
    public void SchemaChecker_Run_ProvenanceWithoutRow_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.AppendRow("harvest-provenance.tsv", "harvest-99999", "masc:x", "masc", "masc:x#1", "upos");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "harvest-provenance.tsv:5" && finding.Message.Contains("does not match any data row"));
    }

    /// <summary>Proves a duplicated provenance entry is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_DuplicateProvenanceEntry_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.AppendRow("synthetic-provenance.tsv", "syn-00001", "s1", "lead", "the _ pipe was");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "duplicate provenance entry for 'syn-00001'"));
    }

    /// <summary>Proves a synthetic row without a synthetic provenance entry is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_SyntheticRowWithoutProvenance_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.RemoveRow("synthetic-provenance.tsv", "syn-00002");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "syn-00002: no entry in synthetic-provenance.tsv"));
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

    /// <summary>Proves the independent holdout source is rejected outside the independent holdout file.</summary>
    [Fact]
    public void SchemaChecker_Run_IndependentHoldoutSourceInTrain_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.AppendDataRow("train.tsv", "hind-9", "lead.verb", "Please lead the long way home.", "lead", "independent-holdout", "CC0-1.0");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(
            report.Findings,
            finding => finding.Location == "train.tsv:5" && finding.Message.Contains("hind-9: source 'independent-holdout' is only allowed in holdout-independent.tsv"));
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

    /// <summary>Proves a harvested row may not use an identifier of a generated row.</summary>
    [Theory]
    [InlineData("syn-harvest")]
    [InlineData("syn2-harvest")]
    [InlineData("hind-harvest")]
    public void SchemaChecker_Run_HarvestRowWithGeneratedId_ReportsFinding(string id)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "harvest-00001", ("id", id));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(
            report.Findings,
            finding => finding.Location == "train.tsv:2" && finding.Message.Contains($"{id}: id of a 'harvest-masc' row must not start with 'syn' or 'hind-'"));
    }

    /// <summary>Proves a synthetic row must have an identifier that starts with <c>syn</c>.</summary>
    [Fact]
    public void SchemaChecker_Run_SyntheticRowIdWithoutSynPrefix_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "syn-00002", ("id", "gen-00002"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "train.tsv:4" && finding.Message.Contains("gen-00002: id of a 'synthetic' row must start with 'syn'"));
    }

    /// <summary>Proves an independent holdout row must have an identifier that starts with <c>hind-</c>.</summary>
    [Fact]
    public void SchemaChecker_Run_IndependentHoldoutRowIdWithoutHindPrefix_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("holdout-independent.tsv", "hind-1", ("id", "other-1"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(
            report.Findings,
            finding => finding.Location == "holdout-independent.tsv:2" && finding.Message.Contains("other-1: id of a 'independent-holdout' row must start with 'hind-'"));
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

    /// <summary>Proves a stale gaps row is reported with the expected and found values.</summary>
    [Fact]
    public void SchemaChecker_Run_StaleGapsRow_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("gaps-v4.tsv", "lead.verb", ("train_natural", "5"));

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "gaps-v4.tsv:2" && finding.Message.Contains("is stale"));
    }

    /// <summary>Proves a reading missing from the gaps file is reported.</summary>
    [Fact]
    public void SchemaChecker_Run_GapsRowMissing_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.RemoveRow("gaps-v4.tsv", "wind.noun");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "reading 'wind.noun' has no row"));
    }

    /// <summary>Proves a gaps row for an unknown reading and a duplicate gaps row are reported.</summary>
    [Fact]
    public void SchemaChecker_Run_GapsRowUnknownOrDuplicate_ReportsFindings()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.AppendRow("gaps-v4.tsv", "ghost.noun", "0", "0", "0", "0", "0", "40", "10");
        fixture.AppendRow("gaps-v4.tsv", "lead.verb", "1", "0", "1", "0", "1", "39", "9");

        // Act
        var report = fixture.CheckSchema();

        // Assert
        Assert.True(DatasetFixture.Has(report, Schema, "'ghost.noun' is not a reading of readings.tsv"));
        Assert.True(DatasetFixture.Has(report, Schema, "duplicate entry for 'lead.verb'"));
    }
}
