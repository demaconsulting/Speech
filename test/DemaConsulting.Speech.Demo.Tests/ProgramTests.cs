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

namespace DemaConsulting.Speech.Demo.Tests;

/// <summary>
///     Unit tests for <see cref="Program"/>, exercised through its testable
///     <c>Run</c> seam so no desktop lifetime is started.
/// </summary>
public sealed class ProgramTests
{
    /// <summary>
    ///     Test that <c>--help</c> prints the usage text to the output writer and never starts the application.
    /// </summary>
    [Fact]
    public void Program_Run_HelpFlag_PrintsUsageAndDoesNotStart()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();
        var started = false;

        // Act
        var exitCode = Program.Run(["--help"], output, error, _ => null, _ => started = true);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.False(started);
        Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>
    ///     Test that an invalid option fails fast with an error message and usage on the error writer, without starting.
    /// </summary>
    [Fact]
    public void Program_Run_UnrecognizedOption_ReportsErrorAndDoesNotStart()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();
        var started = false;

        // Act
        var exitCode = Program.Run(["--not-a-real-option"], output, error, _ => null, _ => started = true);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.False(started);
        Assert.Contains("Error:", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an invalid mirror configuration is rejected before the application starts.
    /// </summary>
    [Fact]
    public void Program_Run_InvalidMirrorUrl_ReportsErrorAndDoesNotStart()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();
        var started = false;

        // Act
        var exitCode = Program.Run(["--mirror-url", "not a url"], output, error, _ => null, _ => started = true);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.False(started);
        Assert.Contains("Error:", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("not a url", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an unrecognized argument is reported without echoing its text.
    /// </summary>
    [Fact]
    public void Program_Run_UnrecognizedSecretArgument_ErrorDoesNotEchoArgument()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = Program.Run(["--mirror-password=hunter2"], output, error, _ => null, _ => { });

        // Assert
        Assert.Equal(1, exitCode);
        Assert.DoesNotContain("hunter2", error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a help request wins over an otherwise invalid mirror configuration.
    /// </summary>
    [Fact]
    public void Program_Run_HelpWithInvalidMirror_PrintsUsageAndReturnsZero()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = Program.Run(["--help", "--mirror-url", "not a url"], output, error, _ => null, _ => { });

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    /// <summary>
    ///     Test that valid options are handed to the start action exactly once.
    /// </summary>
    [Fact]
    public void Program_Run_ValidOptions_StartsWithParsedOptions()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();
        AppLaunchOptions? received = null;
        var calls = 0;

        // Act
        var exitCode = Program.Run(["--models-dir", "/tmp/models"], output, error, _ => null, o => { received = o; calls++; });

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Equal(1, calls);
        Assert.Equal("/tmp/models", received?.ModelsDir);
        Assert.Equal(string.Empty, output.ToString());
    }
}
