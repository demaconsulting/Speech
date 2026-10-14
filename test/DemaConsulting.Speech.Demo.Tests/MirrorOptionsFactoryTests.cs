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
///     Unit tests for <see cref="MirrorOptionsFactory"/>, exercised directly rather than through
///     its callers.
/// </summary>
public sealed class MirrorOptionsFactoryTests
{
    /// <summary>Test that a URL with a user and password builds a mirror carrying Basic credentials.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_UrlWithBasicCredentials_ReturnsConfiguredMirror()
    {
        // Arrange
        const string url = "https://mirror.example.com";

        // Act
        var mirror = MirrorOptionsFactory.Create(url, "alice", "secret", null);

        // Assert
        Assert.NotNull(mirror);
        Assert.Equal(new Uri(url), mirror.BaseUri);
        Assert.Equal("alice", mirror.Credentials?.UserName);
        Assert.Equal("secret", mirror.Credentials?.Password);
        Assert.Null(mirror.BearerToken);
    }

    /// <summary>Test that a URL with a bearer token builds a mirror carrying the token and no Basic credentials.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_UrlWithBearerToken_ReturnsConfiguredMirror()
    {
        // Arrange
        const string url = "https://mirror.example.com";

        // Act
        var mirror = MirrorOptionsFactory.Create(url, null, null, "token-value");

        // Assert
        Assert.NotNull(mirror);
        Assert.Equal(new Uri(url), mirror.BaseUri);
        Assert.Null(mirror.Credentials);
        Assert.Equal("token-value", mirror.BearerToken);
    }

    /// <summary>Test that a URL alone builds an unauthenticated mirror.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_UrlOnly_ReturnsUnauthenticatedMirror()
    {
        // Arrange
        const string url = "https://mirror.example.com";

        // Act
        var mirror = MirrorOptionsFactory.Create(url, null, null, null);

        // Assert
        Assert.NotNull(mirror);
        Assert.Null(mirror.Credentials);
        Assert.Null(mirror.BearerToken);
    }

    /// <summary>Test that a null, empty, or whitespace-only URL with no credentials yields no mirror.</summary>
    /// <param name="url">The blank URL under test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \r\n")]
    public void MirrorOptionsFactory_Create_BlankUrlNoCredentials_ReturnsNull(string? url)
    {
        // Arrange: the blank URL is supplied by the theory data

        // Act
        var mirror = MirrorOptionsFactory.Create(url, null, null, null);

        // Assert
        Assert.Null(mirror);
    }

    /// <summary>Test that a credential or token without a URL is rejected, whichever credential it is.</summary>
    /// <param name="user">The user supplied, if any.</param>
    /// <param name="password">The password supplied, if any.</param>
    /// <param name="token">The bearer token supplied, if any.</param>
    [Theory]
    [InlineData("alice", null, null)]
    [InlineData(null, "secret", null)]
    [InlineData(null, null, "token-value")]
    public void MirrorOptionsFactory_Create_CredentialWithoutUrl_ThrowsWithoutEchoingSecret(
        string? user, string? password, string? token)
    {
        // Arrange: the credential combination is supplied by the theory data

        // Act
        var exception = Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create(null, user, password, token));

