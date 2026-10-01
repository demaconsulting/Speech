using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Layer 3: the loaded, expensive, native-backed text-to-speech model. No playback device is
///     bound yet - obtain an <see cref="ISynthesisSession"/> via <see cref="CreateSessionAsync"/>
///     to bind one and perform repeated, low-latency synthesis against it.
/// </summary>
/// <remarks>
///     Hosts obtain implementations from <see cref="SpeechSynthesizerFactory"/> rather than
///     constructing them directly. Per this library's "nothing throws at composition" decision,
///     <see cref="SpeechSynthesizerFactory.LoadAsync(ModelManagementSubsystem.ISynthesisModel,string,Diagnostics.ISpeechDiagnostics,System.Collections.Generic.IReadOnlyDictionary{string,object}?,System.Threading.CancellationToken)"/>
///     never faults for an ordinary machine state (model not installed, role mismatch, native
///     runtime absent) - it returns <see cref="UnavailableSpeechSynthesizerEngine.Instance"/>
///     instead, which honestly reports <see cref="IsAvailable"/> as <see langword="false"/>.
///     <para>
///     Obtaining this engine is the expensive step: it loads the model into native memory. A
///     session obtained from it is cheap and may be created and run repeatedly without reloading
///     the model - construct one engine per model/parameter combination and reuse it across many
///     sessions rather than disposing and recreating it per turn.
///     </para>
///     <para>
///     <b>Exclusivity.</b> At most one <see cref="ISynthesisSession"/> may be leased from this
///     engine at a time, with the lease held for the session's entire life through
///     <see cref="IAsyncDisposable.DisposeAsync"/> completion. A concurrent
///     <see cref="CreateSessionAsync"/> call while a lease is held fails fast with
///     <see cref="SynthesisEngineBusyException"/>.
///     </para>
/// </remarks>
public interface ISpeechSynthesizerEngine : IAsyncDisposable
{
    /// <summary>
    ///     Gets a value indicating whether this engine is backed by a real, loaded synthesis
    ///     model. When <see langword="false"/>, every operational member throws
    ///     <see cref="SpeechSynthesizerUnavailableException"/> rather than silently doing nothing.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    ///     Binds this engine to exactly one playback device for the entire life of the returned
    ///     session.
    /// </summary>
    /// <param name="device">
    ///     The playback device to bind the session to. Must not be <see langword="null"/>. A
    ///     device reporting <c>IsAvailable == false</c> is an ordinary machine state, not an
    ///     error; the returned session degrades honestly when an operation that needs playback is
    ///     actually attempted.
    /// </param>
    /// <param name="cancellationToken">A token to cancel this call.</param>
    /// <returns>A session bound to <paramref name="device"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="device"/> is <see langword="null"/>.</exception>
    /// <exception cref="SynthesisEngineBusyException">
    ///     Thrown when this engine's exclusivity lease is already held by another session.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled.</exception>
    Task<ISynthesisSession> CreateSessionAsync(
        IAudioPlaybackDevice device,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     One-shot convenience: creates a session bound to <paramref name="device"/>, speaks
    ///     <paramref name="text"/> through it, and disposes the session, for callers with no need
    ///     to reuse a session across multiple calls.
    /// </summary>
    /// <param name="device">The playback device to speak through. Must not be <see langword="null"/>.</param>
    /// <param name="text">The text to speak. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that completes once the text has been fully played.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="device"/> or <paramref name="text"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>.
    /// </exception>
    /// <exception cref="SynthesisEngineBusyException">
    ///     Thrown when this engine's exclusivity lease is already held by another session.
    /// </exception>
    Task SpeakAsync(
        IAudioPlaybackDevice device,
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     One-shot convenience: creates a session, synthesizes <paramref name="text"/> into its
    ///     full-fidelity ordered segments without playing it, and disposes the session.
    /// </summary>
    /// <param name="text">The text to synthesize. Must not be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    ///     The ordered <see cref="SynthesizedSpeech"/> segments produced for <paramref name="text"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Thrown when <see cref="IsAvailable"/> is <see langword="false"/>.
    /// </exception>
    /// <exception cref="SynthesisEngineBusyException">
    ///     Thrown when this engine's exclusivity lease is already held by another session.
    /// </exception>
    /// <remarks>
    ///     This overload never plays audio, so it never binds a real playback device internally;
    ///     no speakers are required to call it.
    /// </remarks>
    Task<IReadOnlyList<SynthesizedSpeech>> SynthesizeAsync(
        string text,
        CancellationToken cancellationToken = default);
}
