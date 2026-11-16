using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.Text.En.Tests.Fakes;

/// <summary>
///     Test double for <see cref="ISpeechDiagnostics"/> that records every event and can be told
///     to throw from <see cref="Report"/>, so tests can inspect exactly what an analyzer reported
///     and prove a misbehaving sink never interrupts an analysis.
/// </summary>
internal sealed class RecordingDiagnostics : ISpeechDiagnostics
{
    /// <summary>Guards <see cref="_events"/>.</summary>
    private readonly object _lock = new();

    /// <summary>Every reported event, in arrival order.</summary>
    private readonly List<(SpeechDiagnosticLevel Level, string Category, string Message)> _events = [];

    /// <summary>Whether <see cref="Report"/> throws after recording.</summary>
    private readonly bool _throwOnReport;

    /// <summary>
    ///     Initializes a new instance.
    /// </summary>
    /// <param name="throwOnReport">
    ///     <see langword="true"/> to make every <see cref="Report"/> call throw after recording
    ///     the event, simulating a faulty host sink.
    /// </param>
    public RecordingDiagnostics(bool throwOnReport = false)
    {
        _throwOnReport = throwOnReport;
    }

    /// <summary>Gets a snapshot of every event reported so far.</summary>
    public IReadOnlyList<(SpeechDiagnosticLevel Level, string Category, string Message)> Events
    {
        get
        {
            lock (_lock)
            {
                return [.. _events];
            }
        }
    }

    /// <inheritdoc/>
    public void Report(SpeechDiagnosticLevel level, string category, string message)
    {
        lock (_lock)
        {
            _events.Add((level, category, message));
        }

        if (_throwOnReport)
        {
            throw new InvalidOperationException("The diagnostics sink failed.");
        }
    }
}
