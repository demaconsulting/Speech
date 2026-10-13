using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="SpeechRecognizerUnavailableException"/>.
/// </summary>
public class SpeechRecognizerUnavailableExceptionTests
{
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
