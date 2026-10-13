// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using DemaConsulting.Speech.Cli.Cli;

namespace DemaConsulting.Speech.Cli.Tests.Cli;

/// <summary>
///     Unit tests for <see cref="ConsoleCancellation"/>. The console signal itself cannot be raised
///     from a test (<see cref="ConsoleCancelEventArgs"/> has no public constructor), so the tests
///     drive the registration's cancellation work through its internal <c>Raise</c> seam. The tests
///     are sequential because registrations subscribe the process-global
///     <see cref="Console.CancelKeyPress"/> event.
/// </summary>
[Collection("Sequential")]
public sealed class ConsoleCancellationTests
{
    /// <summary>Test that Register cancels the source, runs the callback, and suppresses termination.</summary>
    [Fact]
    public void ConsoleCancellation_Register_Raise_CancelsSourceRunsCallbackAndSuppressesTermination()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var callbackSawCancellation = false;
        using var registration = ConsoleCancellation.Register(source, () => callbackSawCancellation = source.IsCancellationRequested);

        // Act
        var suppressed = registration.Raise();

        // Assert
        Assert.True(suppressed);
        Assert.True(source.IsCancellationRequested);
        Assert.True(callbackSawCancellation, "The callback must run after the source is canceled.");
    }

    /// <summary>Test that Register without a callback still cancels the source.</summary>
    [Fact]
    public void ConsoleCancellation_Register_NoCallback_CancelsSource()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        using var registration = ConsoleCancellation.Register(source);

        // Act
        registration.Raise();

        // Assert
        Assert.True(source.IsCancellationRequested);
    }

    /// <summary>
    ///     Test that the real subscribed handler of a Register registration marks the console event
    ///     canceled (so the process is not terminated) and cancels the source.
    /// </summary>
    [Fact]
    public void ConsoleCancellation_Register_Handler_SetsCancelAndCancelsSource()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        using var registration = ConsoleCancellation.Register(source);
        var args = CreateCancelEventArgs();

        // Act
        registration.Handler(null, args);

        // Assert
        Assert.True(args.Cancel);
        Assert.True(source.IsCancellationRequested);
    }

    /// <summary>
    ///     Test that the real subscribed handler of an Observe registration runs the callback but
    ///     leaves the console event's Cancel flag untouched.
    /// </summary>
    [Fact]
    public void ConsoleCancellation_Observe_Handler_RunsCallbackAndLeavesCancelUnset()
    {
        // Arrange
        var calls = 0;
        using var registration = ConsoleCancellation.Observe(() => calls++);
        var args = CreateCancelEventArgs();

        // Act
        registration.Handler(null, args);

        // Assert
        Assert.False(args.Cancel);
        Assert.Equal(1, calls);
    }

    /// <summary>Test that a registration is subscribed until it is disposed, and stays detached afterwards.</summary>
    [Fact]
    public void ConsoleCancellation_Dispose_DetachesHandler()
    {
        // Arrange
        var registration = ConsoleCancellation.Observe(() => { });
        var subscribedBeforeDispose = registration.IsSubscribed;

        // Act
        registration.Dispose();
        registration.Dispose();

        // Assert
        Assert.True(subscribedBeforeDispose);
        Assert.False(registration.IsSubscribed);
    }

    /// <summary>
    ///     Creates console cancel event arguments. The type has no public constructor, so the
    ///     internal one is invoked reflectively.
    /// </summary>
    /// <returns>A new <see cref="ConsoleCancelEventArgs"/> for <see cref="ConsoleSpecialKey.ControlC"/>.</returns>
    private static ConsoleCancelEventArgs CreateCancelEventArgs()
    {
        return (ConsoleCancelEventArgs)Activator.CreateInstance(
            typeof(ConsoleCancelEventArgs),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            binder: null,
            args: [ConsoleSpecialKey.ControlC],
            culture: null)!;
    }

    /// <summary>Test that Observe runs the callback but leaves the termination decision alone.</summary>
    [Fact]
    public void ConsoleCancellation_Observe_Raise_RunsCallbackWithoutSuppressingTermination()
    {
        // Arrange
        var calls = 0;
        using var registration = ConsoleCancellation.Observe(() => calls++);

        // Act
        var suppressed = registration.Raise();

        // Assert
        Assert.False(suppressed);
        Assert.Equal(1, calls);
    }

    /// <summary>Test that disposing a registration is safe to repeat.</summary>
    [Fact]
    public void ConsoleCancellation_Dispose_CalledTwice_DoesNotThrow()
    {
        // Arrange
        var registration = ConsoleCancellation.Observe(() => { });

        // Act
        var exception = Record.Exception(() =>
        {
            registration.Dispose();
            registration.Dispose();
        });

        // Assert
        Assert.Null(exception);
    }

    /// <summary>Test that a null source is rejected.</summary>
    [Fact]
    public void ConsoleCancellation_Register_NullSource_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: the null source is rejected
        var exception = Assert.Throws<ArgumentNullException>(() => ConsoleCancellation.Register(null!));
        Assert.Equal("source", exception.ParamName);
    }

    /// <summary>Test that a null callback is rejected.</summary>
    [Fact]
    public void ConsoleCancellation_Observe_NullCallback_ThrowsArgumentNullException()
    {
        // Arrange / Act / Assert: the null callback is rejected
        var exception = Assert.Throws<ArgumentNullException>(() => ConsoleCancellation.Observe(null!));
        Assert.Equal("onCancel", exception.ParamName);
    }
}
