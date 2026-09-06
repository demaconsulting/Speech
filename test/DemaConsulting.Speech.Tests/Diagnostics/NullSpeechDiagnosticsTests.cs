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
    ///     Proves that <see cref="NullSpeechDiagnostics.Report"/> never throws even when given
    ///     null-ish/edge inputs, confirming the sink cannot become the reason a caller's real
    ///     operation fails.
    /// </summary>
    [Fact]
    public void NullSpeechDiagnostics_Report_NullOrEmptyArguments_DoesNotThrow()
    {
        // Arrange: obtain the shared no-op sink
        var diagnostics = NullSpeechDiagnostics.Instance;

        // Act: report using null category/message, which the interface signature permits at
        // runtime even though nullable annotations discourage it at compile time
        var exception = Record.Exception(() =>
            diagnostics.Report(SpeechDiagnosticLevel.Error, null!, null!));

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
