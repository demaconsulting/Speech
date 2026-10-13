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
///     Unit tests for <see cref="AppLaunchOptions"/>.
/// </summary>
/// <remarks>
///     Every test parses through an injected environment reader (empty unless the test supplies
///     variables), so no test depends on, or is broken by, a developer's ambient
///     <c>DEMASPEECH_MIRROR_*</c> variables. The one test of the arguments-only overload clears
///     those variables for its duration, which is why the class joins the non-parallel
///     <see cref="ProcessWideStateCollection"/>.
/// </remarks>
[Collection(ProcessWideStateCollection.Name)]
public sealed class AppLaunchOptionsTests
{
    /// <summary>
    ///     Builds an environment reader over the given variables; every other name reads as unset.
    /// </summary>
    /// <param name="variables">The variables the fake process environment contains.</param>
    /// <returns>A reader suitable for <see cref="AppLaunchOptions.Parse(string[], Func{string, string?})"/>.</returns>
    private static Func<string, string?> FakeEnvironment(params (string Name, string Value)[] variables) =>
        name => variables.Where(v => v.Name == name).Select(v => v.Value).FirstOrDefault();

    /// <summary>
    ///     Parses <paramref name="args"/> against an empty fake environment.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The parsed options.</returns>
    private static AppLaunchOptions Parse(params string[] args) => AppLaunchOptions.Parse(args, FakeEnvironment());

    /// <summary>
    ///     Creates a fresh scratch directory under the test output folder.
    /// </summary>
    /// <returns>The full path of the new directory.</returns>
    private static string CreateScratchDirectory() =>
        Directory.CreateDirectory(Path.Join(AppContext.BaseDirectory, "AppLaunchOptionsTests", Guid.NewGuid().ToString("N"))).FullName;

    /// <summary>
    ///     Test that parsing an empty argument list returns an options instance with every
    ///     setting at its default (unset) value.
    /// </summary>
    [Fact]
    public void Parse_NoArguments_ReturnsAllDefaults()
    {
        // Arrange: no arguments

        // Act
        var options = Parse();

        // Assert
        Assert.Null(options.ModelsDir);
        Assert.Null(options.VoicesDir);
        Assert.Null(options.MirrorUrl);
        Assert.Null(options.MirrorUser);
        Assert.Null(options.MirrorPassword);
        Assert.Null(options.MirrorBearerToken);
        Assert.False(options.Help);
    }

    /// <summary>
    ///     Test that <c>--help</c>, <c>-h</c>, and <c>-?</c> all set <see cref="AppLaunchOptions.Help"/>.
    /// </summary>
    /// <param name="flag">The help flag under test.</param>
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public void Parse_HelpFlag_SetsHelp(string flag)
    {
        // Arrange: the help flag is supplied by the theory data

        // Act
        var options = Parse(flag);

        // Assert
        Assert.True(options.Help);
    }

    /// <summary>
    ///     Test that <c>--models-dir</c>, <c>--voices-dir</c>, and every <c>--mirror-*</c> option
    ///     parse onto their corresponding settings.
    /// </summary>
    [Fact]
    public void Parse_AllOptions_ParsesOntoProperties()
    {
        // Arrange
        var voices = CreateScratchDirectory();

        try
        {
            // Act
            var options = Parse(
                "--models-dir", "/tmp/models",
                "--voices-dir", voices,
                "--mirror-url", "https://mirror.example.com",
                "--mirror-user", "alice",
                "--mirror-password", "secret",
                "--mirror-bearer-token", "token-value");

            // Assert
            Assert.Equal("/tmp/models", options.ModelsDir);
            Assert.Equal(voices, options.VoicesDir);
            Assert.Equal("https://mirror.example.com", options.MirrorUrl);
            Assert.Equal("alice", options.MirrorUser);
            Assert.Equal("secret", options.MirrorPassword);
            Assert.Equal("token-value", options.MirrorBearerToken);
        }
        finally
        {
            Directory.Delete(voices);
        }
    }

