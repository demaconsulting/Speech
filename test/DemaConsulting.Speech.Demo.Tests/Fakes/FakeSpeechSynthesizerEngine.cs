using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.Fakes;

/// <summary>
///     Hand-written test double for <see cref="ISpeechSynthesizerEngine"/>, used instead of a
///     mocking library so each created session can be a fully test-controlled
///     <see cref="FakeSynthesisSession"/> rather than a canned substitute return value.
/// </summary>
public sealed class FakeSpeechSynthesizerEngine : ISpeechSynthesizerEngine
{
    /// <summary>Gets the sessions created by every call to <see cref="CreateSessionAsync"/>, in order.</summary>
    public List<FakeSynthesisSession> CreatedSessions { get; } = [];

    /// <inheritdoc/>
    public bool IsAvailable { get; set; } = true;

    /// <summary>Gets the number of times <see cref="CreateSessionAsync"/> was called.</summary>
    public int CreateSessionCallCount { get; private set; }

    /// <summary>Gets the number of times <see cref="DisposeAsync"/> was called.</summary>
    public int DisposeCallCount { get; private set; }

    /// <summary>Gets or sets the exception <see cref="CreateSessionAsync"/> throws, if any.</summary>
    public Exception? CreateSessionException { get; set; }

    /// <summary>
    ///     Gets or sets a factory producing the session returned by each
    ///     <see cref="CreateSessionAsync"/> call; defaults to a fresh
    ///     <see cref="FakeSynthesisSession"/> per call when unset.
    /// </summary>
    public Func<IAudioPlaybackDevice, FakeSynthesisSession>? SessionFactory { get; set; }

    /// <inheritdoc/>
    public Task<ISynthesisSession> CreateSessionAsync(
        IAudioPlaybackDevice device,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        CreateSessionCallCount++;

        if (CreateSessionException is not null)
        {
            throw CreateSessionException;
        }

        var session = SessionFactory?.Invoke(device) ?? new FakeSynthesisSession();
        CreatedSessions.Add(session);
        return Task.FromResult<ISynthesisSession>(session);
    }

    /// <inheritdoc/>
    public Task SpeakAsync(IAudioPlaybackDevice device, string text, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by SynthesisPanelViewModel, which only calls CreateSessionAsync.");

    /// <inheritdoc/>
    public Task<IReadOnlyList<SynthesizedSpeech>> SynthesizeAsync(string text, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not used by SynthesisPanelViewModel, which only calls CreateSessionAsync.");

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        return ValueTask.CompletedTask;
    }
}
