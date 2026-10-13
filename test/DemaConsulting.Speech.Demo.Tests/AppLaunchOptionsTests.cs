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
///     <c>DEMASPEECH_MIRROR_*</c> variables.
/// </remarks>
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
    ///     property at its default (unset) value.
    /// </summary>
    [Fact]
    public void Parse_NoArguments_ReturnsAllDefaults()
    {
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
        // Act
        var options = Parse(flag);

        // Assert
        Assert.True(options.Help);
    }

    /// <summary>
    ///     Test that <c>--models-dir</c>, <c>--voices-dir</c>, and every <c>--mirror-*</c> option
    ///     parse onto their corresponding properties.
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
        Assert.Throws<ArgumentException>(() => Parse("--not-a-real-option"));
    }

    /// <summary>
    ///     Test that a value-taking option supplied as the last argument (with no value following
    ///     it) throws a clean <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Parse_OptionMissingValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Parse("--mirror-url"));
    }

    /// <summary>
    ///     Test that a null argument array is rejected.
    /// </summary>
    [Fact]
    public void Parse_NullArguments_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AppLaunchOptions.Parse(null!, FakeEnvironment()));
    }

    /// <summary>
    ///     Test that a null environment reader is rejected.
    /// </summary>
    [Fact]
    public void Parse_NullEnvironmentReader_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AppLaunchOptions.Parse([], null!));
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateStoreOptions"/> returns <see langword="null"/>
    ///     when <c>--models-dir</c> was not supplied.
    /// </summary>
    [Fact]
    public void CreateStoreOptions_NoModelsDir_ReturnsNull()
    {
        var options = Parse();
        Assert.Null(options.CreateStoreOptions());
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateStoreOptions"/> resolves <c>--models-dir</c>
    ///     into a matching <c>RootPathOverride</c>.
    /// </summary>
    [Fact]
    public void CreateStoreOptions_WithModelsDir_ReturnsMatchingRootPathOverride()
    {
        var options = Parse("--models-dir", "/tmp/models");
        var storeOptions = options.CreateStoreOptions();

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
        var options = Parse();
        Assert.Null(options.CreateDownloaderOptions());
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> builds a mirror with
    ///     Basic credentials attached when a matching user/password pair is supplied.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_ValidCredentialedMirror_ReturnsConfiguredMirror()
    {
        var options = Parse(
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret");
        var downloaderOptions = options.CreateDownloaderOptions();

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
        var options = Parse(
            "--mirror-url", "https://mirror.example.com",
            "--mirror-bearer-token", "token-value");
        var downloaderOptions = options.CreateDownloaderOptions();

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
        var options = Parse("--mirror-url", "not a url");
        Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> rejects a
    ///     <c>--mirror-user</c> supplied without the matching <c>--mirror-password</c>.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_MirrorUserWithoutPassword_ThrowsArgumentException()
    {
        var options = Parse(
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice");
        Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> rejects a
    ///     <c>--mirror-password</c> supplied without the matching <c>--mirror-user</c>.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_MirrorPasswordWithoutUser_ThrowsArgumentException()
    {
        var options = Parse(
            "--mirror-url", "https://mirror.example.com",
            "--mirror-password", "secret");
        Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());
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
        var options = Parse(
            "--mirror-user", "alice",
            "--mirror-password", "secret");
        Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> rejects a
    ///     <c>--mirror-bearer-token</c> supplied without the required <c>--mirror-url</c>.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_MirrorBearerTokenWithoutUrl_ThrowsArgumentException()
    {
        var options = Parse("--mirror-bearer-token", "token-value");
        Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());
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
        var missing = Path.Join(AppContext.BaseDirectory, Guid.NewGuid().ToString("N"));
        var exception = Assert.Throws<ArgumentException>(() => Parse("--voices-dir", missing));
        Assert.Contains("--voices-dir", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that <c>--voices-dir</c> as the last argument is rejected for lacking a value.
    /// </summary>
    [Fact]
    public void Parse_VoicesDir_NoValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Parse("--voices-dir"));
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
        Assert.Contains("--mirror-bearer-token <t>  HTTP Bearer token for --mirror-url", output, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a null help writer is rejected.
    /// </summary>
    [Fact]
    public void PrintHelp_NullWriter_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AppLaunchOptions.PrintHelp(null!));
    }

    /// <summary>Test that the mirror URL environment variable applies when <c>--mirror-url</c> is absent.</summary>
    [Fact]
    public void Parse_EnvironmentMirrorUrl_UsedWhenOptionAbsent()
    {
        var options = AppLaunchOptions.Parse([], FakeEnvironment(("DEMASPEECH_MIRROR_URL", "https://env.example.com")));

        Assert.Equal("https://env.example.com", options.MirrorUrl);
    }

    /// <summary>Test that <c>--mirror-url</c> overrides the environment variable.</summary>
    [Fact]
    public void Parse_MirrorUrlOption_OverridesEnvironment()
    {
        var options = AppLaunchOptions.Parse(
            ["--mirror-url", "https://cli.example.com"],
            FakeEnvironment(("DEMASPEECH_MIRROR_URL", "https://env.example.com")));

        Assert.Equal("https://cli.example.com", options.MirrorUrl);
    }

    /// <summary>Test that the user and password variables supply the Basic credential.</summary>
    [Fact]
    public void Parse_EnvironmentBasicCredentials_UsedWhenOptionsAbsent()
    {
        var options = AppLaunchOptions.Parse([], FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
            ("DEMASPEECH_MIRROR_USER", "alice"),
            ("DEMASPEECH_MIRROR_PASSWORD", "env-secret")));

        var downloaderOptions = options.CreateDownloaderOptions();
        Assert.Equal("alice", downloaderOptions?.Mirror?.Credentials?.UserName);
        Assert.Equal("env-secret", downloaderOptions?.Mirror?.Credentials?.Password);
    }

    /// <summary>Test that the bearer token variable supplies the bearer token.</summary>
    [Fact]
    public void Parse_EnvironmentBearerToken_UsedWhenOptionsAbsent()
    {
        var options = AppLaunchOptions.Parse([], FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
            ("DEMASPEECH_MIRROR_BEARER_TOKEN", "env-token")));

        Assert.Equal("env-token", options.CreateDownloaderOptions()?.Mirror?.BearerToken);
    }

    /// <summary>Test that any credential option disables every environment credential.</summary>
    [Fact]
    public void Parse_CredentialOption_DisablesEnvironmentCredentials()
    {
        var options = AppLaunchOptions.Parse(["--mirror-bearer-token", "cli-token"], FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
            ("DEMASPEECH_MIRROR_USER", "alice"),
            ("DEMASPEECH_MIRROR_PASSWORD", "env-secret")));

        var mirror = options.CreateDownloaderOptions()?.Mirror;
        Assert.Null(mirror?.Credentials);
        Assert.Equal("cli-token", mirror?.BearerToken);
    }

    /// <summary>Test that an empty environment variable is treated as unset.</summary>
    [Fact]
    public void Parse_EmptyEnvironmentVariables_TreatedAsUnset()
    {
        var options = AppLaunchOptions.Parse([], FakeEnvironment(
            ("DEMASPEECH_MIRROR_URL", ""),
            ("DEMASPEECH_MIRROR_BEARER_TOKEN", "")));

        Assert.Null(options.CreateDownloaderOptions());
    }

    /// <summary>Test that environment credentials without a URL are rejected without echoing the secret.</summary>
    [Fact]
    public void CreateDownloaderOptions_EnvironmentCredentialWithoutUrl_ThrowsWithoutEchoingSecret()
    {
        var options = AppLaunchOptions.Parse([], FakeEnvironment(("DEMASPEECH_MIRROR_BEARER_TOKEN", "super-secret-value")));

        var exception = Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());
        Assert.DoesNotContain("super-secret-value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that an invalid mirror URL error does not echo the rejected value.</summary>
    [Fact]
    public void CreateDownloaderOptions_InvalidMirrorUrl_ErrorDoesNotEchoValue()
    {
        var options = Parse("--mirror-url", "not a url secret-value");

        var exception = Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());
        Assert.DoesNotContain("secret-value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that the help text names the mirror environment variables.</summary>
    [Fact]
    public void PrintHelp_ListsMirrorEnvironmentVariables()
    {
        using var writer = new StringWriter();

        AppLaunchOptions.PrintHelp(writer);

        var output = writer.ToString();
        Assert.Contains("DEMASPEECH_MIRROR_URL", output, StringComparison.Ordinal);
        Assert.Contains("DEMASPEECH_MIRROR_USER", output, StringComparison.Ordinal);
        Assert.Contains("DEMASPEECH_MIRROR_PASSWORD", output, StringComparison.Ordinal);
        Assert.Contains("DEMASPEECH_MIRROR_BEARER_TOKEN", output, StringComparison.Ordinal);
    }
}
