using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the shared <see cref="TarBz2ArchiveExtractor"/> helper, using small,
///     deterministic in-memory <c>.tar.bz2</c> fixtures so every scenario runs without any real
///     network access or multi-hundred-megabyte production model download.
/// </summary>
public sealed class TarBz2ArchiveExtractorTests : IDisposable
{
    /// <summary>A scratch root directory unique to this test instance, cleaned up on dispose.</summary>
    private readonly string _testRoot;

    /// <summary>
    ///     Initializes a fresh, unique scratch directory for each test.
    /// </summary>
    public TarBz2ArchiveExtractorTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    /// <summary>
    ///     Deletes the scratch directory tree created for this test instance.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup only; a leftover temp directory does not fail the test.
        }
    }

    /// <summary>
    ///     Proves that extracting a single-entry archive writes the entry's content, preserving
    ///     the archive's own top-level folder, and removes the original archive file.
    /// </summary>
    [Fact]
    public async Task TarBz2ArchiveExtractor_ExtractAndDeleteAsync_SingleEntryArchive_ExtractsEntryAndDeletesArchive()
    {
        // Arrange
        var archivePath = Path.Combine(_testRoot, "model.tar.bz2");
        await File.WriteAllBytesAsync(
            archivePath,
            TarBz2ArchiveFixtures.SingleEntryArchiveBytes,
            TestContext.Current.CancellationToken);

        // Act
        await TarBz2ArchiveExtractor.ExtractAndDeleteAsync(
            archivePath,
            _testRoot,
            TestContext.Current.CancellationToken);

        // Assert
        var extractedPath = Path.Combine(
            _testRoot,
            TarBz2ArchiveFixtures.TopLevelFolderName,
            TarBz2ArchiveFixtures.SingleEntryName);
        Assert.True(File.Exists(extractedPath));
        var extractedBytes = await File.ReadAllBytesAsync(extractedPath, TestContext.Current.CancellationToken);
        Assert.Equal(TarBz2ArchiveFixtures.SingleEntryContent, extractedBytes);
        Assert.False(File.Exists(archivePath));
    }

    /// <summary>
    ///     Proves that extracting an archive with multiple entries under the same top-level
    ///     folder extracts every entry, not just the first.
    /// </summary>
    [Fact]
    public async Task TarBz2ArchiveExtractor_ExtractAndDeleteAsync_MultiEntryArchive_ExtractsEveryEntry()
    {
        // Arrange
        var firstContent = "first-entry-content"u8.ToArray();
        var secondContent = "second-entry-content"u8.ToArray();
        var archiveBytes = TarBz2ArchiveFixtures.BuildArchiveBytes(
        [
            ("model-folder/first.onnx", firstContent),
            ("model-folder/nested/second.txt", secondContent),
        ]);
        var archivePath = Path.Combine(_testRoot, "multi.tar.bz2");
        await File.WriteAllBytesAsync(archivePath, archiveBytes, TestContext.Current.CancellationToken);

        // Act
        await TarBz2ArchiveExtractor.ExtractAndDeleteAsync(
            archivePath,
            _testRoot,
            TestContext.Current.CancellationToken);

        // Assert
        var firstPath = Path.Combine(_testRoot, "model-folder", "first.onnx");
        var secondPath = Path.Combine(_testRoot, "model-folder", "nested", "second.txt");
        Assert.Equal(firstContent, await File.ReadAllBytesAsync(firstPath, TestContext.Current.CancellationToken));
        Assert.Equal(secondContent, await File.ReadAllBytesAsync(secondPath, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that a null or empty archive path is rejected eagerly.
    /// </summary>
    [Fact]
    public async Task TarBz2ArchiveExtractor_ExtractAndDeleteAsync_EmptyArchivePath_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => TarBz2ArchiveExtractor.ExtractAndDeleteAsync(
                string.Empty,
                _testRoot,
                TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Proves that a null or empty destination directory is rejected eagerly.
    /// </summary>
    [Fact]
    public async Task TarBz2ArchiveExtractor_ExtractAndDeleteAsync_EmptyDestinationDirectory_ThrowsArgumentException()
    {
        // Arrange
        var archivePath = Path.Combine(_testRoot, "model.tar.bz2");
        await File.WriteAllBytesAsync(
            archivePath,
            TarBz2ArchiveFixtures.SingleEntryArchiveBytes,
            TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => TarBz2ArchiveExtractor.ExtractAndDeleteAsync(
                archivePath,
                string.Empty,
                TestContext.Current.CancellationToken));
    }
}
