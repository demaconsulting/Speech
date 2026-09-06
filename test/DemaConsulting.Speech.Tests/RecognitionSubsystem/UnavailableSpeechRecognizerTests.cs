using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="UnavailableSpeechRecognizer"/> and
///     <see cref="SpeechRecognizerUnavailableException"/>.
/// </summary>
public class UnavailableSpeechRecognizerTests
{
    /// <summary>
    ///     Proves that <see cref="UnavailableSpeechRecognizer.Instance"/> reports itself as
    ///     unavailable.
    /// </summary>
    [Fact]
    public void UnavailableSpeechRecognizer_IsAvailable_Read_ReturnsFalse()
    {
        // Arrange & Act: read the availability flag from the shared instance
        var isAvailable = UnavailableSpeechRecognizer.Instance.IsAvailable;

        // Assert: the recognizer honestly reports itself as unavailable
        Assert.False(isAvailable);
    }

    /// <summary>
    ///     Proves that calling <see cref="ISpeechRecognizer.Start"/> on the unavailable recognizer
    ///     throws <see cref="SpeechRecognizerUnavailableException"/>.
    /// </summary>
    [Fact]
    public void UnavailableSpeechRecognizer_Start_Always_ThrowsSpeechRecognizerUnavailableException()
    {
        // Arrange: the shared unavailable recognizer
        var recognizer = UnavailableSpeechRecognizer.Instance;

        // Act & Assert: starting an unavailable recognizer throws the documented exception
        Assert.Throws<SpeechRecognizerUnavailableException>(recognizer.Start);
    }

    /// <summary>
    ///     Proves that calling <see cref="ISpeechRecognizer.Stop"/> on the unavailable recognizer
    ///     throws <see cref="SpeechRecognizerUnavailableException"/>.
    /// </summary>
    [Fact]
    public void UnavailableSpeechRecognizer_Stop_Always_ThrowsSpeechRecognizerUnavailableException()
    {
        // Arrange: the shared unavailable recognizer
        var recognizer = UnavailableSpeechRecognizer.Instance;

        // Act & Assert: stopping an unavailable recognizer throws the documented exception
        Assert.Throws<SpeechRecognizerUnavailableException>(recognizer.Stop);
    }

    /// <summary>
    ///     Proves that subscribing to and unsubscribing from
    ///     <see cref="ISpeechRecognizer.ResultReceived"/>, and disposing the shared instance
    ///     repeatedly, are all safe no-ops that never throw or invalidate the instance.
    /// </summary>
    [Fact]
    public void UnavailableSpeechRecognizer_SubscriptionAndDispose_Always_AreSafeNoOps()
    {
        // Arrange: the shared unavailable recognizer and a handler to (un)subscribe
        var recognizer = UnavailableSpeechRecognizer.Instance;
        EventHandler<SpeechRecognitionEvent> handler = (_, _) => { };

        // Act: subscribe, unsubscribe, and dispose twice
        var exception = Record.Exception(() =>
        {
            recognizer.ResultReceived += handler;
            recognizer.ResultReceived -= handler;
            recognizer.Dispose();
            recognizer.Dispose();
        });

        // Assert: nothing throws and the shared instance is still usable
        Assert.Null(exception);
        Assert.False(UnavailableSpeechRecognizer.Instance.IsAvailable);
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
