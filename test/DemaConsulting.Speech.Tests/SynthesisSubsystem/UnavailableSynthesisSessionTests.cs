using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="UnavailableSynthesisSession"/>.
/// </summary>
public class UnavailableSynthesisSessionTests
{
    /// <summary>
    ///     Proves that <see cref="UnavailableSynthesisSession.Instance"/> reports itself as
    ///     unavailable.
    /// </summary>
    [Fact]
    public void UnavailableSynthesisSession_IsAvailable_Read_ReturnsFalse()
    {
        // Act
        var isAvailable = UnavailableSynthesisSession.Instance.IsAvailable;

        // Assert
        Assert.False(isAvailable);
    }

    /// <summary>
    ///     Proves that <see cref="UnavailableSynthesisSession.Instance"/> always reports
    ///     <see cref="SynthesisSessionState.Created"/>, since it never transitions.
    /// </summary>
    [Fact]
    public void UnavailableSynthesisSession_State_Read_ReturnsCreated()
    {
        // Act
        var state = UnavailableSynthesisSession.Instance.State;

        // Assert
        Assert.Equal(SynthesisSessionState.Created, state);
    }

    /// <summary>
    ///     Proves that subscribing to and unsubscribing from <see cref="ISynthesisSession.StateChanged"/>
    ///     is a safe no-op, since this session never raises it.
    /// </summary>
    [Fact]
    public void UnavailableSynthesisSession_StateChanged_SubscribeAndUnsubscribe_DoesNotThrow()
    {
        // Arrange
        var session = UnavailableSynthesisSession.Instance;
        EventHandler<SessionStateChangedEventArgs> handler = (_, _) => { };

        // Act
        var exception = Record.Exception(() =>
        {
            session.StateChanged += handler;
            session.StateChanged -= handler;
        });

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that calling <see cref="ISynthesisSession.SpeakAsync"/> throws
    ///     <see cref="SpeechSynthesizerUnavailableException"/>.
    /// </summary>
    [Fact]
    public async Task UnavailableSynthesisSession_SpeakAsync_Always_ThrowsSpeechSynthesizerUnavailableException()
    {
        // Arrange
        var session = UnavailableSynthesisSession.Instance;

        // Act & Assert
        await Assert.ThrowsAsync<SpeechSynthesizerUnavailableException>(
            () => session.SpeakAsync("hello", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that calling <see cref="ISynthesisSession.SpeakAsync"/> with a null text still
    ///     validates the argument, throwing <see cref="ArgumentNullException"/> rather than the
    ///     unavailable fault.
    /// </summary>
    [Fact]
    public async Task UnavailableSynthesisSession_SpeakAsync_NullText_ThrowsArgumentNullException()
    {
        // Arrange
        var session = UnavailableSynthesisSession.Instance;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => session.SpeakAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that calling <see cref="ISynthesisSession.SynthesizeAsync"/> throws
    ///     <see cref="SpeechSynthesizerUnavailableException"/>.
    /// </summary>
    [Fact]
    public async Task UnavailableSynthesisSession_SynthesizeAsync_Always_ThrowsSpeechSynthesizerUnavailableException()
    {
        // Arrange
        var session = UnavailableSynthesisSession.Instance;

        // Act & Assert
        await Assert.ThrowsAsync<SpeechSynthesizerUnavailableException>(
            () => session.SynthesizeAsync("hello", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that calling <see cref="ISynthesisSession.SynthesizeAsync"/> with a null text
    ///     still validates the argument, throwing <see cref="ArgumentNullException"/> rather than
    ///     the unavailable fault.
    /// </summary>
    [Fact]
    public async Task UnavailableSynthesisSession_SynthesizeAsync_NullText_ThrowsArgumentNullException()
    {
        // Arrange
        var session = UnavailableSynthesisSession.Instance;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => session.SynthesizeAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that calling <see cref="ISynthesisSession.StopAsync"/> with no operation in
    ///     flight is a safe no-op.
    /// </summary>
    [Fact]
    public async Task UnavailableSynthesisSession_StopAsync_NoSessionInFlight_IsNoOp()
    {
        // Arrange
        var session = UnavailableSynthesisSession.Instance;

        // Act
        var exception = await Record.ExceptionAsync(() => session.StopAsync(TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that disposing the shared instance is a safe no-op, even when called more than
    ///     once, so a host wrapping it in an <c>await using</c> block never fails.
    /// </summary>
    [Fact]
    public async Task UnavailableSynthesisSession_DisposeAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var session = UnavailableSynthesisSession.Instance;

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await session.DisposeAsync();
            await session.DisposeAsync();
        });

        // Assert
        Assert.Null(exception);
    }
}
