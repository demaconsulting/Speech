using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="UnavailableSpeechRecognizerEngine"/> and
///     <see cref="SpeechRecognizerUnavailableException"/>.
/// </summary>
public class UnavailableSpeechRecognizerEngineTests
{
    /// <summary>
    ///     Proves that <see cref="UnavailableSpeechRecognizerEngine.Instance"/> reports itself as
    ///     unavailable.
    /// </summary>
    [Fact]
    public void UnavailableSpeechRecognizerEngine_IsAvailable_Read_ReturnsFalse()
    {
        // Arrange & Act: read the availability flag from the shared instance
        var isAvailable = UnavailableSpeechRecognizerEngine.Instance.IsAvailable;

        // Assert: the engine honestly reports itself as unavailable
        Assert.False(isAvailable);
    }

    /// <summary>
    ///     Proves that <see cref="UnavailableSpeechRecognizerEngine.CreateSessionAsync"/> never
    ///     throws for ordinary unavailability, instead always returning the shared
    ///     <see cref="UnavailableRecognitionSession.Instance"/>.
    /// </summary>
    [Fact]
    public async Task UnavailableSpeechRecognizerEngine_CreateSessionAsync_Always_ReturnsUnavailableSession()
    {
        // Arrange: the shared unavailable engine and an arbitrary device
        var engine = UnavailableSpeechRecognizerEngine.Instance;
        var device = Substitute.For<IAudioCaptureDevice>();

        // Act: create a session
        var session = await engine.CreateSessionAsync(device, TestContext.Current.CancellationToken);

        // Assert: the shared unavailable session was returned
        Assert.Same(UnavailableRecognitionSession.Instance, session);
    }

    /// <summary>
    ///     Proves that <see cref="UnavailableSpeechRecognizerEngine.CreateSessionAsync"/> still
    ///     rejects a null device, since that is a genuine caller error rather than an ordinary
    ///     unavailable state.
    /// </summary>
    [Fact]
    public async Task UnavailableSpeechRecognizerEngine_CreateSessionAsync_NullDevice_ThrowsArgumentNullException()
    {
        // Arrange: the shared unavailable engine
        var engine = UnavailableSpeechRecognizerEngine.Instance;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => engine.CreateSessionAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that <see cref="UnavailableSpeechRecognizerEngine.CreateSessionAsync"/> still
    ///     honors an already-cancelled token, since that is a genuine caller error rather than
    ///     an ordinary unavailable state - matching the <see cref="ISpeechRecognizerEngine.CreateSessionAsync"/>
    ///     contract every implementation must honor.
    /// </summary>
    [Fact]
    public async Task UnavailableSpeechRecognizerEngine_CreateSessionAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange: the shared unavailable engine, an arbitrary device, and an already-cancelled token
        var engine = UnavailableSpeechRecognizerEngine.Instance;
        var device = Substitute.For<IAudioCaptureDevice>();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => engine.CreateSessionAsync(device, cts.Token));
    }

    /// <summary>
    ///     Proves that calling <see cref="UnavailableSpeechRecognizerEngine.DisposeAsync"/> twice
    ///     is a safe no-op that never invalidates the shared instance.
    /// </summary>
    [Fact]
    public async Task UnavailableSpeechRecognizerEngine_DisposeAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange: the shared unavailable engine
        var engine = UnavailableSpeechRecognizerEngine.Instance;

        // Act
        await engine.DisposeAsync();
        await engine.DisposeAsync();

        // Assert: the shared instance is still usable
        Assert.False(UnavailableSpeechRecognizerEngine.Instance.IsAvailable);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechRecognizerUnavailableException"/> exposes the message
    ///     supplied to its single-argument constructor, confirming standard exception conformance.
    /// </summary>
    [Fact]
    public void SpeechRecognizerUnavailableException_Constructor_WithMessage_ExposesMessage()
    {
        // Arrange: a specific message
        const string message = "No recognition model is installed.";

        // Act: construct the exception with the message
        var exception = new SpeechRecognizerUnavailableException(message);

        // Assert: the message is exposed unchanged
        Assert.Equal(message, exception.Message);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechRecognizerUnavailableException"/> exposes both the message
    ///     and inner exception supplied to its two-argument constructor.
    /// </summary>
    [Fact]
    public void SpeechRecognizerUnavailableException_Constructor_WithInnerException_ExposesBoth()
    {
        // Arrange: a message and an inner exception
        const string message = "Recognition engine failed to load.";
        var inner = new InvalidOperationException("native runtime missing");

        // Act: construct the exception with both
        var exception = new SpeechRecognizerUnavailableException(message, inner);

        // Assert: both the message and inner exception are exposed unchanged
        Assert.Equal(message, exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    ///     Proves that the default (parameterless) constructor of
    ///     <see cref="SpeechRecognizerUnavailableException"/> produces a non-empty default message.
    /// </summary>
    [Fact]
    public void SpeechRecognizerUnavailableException_Constructor_Default_HasNonEmptyMessage()
    {
        // Act: construct with no arguments
        var exception = new SpeechRecognizerUnavailableException();

        // Assert: a default, non-empty message is provided
        Assert.False(string.IsNullOrEmpty(exception.Message));
    }
}
