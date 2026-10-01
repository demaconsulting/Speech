using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="UnavailableSpeechSynthesizerEngine"/> and
///     <see cref="SpeechSynthesizerUnavailableException"/>.
/// </summary>
public class UnavailableSpeechSynthesizerEngineTests
{
    /// <summary>
    ///     Proves that <see cref="UnavailableSpeechSynthesizerEngine.Instance"/> reports itself
    ///     as unavailable.
    /// </summary>
    [Fact]
    public void UnavailableSpeechSynthesizerEngine_IsAvailable_Read_ReturnsFalse()
    {
        // Act
        var isAvailable = UnavailableSpeechSynthesizerEngine.Instance.IsAvailable;

        // Assert
        Assert.False(isAvailable);
    }

    /// <summary>
    ///     Proves that <see cref="ISpeechSynthesizerEngine.CreateSessionAsync"/> always succeeds,
    ///     returning <see cref="UnavailableSynthesisSession.Instance"/> rather than throwing.
    /// </summary>
    [Fact]
    public async Task UnavailableSpeechSynthesizerEngine_CreateSessionAsync_Always_ReturnsUnavailableSession()
    {
        // Arrange
        var engine = UnavailableSpeechSynthesizerEngine.Instance;
        var device = Substitute.For<IAudioPlaybackDevice>();

        // Act
        var session = await engine.CreateSessionAsync(device, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(UnavailableSynthesisSession.Instance, session);
    }

    /// <summary>
    ///     Proves that <see cref="ISpeechSynthesizerEngine.CreateSessionAsync"/> rejects a null
    ///     device.
    /// </summary>
    [Fact]
    public async Task UnavailableSpeechSynthesizerEngine_CreateSessionAsync_NullDevice_ThrowsArgumentNullException()
    {
        // Arrange
        var engine = UnavailableSpeechSynthesizerEngine.Instance;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => engine.CreateSessionAsync(null!, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that calling <see cref="ISpeechSynthesizerEngine.SpeakAsync"/> throws
    ///     <see cref="SpeechSynthesizerUnavailableException"/>.
    /// </summary>
    [Fact]
    public async Task UnavailableSpeechSynthesizerEngine_SpeakAsync_Always_ThrowsSpeechSynthesizerUnavailableException()
    {
        // Arrange
        var engine = UnavailableSpeechSynthesizerEngine.Instance;
        var device = Substitute.For<IAudioPlaybackDevice>();

        // Act & Assert
        await Assert.ThrowsAsync<SpeechSynthesizerUnavailableException>(
            () => engine.SpeakAsync(device, "hello", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that calling <see cref="ISpeechSynthesizerEngine.SynthesizeAsync"/> throws
    ///     <see cref="SpeechSynthesizerUnavailableException"/>.
    /// </summary>
    [Fact]
    public async Task UnavailableSpeechSynthesizerEngine_SynthesizeAsync_Always_ThrowsSpeechSynthesizerUnavailableException()
    {
        // Arrange
        var engine = UnavailableSpeechSynthesizerEngine.Instance;

        // Act & Assert
        await Assert.ThrowsAsync<SpeechSynthesizerUnavailableException>(
            () => engine.SynthesizeAsync("hello", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that disposing the shared instance is a safe no-op, even when called more than
    ///     once, so a host wrapping it in an <c>await using</c> block never fails.
    /// </summary>
    [Fact]
    public async Task UnavailableSpeechSynthesizerEngine_DisposeAsync_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var engine = UnavailableSpeechSynthesizerEngine.Instance;

        // Act
        var exception = await Record.ExceptionAsync(async () =>
        {
            await engine.DisposeAsync();
            await engine.DisposeAsync();
        });

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechSynthesizerUnavailableException"/> exposes the message
    ///     supplied to its single-argument constructor.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerUnavailableException_Constructor_WithMessage_ExposesMessage()
    {
        // Arrange
        const string message = "No speech synthesizer is available.";

        // Act
        var exception = new SpeechSynthesizerUnavailableException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechSynthesizerUnavailableException"/> exposes both the
    ///     message and inner exception supplied to its two-argument constructor.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerUnavailableException_Constructor_WithInnerException_ExposesBoth()
    {
        // Arrange
        const string message = "Synthesis engine load failed.";
        var inner = new InvalidOperationException("native backend faulted");

        // Act
        var exception = new SpeechSynthesizerUnavailableException(message, inner);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    ///     Proves that the default (parameterless) constructor of
    ///     <see cref="SpeechSynthesizerUnavailableException"/> produces a non-empty default
    ///     message.
    /// </summary>
    [Fact]
    public void SpeechSynthesizerUnavailableException_Constructor_Default_HasNonEmptyMessage()
    {
        // Act
        var exception = new SpeechSynthesizerUnavailableException();

        // Assert
        Assert.False(string.IsNullOrEmpty(exception.Message));
    }
}
