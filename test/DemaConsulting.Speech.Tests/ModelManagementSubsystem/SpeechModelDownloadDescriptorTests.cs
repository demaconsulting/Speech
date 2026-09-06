using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the <see cref="SpeechModelDownloadDescriptor"/> record.
/// </summary>
public class SpeechModelDownloadDescriptorTests
{
    /// <summary>A syntactically valid 64-character SHA-256 hex digest used across tests.</summary>
    private const string ValidChecksum = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    ///     Proves that a descriptor preserves the exact order of its declared files.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadDescriptor_Constructor_MultipleFiles_PreservesOrder()
    {
        // Arrange
        var first = new SpeechModelDownloadFile(new Uri("https://example.test/a.onnx"), ValidChecksum, "a.onnx");
        var second = new SpeechModelDownloadFile(new Uri("https://example.test/b.onnx"), ValidChecksum, "b.onnx");

        // Act
        var descriptor = new SpeechModelDownloadDescriptor([first, second]);

        // Assert
        Assert.Equal([first, second], descriptor.Files);
    }

    /// <summary>
    ///     Proves that an empty file list is rejected, since a model must declare at least one
    ///     downloadable file.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadDescriptor_Constructor_EmptyFileList_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SpeechModelDownloadDescriptor([]));
    }

    /// <summary>
    ///     Proves that a null file list is rejected with <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadDescriptor_Constructor_NullFileList_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SpeechModelDownloadDescriptor(null!));
    }

    /// <summary>
    ///     Proves that two files declaring the same relative install path are rejected, since one
    ///     would silently overwrite the other during install.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadDescriptor_Constructor_DuplicateInstallPaths_ThrowsArgumentException()
    {
        // Arrange
        var first = new SpeechModelDownloadFile(new Uri("https://example.test/a.onnx"), ValidChecksum, "model.onnx");
        var second = new SpeechModelDownloadFile(new Uri("https://example.test/b.onnx"), ValidChecksum, "model.onnx");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SpeechModelDownloadDescriptor([first, second]));
    }

    /// <summary>
    ///     Proves that a null element within an otherwise non-empty file list is rejected with
    ///     <see cref="ArgumentException"/> rather than surfacing later as a
    ///     <see cref="NullReferenceException"/> when something dereferences it.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadDescriptor_Constructor_NullElementInFileList_ThrowsArgumentException()
    {
        // Arrange
        var first = new SpeechModelDownloadFile(new Uri("https://example.test/a.onnx"), ValidChecksum, "a.onnx");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new SpeechModelDownloadDescriptor([first, null!]));
    }

    /// <summary>
    ///     Proves that the descriptor defensively copies the caller's file list, so a later
    ///     mutation of the caller's own list does not change the already-validated descriptor.
    /// </summary>
    [Fact]
    public void SpeechModelDownloadDescriptor_Constructor_MutatingCallerList_DoesNotAffectDescriptor()
    {
        // Arrange
        var first = new SpeechModelDownloadFile(new Uri("https://example.test/a.onnx"), ValidChecksum, "a.onnx");
        var second = new SpeechModelDownloadFile(new Uri("https://example.test/b.onnx"), ValidChecksum, "b.onnx");
        var callerList = new List<SpeechModelDownloadFile> { first };
        var descriptor = new SpeechModelDownloadDescriptor(callerList);

        // Act: mutate the caller's own list after construction
        callerList.Add(second);

        // Assert: the descriptor's snapshot is unaffected
        Assert.Equal([first], descriptor.Files);
    }
}
