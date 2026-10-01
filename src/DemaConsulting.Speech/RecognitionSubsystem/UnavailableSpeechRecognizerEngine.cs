using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.RecognitionSubsystem;

/// <summary>
///     Honest fallback <see cref="ISpeechRecognizerEngine"/> used when no real recognition
///     backend can be composed, reporting <see cref="IsAvailable"/> as <see langword="false"/>
///     rather than letting a caller build against an engine that cannot function.
/// </summary>
/// <remarks>
///     Per this library's "nothing throws at composition" decision, obtaining and holding this
///     instance never throws, and <see cref="CreateSessionAsync"/> always succeeds, returning
///     <see cref="UnavailableRecognitionSession.Instance"/> rather than throwing - a missing
///     model, an absent native runtime, and a machine with no microphone are ordinary machine
///     states at application start-up, not programming errors. The type is stateless and holds
///     no resources, so the shared <see cref="Instance"/> is safe for concurrent use by any
///     number of callers and <see cref="DisposeAsync"/> is a no-op that never invalidates it.
///     This mirrors <see cref="UnavailableAudioCaptureDevice"/> exactly.
/// </remarks>
public sealed class UnavailableSpeechRecognizerEngine : ISpeechRecognizerEngine
{
    /// <summary>
    ///     Prevents external construction; callers use the shared <see cref="Instance"/> instead
    ///     since the type carries no state and multiple instances would provide no value.
    /// </summary>
    private UnavailableSpeechRecognizerEngine()
    {
    }

    /// <summary>Gets the single shared unavailable speech recognizer engine.</summary>
    public static UnavailableSpeechRecognizerEngine Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    /// <remarks>
    ///     Always succeeds, returning <see cref="UnavailableRecognitionSession.Instance"/>,
    ///     rather than throwing or faulting - this engine has nothing to bind a device to, but
    ///     that is an ordinary unavailable state, not an error.
    /// </remarks>
    public Task<IRecognitionSession> CreateSessionAsync(
        IAudioCaptureDevice device,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<IRecognitionSession>(UnavailableRecognitionSession.Instance);
    }

    /// <summary>Releases resources held by this engine.</summary>
    /// <remarks>
    ///     A no-op: this engine owns no backend, thread, or native resource. Disposal must not
    ///     throw or invalidate <see cref="Instance"/>, because a host that wraps its engine in an
    ///     <c>await using</c> block gets the shared instance here and may dispose it many times.
    /// </remarks>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
