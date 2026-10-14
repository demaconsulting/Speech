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
    ///     Proves that invoking <see cref="UnavailableRecognitionSession.GetResultsAsync"/>
    ///     throws <see cref="SpeechRecognizerUnavailableException"/> immediately, before any
    ///     enumeration begins.
    /// </summary>
    [Fact]
    public void UnavailableRecognitionSession_GetResultsAsync_Always_ThrowsSpeechRecognizerUnavailableException()
    {
        // Arrange: the shared unavailable session
        var session = UnavailableRecognitionSession.Instance;

        // Act & Assert: the call itself throws, without enumerating the returned sequence
        Assert.Throws<SpeechRecognizerUnavailableException>(
            () => session.GetResultsAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that calling <see cref="UnavailableRecognitionSession.StopAsync"/> repeatedly is
    ///     a safe no-op that never throws, since this session was never running.
    /// </summary>
    [Fact]
    public async Task UnavailableRecognitionSession_StopAsync_Always_IsSafeNoOp()
    {
        // Arrange: the shared unavailable session
        var session = UnavailableRecognitionSession.Instance;
        var exceptions = new List<Exception?>();

        // Act: stop three times in a row
        for (var i = 0; i < 3; i++)
        {
            exceptions.Add(await Record.ExceptionAsync(
                () => session.StopAsync(TestContext.Current.CancellationToken)));
        }

        // Assert: no call threw and the session is still in its idle state
        Assert.All(exceptions, Assert.Null);
        Assert.Equal(RecognitionSessionState.Created, session.State);
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
        Assert.False(session.IsAvailable);
        Assert.Equal(RecognitionSessionState.Created, session.State);
    }
}
