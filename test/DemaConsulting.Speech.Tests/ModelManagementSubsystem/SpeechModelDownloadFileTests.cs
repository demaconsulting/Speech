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
    ///     Proves that an empty, whitespace-only, or parent-escaping install path is rejected,
    ///     since any of these could write outside the model's installed directory or is
    ///     otherwise meaningless.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("../escape.onnx")]
    [InlineData("nested/../../escape.onnx")]
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
        var rooted = Path.Combine(Path.GetPathRoot(Directory.GetCurrentDirectory()) ?? "/", "escape.onnx");

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
}
