namespace DemaConsulting.Speech.Diagnostics;

/// <summary>
///     No-op default implementation of <see cref="ISpeechDiagnostics"/> that discards every
///     reported event.
/// </summary>
/// <remarks>
///     Every composition entry point in this library (e.g. factories) accepts an optional
///     <see cref="ISpeechDiagnostics"/> and substitutes <see cref="Instance"/> when the host
///     supplies none, so "nothing throws at composition" holds even when a host has not wired up
///     its own diagnostics sink. The type is stateless and holds no resources, so a single shared
///     instance is safe for concurrent use by any number of callers.
/// </remarks>
public sealed class NullSpeechDiagnostics : ISpeechDiagnostics
{
    /// <summary>
    ///     Prevents external construction; callers use the shared <see cref="Instance"/> instead
    ///     since the type carries no state and multiple instances would provide no value.
    /// </summary>
    private NullSpeechDiagnostics()
    {
    }

    /// <summary>
    ///     Gets the single shared no-op diagnostics sink.
    /// </summary>
    public static ISpeechDiagnostics Instance { get; } = new NullSpeechDiagnostics();

    /// <inheritdoc/>
    /// <remarks>
    ///     Always a true no-op: does not validate arguments, allocate, or throw for any input,
    ///     including null or empty strings, because a discard sink must never be the reason a
    ///     caller's real operation fails.
    /// </remarks>
    public void Report(SpeechDiagnosticLevel level, string category, string message)
    {
        // Intentionally discards every event; the "level", "category", and "message" parameters
        // exist only to satisfy the ISpeechDiagnostics contract and are never inspected.
    }
}
