using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="SynthesisSessionFaultedException"/>.
/// </summary>
public class SynthesisSessionFaultedExceptionTests
{
    /// <summary>
    ///     Proves that every constructor preserves its message and inner exception.
    /// </summary>
    [Fact]
    public void SynthesisSessionFaultedException_Constructors_PreserveMessageAndInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("root cause");

        // Act
        var defaultException = new SynthesisSessionFaultedException();
        var messageException = new SynthesisSessionFaultedException("custom message");
        var wrappedException = new SynthesisSessionFaultedException("wrapped message", inner);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(defaultException.Message));
        Assert.Null(defaultException.InnerException);
        Assert.Equal("custom message", messageException.Message);
        Assert.Null(messageException.InnerException);
        Assert.Equal("wrapped message", wrappedException.Message);
        Assert.Same(inner, wrappedException.InnerException);
    }
}
