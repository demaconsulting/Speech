using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="SpeechModelDownloadFile"/> record.
/// </summary>
public class SpeechModelDownloadFileTests
{
    /// <summary>A syntactically valid 64-character SHA-256 hex digest used across tests.</summary>
    private const string ValidChecksum = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    ///     Proves that a well-formed HTTPS URI, checksum, and relative path construct successfully
    ///     and expose the supplied values.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadFile_Constructor_ValidValues_ExposesValues()
    {
        // Arrange
        var uri = new Uri("https://example.test/model.onnx");

        // Act
        var file = new SpeechModelDownloadFile(uri, ValidChecksum, "model.onnx");

        // Assert
        Assert.Equal(uri, file.Uri);
        Assert.Equal(ValidChecksum, file.Sha256Checksum);
        Assert.Equal("model.onnx", file.RelativeInstallPath);
    }

    /// <summary>
    ///     Proves that a plain HTTP URI is rejected, since a production model download must
    ///     always use HTTPS.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadFile_Constructor_HttpUri_ThrowsArgumentException()
    {
        // Arrange
        var uri = new Uri("http://example.test/model.onnx");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SpeechModelDownloadFile(uri, ValidChecksum, "model.onnx"));
    }

    /// <summary>
    ///     Proves that a checksum which is not a 64-character hexadecimal string is rejected.
    /// </summary>
    [Theory]
    [InlineData("too-short")]
    [InlineData("zzzz89abcdef0123456789abcdef0123456789abcdef0123456789abcdef01")]
    public void SpeechModelDownloadFile_Constructor_InvalidChecksum_ThrowsArgumentException(string checksum)
    {
        // Arrange
        var uri = new Uri("https://example.test/model.onnx");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SpeechModelDownloadFile(uri, checksum, "model.onnx"));
    }

    /// <summary>
    ///     Proves that an empty, whitespace-only, parent-escaping, or current-directory install
    ///     path is rejected, since any of these could write outside the model's installed
    ///     directory, silently collide with another declared path once collapsed away by
    ///     <see cref="Path.Combine(string[])"/>/<see cref="Uri"/> canonicalization, or is
    ///     otherwise meaningless.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../escape.onnx")]
    [InlineData("nested/../../escape.onnx")]
    [InlineData(".")]
    [InlineData("./model.onnx")]
    [InlineData("tokens/./vocab.txt")]
    [InlineData(@"\\")] // Two literal backslashes: rooted on Windows, but on Linux (where only
                        // a leading '/' is rooted) this normalizes to zero segments instead -
                        // either way it must be rejected, never silently resolved to the staging
                        // directory itself.
    public void SpeechModelDownloadFile_Constructor_InvalidInstallPath_ThrowsArgumentException(string installPath)
    {
        // Arrange
        var uri = new Uri("https://example.test/model.onnx");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SpeechModelDownloadFile(uri, ValidChecksum, installPath));
    }

    /// <summary>
    ///     Proves that a rooted absolute install path is rejected on the current platform.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadFile_Constructor_RootedInstallPath_ThrowsArgumentException()
    {
        // Arrange
        var uri = new Uri("https://example.test/model.onnx");
        var rooted = Path.Join(Path.GetPathRoot(Directory.GetCurrentDirectory()) ?? "/", "escape.onnx");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SpeechModelDownloadFile(uri, ValidChecksum, rooted));
    }

    /// <summary>
    ///     Proves that null constructor arguments are rejected with <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadFile_Constructor_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange
        var uri = new Uri("https://example.test/model.onnx");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SpeechModelDownloadFile(null!, ValidChecksum, "model.onnx"));
        Assert.Throws<ArgumentNullException>(() => new SpeechModelDownloadFile(uri, null!, "model.onnx"));
        Assert.Throws<ArgumentNullException>(() => new SpeechModelDownloadFile(uri, ValidChecksum, null!));
    }

    /// <summary>
    ///     Proves that <see cref="SpeechModelDownloadFile.ResolveStagedPath"/> combines a staging
    ///     directory with a declared install path's segments using this platform's own
    ///     <see cref="Path.DirectorySeparatorChar"/>, regardless of which separator the install
    ///     path was declared with - the exact guarantee an <see cref="ISpeechModel.InstallAsync"/>
    ///     override relies on to locate one of its own declared files.
    /// </summary>
    [Theory]
    [InlineData("model.onnx", "model.onnx")]
    [InlineData("tokens/vocab.txt", "tokens|vocab.txt")]
    [InlineData("tokens\\vocab.txt", "tokens|vocab.txt")]
    public void SpeechModelDownloadFile_ResolveStagedPath_AnySeparator_MatchesDownloaderStaging(
        string installPath,
        string expectedRelativeSegments)
    {
        // Arrange - "|" stands in for this platform's own Path.DirectorySeparatorChar, since the
        // expected combined path must use whatever separator this platform's Path.Combine emits.
        var expected = Path.Combine(
            "staging",
            expectedRelativeSegments.Replace('|', Path.DirectorySeparatorChar));

        // Act
        var actual = SpeechModelDownloadFile.ResolveStagedPath("staging", installPath);

        // Assert
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Proves that <see cref="SpeechModelDownloadFile.ResolveStagedPath"/> rejects null
    ///     arguments, matching the eager-validation style used throughout this type.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadFile_ResolveStagedPath_NullArguments_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SpeechModelDownloadFile.ResolveStagedPath(null!, "model.onnx"));
        Assert.Throws<ArgumentNullException>(() => SpeechModelDownloadFile.ResolveStagedPath("staging", null!));
    }
}
