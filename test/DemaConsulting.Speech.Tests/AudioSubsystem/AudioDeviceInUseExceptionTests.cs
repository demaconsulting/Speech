using DemaConsulting.Speech.AudioSubsystem;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="AudioDeviceInUseException"/>.
/// </summary>
public class AudioDeviceInUseExceptionTests
{
    /// <summary>
    ///     Proves that <see cref="AudioDeviceInUseException"/> exposes the message supplied to
    ///     its single-argument constructor, confirming standard exception conformance.
    /// </summary>
    [Fact]
    public void AudioDeviceInUseException_Constructor_WithMessage_ExposesMessage()
    {
        // Arrange: a specific message
        const string message = "Cannot refresh PortAudio devices while a stream is active on: Mic.";

        // Act: construct the exception with the message
        var exception = new AudioDeviceInUseException(message);

        // Assert: the message is exposed unchanged
        Assert.Equal(message, exception.Message);
    }

    /// <summary>
    ///     Proves that <see cref="AudioDeviceInUseException"/> exposes both the message and inner
    ///     exception supplied to its two-argument constructor.
    /// </summary>
    [Fact]
    public void AudioDeviceInUseException_Constructor_WithInnerException_ExposesBoth()
    {
        // Arrange: a message and an inner exception
        const string message = "Refresh failed while a stream was active.";
        var inner = new InvalidOperationException("native backend faulted");

        // Act: construct the exception with both
        var exception = new AudioDeviceInUseException(message, inner);

        // Assert: both the message and inner exception are exposed unchanged
        Assert.Equal(message, exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    ///     Proves that the default (parameterless) constructor of
    ///     <see cref="AudioDeviceInUseException"/> produces a non-empty default message.
    /// </summary>
    [Fact]
    public void AudioDeviceInUseException_Constructor_Default_HasNonEmptyMessage()
    {
        // Act: construct with no arguments
        var exception = new AudioDeviceInUseException();

        // Assert: a default, non-empty message is provided
        Assert.False(string.IsNullOrEmpty(exception.Message));
    }
}
