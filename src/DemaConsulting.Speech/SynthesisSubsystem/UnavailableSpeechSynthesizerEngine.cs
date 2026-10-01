using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Honest fallback <see cref="ISpeechSynthesizerEngine"/> used when no real synthesis engine
///     can be composed, reporting <see cref="IsAvailable"/> as <see langword="false"/> rather than
///     letting a caller build against an engine that cannot function.
/// </summary>
/// <remarks>
///     Per this library's "nothing throws at composition" decision, obtaining and holding this
///     instance never throws: a missing model, an absent native runtime, and a model whose role
///     does not declare synthesis are ordinary machine states at application start-up, not
///     programming errors. <see cref="CreateSessionAsync"/> always succeeds, returning
///     <see cref="UnavailableSynthesisSession.Instance"/>, since binding a device to an already
///     unavailable engine is itself an ordinary (if useless) composition, not an error; only the
///     session's own operational members throw. The type is stateless and holds no resources, so
///     the shared <see cref="Instance"/> is safe for concurrent use by any number of callers and
///     <see cref="DisposeAsync"/> is a no-op that never invalidates it.
/// </remarks>
public sealed class UnavailableSpeechSynthesizerEngine : ISpeechSynthesizerEngine
{
    /// <summary>
    ///     Prevents external construction; callers use the shared <see cref="Instance"/> instead
    ///     since the type carries no state and multiple instances would provide no value.
    /// </summary>
    private UnavailableSpeechSynthesizerEngine()
    {
    }

    /// <summary>Gets the single shared unavailable speech synthesizer engine.</summary>
    public static UnavailableSpeechSynthesizerEngine Instance { get; } = new();

    /// <inheritdoc/>
    public bool IsAvailable => false;

    /// <inheritdoc/>
    /// <remarks>
    ///     Always succeeds, returning <see cref="UnavailableSynthesisSession.Instance"/>; only the
    ///     returned session's operational members throw.
    /// </remarks>
    public Task<ISynthesisSession> CreateSessionAsync(IAudioPlaybackDevice device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult<ISynthesisSession>(UnavailableSynthesisSession.Instance);
    }

    /// <inheritdoc/>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Always thrown; this engine has no real model to speak with.
    /// </exception>
    public async Task SpeakAsync(IAudioPlaybackDevice device, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(text);

        await using var session = await CreateSessionAsync(device, cancellationToken).ConfigureAwait(false);
        await session.SpeakAsync(text, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <exception cref="SpeechSynthesizerUnavailableException">
    ///     Always thrown; this engine has no real model to synthesize with.
    /// </exception>
    public async Task<IReadOnlyList<SynthesizedSpeech>> SynthesizeAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        await using var session = await CreateSessionAsync(UnavailableAudioPlaybackDevice.Instance, cancellationToken)
            .ConfigureAwait(false);
        return await session.SynthesizeAsync(text, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     A no-op: this engine owns no model, thread, or native resource. Disposal must not throw
    ///     or invalidate <see cref="Instance"/>.
    /// </remarks>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
