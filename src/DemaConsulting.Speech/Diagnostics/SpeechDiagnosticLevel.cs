namespace DemaConsulting.Speech.Diagnostics;

/// <summary>
///     Identifies the severity of a structural fact reported through <see cref="ISpeechDiagnostics"/>.
/// </summary>
/// <remarks>
///     Levels classify how a host should treat a reported event - purely informational, a
///     recoverable degradation (e.g. falling back to an <c>Unavailable*</c> implementation), or
///     an unexpected failure - without requiring the host to parse free-form message text to
///     decide whether to surface, log, or ignore the event.
/// </remarks>
public enum SpeechDiagnosticLevel
{
    /// <summary>
    ///     A normal structural event with no impact on functionality, such as a device being
    ///     selected or a model's state changing as expected.
    /// </summary>
    Info,

    /// <summary>
    ///     A recoverable degradation, such as composition falling back to an honest
    ///     <c>Unavailable*</c> implementation because no real backend could be resolved.
    /// </summary>
    Warning,

    /// <summary>
    ///     An unexpected failure in a component that had previously claimed to be available.
    /// </summary>
    Error
}
