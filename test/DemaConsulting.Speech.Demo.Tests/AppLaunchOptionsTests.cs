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
public sealed class AppLaunchOptionsTests
{
    /// <summary>
    ///     Test that parsing an empty argument list returns an options instance with every
    ///     property at its default (unset) value.
    /// </summary>
    [Fact]
    public void Parse_NoArguments_ReturnsAllDefaults()
    {
        // Act
        var options = AppLaunchOptions.Parse([]);

        // Assert
        Assert.Null(options.ModelsDir);
        Assert.Null(options.MirrorUrl);
        Assert.Null(options.MirrorUser);
        Assert.Null(options.MirrorPassword);
        Assert.Null(options.MirrorBearerToken);
        Assert.False(options.Help);
    }

    /// <summary>
    ///     Test that <c>--help</c>, <c>-h</c>, and <c>-?</c> all set <see cref="AppLaunchOptions.Help"/>.
    /// </summary>
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public void Parse_HelpFlag_SetsHelp(string flag)
    {
        // Act
        var options = AppLaunchOptions.Parse([flag]);

        // Assert
        Assert.True(options.Help);
    }

    /// <summary>
    ///     Test that <c>--models-dir</c> and every <c>--mirror-*</c> option parse onto their
    ///     corresponding properties.
    /// </summary>
    [Fact]
    public void Parse_AllOptions_ParsesOntoProperties()
    {
        // Act
        var options = AppLaunchOptions.Parse([
            "--models-dir", "/tmp/models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret",
            "--mirror-bearer-token", "token-value"
        ]);

        // Assert
        Assert.Equal("/tmp/models", options.ModelsDir);
        Assert.Equal("https://mirror.example.com", options.MirrorUrl);
        Assert.Equal("alice", options.MirrorUser);
        Assert.Equal("secret", options.MirrorPassword);
        Assert.Equal("token-value", options.MirrorBearerToken);
    }

    /// <summary>
    ///     Test that an unrecognized argument throws a clean <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Parse_UnrecognizedArgument_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AppLaunchOptions.Parse(["--not-a-real-option"]));
    }

    /// <summary>
    ///     Test that a value-taking option supplied as the last argument (with no value following
    ///     it) throws a clean <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void Parse_OptionMissingValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AppLaunchOptions.Parse(["--mirror-url"]));
    }

    /// <summary>
    ///     Test that a null argument array is rejected.
    /// </summary>
    [Fact]
    public void Parse_NullArguments_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AppLaunchOptions.Parse(null!));
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateStoreOptions"/> returns <see langword="null"/>
    ///     when <c>--models-dir</c> was not supplied.
    /// </summary>
    [Fact]
    public void CreateStoreOptions_NoModelsDir_ReturnsNull()
    {
        var options = AppLaunchOptions.Parse([]);
        Assert.Null(options.CreateStoreOptions());
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateStoreOptions"/> resolves <c>--models-dir</c>
    ///     into a matching <c>RootPathOverride</c>.
    /// </summary>
    [Fact]
    public void CreateStoreOptions_WithModelsDir_ReturnsMatchingRootPathOverride()
    {
        var options = AppLaunchOptions.Parse(["--models-dir", "/tmp/models"]);
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
        var options = AppLaunchOptions.Parse([]);
        Assert.Null(options.CreateDownloaderOptions());
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> builds a mirror with
    ///     Basic credentials attached when a matching user/password pair is supplied.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_ValidCredentialedMirror_ReturnsConfiguredMirror()
    {
        var options = AppLaunchOptions.Parse([
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret"
        ]);
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
        var options = AppLaunchOptions.Parse([
            "--mirror-url", "https://mirror.example.com",
            "--mirror-bearer-token", "token-value"
        ]);
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
        var options = AppLaunchOptions.Parse(["--mirror-url", "not a url"]);
        Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());
    }

    /// <summary>
    ///     Test that <see cref="AppLaunchOptions.CreateDownloaderOptions"/> rejects a
    ///     <c>--mirror-user</c> supplied without the matching <c>--mirror-password</c>.
    /// </summary>
    [Fact]
    public void CreateDownloaderOptions_MirrorUserWithoutPassword_ThrowsArgumentException()
    {
        var options = AppLaunchOptions.Parse([
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice"
        ]);
        Assert.Throws<ArgumentException>(() => options.CreateDownloaderOptions());
    }
}
