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
        using var source = new CancellationTokenSource();
        using var registration = ConsoleCancellation.Register(source);

        registration.Raise();

        Assert.True(source.IsCancellationRequested);
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
        var registration = ConsoleCancellation.Observe(() => { });

        var exception = Record.Exception(() =>
        {
            registration.Dispose();
            registration.Dispose();
        });

        Assert.Null(exception);
    }

    /// <summary>Test that a null source is rejected.</summary>
    [Fact]
    public void ConsoleCancellation_Register_NullSource_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ConsoleCancellation.Register(null!));
    }

    /// <summary>Test that a null callback is rejected.</summary>
    [Fact]
    public void ConsoleCancellation_Observe_NullCallback_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => ConsoleCancellation.Observe(null!));
    }
}
