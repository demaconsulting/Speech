using System.Text.RegularExpressions;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.AnalysisSubsystem;

/// <summary>
///     Unit tests for <see cref="AnalysisDiagnostics"/>: the aggregate-counts-only reporter.
/// </summary>
public sealed class AnalysisDiagnosticsTests
{
    /// <summary>Proves one request produces exactly one Info event in the documented category carrying the counts.</summary>
    [Fact]
    public void AnalysisDiagnostics_ReportRequest_Sink_ReceivesOneInfoEventWithCounts()
    {
        // Arrange
        var sink = new RecordingDiagnostics();
        var diagnostics = new AnalysisDiagnostics(sink);

        // Act
        diagnostics.ReportRequest(new AnalysisCounts(3, 17, 12, 2, 4, 1, 5, 6, 7));

        // Assert
        var reported = Assert.Single(sink.Events);
        Assert.Equal(SpeechDiagnosticLevel.Info, reported.Level);
        Assert.Equal("TextEn.Analysis", reported.Category);
        Assert.Equal("Analyzed one text: 3 sentences, 17 tokens, 12 annotations, 2 unknown words, 4 normalizations, 1 ambiguous readings, 5 numeric fallbacks, 6 overrides applied, 7 overrides ignored.", reported.Message);
    }

    /// <summary>Proves the message is built from fixed words and numbers only.</summary>
    [Fact]
    public void AnalysisDiagnostics_FormatMessage_AnyCounts_ContainsOnlyFixedWordsAndNumbers()
    {
        // Act
        var message = AnalysisDiagnostics.FormatMessage(new AnalysisCounts(0, 1234567, 8, 9, 10, 11, 12, 13, 14));

        // Assert: strip the fixed template and nothing but the nine numbers remains
        Assert.Matches(
            new Regex(@"^Analyzed one text: \d+ sentences, \d+ tokens, \d+ annotations, \d+ unknown words, \d+ normalizations, \d+ ambiguous readings, \d+ numeric fallbacks, \d+ overrides applied, \d+ overrides ignored\.$", RegexOptions.CultureInvariant),
            message);
    }

    /// <summary>Proves the two override counts are part of the message, and the counts that existed before keep their wording, so a count can never be mistaken for another.</summary>
    [Fact]
    public void AnalysisDiagnostics_FormatMessage_OverrideCounts_AreNamedAndOrdered()
    {
        // Act
        var message = AnalysisDiagnostics.FormatMessage(new AnalysisCounts(1, 2, 3, 4, 5, 6, 7, 8, 9));

        // Assert
        Assert.EndsWith("7 numeric fallbacks, 8 overrides applied, 9 overrides ignored.", message, StringComparison.Ordinal);
        Assert.Equal(0, new AnalysisCounts(1, 2, 3, 4, 5, 6, 7).OverridesApplied);
        Assert.Equal(0, new AnalysisCounts(1, 2, 3, 4, 5, 6, 7).OverridesIgnored);
    }

    /// <summary>Proves a missing sink is allowed and silently reports nothing.</summary>
    [Fact]
    public void AnalysisDiagnostics_ReportRequest_NullSink_DoesNothing()
    {
        // Arrange
        var diagnostics = new AnalysisDiagnostics(null);

        // Act / Assert
        var exception = Record.Exception(() => diagnostics.ReportRequest(new AnalysisCounts(1, 2, 3, 4, 5, 6, 7)));
        Assert.Null(exception);
    }

    /// <summary>Proves a sink that throws is contained.</summary>
    [Fact]
    public void AnalysisDiagnostics_ReportRequest_ThrowingSink_IsContained()
    {
        // Arrange
        var sink = new RecordingDiagnostics(throwOnReport: true);
        var diagnostics = new AnalysisDiagnostics(sink);

        // Act
        var exception = Record.Exception(() => diagnostics.ReportRequest(new AnalysisCounts(1, 2, 3, 4, 5, 6, 7)));

        // Assert: the call completed normally even though the sink threw, and the sink was reached
        Assert.Null(exception);
        Assert.Single(sink.Events);
    }

