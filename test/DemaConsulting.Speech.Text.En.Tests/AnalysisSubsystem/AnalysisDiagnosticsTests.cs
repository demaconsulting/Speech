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
    /// <summary>Proves one request produces exactly one Info event in the documented category carrying each labeled count, in the documented order (the override counts last, each under its own label, and zero when not supplied).</summary>
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
        Assert.Equal(
            [("sentences", 3), ("tokens", 17), ("annotations", 12), ("unknown words", 2), ("normalizations", 4), ("ambiguous readings", 1), ("numeric fallbacks", 5), ("overrides applied", 6), ("overrides ignored", 7)],
            LabeledCounts(reported.Message));
        Assert.Equal(0, new AnalysisCounts(1, 2, 3, 4, 5, 6, 7).OverridesApplied);
        Assert.Equal(0, new AnalysisCounts(1, 2, 3, 4, 5, 6, 7).OverridesIgnored);
    }

    /// <summary>Proves the message is built from fixed words and numbers only: it states nine labeled counts and nothing else with a digit.</summary>
    [Fact]
    public void AnalysisDiagnostics_FormatMessage_AnyCounts_ContainsOnlyFixedWordsAndNumbers()
    {
        // Act
        var message = AnalysisDiagnostics.FormatMessage(new AnalysisCounts(0, 1234567, 8, 9, 10, 11, 12, 13, 14));

        // Assert: the nine numbers are the labeled counts and every digit of the message belongs to one of them
        Assert.Equal([0, 1234567, 8, 9, 10, 11, 12, 13, 14], LabeledCounts(message).Select(count => count.Value));
        Assert.Equal(
            string.Concat(new[] { 0, 1234567, 8, 9, 10, 11, 12, 13, 14 }.Select(value => value.ToString(System.Globalization.CultureInfo.InvariantCulture))),
            string.Concat(message.Where(char.IsAsciiDigit)));
    }

    /// <summary>Reads the labeled counts ("<c>17 tokens</c>", "<c>2 unknown words</c>") of a message, in the order they are stated.</summary>
    /// <param name="message">The message.</param>
    /// <returns>The label and value of every count.</returns>
    private static List<(string Label, int Value)> LabeledCounts(string message) =>
        [.. Regex.Matches(message, @"(?<value>\d+) (?<label>[A-Za-z]+(?: [A-Za-z]+)?)(?=[,.])", RegexOptions.CultureInvariant)
            .Select(match => (match.Groups["label"].Value, int.Parse(match.Groups["value"].Value, System.Globalization.CultureInfo.InvariantCulture)))];

    /// <summary>Proves a missing sink is allowed and silently reports nothing, and a sink that throws is contained.</summary>
    [Fact]
    public void AnalysisDiagnostics_ReportRequest_ThrowingSink_IsContained()
    {
        // Arrange
        var sink = new RecordingDiagnostics(throwOnReport: true);
        var diagnostics = new AnalysisDiagnostics(sink);

        // Act
        var thrown = Record.Exception(() => diagnostics.ReportRequest(new AnalysisCounts(1, 2, 3, 4, 5, 6, 7)));
        var none = Record.Exception(() => new AnalysisDiagnostics(null).ReportRequest(new AnalysisCounts(1, 2, 3, 4, 5, 6, 7)));

        // Assert: both calls completed normally, and the throwing sink was reached
        Assert.Null(thrown);
        Assert.Null(none);
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
