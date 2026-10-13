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
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Cli;

/// <summary>
///     Unit tests for <see cref="Context"/>: command-line parsing, global option placement,
///     mirror configuration, and environment-variable handling.
/// </summary>
[Collection("Sequential")]
public class ContextTests
{
    /// <summary>
    ///     Test that a global option (<c>--models-dir</c>) appearing after the subcommand name is
    ///     still recognized as a global option rather than collected into the subcommand's raw
    ///     arguments.
    /// </summary>
    [Fact]
    public void Context_Create_ModelsDirAfterCommand_IsRecognizedAsGlobalOption()
    {
        // Act: parse a command line with the subcommand first, then a global option
        using var context = Context.Create(["list-models", "--models-dir", "/tmp/models", "--format", "json"]);

        // Assert: the global option is resolved onto the context, not left in CommandArgs
        Assert.Equal("list-models", context.Command);
        Assert.Equal("/tmp/models", context.ModelsDir);
        Assert.Equal(["--format", "json"], context.CommandArgs);
    }

    /// <summary>
    ///     Test that <c>--voices-dir</c> parses onto the context, anywhere relative to the subcommand,
    ///     when the directory exists.
    /// </summary>
    [Fact]
    public void Context_Create_WithExistingVoicesDir_ParsesOntoContext()
    {
        // Arrange
        var dir = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Act
            using var context = Context.Create(["list-voices", "--voices-dir", dir]);

            // Assert
            Assert.Equal(dir, context.VoicesDir);
            Assert.Empty(context.CommandArgs);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a <c>--voices-dir</c> naming a missing directory, or lacking a value, is rejected.
    /// </summary>
    [Fact]
    public void Context_Create_WithBadVoicesDir_ThrowsArgumentException()
    {
        var missing = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));

