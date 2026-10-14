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
        // Arrange: a command line with the subcommand first, then a global option
        string[] args = ["list-models", "--models-dir", "/tmp/models", "--format", "json"];

        // Act: parse the command line
        using var context = Context.Create(args);

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
    ///     Test that a <c>--voices-dir</c> naming a missing directory is rejected.
    /// </summary>
    [Fact]
    public void Context_Create_WithMissingVoicesDir_ThrowsArgumentException()
    {
        // Arrange: a directory path that does not exist
        var missing = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));

        // Act and Assert: creating the context rejects the missing directory
        Assert.Throws<ArgumentException>(() => Context.Create(["list-voices", "--voices-dir", missing]));
    }

    /// <summary>
    ///     Test that a <c>--voices-dir</c> lacking a value is rejected.
    /// </summary>
    [Fact]
    public void Context_Create_VoicesDirWithoutValue_ThrowsArgumentException()
    {
        // Arrange: a command line whose last option has no value
        string[] args = ["list-voices", "--voices-dir"];

        // Act and Assert: creating the context rejects the missing value
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }

    /// <summary>
    ///     Test that <c>--mirror-url</c> and the other <c>--mirror-*</c> global options parse onto
    ///     the <see cref="Context"/> regardless of where they appear relative to the subcommand.
    /// </summary>
    [Fact]
    public void Context_Create_WithMirrorOptions_ParsesOntoContext()
    {
        // Arrange: mirror options following the subcommand
        string[] args =
        [
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret"
        ];

        // Act: parse the command line
        using var context = Context.Create(args);

        // Assert: the three values are recorded and no bearer token is set
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
        // Arrange: a context with no mirror settings
        using var context = Context.Create(["list-models"]);

        // Act: create the mirror
        var mirror = context.CreateMirror();

        // Assert: no mirror is configured
        Assert.Null(mirror);
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> builds a <see cref="DownloadMirror"/> whose
    ///     base URI matches a valid <c>--mirror-url</c>, with Basic credentials attached when a
    ///     matching <c>--mirror-user</c>/<c>--mirror-password</c> pair is supplied.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_ValidCredentialedMirror_ReturnsConfiguredMirror()
    {
        // Arrange: a valid mirror URL with a user and password
        using var context = Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret"
        ]);

        // Act: create the mirror
        var mirror = context.CreateMirror();

        // Assert: the mirror carries the base URI and Basic credentials only
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
        // Arrange: a valid mirror URL with a bearer token only
        using var context = Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-bearer-token", "token-value"
        ]);

        // Act: create the mirror
        var mirror = context.CreateMirror();

        // Assert: the mirror carries the token and no Basic credentials
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
        // Arrange: a mirror URL that is not an absolute URL
        string[] args = ["list-models", "--mirror-url", "not a url"];

        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-url</c> carrying a
    ///     query string, fragment, or user-info component, or using a scheme other than http/https.
    /// </summary>
    [Theory]
    [InlineData("https://mirror.example.com/base?token=abc")]
    [InlineData("https://mirror.example.com/base#fragment")]
    [InlineData("https://user@mirror.example.com/base")]
    [InlineData("https://user:secret@mirror.example.com/base")]
    [InlineData("ftp://mirror.example.com/base")]
    public void Context_CreateMirror_UnsupportedMirrorUrlShape_ThrowsArgumentException(string mirrorUrl)
    {
        // Arrange: a mirror URL of an unsupported shape
        string[] args = ["list-models", "--mirror-url", mirrorUrl];

        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects an empty or whitespace-only bearer token.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_WhitespaceBearerToken_ThrowsArgumentException()
    {
        // Arrange: a valid mirror URL with a whitespace-only bearer token
        string[] args =
        [
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-bearer-token", "   "
        ];

        // Act and Assert
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects Basic credentials combined with a
    ///     plain <c>http</c> mirror on a non-loopback host, since the credential would be sent in
    ///     cleartext.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_BasicCredentialsOverPlainHttp_ThrowsArgumentException()
    {
        // Arrange: Basic credentials with a non-loopback http mirror
        string[] args =
        [
            "list-models",
            "--mirror-url", "http://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret"
        ];

        // Act and Assert
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a bearer token combined with a
    ///     plain <c>http</c> mirror on a non-loopback host, since the token would be sent in cleartext.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_BearerTokenOverPlainHttp_ThrowsArgumentException()
    {
        // Arrange: a bearer token with a non-loopback http mirror
        string[] args =
        [
            "list-models",
            "--mirror-url", "http://mirror.example.com",
            "--mirror-bearer-token", "token-value"
        ];

        // Act and Assert
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> accepts a credential combined with a plain
    ///     <c>http</c> mirror on a loopback host.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_BearerTokenOverPlainHttpLoopback_ReturnsMirror()
    {
        // Arrange: a bearer token with a loopback http mirror
        using var loopback = Context.Create([
            "list-models",
            "--mirror-url", "http://localhost:8080",
            "--mirror-bearer-token", "token-value"
        ]);

        // Act: create the mirror
        var mirror = loopback.CreateMirror();

        // Assert: the loopback mirror is accepted
        Assert.NotNull(mirror);
    }

    /// <summary>
    ///     Test that an unsupported argument produces an operator-facing message without the internal
    ///     <c>(Parameter ...)</c> suffix that a parameter name would append.
    /// </summary>
    [Fact]
    public void Context_Create_UnsupportedArgument_MessageHasNoParameterSuffix()
    {
        // Arrange: an argument that is neither a known option nor a subcommand
        string[] args = ["bogus-command"];

        // Act: attempt to create the context
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(args));

        // Assert: the message names the argument and carries no parameter name
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
        // Arrange: an option that requires a value, given none
        string[] args = ["--models-dir"];

        // Act: attempt to create the context
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(args));

        // Assert: the message carries no parameter suffix or name
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
        // Arrange: an error writer whose writes fail, and the original console color
        var originalError = Console.Error;
        var originalColor = Console.ForegroundColor;
        try
        {
            Console.SetError(new ThrowingWriter());
            using var context = Context.Create(["--version"]);

            // Act: write an error, which throws from the writer
            Assert.Throws<IOException>(() => context.WriteError("boom"));

            // Assert: the foreground color was restored
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
        // Arrange: a help request carrying a mirror user but no mirror URL
        string[] args = ["--help", "--mirror-user", "alice"];

        // Act and Assert
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-user</c> supplied
    ///     without the matching <c>--mirror-password</c>.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_MirrorUserWithoutPassword_ThrowsArgumentException()
    {
        // Arrange: a valid mirror URL with a user and no password
        string[] args =
        [
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice"
        ];

        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create(args));
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
        // Arrange: a valid mirror URL with Basic credentials and a bearer token
        string[] args =
        [
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret",
            "--mirror-bearer-token", "token-value"
        ];

        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-user</c>/
    ///     <c>--mirror-password</c> pair supplied without <c>--mirror-url</c>, since the
    ///     credential would otherwise be silently ignored rather than applied anywhere.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_CredentialsWithoutMirrorUrl_ThrowsArgumentException()
    {
        // Arrange: a user and password with no mirror URL
        string[] args =
        [
            "list-models",
            "--mirror-user", "alice",
            "--mirror-password", "secret"
        ];

        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-bearer-token</c>
    ///     supplied without <c>--mirror-url</c>, since the token would otherwise be silently
    ///     ignored rather than applied anywhere.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_BearerTokenWithoutMirrorUrl_ThrowsArgumentException()
    {
        // Arrange: a bearer token with no mirror URL
        string[] args = ["list-models", "--mirror-bearer-token", "token-value"];

        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create(args));
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
        // Arrange: the mirror URL variable is set and the option is absent
        var environment = FakeEnvironment(("DEMASPEECH_MIRROR_URL", "https://env.example.com"));

        // Act: parse the command line
        using var context = Context.Create(["list-models"], environment);

        // Assert: the variable supplies the URL
        Assert.Equal("https://env.example.com", context.MirrorUrl);
    }

    /// <summary>
    ///     Test that <c>--mirror-url</c> takes precedence over <c>DEMASPEECH_MIRROR_URL</c>.
    /// </summary>
    [Fact]
    public void Context_Create_MirrorUrlOption_OverridesEnvironment()
    {
        // Arrange: both the option and the variable are present
        var environment = FakeEnvironment(("DEMASPEECH_MIRROR_URL", "https://env.example.com"));

        // Act: parse the command line
        using var context = Context.Create(["list-models", "--mirror-url", "https://cli.example.com"], environment);

        // Assert: the option wins
        Assert.Equal("https://cli.example.com", context.MirrorUrl);
    }

    /// <summary>
    ///     Test that the user and password variables supply the Basic credential when no credential option is given.
    /// </summary>
    [Fact]
    public void Context_Create_EnvironmentBasicCredentials_UsedWhenOptionsAbsent()
    {
        // Arrange: the URL, user and password variables are set and no credential option is given
        var environment = FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
            ("DEMASPEECH_MIRROR_USER", "alice"),
            ("DEMASPEECH_MIRROR_PASSWORD", "env-secret"));
        using var context = Context.Create(["list-models"], environment);

        // Act: create the mirror
        var mirror = context.CreateMirror();

        // Assert: the mirror carries the variables' Basic credentials
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
        // Arrange: the URL and bearer-token variables are set and no credential option is given
        var environment = FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
            ("DEMASPEECH_MIRROR_BEARER_TOKEN", "env-token"));
        using var context = Context.Create(["list-models"], environment);

        // Act: create the mirror
        var mirror = context.CreateMirror();

        // Assert: the mirror carries the variable's token
        Assert.NotNull(mirror);
        Assert.Equal("env-token", mirror.BearerToken);
    }

    /// <summary>
    ///     Test that any credential option disables every environment credential, so the two sources never mix.
    /// </summary>
    [Fact]
    public void Context_Create_CredentialOption_DisablesEnvironmentCredentials()
    {
        // Arrange: a bearer-token option while user and password variables are set
        var environment = FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
            ("DEMASPEECH_MIRROR_USER", "alice"),
            ("DEMASPEECH_MIRROR_PASSWORD", "env-secret"));
        using var context = Context.Create(["list-models", "--mirror-bearer-token", "cli-token"], environment);

        // Act: create the mirror
        var mirror = context.CreateMirror();

        // Assert: only the option's token is used
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
        // Arrange: the URL, user and bearer-token variables are all empty
        var environment = FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", ""),
            ("DEMASPEECH_MIRROR_USER", ""),
            ("DEMASPEECH_MIRROR_BEARER_TOKEN", ""));
        using var context = Context.Create(["list-models"], environment);

        // Act: create the mirror
        var mirror = context.CreateMirror();

        // Assert: no mirror is configured
        Assert.Null(mirror);
    }

    /// <summary>
    ///     Test that environment credentials without any mirror URL are rejected, without echoing their values.
    /// </summary>
    [Fact]
    public void Context_Create_EnvironmentCredentialWithoutUrl_ThrowsWithoutEchoingSecret()
    {
        // Arrange: only the bearer-token variable is set
        var environment = FakeEnvironment(("DEMASPEECH_MIRROR_BEARER_TOKEN", "super-secret-value"));

        // Act: attempt to create the context
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["list-models"], environment));

        // Assert: the error does not echo the secret
        Assert.DoesNotContain("super-secret-value", exception.Message);
    }

    /// <summary>
    ///     Test that an invalid mirror URL error does not echo the rejected value.
    /// </summary>
    [Fact]
    public void Context_Create_InvalidMirrorUrl_ErrorDoesNotEchoValue()
    {
        // Arrange: the URL variable holds an invalid value containing a marker
        var environment = FakeEnvironment(("DEMASPEECH_MIRROR_URL", "not a url secret-value"));

        // Act: attempt to create the context
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["list-models"], environment));

        // Assert: the error does not echo the rejected value
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
        // Arrange: a validation command line with an in-range depth
        string[] args = ["--validate", "--depth", value];

        // Act: parse the command line
        using var context = Context.Create(args);

        // Assert: the depth is recorded
        Assert.Equal(expected, context.HeadingDepth);
    }

    /// <summary>
    ///     Test that <c>--depth</c> rejects a value outside 1-6 or a non-numeric value.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("-1")]
    [InlineData("deep")]
    public void Context_Create_DepthOutOfRangeOrNonNumeric_ThrowsArgumentException(string value)
    {
        // Arrange: a validation command line with an invalid depth
        string[] args = ["--validate", "--depth", value];

        // Act: attempt to create the context
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(args));

        // Assert: the message names the option
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test that <c>--depth</c> without a value is rejected.
    /// </summary>
    [Fact]
    public void Context_Create_DepthWithoutValue_ThrowsArgumentException()
    {
        // Arrange: --depth as the last argument
        string[] args = ["--validate", "--depth"];

        // Act and Assert
        Assert.Throws<ArgumentException>(() => Context.Create(args));
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
        // Arrange: the verbose flag follows the subcommand
        string[] args = ["list-devices", "--verbose"];

        // Act: parse the command line
        using var context = Context.Create(args);

        // Assert: diagnostics are enabled and the flag is not collected into the subcommand arguments
        Assert.True(context.Diagnostics);
        Assert.Empty(context.CommandArgs);
    }

    /// <summary>
    ///     Test that <c>--no-emotions</c> before the subcommand is recognized as a global option.
    /// </summary>
    [Fact]
    public void Context_Create_NoEmotionsBeforeCommand_IsRecognizedAsGlobalOption()
    {
        // Arrange: the flag precedes a non-voices subcommand
        string[] args = ["--no-emotions", "list-devices"];

        // Act: parse the command line
        using var context = Context.Create(args);

        // Assert: the flag is recorded
        Assert.True(context.NoEmotions);
    }

    /// <summary>
    ///     Test that <c>--no-emotions</c> after a non-voices subcommand is recognized as a global
    ///     option and not collected into the subcommand's arguments.
    /// </summary>
    [Fact]
    public void Context_Create_NoEmotionsOnNonVoicesCommand_IsRecognizedAsGlobalOption()
    {
        // Arrange: the flag follows a non-voices subcommand
        string[] args = ["list-devices", "--no-emotions"];

        // Act: parse the command line
        using var context = Context.Create(args);

        // Assert: the flag is recorded and not left in CommandArgs
        Assert.True(context.NoEmotions);
        Assert.Empty(context.CommandArgs);
    }

    /// <summary>
    ///     Test that <c>--no-emotions</c> is off by default.
    /// </summary>
    [Fact]
    public void Context_Create_NoEmotionsAbsent_DefaultsToOff()
    {
        // Arrange: a command line without the flag
        string[] args = ["list-devices"];

        // Act: parse the command line
        using var context = Context.Create(args);

        // Assert: the flag is off
        Assert.False(context.NoEmotions);
    }

    /// <summary>
    ///     Test that <c>--results</c> stores the results file path.
    /// </summary>
    [Fact]
    public void Context_Create_ResultsOption_ParsesPath()
    {
        // Arrange: a validation command line with --results
        string[] args = ["--validate", "--results", "out.trx"];

        // Act: parse the command line
        using var results = Context.Create(args);

        // Assert: the path is recorded
        Assert.Equal("out.trx", results.ResultsFile);
    }

    /// <summary>
    ///     Test that the <c>--result</c> alias stores the results file path.
    /// </summary>
    [Fact]
    public void Context_Create_ResultAlias_ParsesPath()
    {
        // Arrange: a validation command line with the alias
        string[] args = ["--validate", "--result", "out.xml"];

        // Act: parse the command line
        using var alias = Context.Create(args);

        // Assert: the path is recorded
        Assert.Equal("out.xml", alias.ResultsFile);
    }

    /// <summary>
    ///     Test that <c>--results</c> without a value is rejected.
    /// </summary>
    [Fact]
    public void Context_Create_ResultsOptionWithoutValue_ThrowsArgumentException()
    {
        // Arrange: --results as the last argument
        string[] args = ["--validate", "--results"];

        // Act and Assert
        Assert.Throws<ArgumentException>(() => Context.Create(args));
    }
}
