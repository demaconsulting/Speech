namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Reports progress of a single file transfer within a model download, suitable for
///     <see cref="IProgress{T}"/> consumption by a GUI progress bar.
/// </summary>
/// <param name="FileIndex">
///     The zero-based index of the file currently being transferred, within the parent
///     <see cref="SpeechModelDownloadDescriptor.Files"/> list.
/// </param>
/// <param name="FileCount">The total number of files being transferred for this model.</param>
/// <param name="BytesTransferred">The number of bytes transferred so far for the current file.</param>
/// <param name="TotalBytes">
///     The current file's total size in bytes, or <see langword="null"/> when the server did not
///     report a <c>Content-Length</c> and the total is not yet known.
/// </param>
/// <remarks>
///     One instance describes progress for the file at <paramref name="FileIndex"/> only; a
///     multi-file model download reports a new sequence of instances per file, with
///     <see cref="FileIndex"/> advancing between files. <see cref="IModelDownloadClient"/>
///     implementations report <see cref="FileIndex"/> as <c>0</c> and <see cref="FileCount"/> as
///     <c>1</c> for the single file they were asked to fetch; <see cref="SpeechModelDownloader"/>
///     rewrites both fields to reflect the file's true position within the model's overall
///     download before forwarding progress to a caller-supplied <see cref="IProgress{T}"/>.
/// </remarks>
public sealed record SpeechModelDownloadProgress(int FileIndex, int FileCount, long BytesTransferred, long? TotalBytes)
{
    /// <summary>
    ///     Gets the fraction of the current file transferred so far, in the range [0, 1], or
    ///     <see langword="null"/> when <see cref="TotalBytes"/> is unknown.
    /// </summary>
    /// <remarks>
    ///     Computed on demand rather than stored, since it is fully determined by
    ///     <see cref="BytesTransferred"/> and <see cref="TotalBytes"/>. A <see cref="TotalBytes"/>
    ///     of zero is treated as fully complete (1.0) rather than dividing by zero.
    /// </remarks>
    public double? FractionComplete
    {
        get
        {
            if (TotalBytes is null)
            {
                return null;
            }

            return TotalBytes.Value == 0 ? 1.0 : (double)BytesTransferred / TotalBytes.Value;
        }
    }
}
