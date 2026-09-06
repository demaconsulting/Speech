using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="SpeechModelStoreException"/> class.
/// </summary>
public sealed class SpeechModelStoreExceptionTests
{
    /// <summary>
    ///     Proves that the message-only constructor exposes the supplied message unchanged.
    /// </summary>
    [Fact]
    public void SpeechModelStoreException_Constructor_WithMessage_ExposesMessage()
    {
        // Arrange
        const string message = "Failed to uninstall model 'demo': its installed directory could not be removed.";

        // Act
        var exception = new SpeechModelStoreException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    /// <summary>
    ///     Proves that the message-and-inner-exception constructor exposes both unchanged.
    /// </summary>
    [Fact]
    public void SpeechModelStoreException_Constructor_WithInnerException_ExposesBoth()
    {
        // Arrange
        const string message = "Failed to uninstall model 'demo'.";
        var inner = new IOException("The process cannot access the file because it is being used by another process.");

        // Act
        var exception = new SpeechModelStoreException(message, inner);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    ///     Proves that the default constructor provides a non-empty message.
    /// </summary>
    [Fact]
    public void SpeechModelStoreException_Constructor_Default_HasNonEmptyMessage()
    {
        // Act
        var exception = new SpeechModelStoreException();

        // Assert
        Assert.False(string.IsNullOrEmpty(exception.Message));
    }
}
