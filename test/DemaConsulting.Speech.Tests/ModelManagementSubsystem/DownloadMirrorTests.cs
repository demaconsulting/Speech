using System.Net;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="DownloadMirror"/> record.
/// </summary>
public class DownloadMirrorTests
{
    /// <summary>
    ///     Proves that an absolute HTTPS base URI with neither credentials nor a bearer token
    ///     constructs successfully and exposes the supplied values, with both auth members
    ///     defaulting to <see langword="null"/>.
    /// </summary>
    [Fact]
    public void DownloadMirror_Constructor_BaseUriOnly_ExposesValuesWithNullAuth()
    {
        // Arrange
        var baseUri = new Uri("https://mirror.internal/models");

        // Act
        var mirror = new DownloadMirror(baseUri);

        // Assert
        Assert.Equal(baseUri, mirror.BaseUri);
        Assert.Null(mirror.Credentials);
        Assert.Null(mirror.BearerToken);
    }

    /// <summary>
    ///     Proves that both the <c>http</c> and <c>https</c> schemes are accepted for
    ///     <see cref="DownloadMirror.BaseUri"/>.
    /// </summary>
    [Theory]
    [InlineData("http://mirror.internal/models")]
    [InlineData("https://mirror.internal/models")]
    public void DownloadMirror_Constructor_HttpOrHttpsScheme_Succeeds(string baseUri)
    {
        // Act
        var mirror = new DownloadMirror(new Uri(baseUri));

        // Assert
        Assert.Equal(new Uri(baseUri), mirror.BaseUri);
    }

    /// <summary>
    ///     Proves that supplying only <see cref="DownloadMirror.Credentials"/> (Basic/NTLM)
    ///     constructs successfully and exposes it, with <see cref="DownloadMirror.BearerToken"/>
    ///     left <see langword="null"/>.
    /// </summary>
    [Fact]
    public void DownloadMirror_Constructor_CredentialsOnly_ExposesCredentials()
    {
        // Arrange
        var baseUri = new Uri("https://mirror.internal/models");
        var credentials = new NetworkCredential("user", "pass");

        // Act
        var mirror = new DownloadMirror(baseUri, credentials: credentials);

        // Assert
        Assert.Same(credentials, mirror.Credentials);
        Assert.Null(mirror.BearerToken);
    }

    /// <summary>
    ///     Proves that supplying only <see cref="DownloadMirror.BearerToken"/> constructs
    ///     successfully and exposes it, with <see cref="DownloadMirror.Credentials"/> left
    ///     <see langword="null"/>.
    /// </summary>
    [Fact]
    public void DownloadMirror_Constructor_BearerTokenOnly_ExposesBearerToken()
    {
        // Arrange
        var baseUri = new Uri("https://mirror.internal/models");

        // Act
        var mirror = new DownloadMirror(baseUri, bearerToken: "secret-token");

        // Assert
        Assert.Equal("secret-token", mirror.BearerToken);
        Assert.Null(mirror.Credentials);
    }

    /// <summary>
    ///     Proves that supplying both <see cref="DownloadMirror.Credentials"/> and
    ///     <see cref="DownloadMirror.BearerToken"/> is rejected as a configuration error, rather
    ///     than silently preferring one over the other.
    /// </summary>
    [Fact]
    public void DownloadMirror_Constructor_BothCredentialsAndBearerToken_ThrowsArgumentException()
    {
        // Arrange
        var baseUri = new Uri("https://mirror.internal/models");
        var credentials = new NetworkCredential("user", "pass");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new DownloadMirror(baseUri, credentials, "secret-token"));
    }

    /// <summary>
    ///     Proves that a <see langword="null"/> <see cref="DownloadMirror.BaseUri"/> is rejected
    ///     with <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void DownloadMirror_Constructor_NullBaseUri_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DownloadMirror(null!));
    }

    /// <summary>
    ///     Proves that a relative (non-absolute) base URI is rejected, since it has no meaningful
    ///     way to combine with a model's declared relative install path.
    /// </summary>
    [Fact]
    public void DownloadMirror_Constructor_RelativeBaseUri_ThrowsArgumentException()
    {
        // Arrange
        var relativeUri = new Uri("models/mirror", UriKind.Relative);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new DownloadMirror(relativeUri));
    }

    /// <summary>
    ///     Proves that a non-HTTP(S) scheme (for example <c>file</c>) is rejected with a clear
    ///     message naming the offending scheme, since a <c>file://</c>/UNC mirror has no
    ///     implementation path anywhere in this library.
    /// </summary>
    [Theory]
    [InlineData("file:///C:/mirror/models")]
    [InlineData("ftp://mirror.internal/models")]
    public void DownloadMirror_Constructor_NonHttpScheme_ThrowsArgumentExceptionNamingScheme(string baseUri)
    {
        // Arrange
        var uri = new Uri(baseUri);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new DownloadMirror(uri));
        Assert.Contains(uri.Scheme, ex.Message, StringComparison.Ordinal);
    }
}