        // Assert
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("token-value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that a user without a password is rejected.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_UserWithoutPassword_ThrowsArgumentException()
    {
        // Arrange
        const string url = "https://mirror.example.com";

        // Act
        var exception = Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create(url, "alice", null, null));

        // Assert
        Assert.Contains("both be supplied", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that a password without a user is rejected.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_PasswordWithoutUser_ThrowsArgumentException()
    {
        // Arrange
        const string url = "https://mirror.example.com";

        // Act
        var exception = Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create(url, null, "secret", null));

        // Assert
        Assert.Contains("both be supplied", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that a URL that is not a valid absolute URL is rejected without echoing it.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_InvalidUrl_ThrowsWithoutEchoingValue()
    {
        // Arrange
        const string url = "not a url secret-value";

        // Act
        var exception = Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create(url, null, null, null));

        // Assert
        Assert.DoesNotContain("secret-value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that a credential sent over plain HTTP to a non-loopback host is rejected by the library's mirror.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_CredentialOverPlainHttpToRemoteHost_ThrowsArgumentException()
    {
        // Arrange
        const string url = "http://mirror.example.com";

        // Act
        var exception = Record.Exception(() => MirrorOptionsFactory.Create(url, null, null, "token-value"));

        // Assert
        Assert.IsAssignableFrom<ArgumentException>(exception);
    }

    /// <summary>Test that Basic credentials sent over plain HTTP to a non-loopback host are rejected without echoing them.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_BasicCredentialsOverPlainHttpToRemoteHost_ThrowsWithoutEchoingSecret()
    {
        // Arrange
        const string url = "http://mirror.example.com";

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => MirrorOptionsFactory.Create(url, "alice", "basic-secret", null));

        // Assert
        Assert.DoesNotContain("basic-secret", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that a credential over plain HTTP to a loopback host is allowed.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_CredentialOverPlainHttpToLoopback_ReturnsMirror()
    {
        // Arrange
        const string url = "http://localhost:8080";

        // Act
        var mirror = MirrorOptionsFactory.Create(url, null, null, "token-value");

        // Assert
        Assert.NotNull(mirror);
        Assert.Equal("token-value", mirror.BearerToken);
    }

    /// <summary>Test that Basic credentials together with a bearer token are rejected without echoing either.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_BasicCredentialsAndBearerToken_ThrowsWithoutEchoingSecrets()
    {
        // Arrange
        const string url = "https://mirror.example.com";

        // Act
        var exception = Assert.Throws<ArgumentException>(
            () => MirrorOptionsFactory.Create(url, "alice", "basic-secret", "token-secret"));

        // Assert
        Assert.DoesNotContain("basic-secret", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("token-secret", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that a URL whose scheme is not HTTP(S) is rejected, and that the scheme text (which can be a pasted secret) is not echoed.</summary>
    /// <param name="url">The URL under test.</param>
    [Theory]
    [InlineData("ftp://mirror.example.com")]
    [InlineData("hunter2:token@mirror.example.com")]
    public void MirrorOptionsFactory_Create_NonHttpScheme_ThrowsWithoutEchoingValue(string url)
    {
        // Arrange: the URL is supplied by the theory data

        // Act
        var exception = Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create(url, null, null, null));

        // Assert
        Assert.DoesNotContain("hunter2", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("ftp", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that a relative or scheme-less URL is rejected as not absolute.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_RelativeUrl_ThrowsArgumentException()
    {
        // Arrange
        const string url = "mirror.example.com/models";

        // Act
        var exception = Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create(url, null, null, null));

        // Assert
        Assert.Contains("absolute", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that empty credential strings (a cleared panel field) count as no credential when the URL is also blank.</summary>
    /// <param name="user">The user supplied, if any.</param>
    /// <param name="password">The password supplied, if any.</param>
    /// <param name="token">The bearer token supplied, if any.</param>
    [Theory]
    [InlineData("", "", "")]
    [InlineData("", null, null)]
    [InlineData(null, null, "")]
    public void MirrorOptionsFactory_Create_BlankUrlWithEmptyCredentials_ReturnsNull(string? user, string? password, string? token)
    {
        // Arrange: the empty credentials are supplied by the theory data

        // Act
        var emptyUrlMirror = MirrorOptionsFactory.Create("", user, password, token);
        var nullUrlMirror = MirrorOptionsFactory.Create(null, user, password, token);

        // Assert
        Assert.Null(emptyUrlMirror);
        Assert.Null(nullUrlMirror);
    }

    /// <summary>Test that empty credential strings with a URL build an unauthenticated mirror instead of an empty credential.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_UrlWithEmptyCredentials_ReturnsUnauthenticatedMirror()
    {
        // Arrange
        const string url = "https://mirror.example.com";

        // Act
        var mirror = MirrorOptionsFactory.Create(url, "", "", "");

        // Assert
        Assert.NotNull(mirror);
        Assert.Null(mirror.Credentials);
        Assert.Null(mirror.BearerToken);
    }

    /// <summary>Test that an empty user with a real password is still an incomplete pair.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_EmptyUserWithPassword_ThrowsArgumentException()
    {
        // Arrange
        const string url = "https://mirror.example.com";

        // Act
        var exception = Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create(url, "", "secret", null));

        // Assert
        Assert.Contains("both be supplied", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Regression test: whitespace-only credential strings with a URL are treated as not
    ///     supplied, so they neither build empty credentials nor trigger credential validation.
    /// </summary>
    /// <param name="blank">The whitespace-only value placed in every credential field.</param>
    [Theory]
    [InlineData(" ")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void MirrorOptionsFactory_Create_UrlWithWhitespaceCredentials_ReturnsUnauthenticatedMirror(string blank)
    {
        // Arrange
        const string url = "https://mirror.example.com";

        // Act
        var mirror = MirrorOptionsFactory.Create(url, blank, blank, blank);

        // Assert
        Assert.NotNull(mirror);
        Assert.Null(mirror.Credentials);
        Assert.Null(mirror.BearerToken);
    }

    /// <summary>
    ///     Regression test: a whitespace-only user beside a real password is an incomplete pair,
    ///     exactly like an empty user.
    /// </summary>
    [Fact]
    public void MirrorOptionsFactory_Create_WhitespaceUserWithPassword_ThrowsArgumentException()
    {
        // Arrange
        const string url = "https://mirror.example.com";

        // Act
        var exception = Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create(url, "  ", "secret", null));

        // Assert
        Assert.Contains("both be supplied", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Regression test: a whitespace-only bearer token beside Basic credentials is not a
    ///     competing scheme, so the Basic credentials alone are used.
    /// </summary>
    [Fact]
    public void MirrorOptionsFactory_Create_WhitespaceTokenWithBasicCredentials_ReturnsBasicMirror()
    {
        // Arrange
        const string url = "https://mirror.example.com";

        // Act
        var mirror = MirrorOptionsFactory.Create(url, "alice", "secret", "  ");

        // Assert
        Assert.NotNull(mirror);
        Assert.Equal("alice", mirror.Credentials?.UserName);
        Assert.Null(mirror.BearerToken);
    }

    /// <summary>
    ///     Regression test: a whitespace-only URL beside real credentials is a blank URL, so the
    ///     credentials are rejected as having nothing to apply to (not as an invalid URL).
    /// </summary>
    [Fact]
    public void MirrorOptionsFactory_Create_WhitespaceUrlWithCredential_ThrowsCredentialWithoutUrl()
    {
        // Arrange
        const string blankUrl = "   ";

        // Act
        var exception = Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create(blankUrl, null, null, "token-value"));

        // Assert
        Assert.Contains("require a mirror URL", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("token-value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that whitespace credentials with a whitespace URL are all blank and yield no mirror.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_AllWhitespace_ReturnsNull()
    {
        // Arrange
        const string blank = "  ";

        // Act
        var mirror = MirrorOptionsFactory.Create(blank, blank, blank, blank);

        // Assert
        Assert.Null(mirror);
    }
}
