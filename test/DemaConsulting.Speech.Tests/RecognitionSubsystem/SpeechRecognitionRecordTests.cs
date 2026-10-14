using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for the <see cref="SpeechRecognitionResult"/> and <see cref="SpeechRecognitionEvent"/>
///     value types that accompany <see cref="IRecognitionSession"/>.
/// </summary>
public class SpeechRecognitionRecordTests
{
    /// <summary>
    ///     Proves that constructing a result with null text is rejected.
    /// </summary>
    [Fact]
    public void SpeechRecognitionResult_Constructor_NullText_ThrowsArgumentNullException()
    {
        // Arrange: no setup needed

        // Act & Assert: null text is rejected
        Assert.Throws<ArgumentNullException>(() => new SpeechRecognitionResult(null!, IsFinal: true));
    }

    /// <summary>
    ///     Proves that a non-null text, including an empty string, is accepted and exposed unchanged.
    /// </summary>
    [Fact]
    public void SpeechRecognitionResult_Constructor_ValidText_ExposesValues()
    {
        // Arrange & Act
        var result = new SpeechRecognitionResult("hello", IsFinal: true);
        var empty = new SpeechRecognitionResult(string.Empty, IsFinal: false);

        // Assert
        Assert.Equal("hello", result.Text);
        Assert.True(result.IsFinal);
        Assert.Equal(string.Empty, empty.Text);
        Assert.False(empty.IsFinal);
    }

    /// <summary>
    ///     Proves that copying a result with null text via a with-expression is rejected.
    /// </summary>
    [Fact]
    public void SpeechRecognitionResult_WithExpression_NullText_ThrowsArgumentNullException()
    {
        // Arrange
        var result = new SpeechRecognitionResult("hello", IsFinal: false);

        // Act & Assert: the copy cannot smuggle in a null
        Assert.Throws<ArgumentNullException>(() => result with { Text = null! });
    }

    /// <summary>
    ///     Proves that constructing an event with a null result is rejected.
    /// </summary>
    [Fact]
    public void SpeechRecognitionEvent_Constructor_NullResult_ThrowsArgumentNullException()
    {
        // Arrange: no setup needed

        // Act & Assert: a null result is rejected
        Assert.Throws<ArgumentNullException>(() => new SpeechRecognitionEvent(null!));
    }

    /// <summary>
    ///     Proves that an event exposes the result it was constructed with.
    /// </summary>
    [Fact]
    public void SpeechRecognitionEvent_Constructor_ValidResult_ExposesResult()
    {
        // Arrange
        var result = new SpeechRecognitionResult("hello", IsFinal: true);

        // Act
        var recognitionEvent = new SpeechRecognitionEvent(result);

        // Assert
        Assert.Same(result, recognitionEvent.Result);
    }
}
