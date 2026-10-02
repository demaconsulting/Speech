using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="UnavailableRecognitionSession"/>.
/// </summary>
public class UnavailableRecognitionSessionTests
{
    /// <summary>
    ///     Proves that <see cref="UnavailableRecognitionSession.Instance"/> reports itself as
    ///     unavailable.
    /// </summary>
    [Fact]
    public void UnavailableRecognitionSession_IsAvailable_Read_ReturnsFalse()
    {
        // Arrange & Act: read the availability flag from the shared instance
        var isAvailable = UnavailableRecognitionSession.Instance.IsAvailable;

        // Assert: the session honestly reports itself as unavailable
        Assert.False(isAvailable);
    }

    /// <summary>
    ///     Proves that calling <see cref="UnavailableRecognitionSession.StartAsync"/> throws
    ///     <see cref="SpeechRecognizerUnavailableException"/>, since this session has no real
    ///     engine or capture device to start.
    /// </summary>
    [Fact]
    public async Task UnavailableRecognitionSession_StartAsync_Always_ThrowsSpeechRecognizerUnavailableException()
    {
        // Arrange: the shared unavailable session
        var session = UnavailableRecognitionSession.Instance;

        // Act & Assert
        await Assert.ThrowsAsync<SpeechRecognizerUnavailableException>(() => session.StartAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that enumerating <see cref="UnavailableRecognitionSession.GetResultsAsync"/>
    ///     throws <see cref="SpeechRecognizerUnavailableException"/>.
    /// </summary>
    [Fact]
    public async Task UnavailableRecognitionSession_GetResultsAsync_Always_ThrowsSpeechRecognizerUnavailableException()
    {
        // Arrange: the shared unavailable session
        var session = UnavailableRecognitionSession.Instance;

        // Act & Assert: enumerating the (lazily-evaluated) sequence surfaces the exception
        await Assert.ThrowsAsync<SpeechRecognizerUnavailableException>(async () =>
        {
            await foreach (var _ in session.GetResultsAsync(TestContext.Current.CancellationToken))
            {
                // No iterations are expected: the exception is thrown before the first result.
            }
        });
    }

    /// <summary>
    ///     Proves that calling <see cref="UnavailableRecognitionSession.StopAsync"/> is a safe
    ///     no-op that never throws, since this session was never running.
    /// </summary>
    [Fact]
    public async Task UnavailableRecognitionSession_StopAsync_Always_IsSafeNoOp()
    {
        // Arrange: the shared unavailable session
        var session = UnavailableRecognitionSession.Instance;

        // Act
        var exception = await Record.ExceptionAsync(() => session.StopAsync(TestContext.Current.CancellationToken));

        // Assert: nothing threw
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that calling <see cref="UnavailableRecognitionSession.DisposeAsync"/> twice,
    ///     along with subscribing to and unsubscribing from <see cref="UnavailableRecognitionSession.StateChanged"/>,
    ///     are all safe no-ops that never invalidate the shared instance.
    /// </summary>
    [Fact]
    public async Task UnavailableRecognitionSession_DisposeAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange: the shared unavailable session and a handler to (un)subscribe
        var session = UnavailableRecognitionSession.Instance;
        EventHandler<SessionStateChangedEventArgs> handler = (_, _) => { };

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            session.StateChanged += handler;
            session.StateChanged -= handler;
            await session.DisposeAsync();
            await session.DisposeAsync();
        });

        // Assert: nothing throws and the shared instance is still usable
        Assert.Null(exception);
        Assert.False(UnavailableRecognitionSession.Instance.IsAvailable);
        Assert.Equal(RecognitionSessionState.Created, UnavailableRecognitionSession.Instance.State);
    }
}
