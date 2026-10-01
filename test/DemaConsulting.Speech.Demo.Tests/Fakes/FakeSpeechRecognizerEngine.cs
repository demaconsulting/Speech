using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Demo.Tests.Fakes;

/// <summary>
///     Hand-written test double for <see cref="ISpeechRecognizerEngine"/>, used instead of a
///     mocking library so each created session can be a fully test-controlled
///     <see cref="FakeRecognitionSession"/> rather than a canned substitute return value.
/// </summary>
public sealed class FakeSpeechRecognizerEngine : ISpeechRecognizerEngine
{
    /// <summary>Gets the sessions created by every call to <see cref="CreateSessionAsync"/>, in order.</summary>
    public List<FakeRecognitionSession> CreatedSessions { get; } = [];

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
    ///     <see cref="FakeRecognitionSession"/> per call when unset.
    /// </summary>
    public Func<IAudioCaptureDevice, FakeRecognitionSession>? SessionFactory { get; set; }

    /// <inheritdoc/>
    public Task<IRecognitionSession> CreateSessionAsync(
        IAudioCaptureDevice device,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        CreateSessionCallCount++;

        if (CreateSessionException is not null)
        {
            throw CreateSessionException;
        }

        var session = SessionFactory?.Invoke(device) ?? new FakeRecognitionSession();
        CreatedSessions.Add(session);
        return Task.FromResult<IRecognitionSession>(session);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        DisposeCallCount++;
        return ValueTask.CompletedTask;
    }
}
