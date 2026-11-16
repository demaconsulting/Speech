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
        diagnostics.ReportRequest(new AnalysisCounts(3, 17, 12, 2));

        // Assert
        var reported = Assert.Single(sink.Events);
        Assert.Equal(SpeechDiagnosticLevel.Info, reported.Level);
        Assert.Equal("TextEn.Analysis", reported.Category);
        Assert.Equal("Analyzed one text: 3 sentences, 17 tokens, 12 annotations, 2 unknown words.", reported.Message);
    }

    /// <summary>Proves the message is built from fixed words and numbers only.</summary>
    [Fact]
    public void AnalysisDiagnostics_FormatMessage_AnyCounts_ContainsOnlyFixedWordsAndNumbers()
    {
        // Act
        var message = AnalysisDiagnostics.FormatMessage(new AnalysisCounts(0, 1234567, 8, 9));

        // Assert: strip the fixed template and nothing but the four numbers remains
        Assert.Matches(
            new Regex(@"^Analyzed one text: \d+ sentences, \d+ tokens, \d+ annotations, \d+ unknown words\.$", RegexOptions.CultureInvariant),
            message);
    }

    /// <summary>Proves a missing sink is allowed and silently reports nothing.</summary>
    [Fact]
    public void AnalysisDiagnostics_ReportRequest_NullSink_DoesNothing()
    {
        // Arrange
        var diagnostics = new AnalysisDiagnostics(null);

        // Act / Assert
        var exception = Record.Exception(() => diagnostics.ReportRequest(new AnalysisCounts(1, 2, 3, 4)));
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
        var exception = Record.Exception(() => diagnostics.ReportRequest(new AnalysisCounts(1, 2, 3, 4)));

        // Assert: the call completed normally even though the sink threw, and the sink was reached
        Assert.Null(exception);
        Assert.Single(sink.Events);
    }
}
