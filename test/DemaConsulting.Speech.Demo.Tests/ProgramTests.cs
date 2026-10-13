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
    ///     Test that a mistyped secret-bearing argument (an option written in the unsupported
    ///     <c>name=value</c> form) is reported as an error without echoing the secret.
    /// </summary>
    [Fact]
    public void Program_Run_UnrecognizedSecretArgument_ErrorDoesNotEchoArgument()
    {
        // Arrange
        const string secret = "hunter2";
        using var output = new StringWriter();
        using var error = new StringWriter();
        var started = false;
        var mistypedArgument = "--mirror-password" + "=" + secret;

        // Act
        var exitCode = Program.Run([mistypedArgument], output, error, _ => null, _ => started = true);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.False(started);
        Assert.Contains("Error:", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a secret passed with a valid option but an invalid combination (a password
    ///     with no mirror URL) is reported as an error without echoing the secret.
    /// </summary>
    [Fact]
    public void Program_Run_PasswordWithoutMirrorUrl_ReportsErrorWithoutEchoingSecret()
    {
        // Arrange
        const string secret = "hunter2";
        using var output = new StringWriter();
        using var error = new StringWriter();
        var started = false;

        // Act
        var exitCode = Program.Run(["--mirror-password", secret], output, error, _ => null, _ => started = true);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.False(started);
        Assert.Contains("Error:", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a value-taking option followed by another recognized option is rejected
    ///     before the application starts, rather than swallowing the option.
    /// </summary>
    [Fact]
    public void Program_Run_OptionFollowedByOption_ReportsErrorAndDoesNotStart()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();
        var started = false;

        // Act
        var exitCode = Program.Run(["--mirror-url", "--help"], output, error, _ => null, _ => started = true);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.False(started);
        Assert.Contains("--mirror-url requires a value", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, output.ToString());
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
    ///     Test that every accepted launch option (models folder, voices folder, mirror URL, and
    ///     Basic credentials) is handed to the start action, exactly once.
    /// </summary>
    [Fact]
    public void Program_Run_ValidOptions_StartsWithAllParsedOptions()
    {
        // Arrange
        var voices = Directory.CreateDirectory(Path.Join(AppContext.BaseDirectory, "ProgramTests", Guid.NewGuid().ToString("N"))).FullName;
        using var output = new StringWriter();
        using var error = new StringWriter();
        AppLaunchOptions? received = null;
        var calls = 0;

        try
        {
            // Act
            var exitCode = Program.Run(
                [
                    "--models-dir", "/tmp/models",
                    "--voices-dir", voices,
                    "--mirror-url", "https://mirror.example.com",
                    "--mirror-user", "alice",
                    "--mirror-password", "basic-value",
                ],
                output,
                error,
                _ => null,
                o => { received = o; calls++; });

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Equal(1, calls);
            Assert.NotNull(received);
            Assert.Equal("/tmp/models", received.ModelsDir);
            Assert.Equal(voices, received.VoicesDir);
            Assert.Equal("https://mirror.example.com", received.MirrorUrl);
            Assert.Equal("alice", received.MirrorUser);
            Assert.Equal("basic-value", received.MirrorPassword);
            Assert.Null(received.MirrorBearerToken);
            Assert.False(received.Help);
            Assert.Equal(string.Empty, output.ToString());
            Assert.Equal(string.Empty, error.ToString());
        }
        finally
        {
            Directory.Delete(voices);
        }
    }

    /// <summary>
    ///     Test that a bearer-token mirror configuration is handed to the start action.
    /// </summary>
    [Fact]
    public void Program_Run_BearerTokenMirror_StartsWithParsedToken()
    {
        // Arrange
        using var output = new StringWriter();
        using var error = new StringWriter();
        AppLaunchOptions? received = null;

        // Act
        var exitCode = Program.Run(
            ["--mirror-url", "https://mirror.example.com", "--mirror-bearer-token", "token-value"],
            output,
            error,
            _ => null,
            o => received = o);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.NotNull(received);
        Assert.Equal("https://mirror.example.com", received.MirrorUrl);
        Assert.Equal("token-value", received.MirrorBearerToken);
        Assert.Null(received.MirrorUser);
        Assert.Null(received.MirrorPassword);
    }
}
