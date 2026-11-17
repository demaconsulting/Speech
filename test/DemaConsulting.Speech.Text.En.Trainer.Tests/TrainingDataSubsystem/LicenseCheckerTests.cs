using DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;
using DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.TrainingDataSubsystem;

/// <summary>
///     Unit tests for <see cref="LicenseChecker"/>: the allow-list, the per-source policy,
///     evaluation-only paths, restricted terms and the attribution and declaration rules.
/// </summary>
public sealed class LicenseCheckerTests
{
    /// <summary>The check name the license checker reports under.</summary>
    private const string License = "license";

    /// <summary>Proves the valid fixture has no license findings and reports the rows by license.</summary>
    [Fact]
    public void LicenseChecker_Run_ValidFixture_ReportsNothingAndCountsLicenses()
    {
        // Arrange
        using var fixture = new DatasetFixture();

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.Empty(report.Findings);
        Assert.Contains("rows by license: CC-BY-3.0-US: 2, CC0-1.0: 6, Public-Domain: 1", report.Info);
    }

    /// <summary>Proves share-alike, non-commercial and no-derivatives licenses on a row are rejected.</summary>
    [Theory]
    [InlineData("CC-BY-SA-4.0")]
    [InlineData("CC-BY-NC-4.0")]
    [InlineData("CC-BY-ND-4.0")]
    public void LicenseChecker_Run_RestrictedLicenseOnRow_ReportsFinding(string license)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "syn-00001", ("license", license));

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "train.tsv:3" && finding.Message.Contains($"license '{license}' is not allowed"));
    }

    /// <summary>Proves a generated row that is not CC0 is rejected even when the license is otherwise allowed.</summary>
    [Fact]
    public void LicenseChecker_Run_GeneratedRowNotCc0_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "syn-00001", ("license", "Public-Domain"));

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.True(DatasetFixture.Has(report, License, "generated row (source 'synthetic') must be CC0-1.0"));
    }

    /// <summary>Proves a MASC row that is not CC-BY-3.0-US is rejected by the per-source policy.</summary>
    [Fact]
    public void LicenseChecker_Run_MascRowNotCcBy_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "harvest-00001", ("license", "CC0-1.0"));

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.True(DatasetFixture.Has(report, License, "license 'CC0-1.0' is not allowed for source 'harvest-masc'"));
    }

    /// <summary>Proves a harvested row without a provenance entry is reported.</summary>
    [Fact]
    public void LicenseChecker_Run_HarvestRowWithoutProvenance_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.RemoveRow("harvest-provenance.tsv", "harvest-00003");

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.True(DatasetFixture.Has(report, License, "harvest-00003: harvested row without an entry in harvest-provenance.tsv"));
    }

    /// <summary>Proves an unknown source is reported only once, by the schema check, and not again as a missing provenance entry.</summary>
    [Fact]
    public void LicenseChecker_Run_UnknownSource_IsReportedOnlyBySchemaCheck()
    {
        // Arrange: a source outside the closed vocabulary that has no harvest provenance entry
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "syn-00001", ("source", "scraped-web"));

        // Act
        var licenseReport = fixture.CheckLicense();
        var schemaReport = fixture.CheckSchema();

        // Assert: the license check stays silent and the unknown source is reported exactly once, by the schema check
        Assert.Empty(licenseReport.Findings);
        var unknown = schemaReport.Findings.Where(finding => finding.Message.Contains("unknown source 'scraped-web'", StringComparison.Ordinal)).ToList();
        var single = Assert.Single(unknown);
        Assert.Equal("schema", single.Check);
    }

    /// <summary>Proves a file whose path names an evaluation-only set is rejected.</summary>
    [Theory]
    [InlineData("audit/heteronym-bench-copy.md")]
    [InlineData("wikipedia-sentences.tsv")]
    [InlineData("corpus/ewt.conllu")]
    [InlineData("misaki-lexicon.md")]
    public void LicenseChecker_Run_EvaluationOnlyPath_ReportsFinding(string path)
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write(path, "a\tb\n");

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == path && finding.Message.Contains("evaluation-only set"));
    }

    /// <summary>Proves the id-only mapping file whd-mapping.tsv is the one allowed path, and local caches are not scanned.</summary>
    [Fact]
    public void LicenseChecker_Run_MappingFileAndLocalCaches_AreNotReportedAsEvaluationPaths()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("work/whd-notes.tsv", "a\tb\n");

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.DoesNotContain(report.Findings, finding => finding.Message.Contains("evaluation-only"));
    }

    /// <summary>Proves the name of a forbidden source in a TSV other than the data files is rejected.</summary>
    [Fact]
    public void LicenseChecker_Run_MisakiTermInProvenanceTsv_ReportsFindingWithLine()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("harvest-provenance.tsv", "harvest-00003", ("label_method", "misaki"));

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "harvest-provenance.tsv:3" && finding.Message.Contains("'misaki'"));
    }

    /// <summary>Proves a restricted term in the source column of a data file is rejected.</summary>
    [Fact]
    public void LicenseChecker_Run_RestrictedTermInSourceColumn_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("dev.tsv", "harvest-00003", ("source", "wikipedia"));

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "dev.tsv:3" && finding.Message.Contains("restricted-license term 'wikipedia'"));
    }

    /// <summary>Proves restricted words inside a sentence are never scanned (a sentence is free text).</summary>
    [Fact]
    public void LicenseChecker_Run_RestrictedTermInSentence_IsNotReported()
    {
        // Arrange: the sentence mentions Wikipedia and share-alike, the offsets stay valid
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "harvest-00001", ("sentence", "She will lead the team today, says Wikipedia, under share-alike."));

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.Empty(report.Findings);
    }

    /// <summary>Proves MASC rows without an attribution file are reported.</summary>
    [Fact]
    public void LicenseChecker_Run_AttributionFileMissing_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        File.Delete(fixture.PathOf("ATTRIBUTION.md"));

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.True(DatasetFixture.Has(report, License, "CC-BY-3.0-US rows exist but ATTRIBUTION.md is missing"));
    }

    /// <summary>Proves an attribution file that does not name MASC is reported.</summary>
    [Fact]
    public void LicenseChecker_Run_AttributionWithoutMasc_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("ATTRIBUTION.md", "# Attribution\n\nLicense: CC-BY-3.0-US\n\nSome other corpus.\n");

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.True(DatasetFixture.Has(report, License, "does not name MASC"));
    }

    /// <summary>Proves no attribution file is needed when no row carries the attribution license.</summary>
    [Fact]
    public void LicenseChecker_Run_NoAttributionRows_DoesNotRequireAttributionFile()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.EditRow("train.tsv", "harvest-00001", ("source", "harvest-compound"), ("license", "Public-Domain"));
        fixture.EditRow("holdout-natural.tsv", "harvest-u00001", ("source", "harvest-compound-llm"), ("license", "Public-Domain"));
        File.Delete(fixture.PathOf("ATTRIBUTION.md"));

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.Empty(report.Findings);
    }

    /// <summary>Proves a README without a <c>License:</c> line is reported.</summary>
    [Fact]
    public void LicenseChecker_Run_ReadmeWithoutLicenseLine_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("prompts/README.md", "# Prompts\n\nNo license here.\n");

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "prompts/README.md" && finding.Message.Contains("License:"));
    }

    /// <summary>Proves a README that declares a license outside the allow-list is reported.</summary>
    [Fact]
    public void LicenseChecker_Run_ReadmeWithDisallowedLicense_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        fixture.Write("README.md", "# Fixture\n\nLicense: CC0-1.0 AND GPL-3.0-only\n");

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "README.md" && finding.Message.Contains("License:"));
    }

    /// <summary>Proves a missing declaring document is reported.</summary>
    [Fact]
    public void LicenseChecker_Run_AuditSummaryMissing_ReportsFinding()
    {
        // Arrange
        using var fixture = new DatasetFixture();
        File.Delete(fixture.PathOf("audit/audit-summary.md"));

        // Act
        var report = fixture.CheckLicense();

        // Assert
        Assert.Contains(report.Findings, finding => finding.Location == "audit/audit-summary.md" && finding.Message.Contains("missing"));
    }
}
