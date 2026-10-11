namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Immutable description of a downloadable pack of reference voices: where to fetch its archive, how to verify
///     it, which parts of it are extracted, and the license and attribution that travel with the voices.
/// </summary>
/// <remarks>
///     A pack is model-agnostic: it only names audio clips of speakers. Any voice-cloning text-to-speech model can
///     consume the extracted clips through <see cref="ReferenceVoiceLibrary"/>. The pack is downloaded as one
///     whole, SHA-256-verified archive through the same mirror-aware downloader the models use, so a mirror serves
///     it at <c>{mirror}/{Id}/{ArchiveFileName}</c>. Only the <c>voices/</c> clips and the license/readme files are
///     extracted; the archive is deleted afterwards. See <see cref="VoiceZero10"/> for the one built-in pack.
/// </remarks>
public sealed record ReferenceVoicePack
{
    /// <summary>The characters that make a pack id unusable as a single directory name segment.</summary>
    private static readonly char[] InvalidIdCharacters = [.. Path.GetInvalidFileNameChars(), '/', '\\'];

    /// <summary>Gets the built-in voice-zero 1.0 pack (CC0, about 154 speakers, about 570 MiB to download).</summary>
#pragma warning disable S1075 // Download URLs are intentionally compiled-in, reviewed literals.
    public static ReferenceVoicePack VoiceZero10 { get; } = new(
        id: "voice-zero-1.0",
        displayName: "voice-zero 1.0",
        uri: new Uri("https://github.com/OwenTyme/voice-zero/releases/download/1.0/voice-zero-1.0.zip"),
        sha256Checksum: "001d5982fc4b602f7c62a764046dea1f53999a493de43955f35686d2c16d7c66",
        sizeBytes: 597_269_359L,
        archiveFileName: "voice-zero-1.0.zip",
        archiveRootFolder: "voice-zero",
        licenseName: "CC0 1.0 Universal",
        licenseUrl: new Uri("https://creativecommons.org/publicdomain/zero/1.0/"),
        attribution: "voice-zero by OwenTyme",
        sourceUrl: new Uri("https://github.com/OwenTyme/voice-zero"));
#pragma warning restore S1075

    /// <summary>Gets every pack this library version knows about.</summary>
    public static IReadOnlyList<ReferenceVoicePack> All { get; } = [VoiceZero10];

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReferenceVoicePack"/> class, validating every field eagerly.
    /// </summary>
    /// <param name="id">The pack identifier; also the directory name it installs under. Must be a valid single directory name.</param>
    /// <param name="displayName">The human-readable name.</param>
    /// <param name="uri">The HTTPS location of the archive.</param>
    /// <param name="sha256Checksum">The 64-character hexadecimal SHA-256 of the whole archive.</param>
    /// <param name="sizeBytes">The archive size in bytes (informational, for progress and warnings); must be positive.</param>
    /// <param name="archiveFileName">The archive's file name, which is also its mirror-relative name.</param>
    /// <param name="archiveRootFolder">The single top-level folder every archive entry lives under.</param>
    /// <param name="licenseName">The license name.</param>
    /// <param name="licenseUrl">The license page.</param>
    /// <param name="attribution">The attribution line to show with the voices.</param>
    /// <param name="sourceUrl">The upstream project page.</param>
    /// <exception cref="ArgumentException">Thrown when a text argument is blank, the id/file name/root folder is not a safe name, the URI is not HTTPS, or the checksum is malformed.</exception>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sizeBytes"/> is not positive.</exception>
    public ReferenceVoicePack(
        string id,
        string displayName,
        Uri uri,
        string sha256Checksum,
        long sizeBytes,
        string archiveFileName,
        string archiveRootFolder,
        string licenseName,
        Uri licenseUrl,
        string attribution,
        Uri sourceUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(sha256Checksum);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sizeBytes, 0L);
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(archiveRootFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseName);
        ArgumentNullException.ThrowIfNull(licenseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(attribution);
        ArgumentNullException.ThrowIfNull(sourceUrl);

        RequireSafeName(id, nameof(id));
        RequireSafeName(archiveFileName, nameof(archiveFileName));
        RequireSafeName(archiveRootFolder, nameof(archiveRootFolder));

        // The file descriptor enforces the HTTPS scheme and the 64-hex checksum shape
        DownloadDescriptor = new SpeechModelDownloadDescriptor([new SpeechModelDownloadFile(uri, sha256Checksum, archiveFileName)]);

        Id = id;
        DisplayName = displayName;
        Uri = uri;
        Sha256Checksum = sha256Checksum;
        SizeBytes = sizeBytes;
        ArchiveFileName = archiveFileName;
        ArchiveRootFolder = archiveRootFolder;
        LicenseName = licenseName;
        LicenseUrl = licenseUrl;
        Attribution = attribution;
        SourceUrl = sourceUrl;
    }

    /// <summary>Gets the pack identifier (also its directory name in the store).</summary>
    public string Id { get; }

    /// <summary>Gets the human-readable name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the HTTPS location of the archive.</summary>
    public Uri Uri { get; }

    /// <summary>Gets the expected SHA-256 of the whole archive.</summary>
    public string Sha256Checksum { get; }

    /// <summary>Gets the archive size in bytes.</summary>
    public long SizeBytes { get; }

    /// <summary>Gets the archive's file name, which is also its mirror-relative name.</summary>
    public string ArchiveFileName { get; }

    /// <summary>Gets the single top-level folder every archive entry lives under; it is stripped on extraction.</summary>
    public string ArchiveRootFolder { get; }

    /// <summary>Gets the license name.</summary>
    public string LicenseName { get; }

    /// <summary>Gets the license page.</summary>
    public Uri LicenseUrl { get; }

    /// <summary>Gets the attribution line to show with the voices.</summary>
    public string Attribution { get; }

    /// <summary>Gets the upstream project page.</summary>
    public Uri SourceUrl { get; }

    /// <summary>Gets the download descriptor for the archive (one file at <see cref="ArchiveFileName"/>).</summary>
    internal SpeechModelDownloadDescriptor DownloadDescriptor { get; }

    /// <summary>Rejects a name that is not a single, safe path segment.</summary>
    private static void RequireSafeName(string value, string parameterName)
    {
        if (value.IndexOfAny(InvalidIdCharacters) >= 0 || value is "." or "..")
        {
            throw new ArgumentException($"'{value}' is not a valid single file or directory name.", parameterName);
        }
    }
}
