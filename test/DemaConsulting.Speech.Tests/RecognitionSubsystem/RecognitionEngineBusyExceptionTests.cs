using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="RecognitionEngineBusyException"/>.
/// </summary>
public class RecognitionEngineBusyExceptionTests
{
    /// <summary>
    ///     Proves that the default (parameterless) constructor of
    ///     <see cref="RecognitionEngineBusyException"/> produces a non-empty default message.
    /// </summary>
    [Fact]
    public void RecognitionEngineBusyException_Constructor_Default_HasNonEmptyMessage()
    {
        // Act: construct with no arguments
        var exception = new RecognitionEngineBusyException();

        // Assert: a default, non-empty message is provided
        Assert.False(string.IsNullOrEmpty(exception.Message));
    }

    /// <summary>
    ///     Proves that <see cref="RecognitionEngineBusyException"/> exposes the message supplied
    ///     to its single-argument constructor, confirming standard exception conformance.
    /// </summary>
    [Fact]
    public void RecognitionEngineBusyException_Constructor_WithMessage_ExposesMessage()
    {
        // Arrange: a specific message
        const string message = "Cannot create a recognition session: this engine's backend is already leased.";

        // Act: construct the exception with the message
        var exception = new RecognitionEngineBusyException(message);

        // Assert: the message is exposed unchanged
        Assert.Equal(message, exception.Message);
    }

    /// <summary>
    ///     Proves that <see cref="RecognitionEngineBusyException"/> exposes both the message and
    ///     inner exception supplied to its two-argument constructor.
    /// </summary>
    [Fact]
    public void RecognitionEngineBusyException_Constructor_WithInnerException_ExposesBoth()
    {
        // Arrange: a message and an inner exception
        const string message = "Cannot create a recognition session: this engine's backend is already leased.";
        var inner = new InvalidOperationException("lease semaphore faulted");

        // Act: construct the exception with both
        var exception = new RecognitionEngineBusyException(message, inner);

        // Assert: both the message and inner exception are exposed unchanged
        Assert.Equal(message, exception.Message);
        Assert.Same(inner, exception.InnerException);
    }
}
