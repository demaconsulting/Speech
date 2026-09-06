using SharpCompress.Common;
using SharpCompress.Readers;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Shared helper that extracts a downloaded <c>.tar.bz2</c> archive into a directory and
///     deletes the original archive file, for use by any <see cref="ISpeechModel.InstallAsync"/>
///     implementation whose declared download payload is a BZip2-compressed tar archive.
/// </summary>
/// <remarks>
///     The BCL has no BZip2 decoder, so this type is the sole reason
///     <c>DemaConsulting.Speech.csproj</c> references the <c>SharpCompress</c> OTS package (see
///     <c>docs/design/ots/sharp-compress.md</c>). Both <c>SherpaOnnxZipformerEnRecognitionModel</c>
///     and <c>SherpaOnnxNemotronStreamingEnRecognitionModel</c> declare their downloadable
///     payload as a single <c>.tar.bz2</c> archive (matching the exact file sherpa-onnx itself
///     publishes) and call this helper from their own <c>InstallAsync</c> override - mirroring
///     the "extract then delete the archive, leaving only its extracted entries" pattern already
///     used by this project's zip-archive test fakes.
/// </remarks>
internal static class TarBz2ArchiveExtractor
{
    /// <summary>
    ///     Extracts every file entry of a <c>.tar.bz2</c> archive into a destination directory,
    ///     preserving the archive's own relative folder structure, and then deletes the archive
    ///     file.
    /// </summary>
    /// <param name="archivePath">
    ///     The absolute path of the downloaded <c>.tar.bz2</c> archive file. Must not be null or
    ///     empty, and must name a file that exists.
    /// </param>
    /// <param name="destinationDirectory">
    ///     The absolute path of the directory to extract the archive's entries into. Must not be
    ///     null or empty; created entries are written using the archive's own relative paths, so
    ///     an archive whose entries are all nested under one top-level folder produces that same
    ///     folder under this directory.
    /// </param>
    /// <param name="cancellationToken">A token to observe while extracting.</param>
    /// <returns>A task that completes once every entry has been extracted and the archive file removed.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="archivePath"/> or <paramref name="destinationDirectory"/>
    ///     is null or empty.
    /// </exception>
    /// <remarks>
    ///     Directory entries within the archive are skipped explicitly - <c>SharpCompress</c>
    ///     creates any directory an extracted file entry needs as a side effect of
    ///     <c>ExtractFullPath = true</c>, so a separate directory-entry write is both unnecessary
    ///     and (for some archive layouts) redundant. An exception thrown while reading or
    ///     extracting propagates to the caller, leaving the archive file in place - the model's
    ///     staged files are discarded by <c>SpeechModelDownloader</c>/<c>SpeechModelStore</c> the
    ///     same way any other install-hook failure is, so a partially extracted directory never
    ///     becomes an installed model's <c>current/</c> content.
    /// </remarks>
    internal static async Task ExtractAndDeleteAsync(
        string archivePath,
        string destinationDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(archivePath);
        ArgumentException.ThrowIfNullOrEmpty(destinationDirectory);

        var extractionOptions = new ExtractionOptions { ExtractFullPath = true, Overwrite = true };

        using (var archiveStream = File.OpenRead(archivePath))
        using (var reader = ReaderFactory.Open(archiveStream))
        {
            while (await reader.MoveToNextEntryAsync(cancellationToken).ConfigureAwait(false))
            {
                if (reader.Entry.IsDirectory)
                {
                    continue;
                }

                await reader
                    .WriteEntryToDirectoryAsync(destinationDirectory, extractionOptions, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        // Only remove the archive once every entry extracted successfully - an exception above
        // leaves the archive file in place, which is irrelevant anyway since the whole staging
        // directory (archive included) is discarded on install-hook failure.
        File.Delete(archivePath);
    }
}