    /// <summary>Proves the heteronym event is one Info event in its own category, built from the counts and the threshold, and the existing request event is untouched.</summary>
    [Fact]
    public void AnalysisDiagnostics_ReportReadings_Counts_ReceivesOneInfoEventInHeteronymCategory()
    {
        // Arrange
        var sink = new RecordingDiagnostics();
        var diagnostics = new AnalysisDiagnostics(sink);

        // Act
        diagnostics.ReportReadings(new ReadingCounts(3, 2, 1, 1), 0.6);

        // Assert
        var reported = Assert.Single(sink.Events);
        Assert.Equal(SpeechDiagnosticLevel.Info, reported.Level);
        Assert.Equal("TextEn.Heteronym", reported.Category);
        Assert.Equal("Assigned 6 heteronym readings: 3 classified, 2 single-candidate, 1 by override, 1 below confidence 0.60.", reported.Message);
    }

    /// <summary>Proves the heteronym message uses invariant digits and a period under a culture with other digits and separators, and contains only fixed words and numbers.</summary>
    [Fact]
    public void AnalysisDiagnostics_FormatReadingMessage_AnyCulture_ContainsOnlyFixedWordsAndInvariantNumbers()
    {
        // Arrange
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");

            // Act
            var message = AnalysisDiagnostics.FormatReadingMessage(new ReadingCounts(1234, 5, 6, 7), 0.5);

            // Assert
            Assert.Equal("Assigned 1245 heteronym readings: 1234 classified, 5 single-candidate, 6 by override, 7 below confidence 0.50.", message);
            Assert.Matches(new Regex(@"^Assigned \d+ heteronym readings: \d+ classified, \d+ single-candidate, \d+ by override, \d+ below confidence \d\.\d\d\.$", RegexOptions.CultureInvariant), message);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Proves no heteronym event is reported when no reading was assigned, even with a sink.</summary>
    [Fact]
    public void AnalysisDiagnostics_ReportReadings_NothingAssigned_ReportsNothing()
    {
        // Arrange
        var sink = new RecordingDiagnostics();
        var diagnostics = new AnalysisDiagnostics(sink);

        // Act
        diagnostics.ReportReadings(new ReadingCounts(0, 0, 0, 0), 0.6);

        // Assert
        Assert.Empty(sink.Events);
        Assert.Equal(0, new ReadingCounts(0, 0, 0, 0).Assigned);
    }

    /// <summary>Proves the model warning is one fixed Warning message in its own category that names neither a file nor any text.</summary>
    [Fact]
    public void AnalysisDiagnostics_ReportModelProblem_Sink_ReceivesOneFixedWarning()
    {
        // Arrange
        var sink = new RecordingDiagnostics();
        var diagnostics = new AnalysisDiagnostics(sink);

        // Act
        diagnostics.ReportModelProblem();

        // Assert
        var reported = Assert.Single(sink.Events);
        Assert.Equal(SpeechDiagnosticLevel.Warning, reported.Level);
        Assert.Equal("TextEn.Model", reported.Category);
        Assert.Equal("Heteronym model unusable (invalid or incompatible file); readings disabled.", reported.Message);
    }

    /// <summary>Proves the heteronym and model reports tolerate a missing sink and a sink that throws.</summary>
    [Fact]
    public void AnalysisDiagnostics_ReportReadingsAndModelProblem_NullOrThrowingSink_AreContained()
    {
        // Arrange
        var throwing = new RecordingDiagnostics(throwOnReport: true);

        // Act
        var none = Record.Exception(() =>
        {
            var diagnostics = new AnalysisDiagnostics(null);
            diagnostics.ReportReadings(new ReadingCounts(1, 0, 0, 0), 0.6);
            diagnostics.ReportModelProblem();
        });
        var thrown = Record.Exception(() =>
        {
            var diagnostics = new AnalysisDiagnostics(throwing);
            diagnostics.ReportReadings(new ReadingCounts(1, 0, 0, 0), 0.6);
            diagnostics.ReportModelProblem();
        });

        // Assert
        Assert.Null(none);
        Assert.Null(thrown);
        Assert.Equal(2, throwing.Events.Count);
    }
}
