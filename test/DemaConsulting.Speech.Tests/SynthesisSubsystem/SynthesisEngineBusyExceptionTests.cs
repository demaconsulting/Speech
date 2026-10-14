using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="SynthesisEngineBusyException"/>.
/// </summary>
public class SynthesisEngineBusyExceptionTests
{
    /// <summary>
    ///     Proves that every constructor preserves its message and inner exception.
    /// </summary>
    [Fact]
    public void SynthesisEngineBusyException_Constructors_PreserveMessageAndInnerException()
    {
        // Arrange
        var inner = new InvalidOperationException("root cause");

        // Act
        var defaultException = new SynthesisEngineBusyException();
        var messageException = new SynthesisEngineBusyException("custom message");
        var wrappedException = new SynthesisEngineBusyException("wrapped message", inner);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(defaultException.Message));
        Assert.Null(defaultException.InnerException);
        Assert.Equal("custom message", messageException.Message);
        Assert.Null(messageException.InnerException);
        Assert.Equal("wrapped message", wrappedException.Message);
        Assert.Same(inner, wrappedException.InnerException);
    }
}
