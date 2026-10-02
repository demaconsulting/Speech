namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Layer 5: a cheap text-to-speech session, bound to exactly one playback device instance for
///     its entire life, obtained from <see cref="ISpeechSynthesizerEngine.CreateSessionAsync"/>.
/// </summary>
/// <remarks>
///     Binding a session to one concrete device instance for its whole life neutralizes the
///     device-remap hazard of <c>AudioSubsystem.AudioDeviceFactory.RefreshDevices()</c>: a session
///     never silently starts talking to a different physical device than the one it was created
///     with.
///     <para>
///     <b>Reuse for low latency.</b> A session is cheap to create once per model/device
///     combination and cheap to run repeatedly: call <see cref="SpeakAsync"/> or
///     <see cref="SynthesizeAsync"/> as many times as needed rather than disposing and recreating
///     the session per call; only dispose and recreate to change model, device, or parameters.
///     </para>
///     <para>
///     <b>Concurrency.</b> <see cref="SpeakAsync"/> and <see cref="SynthesizeAsync"/> do not
///     overlap on the same session: a second call while one is already in flight throws
///     <see cref="InvalidOperationException"/> rather than producing undefined interleaving.
///     <see cref="StopAsync"/> may be called at any time, from any thread, to request cancellation
///     of whichever operation is currently in flight.
///     </para>
/// </remarks>
public interface ISynthesisSession : IAsyncDisposable
{
    /// <summary>
    ///     Gets a value indicating whether this session is backed by a real, loaded synthesis
    ///     engine and has not faulted or been disposed. When <see langword="false"/>,
    ///     <see cref="SpeakAsync"/> and <see cref="SynthesizeAsync"/> throw
    ///     <see cref="SpeechSynthesizerUnavailableException"/> (or, once faulted,
    ///     <see cref="SynthesisSessionFaultedException"/>) rather than silently doing nothing.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>Gets this session's current lifecycle state.</summary>
    SynthesisSessionState State { get; }

    /// <summary>
    ///     Raised every time <see cref="State"/> changes. Handler exceptions are caught and
    ///     reported through diagnostics rather than propagated, mirroring this library's other
    ///     event-exception-isolation conventions.
    /// </summary>
    event EventHandler<SessionStateChangedEventArgs>? StateChanged;

    /// <summary>
    ///     Synthesizes and plays <paramref name="text"/> through this session's bound playback
    ///     device.
    /// </summary>
    /// <param name="text">The text to speak. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel this operation.</param>
    /// <returns>A task that completes once the text has been fully played.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="SpeechSynthesizerUnavailableException">Thrown when <see cref="IsAvailable"/> is <see langword="false"/>.</exception>
    /// <exception cref="SynthesisSessionFaultedException">Thrown when this session has faulted.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when another <see cref="SpeakAsync"/> or <see cref="SynthesizeAsync"/> call is
    ///     already in flight on this session.
    /// </exception>
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Synthesizes <paramref name="text"/> into its full-fidelity ordered segments without
    ///     playing it.
    /// </summary>
    /// <param name="text">The text to synthesize. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel this operation.</param>
    /// <returns>The ordered <see cref="SynthesizedSpeech"/> segments produced for <paramref name="text"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="SpeechSynthesizerUnavailableException">Thrown when <see cref="IsAvailable"/> is <see langword="false"/>.</exception>
    /// <exception cref="SynthesisSessionFaultedException">Thrown when this session has faulted.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when another <see cref="SpeakAsync"/> or <see cref="SynthesizeAsync"/> call is
    ///     already in flight on this session.
    /// </exception>
    Task<IReadOnlyList<SynthesizedSpeech>> SynthesizeAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Requests cancellation of any in-flight <see cref="SpeakAsync"/> or
    ///     <see cref="SynthesizeAsync"/> operation. Idempotent and safe to call with no operation
    ///     in flight, and safe to call concurrently, from any thread, without external
    ///     synchronization.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel waiting for the in-flight operation to stop.</param>
    /// <returns>
    ///     A task that completes once the in-flight operation (if any) has stopped, or immediately
    ///     if none was in flight.
    /// </returns>
    Task StopAsync(CancellationToken cancellationToken = default);
}
