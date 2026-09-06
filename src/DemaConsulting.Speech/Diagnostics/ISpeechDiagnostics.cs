namespace DemaConsulting.Speech.Diagnostics;

/// <summary>
///     Host-supplied sink for structural diagnostic facts emitted by the library.
/// </summary>
/// <remarks>
///     Implementations MUST treat every reported event as structural metadata only - facts such
///     as "capture device selected", "model state changed", or "recognition started" - and MUST
///     NEVER receive raw user audio samples or recognized/synthesized text content. This
///     boundary is what makes it safe for a host to wire an <see cref="ISpeechDiagnostics"/>
///     sink to a visible developer log without risking exposure of user speech or dictated text.
///     A host that supplies no sink still gets a fully working, safe default; see
///     <see cref="NullSpeechDiagnostics"/>.
/// </remarks>
public interface ISpeechDiagnostics
{
    /// <summary>
    ///     Reports a single structural diagnostic event.
    /// </summary>
    /// <param name="level">The severity of the event.</param>
    /// <param name="category">
    ///     A short, stable identifier for the reporting component (e.g. <c>"AudioSubsystem"</c>),
    ///     letting a host filter or group events without parsing <paramref name="message"/>.
    /// </param>
    /// <param name="message">
    ///     A human-readable description of the structural event. Must never contain user audio
    ///     samples or recognized/synthesized text content - only facts about library state.
    /// </param>
    /// <remarks>
    ///     Implementations must not throw for any input, including a null or empty
    ///     <paramref name="category"/> or <paramref name="message"/> - a diagnostics sink failing
    ///     must never interrupt the primary audio/model/recognition/synthesis operation it is
    ///     merely observing.
    /// </remarks>
    void Report(SpeechDiagnosticLevel level, string category, string message);
}
