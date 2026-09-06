namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Describes the complete, ordered set of files that make up one model's downloadable
///     payload.
/// </summary>
/// <remarks>
///     Every individual <see cref="SpeechModelDownloadFile"/> already validates its own URI
///     scheme, checksum format, and install path at construction (see that type's remarks); this
///     descriptor additionally rejects an empty file list and duplicate install paths, since two
///     files writing to the same relative path would silently clobber one another during install.
/// </remarks>
public sealed record SpeechModelDownloadDescriptor
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelDownloadDescriptor"/> record,
    ///     validating the file list eagerly. See the type-level remarks for the exact rules
    ///     enforced.
    /// </summary>
    /// <param name="Files">
    ///     The ordered files to download for this model. Must contain at least one entry; order
    ///     is preserved and is the order <see cref="SpeechModelDownloader"/> fetches and verifies
    ///     each file in, which matters for deterministic, reproducible progress reporting even
    ///     though install order has no other functional significance.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="Files"/> is empty, contains a <see langword="null"/>
    ///     element, or when two or more entries share the same
    ///     <see cref="SpeechModelDownloadFile.RelativeInstallPath"/> (using an exact,
    ///     case-sensitive comparison).
    /// </exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="Files"/> is <see langword="null"/>.</exception>
    public SpeechModelDownloadDescriptor(IReadOnlyList<SpeechModelDownloadFile> Files)
    {
        ArgumentNullException.ThrowIfNull(Files);

        if (Files.Count == 0)
        {
            throw new ArgumentException(
                "A model download descriptor must declare at least one file.",
                nameof(Files));
        }

        // A null element would only surface later as a NullReferenceException once something
        // dereferences it (e.g. RelativeInstallPath below) - reject it here instead, at the
        // point the caller's mistake was actually made.
        if (Files.Any(file => file is null))
        {
            throw new ArgumentException(
                "A model download descriptor must not contain a null file entry.",
                nameof(Files));
        }

        // Two files landing at the same relative path would silently overwrite one another
        // during install - reject that up front rather than letting install order decide.
        var distinctPathCount = Files
            .Select(file => file.RelativeInstallPath)
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (distinctPathCount != Files.Count)
        {
            throw new ArgumentException(
                "A model download descriptor must not declare two files with the same relative install path.",
                nameof(Files));
        }

        // Defensively copy into an immutable snapshot rather than storing the caller's own list
        // reference, so a later mutation of the caller's list cannot silently change this
        // already-validated descriptor's contents out from under it.
        this.Files = Files.ToList().AsReadOnly();
    }

    /// <summary>
    ///     Gets the ordered files to download for this model.
    /// </summary>
    public IReadOnlyList<SpeechModelDownloadFile> Files { get; }
}
