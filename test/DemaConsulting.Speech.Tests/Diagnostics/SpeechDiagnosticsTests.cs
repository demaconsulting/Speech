using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.Tests.Diagnostics;

/// <summary>
///     Unit tests for the <see cref="ISpeechDiagnostics"/> contract, exercised through a
///     recording host-supplied sink.
/// </summary>
public class SpeechDiagnosticsTests
{
    /// <summary>
    ///     Proves that a sink receives the severity of each reported event unchanged.
    /// </summary>
    [Theory]
    [InlineData(SpeechDiagnosticLevel.Info)]
    [InlineData(SpeechDiagnosticLevel.Warning)]
    [InlineData(SpeechDiagnosticLevel.Error)]
    public void SpeechDiagnostics_Report_AnySeverity_SinkReceivesSeverity(SpeechDiagnosticLevel level)
    {
        // Arrange: a recording sink
        var sink = new RecordingDiagnostics();

        // Act: report an event at the given severity
        ((ISpeechDiagnostics)sink).Report(level, "Category", "Message");

        // Assert: the sink observed exactly that severity
        var recorded = Assert.Single(sink.Events);
        Assert.Equal(level, recorded.Level);
    }

    /// <summary>
    ///     Proves that a sink receives the reporting component's category unchanged, so a host
    ///     can filter or group events by it.
    /// </summary>
    [Fact]
    public void SpeechDiagnostics_Report_Category_SinkReceivesCategory()
    {
        // Arrange: a recording sink
        var sink = new RecordingDiagnostics();

        // Act: report an event with a specific category
        ((ISpeechDiagnostics)sink).Report(SpeechDiagnosticLevel.Info, "AudioSubsystem", "Message");

        // Assert: the sink observed exactly that category
        var recorded = Assert.Single(sink.Events);
        Assert.Equal("AudioSubsystem", recorded.Category);
    }

    /// <summary>
    ///     Proves that a sink receives the human-readable message unchanged.
    /// </summary>
    [Fact]
    public void SpeechDiagnostics_Report_Message_SinkReceivesMessage()
    {
        // Arrange: a recording sink
        var sink = new RecordingDiagnostics();

        // Act: report an event with a specific message
        ((ISpeechDiagnostics)sink).Report(SpeechDiagnosticLevel.Info, "Category", "Device selected.");

        // Assert: the sink observed exactly that message
        var recorded = Assert.Single(sink.Events);
        Assert.Equal("Device selected.", recorded.Message);
    }

    /// <summary>A sink that records every reported event.</summary>
    private sealed class RecordingDiagnostics : ISpeechDiagnostics
    {
        /// <summary>Gets the events reported so far, in order.</summary>
        public List<(SpeechDiagnosticLevel Level, string Category, string Message)> Events { get; } = [];

        /// <inheritdoc />
        public void Report(SpeechDiagnosticLevel level, string category, string message) =>
            Events.Add((level, category, message));
    }
}