    /// <summary>
    ///     Test that an unrecognized argument throws a clean <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Parse_UnrecognizedArgument_ThrowsArgumentException()
    {
        // Arrange
        const string unknown = "--not-a-real-option";

        // Act
        var exception = Assert.Throws<ArgumentException>(() => Parse(unknown));

        // Assert
        Assert.Contains("Unrecognized argument", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the error for an unrecognized argument never echoes the argument itself,
    ///     because a mistyped <c>--mirror-*=value</c> form would otherwise leak the secret.
    /// </summary>
    [Fact]
    public void Parse_UnrecognizedArgument_ErrorDoesNotEchoArgument()
    {
        // Arrange
        const string secret = "hunter2";
        var mistypedArgument = "--mirror-password" + "=" + secret;

        // Act
        var exception = Assert.Throws<ArgumentException>(() => Parse("--models-dir", "/tmp/models", mistypedArgument));

        // Assert
        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("--mirror-password", exception.Message, StringComparison.Ordinal);
        Assert.Contains("position 3", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a help flag keeps the help request even when <c>--voices-dir</c> names a
    ///     missing folder, so asking for usage always succeeds.
    /// </summary>
    [Fact]
    public void Parse_HelpWithMissingVoicesDir_StillReportsHelp()
    {
        // Arrange
        var missing = Path.Join(AppContext.BaseDirectory, Guid.NewGuid().ToString("N"));

        // Act
        var options = Parse("--voices-dir", missing, "--help");

        // Assert
        Assert.True(options.Help);
    }

    /// <summary>
    ///     Test that a repeated option keeps its last value.
    /// </summary>
    [Fact]
    public void Parse_RepeatedOption_LastValueWins()
    {
        // Arrange: the same option is supplied twice

        // Act
        var options = Parse("--models-dir", "/first", "--models-dir", "/second");

        // Assert
        Assert.Equal("/second", options.ModelsDir);
    }

    /// <summary>
    ///     Test that the one-argument overload, which reads the process environment, parses a help
    ///     flag the same way as the injected-environment overload. The ambient
    ///     <c>DEMASPEECH_MIRROR_*</c> variables are cleared for the call and restored afterward, so
    ///     the result cannot depend on the developer's environment.
    /// </summary>
    [Fact]
    public void Parse_ArgumentsOnlyOverload_ParsesHelpFlag()
    {
        // Arrange: clear and remember the ambient mirror variables
        var names = new[]
        {
            AppLaunchOptions.MirrorUrlEnvironmentVariable,
            AppLaunchOptions.MirrorUserEnvironmentVariable,
            AppLaunchOptions.MirrorPasswordEnvironmentVariable,
            AppLaunchOptions.MirrorBearerTokenEnvironmentVariable,
        };
        var saved = names.Select(Environment.GetEnvironmentVariable).ToArray();
        foreach (var name in names)
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        try
        {
            // Act
            var options = AppLaunchOptions.Parse(["--help"]);

            // Assert
            Assert.True(options.Help);
            Assert.Null(options.MirrorUrl);
            Assert.Null(options.MirrorUser);
            Assert.Null(options.MirrorPassword);
            Assert.Null(options.MirrorBearerToken);
        }
        finally
        {
            for (var i = 0; i < names.Length; i++)
            {
                Environment.SetEnvironmentVariable(names[i], saved[i]);
            }
        }
    }

    /// <summary>
    ///     Test that an empty <c>--mirror-url</c> option is treated as unset, so the environment variable applies.
    /// </summary>
    [Fact]
    public void Parse_EmptyMirrorUrlOption_FallsBackToEnvironment()
    {
        // Arrange
        var environment = FakeEnvironment(("DEMASPEECH_MIRROR_URL", "https://env.example.com"));

        // Act
        var options = AppLaunchOptions.Parse(["--mirror-url", ""], environment);

        // Assert
        Assert.Equal("https://env.example.com", options.MirrorUrl);
    }

    /// <summary>
    ///     Test that an empty credential option does not count as a credential on the command
    ///     line, so the environment credentials still apply.
    /// </summary>
    [Fact]
    public void Parse_EmptyCredentialOption_DoesNotDisableEnvironmentCredentials()
    {
        // Arrange
        var environment = FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
            ("DEMASPEECH_MIRROR_BEARER_TOKEN", "env-token"));

        // Act
        var options = AppLaunchOptions.Parse(["--mirror-user", ""], environment);

        // Assert
        Assert.Null(options.MirrorUser);
        Assert.Equal("env-token", options.MirrorBearerToken);
    }

    /// <summary>
    ///     Test that a Basic username on the command line suppresses the environment password, so
    ///     the two sources are never mixed and the incomplete pair is rejected.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_CommandLineUserWithEnvironmentPassword_IsRejectedNotMixed()
    {
        // Arrange
        var options = AppLaunchOptions.Parse(
            ["--mirror-url", "https://mirror.example.com", "--mirror-user", "alice"],
            FakeEnvironment(("DEMASPEECH_MIRROR_PASSWORD", "env-secret")));

        // Act
        var exception = Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());

        // Assert
        Assert.Null(options.MirrorPassword);
        Assert.DoesNotContain("env-secret", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that an environment user without a password is rejected without echoing either value.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_EnvironmentUserWithoutPassword_ThrowsWithoutEchoingUser()
    {
        // Arrange
        var options = AppLaunchOptions.Parse([], FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
            ("DEMASPEECH_MIRROR_USER", "env-user-name")));

        // Act
        var exception = Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());

        // Assert
        Assert.DoesNotContain("env-user-name", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that supplying both Basic credentials and a bearer token is rejected without echoing a secret.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_BasicCredentialsAndBearerToken_ThrowsWithoutEchoingSecrets()
    {
        // Arrange
        var options = Parse(
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "basic-secret",
            "--mirror-bearer-token", "token-secret");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());

        // Assert
        Assert.DoesNotContain("basic-secret", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("token-secret", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a Basic credential configured for a plain-HTTP remote mirror is rejected without echoing the password.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_BasicCredentialsOverPlainHttp_ThrowsWithoutEchoingPassword()
    {
        // Arrange
        var options = Parse(
            "--mirror-url", "http://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "basic-secret");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());

        // Assert
        Assert.DoesNotContain("basic-secret", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that every value-taking option supplied as the last argument (with no value
    ///     following it) throws a clean <see cref="ArgumentException"/> naming the option.
    /// </summary>
    /// <param name="option">The value-taking option under test.</param>
    [Theory]
    [InlineData("--models-dir")]
    [InlineData("--voices-dir")]
    [InlineData("--mirror-url")]
    [InlineData("--mirror-user")]
    [InlineData("--mirror-password")]
    [InlineData("--mirror-bearer-token")]
    public void Parse_OptionMissingValue_ThrowsArgumentException(string option)
    {
        // Arrange: the option is the final argument

        // Act
        var exception = Assert.Throws<ArgumentException>(() => Parse(option));

        // Assert
        Assert.Contains(option + " requires a value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Regression test: a value-taking option followed by another recognized option is
    ///     rejected rather than consuming that option as its value (for example
    ///     <c>--mirror-url --help</c> previously set the mirror URL to <c>--help</c> and dropped
    ///     the help request).
    /// </summary>
    /// <param name="option">The value-taking option under test.</param>
    /// <param name="next">The recognized option that follows it.</param>
    [Theory]
    [InlineData("--models-dir", "--help")]
    [InlineData("--voices-dir", "--help")]
    [InlineData("--mirror-url", "--help")]
    [InlineData("--mirror-user", "--help")]
    [InlineData("--mirror-password", "--help")]
    [InlineData("--mirror-bearer-token", "--help")]
    [InlineData("--mirror-url", "-h")]
    [InlineData("--mirror-url", "-?")]
    [InlineData("--models-dir", "--voices-dir")]
    [InlineData("--mirror-user", "--mirror-password")]
    [InlineData("--mirror-password", "--mirror-bearer-token")]
    [InlineData("--mirror-bearer-token", "--mirror-url")]
    public void Parse_ValueOptionFollowedByRecognizedOption_ThrowsArgumentException(string option, string next)
    {
        // Arrange: the option and the option following it come from the theory data

        // Act
        var exception = Assert.Throws<ArgumentException>(() => Parse(option, next));

        // Assert
        Assert.Contains(option + " requires a value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a value-taking option followed by a recognized option in its
    ///     <c>--option=value</c> spelling is also rejected, without echoing the following argument
    ///     (which may be a secret).
    /// </summary>
    [Fact]
    public void Parse_ValueOptionFollowedByLongFormOption_ThrowsArgumentException()
    {
        // Arrange
        const string secret = "hunter2";
        var following = "--mirror-password" + "=" + secret;

        // Act
        var exception = Assert.Throws<ArgumentException>(() => Parse("--mirror-user", following));

        // Assert
        Assert.Contains("--mirror-user requires a value", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secret, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a value beginning with a hyphen that is not a recognized option (a negative
    ///     number, or a path or secret that happens to start with <c>-</c>) is accepted as the value.
    /// </summary>
    /// <param name="option">The value-taking option under test.</param>
    /// <param name="value">The hyphen-leading value.</param>
    [Theory]
    [InlineData("--models-dir", "-models")]
    [InlineData("--mirror-user", "-alice")]
    [InlineData("--mirror-password", "-1")]
    [InlineData("--mirror-password", "--not-an-option")]
    [InlineData("--mirror-bearer-token", "-")]
    public void Parse_ValueOptionFollowedByNonOptionHyphenValue_AcceptsValue(string option, string value)
    {
        // Arrange: the option and its hyphen-leading value come from the theory data

        // Act
        var options = Parse(option, value);

        // Assert
        var actual = option switch
        {
            "--models-dir" => options.ModelsDir,
            "--mirror-user" => options.MirrorUser,
            "--mirror-password" => options.MirrorPassword,
            _ => options.MirrorBearerToken,
        };
        Assert.Equal(value, actual);
    }

    /// <summary>
    ///     Test that a null argument array is rejected.
    /// </summary>
    [Fact]
    public void Parse_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange
        var environment = FakeEnvironment();

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => AppLaunchOptions.Parse(null!, environment));

        // Assert
        Assert.Equal("args", exception.ParamName);
    }

    /// <summary>
    ///     Test that a null environment reader is rejected.
    /// </summary>
    [Fact]
    public void Parse_NullEnvironmentReader_ThrowsArgumentNullException()
    {
        // Arrange: a valid argument list and a null reader

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => AppLaunchOptions.Parse([], null!));

        // Assert
        Assert.Equal("getEnvironmentVariable", exception.ParamName);
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateStoreOptions"/> returns <see langword="null"/>
    ///     when <c>--models-dir</c> was not supplied.
    /// </summary>
    [Fact]
    public void CreateStoreOptions_NoModelsDir_ReturnsNull()
    {
        // Arrange
        var options = Parse();

        // Act
        var storeOptions = options.CreateStoreOptions();

        // Assert
        Assert.Null(storeOptions);
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateStoreOptions"/> resolves <c>--models-dir</c>
    ///     into a matching <c>RootPathOverride</c>.
    /// </summary>
    [Fact]
    public void CreateStoreOptions_WithModelsDir_ReturnsMatchingRootPathOverride()
    {
        // Arrange
        var options = Parse("--models-dir", "/tmp/models");

        // Act
        var storeOptions = options.CreateStoreOptions();

        // Assert
        Assert.NotNull(storeOptions);
        Assert.Equal("/tmp/models", storeOptions.RootPathOverride);
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> returns
    ///     <see langword="null"/> when <c>--mirror-url</c> was not supplied.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_NoMirrorUrl_ReturnsNull()
    {
        // Arrange
        var options = Parse();

        // Act
        var downloaderOptions = options.CreateDownloaderOptions();

        // Assert
        Assert.Null(downloaderOptions);
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> builds a mirror with
    ///     Basic credentials attached when a matching user/password pair is supplied.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_ValidCredentialedMirror_ReturnsConfiguredMirror()
    {
        // Arrange
        var options = Parse(
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret");

        // Act
        var downloaderOptions = options.CreateDownloaderOptions();

        // Assert
        Assert.NotNull(downloaderOptions);
        Assert.Equal(new Uri("https://mirror.example.com"), downloaderOptions.Mirror?.BaseUri);
        Assert.Equal("alice", downloaderOptions.Mirror?.Credentials?.UserName);
        Assert.Equal("secret", downloaderOptions.Mirror?.Credentials?.Password);
        Assert.Null(downloaderOptions.Mirror?.BearerToken);
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> builds a
    ///     bearer-token-authenticated mirror when only <c>--mirror-bearer-token</c> is supplied.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_BearerTokenMirror_ReturnsConfiguredMirror()
    {
        // Arrange
        var options = Parse(
            "--mirror-url", "https://mirror.example.com",
            "--mirror-bearer-token", "token-value");

        // Act
        var downloaderOptions = options.CreateDownloaderOptions();

        // Assert
        Assert.NotNull(downloaderOptions);
        Assert.Null(downloaderOptions.Mirror?.Credentials);
        Assert.Equal("token-value", downloaderOptions.Mirror?.BearerToken);
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> rejects a
    ///     <c>--mirror-url</c> that is not a valid absolute URL.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_InvalidMirrorUrl_ThrowsArgumentException()
    {
        // Arrange
        var options = Parse("--mirror-url", "not a url");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());

        // Assert
        Assert.Contains("not a valid absolute URL", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> rejects a
    ///     <c>--mirror-user</c> supplied without the matching <c>--mirror-password</c>.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_MirrorUserWithoutPassword_ThrowsArgumentException()
    {
        // Arrange
        var options = Parse(
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());

        // Assert
        Assert.Contains("both be supplied", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> rejects a
    ///     <c>--mirror-password</c> supplied without the matching <c>--mirror-user</c>.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_MirrorPasswordWithoutUser_ThrowsArgumentException()
    {
        // Arrange
        var options = Parse(
            "--mirror-url", "https://mirror.example.com",
            "--mirror-password", "secret");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());

        // Assert
        Assert.Contains("both be supplied", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> rejects
    ///     <c>--mirror-user</c>/<c>--mirror-password</c> supplied without the required
    ///     <c>--mirror-url</c>, rather than silently discarding the stray credentials, matching
    ///     the CLI's own <c>Context.CreateMirror</c> behavior for the same case.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_MirrorCredentialsWithoutUrl_ThrowsArgumentException()
    {
        // Arrange
        var options = Parse(
            "--mirror-user", "alice",
            "--mirror-password", "secret");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());

        // Assert
        Assert.Contains("require a mirror URL", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> rejects a
    ///     <c>--mirror-bearer-token</c> supplied without the required <c>--mirror-url</c>.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_MirrorBearerTokenWithoutUrl_ThrowsArgumentException()
    {
        // Arrange
        var options = Parse("--mirror-bearer-token", "token-value");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());

        // Assert
        Assert.Contains("require a mirror URL", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that <c>--voices-dir</c> with an existing directory is captured in
    ///     <see cref="AppLaunchOptions.VoicesDir"/>.
    /// </summary>
    [Fact]
    public void Parse_VoicesDir_ExistingDirectory_SetsVoicesDir()
    {
        // Arrange
        var voices = CreateScratchDirectory();

        try
        {
            // Act
            var options = Parse("--voices-dir", voices);

            // Assert
            Assert.Equal(voices, options.VoicesDir);
        }
        finally
        {
            Directory.Delete(voices);
        }
    }

    /// <summary>
    ///     Test that <c>--voices-dir</c> naming a missing directory fails fast.
    /// </summary>
    [Fact]
    public void Parse_VoicesDir_MissingDirectory_ThrowsArgumentException()
    {
        // Arrange
        var missing = Path.Join(AppContext.BaseDirectory, Guid.NewGuid().ToString("N"));

        // Act
        var exception = Assert.Throws<ArgumentException>(() => Parse("--voices-dir", missing));

        // Assert
        Assert.Contains("--voices-dir", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that <c>--voices-dir</c> as the last argument is rejected for lacking a value.
    /// </summary>
    [Fact]
    public void Parse_VoicesDir_NoValue_ThrowsArgumentException()
    {
        // Arrange: --voices-dir is the final argument

        // Act
        var exception = Assert.Throws<ArgumentException>(() => Parse("--voices-dir"));

        // Assert
        Assert.Contains("--voices-dir requires a value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the help text documents <c>--voices-dir</c>.
    /// </summary>
    [Fact]
    public void PrintHelp_ListsVoicesDirOption()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        AppLaunchOptions.PrintHelp(writer);

        // Assert
        Assert.Contains("--voices-dir <path>", writer.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the help text documents every help flag and the bearer-token option legibly.
    /// </summary>
    [Fact]
    public void PrintHelp_ListsHelpFlagsAndBearerTokenOption()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        AppLaunchOptions.PrintHelp(writer);

        // Assert
        var output = writer.ToString();
        Assert.Contains("-h, -?, --help", output, StringComparison.Ordinal);
        Assert.Contains("--mirror-bearer-token <t>", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a null help writer is rejected.
    /// </summary>
    [Fact]
    public void PrintHelp_NullWriter_ThrowsArgumentNullException()
    {
        // Arrange: a null writer

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => AppLaunchOptions.PrintHelp(null!));

        // Assert
        Assert.Equal("writer", exception.ParamName);
    }

    /// <summary>
    ///     Test that the parameterless <c>PrintHelp()</c> overload writes the same usage text to
    ///     the console's standard output.
    /// </summary>
    /// <remarks>
    ///     Redirects the process-wide console, so this class runs in the non-parallel
    ///     <see cref="ProcessWideStateCollection"/>; the console is restored even on failure.
    /// </remarks>
    [Fact]
    public void PrintHelp_NoWriter_WritesUsageToConsoleOutput()
    {
        // Arrange: capture the console's standard output
        var original = Console.Out;
        using var captured = new StringWriter();
        Console.SetOut(captured);

        try
        {
            // Act
            AppLaunchOptions.PrintHelp();
        }
        finally
        {
            Console.SetOut(original);
        }

        // Assert: the console received exactly the text the writer overload produces
        using var expected = new StringWriter();
        AppLaunchOptions.PrintHelp(expected);
        Assert.Equal(expected.ToString(), captured.ToString());
    }

    /// <summary>Test that the mirror URL environment variable applies when <c>--mirror-url</c> is absent.</summary>
    [Fact]
    public void Parse_EnvironmentMirrorUrl_UsedWhenOptionAbsent()
    {
        // Arrange
        var environment = FakeEnvironment(("DEMASPEECH_MIRROR_URL", "https://env.example.com"));

        // Act
        var options = AppLaunchOptions.Parse([], environment);

        // Assert
        Assert.Equal("https://env.example.com", options.MirrorUrl);
    }

    /// <summary>Test that <c>--mirror-url</c> overrides the environment variable.</summary>
    [Fact]
    public void Parse_MirrorUrlOption_OverridesEnvironment()
    {
        // Arrange
        var environment = FakeEnvironment(("DEMASPEECH_MIRROR_URL", "https://env.example.com"));

        // Act
        var options = AppLaunchOptions.Parse(["--mirror-url", "https://cli.example.com"], environment);

        // Assert
        Assert.Equal("https://cli.example.com", options.MirrorUrl);
    }

    /// <summary>Test that the user and password variables supply the Basic credential.</summary>
    [Fact]
    public void Parse_EnvironmentBasicCredentials_UsedWhenOptionsAbsent()
    {
        // Arrange
        var environment = FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
            ("DEMASPEECH_MIRROR_USER", "alice"),
            ("DEMASPEECH_MIRROR_PASSWORD", "env-secret"));

        // Act
        var options = AppLaunchOptions.Parse([], environment);
        var downloaderOptions = options.CreateDownloaderOptions();

        // Assert
        Assert.Equal("alice", downloaderOptions?.Mirror?.Credentials?.UserName);
        Assert.Equal("env-secret", downloaderOptions?.Mirror?.Credentials?.Password);
    }

    /// <summary>Test that the bearer token variable supplies the bearer token.</summary>
    [Fact]
    public void Parse_EnvironmentBearerToken_UsedWhenOptionsAbsent()
    {
        // Arrange
        var environment = FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
            ("DEMASPEECH_MIRROR_BEARER_TOKEN", "env-token"));

        // Act
        var options = AppLaunchOptions.Parse([], environment);

        // Assert
        Assert.Equal("env-token", options.CreateDownloaderOptions()?.Mirror?.BearerToken);
    }

    /// <summary>Test that any credential option disables every environment credential.</summary>
    [Fact]
    public void Parse_CredentialOption_DisablesEnvironmentCredentials()
    {
        // Arrange
        var environment = FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
            ("DEMASPEECH_MIRROR_USER", "alice"),
            ("DEMASPEECH_MIRROR_PASSWORD", "env-secret"));

        // Act
        var options = AppLaunchOptions.Parse(["--mirror-bearer-token", "cli-token"], environment);
        var mirror = options.CreateDownloaderOptions()?.Mirror;

        // Assert
        Assert.Null(mirror?.Credentials);
        Assert.Equal("cli-token", mirror?.BearerToken);
    }

    /// <summary>Test that an empty environment variable is treated as unset.</summary>
    [Fact]
    public void Parse_EmptyEnvironmentVariables_TreatedAsUnset()
    {
        // Arrange
        var environment = FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", ""),
            ("DEMASPEECH_MIRROR_BEARER_TOKEN", ""));

        // Act
        var options = AppLaunchOptions.Parse([], environment);

        // Assert
        Assert.Null(options.CreateDownloaderOptions());
    }

    /// <summary>Test that environment credentials without a URL are rejected without echoing the secret.</summary>
    [Fact]
    public void CreateDownloaderOptions_EnvironmentCredentialWithoutUrl_ThrowsWithoutEchoingSecret()
    {
        // Arrange
        var options = AppLaunchOptions.Parse([], FakeEnvironment(("DEMASPEECH_MIRROR_BEARER_TOKEN", "super-secret-value")));

        // Act
        var exception = Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());

        // Assert
        Assert.DoesNotContain("super-secret-value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that an invalid mirror URL error does not echo the rejected value.</summary>
    [Fact]
    public void CreateDownloaderOptions_InvalidMirrorUrl_ErrorDoesNotEchoValue()
    {
        // Arrange
        var options = Parse("--mirror-url", "not a url secret-value");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());

        // Assert
        Assert.DoesNotContain("secret-value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that the help text names the mirror environment variables.</summary>
    [Fact]
    public void PrintHelp_ListsMirrorEnvironmentVariables()
    {
        // Arrange
        using var writer = new StringWriter();

        // Act
        AppLaunchOptions.PrintHelp(writer);

        // Assert
        var output = writer.ToString();
        Assert.Contains("DEMASPEECH_MIRROR_URL", output, StringComparison.Ordinal);
        Assert.Contains("DEMASPEECH_MIRROR_USER", output, StringComparison.Ordinal);
        Assert.Contains("DEMASPEECH_MIRROR_PASSWORD", output, StringComparison.Ordinal);
        Assert.Contains("DEMASPEECH_MIRROR_BEARER_TOKEN", output, StringComparison.Ordinal);
    }
}

/// <summary>
///     Collection definition for test classes that read or change process-wide state (the console
///     writer or <c>DEMASPEECH_CAPTURE_DEBUG_DIR</c>). Disabling parallelization keeps those
///     classes serial and apart from every other test, so one test's redirection or environment
///     change can never be observed by another.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public static class ProcessWideStateCollection
{
    /// <summary>The collection name test classes join with <c>[Collection]</c>.</summary>
    public const string Name = "ProcessWideState";
}