        Assert.Throws<ArgumentException>(() => Context.Create(["list-voices", "--voices-dir", missing]));
        Assert.Throws<ArgumentException>(() => Context.Create(["list-voices", "--voices-dir"]));
    }

    /// <summary>
    ///     Test that <c>--mirror-url</c> and the other <c>--mirror-*</c> global options parse onto
    ///     the <see cref="Context"/> regardless of where they appear relative to the subcommand.
    /// </summary>
    [Fact]
    public void Context_Create_WithMirrorOptions_ParsesOntoContext()
    {
        // Act
        using var context = Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret"
        ]);

        // Assert
        Assert.Equal("https://mirror.example.com", context.MirrorUrl);
        Assert.Equal("alice", context.MirrorUser);
        Assert.Equal("secret", context.MirrorPassword);
        Assert.Null(context.MirrorBearerToken);
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> returns <see langword="null"/> when no
    ///     <c>--mirror-url</c> was supplied.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_NoMirrorUrl_ReturnsNull()
    {
        // Act
        using var context = Context.Create(["list-models"]);

        // Assert
        Assert.Null(context.CreateMirror());
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> builds a <see cref="DownloadMirror"/> whose
    ///     base URI matches a valid <c>--mirror-url</c>, with Basic credentials attached when a
    ///     matching <c>--mirror-user</c>/<c>--mirror-password</c> pair is supplied.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_ValidCredentialedMirror_ReturnsConfiguredMirror()
    {
        // Act
        using var context = Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret"
        ]);
        var mirror = context.CreateMirror();

        // Assert
        Assert.NotNull(mirror);
        Assert.Equal(new Uri("https://mirror.example.com"), mirror.BaseUri);
        Assert.Equal("alice", mirror.Credentials?.UserName);
        Assert.Equal("secret", mirror.Credentials?.Password);
        Assert.Null(mirror.BearerToken);
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> builds a bearer-token-authenticated
    ///     <see cref="DownloadMirror"/> when only <c>--mirror-bearer-token</c> is supplied.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_BearerTokenMirror_ReturnsConfiguredMirror()
    {
        // Act
        using var context = Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-bearer-token", "token-value"
        ]);
        var mirror = context.CreateMirror();

        // Assert
        Assert.NotNull(mirror);
        Assert.Null(mirror.Credentials);
        Assert.Equal("token-value", mirror.BearerToken);
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-url</c> that is not a
    ///     valid absolute URL.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_InvalidMirrorUrl_ThrowsArgumentException()
    {
        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create(["list-models", "--mirror-url", "not a url"]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-url</c> carrying a
    ///     query string, fragment, or user-info component, or using a scheme other than http/https.
    /// </summary>
    [Theory]
    [InlineData("https://mirror.example.com/base?token=abc")]
    [InlineData("https://mirror.example.com/base#fragment")]
    [InlineData("https://user:secret@mirror.example.com/base")]
    [InlineData("ftp://mirror.example.com/base")]
    public void Context_CreateMirror_UnsupportedMirrorUrlShape_ThrowsArgumentException(string mirrorUrl)
    {
        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create(["list-models", "--mirror-url", mirrorUrl]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects an empty or whitespace-only bearer token.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_WhitespaceBearerToken_ThrowsArgumentException()
    {
        // Act and Assert
        Assert.Throws<ArgumentException>(() => Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-bearer-token", "   "
        ]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects either credential form combined
    ///     with a plain <c>http</c> mirror on a non-loopback host, since the credential would be
    ///     sent in cleartext, but accepts the same combination on a loopback host.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_CredentialOverPlainHttp_RejectedUnlessLoopback()
    {
        // Act and Assert: non-loopback host, Basic credentials and bearer token
        Assert.Throws<ArgumentException>(() => Context.Create([
            "list-models",
            "--mirror-url", "http://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret"
        ]));

        Assert.Throws<ArgumentException>(() => Context.Create([
            "list-models",
            "--mirror-url", "http://mirror.example.com",
            "--mirror-bearer-token", "token-value"
        ]));

        // Loopback host is accepted
        using var loopback = Context.Create([
            "list-models",
            "--mirror-url", "http://localhost:8080",
            "--mirror-bearer-token", "token-value"
        ]);
        Assert.NotNull(loopback.CreateMirror());
    }

    /// <summary>
    ///     Test that an unsupported argument produces an operator-facing message without the internal
    ///     <c>(Parameter ...)</c> suffix that a parameter name would append.
    /// </summary>
    [Fact]
    public void Context_Create_UnsupportedArgument_MessageHasNoParameterSuffix()
    {
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["bogus-command"]));

        Assert.Equal("Unsupported argument 'bogus-command'", exception.Message);
        Assert.Null(exception.ParamName);
    }

    /// <summary>
    ///     Test that a global option missing its value produces a message without the internal
    ///     <c>(Parameter ...)</c> suffix.
    /// </summary>
    [Fact]
    public void Context_Create_OptionMissingValue_MessageHasNoParameterSuffix()
    {
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--models-dir"]));

        Assert.DoesNotContain("(Parameter", exception.Message, StringComparison.Ordinal);
        Assert.Null(exception.ParamName);
    }

    /// <summary>
    ///     Test that <see cref="Context.WriteError"/> restores the console foreground color even when
    ///     writing to the error stream throws.
    /// </summary>
    [Fact]
    public void Context_WriteError_ErrorWriterThrows_RestoresForegroundColor()
    {
        var originalError = Console.Error;
        var originalColor = Console.ForegroundColor;
        try
        {
            Console.SetError(new ThrowingWriter());
            using var context = Context.Create(["--version"]);

            Assert.Throws<IOException>(() => context.WriteError("boom"));

            Assert.Equal(originalColor, Console.ForegroundColor);
        }
        finally
        {
            Console.SetError(originalError);
            Console.ForegroundColor = originalColor;
        }
    }

    /// <summary>A text writer whose every write fails.</summary>
    private sealed class ThrowingWriter : StringWriter
    {
        /// <inheritdoc/>
        public override void WriteLine(string? value) => throw new IOException("write failed");
    }

    /// <summary>
    ///     Test that mirror credentials supplied without <c>--mirror-url</c> are rejected even for
    ///     invocations that never build a model catalog, such as <c>--help</c>.
    /// </summary>
    [Fact]
    public void Context_Create_MirrorCredentialWithoutUrlAndHelp_ThrowsArgumentException()
    {
        // Act and Assert
        Assert.Throws<ArgumentException>(() => Context.Create(["--help", "--mirror-user", "alice"]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-user</c> supplied
    ///     without the matching <c>--mirror-password</c>.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_MirrorUserWithoutPassword_ThrowsArgumentException()
    {
        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice"
        ]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects supplying both HTTP Basic
    ///     credentials (<c>--mirror-user</c>/<c>--mirror-password</c>) and a bearer token
    ///     (<c>--mirror-bearer-token</c>) together, since the two authentication mechanisms are
    ///     mutually exclusive.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_BasicCredentialsAndBearerTokenTogether_ThrowsArgumentException()
    {
        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret",
            "--mirror-bearer-token", "token-value"
        ]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-user</c>/
    ///     <c>--mirror-password</c> pair supplied without <c>--mirror-url</c>, since the
    ///     credential would otherwise be silently ignored rather than applied anywhere.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_CredentialsWithoutMirrorUrl_ThrowsArgumentException()
    {
        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create([
            "list-models",
            "--mirror-user", "alice",
            "--mirror-password", "secret"
        ]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-bearer-token</c>
    ///     supplied without <c>--mirror-url</c>, since the token would otherwise be silently
    ///     ignored rather than applied anywhere.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_BearerTokenWithoutMirrorUrl_ThrowsArgumentException()
    {
        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create(["list-models", "--mirror-bearer-token", "token-value"]));
    }

    /// <summary>
    ///     Builds an environment reader over a fixed set of variables, so tests never mutate the process environment.
    /// </summary>
    private static Func<string, string?> FakeEnvironment(params (string Name, string Value)[] variables) =>
        name => variables.Where(v => v.Name == name).Select(v => v.Value).FirstOrDefault();

    /// <summary>
    ///     Test that <c>DEMASPEECH_MIRROR_URL</c> supplies the mirror URL when <c>--mirror-url</c> is absent.
    /// </summary>
    [Fact]
    public void Context_Create_EnvironmentMirrorUrl_UsedWhenOptionAbsent()
    {
        using var context = Context.Create(
            ["list-models"],
            FakeEnvironment(("DEMASPEECH_MIRROR_URL", "https://env.example.com")));

        Assert.Equal("https://env.example.com", context.MirrorUrl);
    }

    /// <summary>
    ///     Test that <c>--mirror-url</c> takes precedence over <c>DEMASPEECH_MIRROR_URL</c>.
    /// </summary>
    [Fact]
    public void Context_Create_MirrorUrlOption_OverridesEnvironment()
    {
        using var context = Context.Create(
            ["list-models", "--mirror-url", "https://cli.example.com"],
            FakeEnvironment(("DEMASPEECH_MIRROR_URL", "https://env.example.com")));

        Assert.Equal("https://cli.example.com", context.MirrorUrl);
    }

    /// <summary>
    ///     Test that the user and password variables supply the Basic credential when no credential option is given.
    /// </summary>
    [Fact]
    public void Context_Create_EnvironmentBasicCredentials_UsedWhenOptionsAbsent()
    {
        using var context = Context.Create(
            ["list-models"],
            FakeEnvironment(
                ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
                ("DEMASPEECH_MIRROR_USER", "alice"),
                ("DEMASPEECH_MIRROR_PASSWORD", "env-secret")));

        var mirror = context.CreateMirror();
        Assert.NotNull(mirror);
        Assert.Equal("alice", mirror.Credentials?.UserName);
        Assert.Equal("env-secret", mirror.Credentials?.Password);
    }

    /// <summary>
    ///     Test that <c>DEMASPEECH_MIRROR_BEARER_TOKEN</c> supplies the bearer token when no credential option is given.
    /// </summary>
    [Fact]
    public void Context_Create_EnvironmentBearerToken_UsedWhenOptionsAbsent()
    {
        using var context = Context.Create(
            ["list-models"],
            FakeEnvironment(
                ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
                ("DEMASPEECH_MIRROR_BEARER_TOKEN", "env-token")));

        var mirror = context.CreateMirror();
        Assert.NotNull(mirror);
        Assert.Equal("env-token", mirror.BearerToken);
    }

    /// <summary>
    ///     Test that any credential option disables every environment credential, so the two sources never mix.
    /// </summary>
    [Fact]
    public void Context_Create_CredentialOption_DisablesEnvironmentCredentials()
    {
        using var context = Context.Create(
            ["list-models", "--mirror-bearer-token", "cli-token"],
            FakeEnvironment(
                ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
                ("DEMASPEECH_MIRROR_USER", "alice"),
                ("DEMASPEECH_MIRROR_PASSWORD", "env-secret")));

        var mirror = context.CreateMirror();
        Assert.NotNull(mirror);
        Assert.Null(mirror.Credentials);
        Assert.Equal("cli-token", mirror.BearerToken);
    }

    /// <summary>
    ///     Test that an empty environment variable is treated as unset.
    /// </summary>
    [Fact]
    public void Context_Create_EmptyEnvironmentVariables_TreatedAsUnset()
    {
        using var context = Context.Create(
            ["list-models"],
            FakeEnvironment(
                ("DEMASPEECH_MIRROR_URL", ""),
                ("DEMASPEECH_MIRROR_USER", ""),
                ("DEMASPEECH_MIRROR_BEARER_TOKEN", "")));

        Assert.Null(context.CreateMirror());
    }

    /// <summary>
    ///     Test that environment credentials without any mirror URL are rejected, without echoing their values.
    /// </summary>
    [Fact]
    public void Context_Create_EnvironmentCredentialWithoutUrl_ThrowsWithoutEchoingSecret()
    {
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(
            ["list-models"],
            FakeEnvironment(("DEMASPEECH_MIRROR_BEARER_TOKEN", "super-secret-value"))));

        Assert.DoesNotContain("super-secret-value", exception.Message);
    }

    /// <summary>
    ///     Test that an invalid mirror URL error does not echo the rejected value.
    /// </summary>
    [Fact]
    public void Context_Create_InvalidMirrorUrl_ErrorDoesNotEchoValue()
    {
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(
            ["list-models"],
            FakeEnvironment(("DEMASPEECH_MIRROR_URL", "not a url secret-value"))));

        Assert.DoesNotContain("secret-value", exception.Message);
    }

    /// <summary>
    ///     Test that <c>--depth</c> accepts every value from 1 through 6 and stores it on the context.
    /// </summary>
    [Theory]
    [InlineData("1", 1)]
    [InlineData("6", 6)]
    public void Context_Create_DepthWithinRange_ParsesOntoContext(string value, int expected)
    {
        // Act
        using var context = Context.Create(["--validate", "--depth", value]);

        // Assert
        Assert.Equal(expected, context.HeadingDepth);
    }

    /// <summary>
    ///     Test that <c>--depth</c> rejects a value outside 1-6, a non-numeric value, and a missing value.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("-1")]
    [InlineData("deep")]
    public void Context_Create_DepthOutOfRangeOrNonNumeric_ThrowsArgumentException(string value)
    {
        // Act and Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--validate", "--depth", value]));
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test that <c>--depth</c> without a value is rejected.
    /// </summary>
    [Fact]
    public void Context_Create_DepthWithoutValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Context.Create(["--validate", "--depth"]));
    }

    /// <summary>
    ///     Test that <c>--verbose</c> and its alias <c>--diagnostics</c> enable diagnostics output,
    ///     and that diagnostics stay off by default.
    /// </summary>
    [Theory]
    [InlineData("--verbose", true)]
    [InlineData("--diagnostics", true)]
    [InlineData("", false)]
    public void Context_Create_VerboseOrDiagnosticsFlag_SetsDiagnostics(string flag, bool expected)
    {
        // Arrange
        string[] args = flag.Length == 0 ? ["list-devices"] : [flag, "list-devices"];

        // Act
        using var context = Context.Create(args);

        // Assert
        Assert.Equal(expected, context.Diagnostics);
    }

    /// <summary>
    ///     Test that <c>--verbose</c> after the subcommand is still a global option and is not
    ///     collected into the subcommand's arguments.
    /// </summary>
    [Fact]
    public void Context_Create_VerboseAfterCommand_IsRecognizedAsGlobalOption()
    {
        using var context = Context.Create(["list-devices", "--verbose"]);

        Assert.True(context.Diagnostics);
        Assert.Empty(context.CommandArgs);
    }

    /// <summary>
    ///     Test that <c>--no-emotions</c> is a global option recognized before or after any
    ///     subcommand (not only <c>voices install</c>), and that it is off by default.
    /// </summary>
    [Fact]
    public void Context_Create_NoEmotionsOnNonVoicesCommand_IsRecognizedAsGlobalOption()
    {
        // Act
        using var before = Context.Create(["--no-emotions", "list-devices"]);
        using var after = Context.Create(["list-devices", "--no-emotions"]);
        using var absent = Context.Create(["list-devices"]);

        // Assert
        Assert.True(before.NoEmotions);
        Assert.True(after.NoEmotions);
        Assert.Empty(after.CommandArgs);
        Assert.False(absent.NoEmotions);
    }

    /// <summary>
    ///     Test that <c>--results</c> and its alias <c>--result</c> store the results file path,
    ///     and that a missing value is rejected.
    /// </summary>
    [Fact]
    public void Context_Create_ResultsOption_ParsesPathAndRejectsMissingValue()
    {
        // Act
        using var results = Context.Create(["--validate", "--results", "out.trx"]);
        using var alias = Context.Create(["--validate", "--result", "out.xml"]);

        // Assert
        Assert.Equal("out.trx", results.ResultsFile);
        Assert.Equal("out.xml", alias.ResultsFile);
        Assert.Throws<ArgumentException>(() => Context.Create(["--validate", "--results"]));
    }
}
