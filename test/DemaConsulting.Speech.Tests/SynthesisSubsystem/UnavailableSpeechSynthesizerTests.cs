using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="UnavailableSpeechSynthesizer"/> and
///     <see cref="SpeechSynthesizerUnavailableException"/>.
/// </summary>
public class UnavailableSpeechSynthesizerTests
{
    /// <summary>
    ///     Proves that <see cref="UnavailableSpeechSynthesizer.Instance"/> reports itself as
    ///     unavailable.
    /// </summary>
    [Fact]
    public void UnavailableSpeechSynthesizer_IsAvailable_Read_ReturnsFalse()
    {
        // Act
        var isAvailable = UnavailableSpeechSynthesizer.Instance.IsAvailable;

        // Assert
        Assert.False(isAvailable);
    }

    /// <summary>
    ///     Proves that calling <see cref="ISpeechSynthesizer.SynthesizeStreamAsync"/> throws
    ///     <see cref="SpeechSynthesizerUnavailableException"/>.
    /// </summary>
    [Fact]
    public void UnavailableSpeechSynthesizer_SynthesizeStreamAsync_Always_ThrowsSpeechSynthesizerUnavailableException()
    {
        // Arrange
        var synthesizer = UnavailableSpeechSynthesizer.Instance;

        // Act & Assert
        Assert.Throws<SpeechSynthesizerUnavailableException>(
            () => synthesizer.SynthesizeStreamAsync("hello", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that calling <see cref="ISpeechSynthesizer.PlayStreamAsync"/> throws
    ///     <see cref="SpeechSynthesizerUnavailableException"/>.
    /// </summary>
    [Fact]
    public async Task UnavailableSpeechSynthesizer_PlayStreamAsync_Always_ThrowsSpeechSynthesizerUnavailableException()
    {
        // Arrange
        var synthesizer = UnavailableSpeechSynthesizer.Instance;

        // Act & Assert
        await Assert.ThrowsAsync<SpeechSynthesizerUnavailableException>(
            () => synthesizer.PlayStreamAsync(EmptyStream(), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that calling <see cref="ISpeechSynthesizer.SpeakAsync"/> throws
    ///     <see cref="SpeechSynthesizerUnavailableException"/>.
    /// </summary>
    [Fact]
    public async Task UnavailableSpeechSynthesizer_SpeakAsync_Always_ThrowsSpeechSynthesizerUnavailableException()
    {
        // Arrange
        var synthesizer = UnavailableSpeechSynthesizer.Instance;

        // Act & Assert
        await Assert.ThrowsAsync<SpeechSynthesizerUnavailableException>(
            () => synthesizer.SpeakAsync("hello", TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that calling <see cref="ISpeechSynthesizer.Stop"/> throws
    ///     <see cref="SpeechSynthesizerUnavailableException"/>.
    /// </summary>
    [Fact]
    public void UnavailableSpeechSynthesizer_Stop_Always_ThrowsSpeechSynthesizerUnavailableException()
    {
        // Arrange
        var synthesizer = UnavailableSpeechSynthesizer.Instance;

        // Act & Assert
        Assert.Throws<SpeechSynthesizerUnavailableException>(synthesizer.Stop);
    }

    /// <summary>
    ///     Proves that disposing the shared instance is a safe no-op, even when called more than
    ///     once, so a host wrapping it in a <c>using</c> block never fails.
    /// </summary>
    [Fact]
    public void UnavailableSpeechSynthesizer_Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var synthesizer = UnavailableSpeechSynthesizer.Instance;

        // Act
        var exception = Record.Exception(() =>
        {
            synthesizer.Dispose();
            synthesizer.Dispose();
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

    /// <summary>An empty asynchronous stream, standing in for a real synthesized-speech sequence.</summary>
    private static async IAsyncEnumerable<SynthesizedSpeech> EmptyStream()
    {
        await Task.CompletedTask;
        yield break;
    }
}
