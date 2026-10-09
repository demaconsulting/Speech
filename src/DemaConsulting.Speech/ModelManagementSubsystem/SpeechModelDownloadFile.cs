namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Describes one downloadable file belonging to a model's download payload: where to fetch it
///     from, the SHA-256 checksum it must match, and where it lands once verified.
/// </summary>
/// <remarks>
///     Validation happens eagerly in the constructor so an invalid descriptor is rejected the
///     moment it is created, not silently accepted and only discovered mid-download.
/// </remarks>
public sealed record SpeechModelDownloadFile
{
    /// <summary>The path-separator characters recognized when splitting <c>RelativeInstallPath</c> into segments.</summary>
    private static readonly char[] PathSeparators = ['/', '\\'];

    /// <summary>
    ///     Splits a declared <see cref="RelativeInstallPath"/> into its individual non-empty
    ///     segments, accepting either path separator (<c>/</c> or <c>\</c>) so a path declared
    ///     with either separator normalizes to the same segments. Shared by every consumer that
    ///     must treat <c>"tokens/vocab.txt"</c> and <c>"tokens\vocab.txt"</c> as the same logical
    ///     path - for example <see cref="SpeechModelDownloadDescriptor"/>'s duplicate-path check
    ///     and <see cref="SpeechModelDownloader"/>'s staging and mirror-URI resolution - so the
    ///     normalization rule cannot drift between them.
    /// </summary>
    /// <param name="relativeInstallPath">The declared relative install path to split.</param>
    /// <returns>The path's non-empty segments, in order. Never empty for an already-validated path.</returns>
    internal static string[] SplitRelativeInstallPathSegments(string relativeInstallPath) =>
        relativeInstallPath.Split(PathSeparators, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelDownloadFile"/> record,
    ///     validating every field eagerly. See the type-level remarks for the exact rules
    ///     enforced.
    /// </summary>
    /// <param name="Uri">
    ///     The HTTPS location to download the file from. Must use the <c>https</c> scheme; per
    ///     this library's download-integrity decision, plain HTTP is never valid for a
    ///     production model download.
    /// </param>
    /// <param name="Sha256Checksum">
    ///     The expected SHA-256 checksum of the downloaded file's bytes, as a 64-character
    ///     lowercase or uppercase hexadecimal string. Verified before the file is treated as
    ///     trustworthy.
    /// </param>
    /// <param name="RelativeInstallPath">
    ///     The file's path, relative to the model's installed directory, that the downloaded
    ///     bytes are written to once verified (e.g. <c>"model.onnx"</c> or
    ///     <c>"tokens/vocab.txt"</c>). Must be a relative path containing at least one named
    ///     segment, with no parent-directory (<c>".."</c>) or current-directory (<c>"."</c>)
    ///     segments - a <c>".."</c> segment could escape the model's own installed directory, a
    ///     <c>"."</c> segment would silently collapse away under
    ///     <see cref="Path.Combine(string[])"/>/<see cref="Uri"/> canonicalization (letting two
    ///     declared paths that look distinct collide at the same staged file), and a path made up
    ///     entirely of separators (e.g. <c>"/"</c>) has no named segment at all and would
    ///     otherwise resolve to the staging directory itself.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="Uri"/> does not use the <c>https</c> scheme, when
    ///     <paramref name="Sha256Checksum"/> is not a 64-character hexadecimal string, or when
    ///     <paramref name="RelativeInstallPath"/> is empty, whitespace-only, rooted, contains a
    ///     <c>"."</c> or <c>".."</c> segment, or contains no named segment at all.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="Uri"/>, <paramref name="Sha256Checksum"/>, or
    ///     <paramref name="RelativeInstallPath"/> is <see langword="null"/>.
    /// </exception>
    public SpeechModelDownloadFile(Uri Uri, string Sha256Checksum, string RelativeInstallPath)
    {
        ArgumentNullException.ThrowIfNull(Uri);
        ArgumentNullException.ThrowIfNull(Sha256Checksum);
        ArgumentNullException.ThrowIfNull(RelativeInstallPath);

        // Reject non-HTTPS URIs outright - a model payload is never fetched over plain HTTP.
        if (!Uri.IsAbsoluteUri || !string.Equals(Uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Model download URI must be an absolute HTTPS URI, but was '{Uri}'.",
                nameof(Uri));
        }

        // A SHA-256 checksum is always exactly 64 hexadecimal characters - reject anything else
        // now rather than failing an obscure format check deep inside the downloader.
        if (Sha256Checksum.Length != 64 || !Sha256Checksum.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException(
                "Model download checksum must be a 64-character hexadecimal SHA-256 digest.",
                nameof(Sha256Checksum));
        }

        // A rooted, parent-escaping, or current-directory ('.') segment could write outside the
        // model's installed directory or silently collide with another declared path once
        // Path.Combine/Uri canonicalization collapses the '.' segment away - reject all three up
        // front rather than trusting descriptor authors. A whitespace-only path is just as
        // meaningless as an empty one, so both are rejected here too. A path made up entirely of
        // separators (e.g. "/" or "\\\\") contains no actual file name at all once split into
        // segments - reject that too, rather than letting it resolve to the staging directory
        // itself (ResolveStagedPath's Path.Combine of zero segments returns stagedFilesDirectory
        // unchanged).
        var segments = SplitRelativeInstallPathSegments(RelativeInstallPath);
        if (string.IsNullOrWhiteSpace(RelativeInstallPath) ||
            Path.IsPathRooted(RelativeInstallPath) ||
            segments.Length == 0 ||
            segments.Any(segment => segment is ".." or "."))
        {
            throw new ArgumentException(
                $"Model download install path must be a relative path with at least one named "
                + $"segment and no '.' or '..' segments, but was '{RelativeInstallPath}'.",
                nameof(RelativeInstallPath));
        }

        this.Uri = Uri;
        this.Sha256Checksum = Sha256Checksum;
        this.RelativeInstallPath = RelativeInstallPath;
    }

    /// <summary>
    ///     Gets the HTTPS location to download the file from.
    /// </summary>
    public Uri Uri { get; }

    /// <summary>
    ///     Gets the expected SHA-256 checksum of the downloaded file's bytes.
    /// </summary>
    public string Sha256Checksum { get; }

    /// <summary>
    ///     Gets the file's path, relative to the model's installed directory.
    /// </summary>
    public string RelativeInstallPath { get; }

    /// <summary>
    ///     Resolves the exact file-system path this declared file is staged at (and is installed
    ///     at, since <see cref="SpeechModelStore"/> atomically swaps the staging directory in
    ///     place), using this platform's own <see cref="Path.DirectorySeparatorChar"/> regardless
    ///     of which separator <see cref="RelativeInstallPath"/> was declared with.
    /// </summary>
    /// <param name="stagedFilesDirectory">
    ///     The directory the file is staged (or installed) under - typically the
    ///     <c>stagedFilesDirectory</c> parameter an <see cref="ISpeechModel.InstallAsync"/>
    ///     override receives.
    /// </param>
    /// <returns>
    ///     The combined, platform-correct file-system path, equivalent to
    ///     <c>Path.Combine(stagedFilesDirectory, segment1, segment2, ...)</c> for every
    ///     non-empty segment of <see cref="RelativeInstallPath"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="stagedFilesDirectory"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    ///     Deliberately an instance method operating only on this already-validated
    ///     <see cref="RelativeInstallPath"/>, rather than a static method accepting an arbitrary
    ///     caller-supplied path string - the constructor's eager rejection of rooted, empty, and
    ///     <c>.</c>/<c>..</c> segments is this type's single validation chokepoint, and a static
    ///     overload taking a raw string would let a caller bypass that chokepoint entirely (for
    ///     example resolving <c>"../outside.bin"</c> straight to a path outside
    ///     <paramref name="stagedFilesDirectory"/>). <see cref="SpeechModelDownloader"/> uses this
    ///     same method to compute each file's staging destination, so an
    ///     <see cref="ISpeechModel.InstallAsync"/> override that needs to locate one of its own
    ///     declared files within <paramref name="stagedFilesDirectory"/> must use this method too,
    ///     rather than <see cref="Path.Join(string, string)"/> or string concatenation directly on
    ///     <see cref="RelativeInstallPath"/> - a raw join would create a single, wrongly named
    ///     file on a platform whose directory separator differs from the one the path happened to
    ///     be declared with (for example a literal <c>"tokens\vocab.txt"</c> joined as one file
    ///     literally named <c>"tokens\vocab.txt"</c> on Linux, instead of the nested file this
    ///     method - and the downloader - actually stage it as).
    /// </remarks>
    public string ResolveStagedPath(string stagedFilesDirectory)
    {
        ArgumentNullException.ThrowIfNull(stagedFilesDirectory);

        return Path.Combine(stagedFilesDirectory, Path.Combine(SplitRelativeInstallPathSegments(RelativeInstallPath)));
    }
}
