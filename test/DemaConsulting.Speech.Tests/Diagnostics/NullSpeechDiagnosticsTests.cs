using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.Tests.Diagnostics;

/// <summary>
///     Unit tests for the <see cref="NullSpeechDiagnostics"/> class.
/// </summary>
public class NullSpeechDiagnosticsTests
{
    /// <summary>
    ///     Proves that <see cref="NullSpeechDiagnostics.Instance"/> always returns the same
    ///     shared instance, confirming the documented singleton contract.
    /// </summary>
    [Fact]
    public void NullSpeechDiagnostics_Instance_ReadTwice_ReturnsSameInstance()
    {
        // Arrange: read the singleton twice
        var first = NullSpeechDiagnostics.Instance;

        // Act: read it again
        var second = NullSpeechDiagnostics.Instance;

        // Assert: both reads return the identical object
        Assert.Same(first, second);
    }

    /// <summary>
    ///     Proves that a caller holding the default sink can report an event and carry on with its own
    ///     operation undisturbed.
    /// </summary>
    [Fact]
    public void NullSpeechDiagnostics_Instance_ReportEvent_OperationContinues()
    {
        // Arrange: a caller that works through the default sink
        ISpeechDiagnostics diagnostics = NullSpeechDiagnostics.Instance;
        var completedSteps = 0;

        // Act: the caller reports an event between two steps of its own work
        completedSteps++;
        diagnostics.Report(SpeechDiagnosticLevel.Warning, "AudioSubsystem", "Fallback in use.");
        completedSteps++;

        // Assert: both steps completed
        Assert.Equal(2, completedSteps);
    }

    /// <summary>
    ///     Proves that <see cref="NullSpeechDiagnostics.Report"/> is a true no-op for a normal,
    ///     well-formed event and never throws.
    /// </summary>
    [Fact]
    public void NullSpeechDiagnostics_Report_NormalEvent_DoesNotThrow()
    {
        // Arrange: obtain the shared no-op sink
        var diagnostics = NullSpeechDiagnostics.Instance;

        // Act: report a normal, well-formed structural event
        var exception = Record.Exception(() =>
            diagnostics.Report(SpeechDiagnosticLevel.Info, "AudioSubsystem", "Device selected."));

        // Assert: no exception is thrown
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that <see cref="NullSpeechDiagnostics.Report"/> never throws when given a null,
    ///     empty, or non-empty category or message, confirming the sink cannot become the reason a
    ///     caller's real operation fails.
    /// </summary>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("", null)]
    [InlineData("Category", null)]
    [InlineData(null, "Message")]
    [InlineData("Category", "")]
    [InlineData("", "Message")]
    public void NullSpeechDiagnostics_Report_NullOrEmptyArguments_DoesNotThrow(string? category, string? message)
    {
        // Arrange: obtain the shared no-op sink
        var diagnostics = NullSpeechDiagnostics.Instance;

        // Act: report using the null or empty category/message, which the interface signature
        // permits at runtime even though nullable annotations discourage it at compile time
        var exception = Record.Exception(() =>
            diagnostics.Report(SpeechDiagnosticLevel.Error, category!, message!));

        // Assert: no exception is thrown
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that every <see cref="SpeechDiagnosticLevel"/> value is accepted without error,
    ///     confirming the sink discards events regardless of severity.
    /// </summary>
    [Theory]
    [InlineData(SpeechDiagnosticLevel.Info)]
    [InlineData(SpeechDiagnosticLevel.Warning)]
    [InlineData(SpeechDiagnosticLevel.Error)]
    public void NullSpeechDiagnostics_Report_AnyLevel_DoesNotThrow(SpeechDiagnosticLevel level)
    {
        // Arrange: obtain the shared no-op sink
        var diagnostics = NullSpeechDiagnostics.Instance;

        // Act: report an event at the given severity level
        var exception = Record.Exception(() => diagnostics.Report(level, "Category", "Message"));

        // Assert: no exception is thrown
        Assert.Null(exception);
    }
}
