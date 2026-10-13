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
        var mirror = MirrorOptionsFactory.Create("https://mirror.example.com", "alice", "secret", null);

        Assert.NotNull(mirror);
        Assert.Equal(new Uri("https://mirror.example.com"), mirror.BaseUri);
        Assert.Equal("alice", mirror.Credentials?.UserName);
        Assert.Equal("secret", mirror.Credentials?.Password);
        Assert.Null(mirror.BearerToken);
    }

    /// <summary>Test that a URL with a bearer token builds a mirror carrying the token and no Basic credentials.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_UrlWithBearerToken_ReturnsConfiguredMirror()
    {
        var mirror = MirrorOptionsFactory.Create("https://mirror.example.com", null, null, "token-value");

        Assert.NotNull(mirror);
        Assert.Equal(new Uri("https://mirror.example.com"), mirror.BaseUri);
        Assert.Null(mirror.Credentials);
        Assert.Equal("token-value", mirror.BearerToken);
    }

    /// <summary>Test that a URL alone builds an unauthenticated mirror.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_UrlOnly_ReturnsUnauthenticatedMirror()
    {
        var mirror = MirrorOptionsFactory.Create("https://mirror.example.com", null, null, null);

        Assert.NotNull(mirror);
        Assert.Null(mirror.Credentials);
        Assert.Null(mirror.BearerToken);
    }

    /// <summary>Test that a null or empty URL with no credentials yields no mirror.</summary>
    /// <param name="url">The blank URL under test.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MirrorOptionsFactory_Create_BlankUrlNoCredentials_ReturnsNull(string? url)
    {
        Assert.Null(MirrorOptionsFactory.Create(url, null, null, null));
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
        var exception = Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create(null, user, password, token));

        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("token-value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that a user without a password is rejected.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_UserWithoutPassword_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create("https://mirror.example.com", "alice", null, null));
    }

    /// <summary>Test that a password without a user is rejected.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_PasswordWithoutUser_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create("https://mirror.example.com", null, "secret", null));
    }

    /// <summary>Test that a URL that is not a valid absolute URL is rejected without echoing it.</summary>
    /// <param name="url">The invalid URL under test.</param>
    [Theory]
    [InlineData("not a url secret-value")]
    [InlineData("   ")]
    public void MirrorOptionsFactory_Create_InvalidUrl_ThrowsWithoutEchoingValue(string url)
    {
        var exception = Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create(url, null, null, null));

        Assert.DoesNotContain("secret-value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Test that a credential sent over plain HTTP to a non-loopback host is rejected by the library's mirror.</summary>
    [Fact]
    public void MirrorOptionsFactory_Create_CredentialOverPlainHttpToRemoteHost_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MirrorOptionsFactory.Create("http://mirror.example.com", null, null, "token-value"));
    }
}
