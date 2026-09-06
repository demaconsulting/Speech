using SharpCompress.Common;
using SharpCompress.Common.Tar.Headers;
using SharpCompress.Compressors;
using SharpCompress.Compressors.BZip2;
using SharpCompress.Writers.Tar;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem.Fakes;

/// <summary>
///     Builds small, deterministic in-memory <c>.tar.bz2</c> archive fixtures used by
///     <c>TarBz2ArchiveExtractorTests</c> and the two sherpa-onnx recognition model tests, so
///     every scenario exercises the real BZip2+tar extraction path without depending on any of
///     the real, multi-hundred-megabyte production model downloads.
/// </summary>
/// <remarks>
///     <c>SharpCompress</c>'s <see cref="BZip2Stream"/> closes its underlying stream when
///     disposed, so building archive bytes goes through a scratch temporary file rather than a
///     <see cref="MemoryStream"/> that would otherwise be closed out from under the caller.
/// </remarks>
internal static class TarBz2ArchiveFixtures
{
    /// <summary>The name of the top-level folder every fixture archive nests its entries under.</summary>
    public const string TopLevelFolderName = "fake-model-folder";

    /// <summary>The relative name of the single entry packed into <see cref="SingleEntryArchiveBytes"/>.</summary>
    public const string SingleEntryName = "model.txt";

    /// <summary>The fixed in-memory content of <see cref="SingleEntryName"/> within <see cref="SingleEntryArchiveBytes"/>.</summary>
    public static byte[] SingleEntryContent { get; } = "fake-model-tar-bz2-entry-payload"u8.ToArray();

    /// <summary>
    ///     Gets a small, deterministic in-memory <c>.tar.bz2</c> archive containing one entry
    ///     nested under <see cref="TopLevelFolderName"/> (<see cref="SingleEntryName"/>, whose
    ///     content is <see cref="SingleEntryContent"/>), mirroring the real sherpa-onnx archive
    ///     layout of "one top-level folder containing the model's files".
    /// </summary>
    public static byte[] SingleEntryArchiveBytes { get; } =
        BuildArchiveBytes([(TopLevelFolderName + "/" + SingleEntryName, SingleEntryContent)]);

    /// <summary>
    ///     Builds a <c>.tar.bz2</c> archive containing the given set of (relative path, content)
    ///     entries, returning the archive's raw bytes.
    /// </summary>
    /// <param name="entries">The relative path/content pairs to pack into the archive.</param>
    /// <returns>The raw bytes of the built <c>.tar.bz2</c> archive.</returns>
    public static byte[] BuildArchiveBytes(IReadOnlyList<(string RelativePath, byte[] Content)> entries)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "DemaConsulting.Speech.Tests", Guid.NewGuid() + ".tar.bz2");
        Directory.CreateDirectory(Path.GetDirectoryName(tempFile)!);
        try
        {
            using (var fileStream = new FileStream(tempFile, FileMode.Create))
            {
                using var bzip2Stream = new BZip2Stream(fileStream, CompressionMode.Compress, decompressConcatenated: false);
                using var tarWriter = new TarWriter(
                    bzip2Stream,
                    new TarWriterOptions(CompressionType.None, finalizeArchiveOnClose: true, TarHeaderWriteFormat.USTAR));

                foreach (var (relativePath, content) in entries)
                {
                    using var entryStream = new MemoryStream(content);
                    tarWriter.Write(relativePath, entryStream, modificationTime: null);
                }
            }

            return File.ReadAllBytes(tempFile);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
