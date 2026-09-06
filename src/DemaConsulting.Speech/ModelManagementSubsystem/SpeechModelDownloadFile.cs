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
    ///     Initializes a new instance of the <see cref="SpeechModelDownloadFile"/> record,
    ///     validating every field eagerly. See the type-level remarks for the exact rules
    ///     enforced.
    /// </summary>
    /// <param name="Uri">
    ///     The HTTPS location to download the file from. Must use the <c>https</c> scheme; per
    ///     architecture.md's download-integrity decision, plain HTTP is never valid for a
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
    ///     <c>"tokens/vocab.txt"</c>). Must be a relative path with no parent-directory
    ///     (<c>".."</c>) segments, so a malicious or buggy descriptor cannot write outside the
    ///     model's own installed directory.
    /// </param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="Uri"/> does not use the <c>https</c> scheme, when
    ///     <paramref name="Sha256Checksum"/> is not a 64-character hexadecimal string, or when
    ///     <paramref name="RelativeInstallPath"/> is empty, whitespace-only, rooted, or escapes
    ///     the install directory via a parent-directory segment.
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

        // A rooted or parent-escaping relative path could write outside the model's installed
        // directory - reject both up front rather than trusting descriptor authors. A
        // whitespace-only path is just as meaningless as an empty one, so both are rejected here.
        if (string.IsNullOrWhiteSpace(RelativeInstallPath) ||
            Path.IsPathRooted(RelativeInstallPath) ||
            RelativeInstallPath.Split(PathSeparators).Any(segment => segment == ".."))
        {
            throw new ArgumentException(
                $"Model download install path must be a relative, non-parent-escaping path, but was '{RelativeInstallPath}'.",
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
}
