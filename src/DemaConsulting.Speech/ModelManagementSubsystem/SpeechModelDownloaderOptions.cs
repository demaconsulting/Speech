namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Host-configurable options controlling how <see cref="SpeechModelDownloader"/> fetches a
///     model's declared files.
/// </summary>
/// <remarks>
///     Per this library's "universal download mirror" decision, a host almost never needs to
///     supply this type - when omitted (or <see cref="Mirror"/> is left <see langword="null"/>),
///     every file is fetched from exactly the URI its own
///     <see cref="SpeechModelDownloadFile.Uri"/> declares, byte-for-byte identical to this
///     library's behavior before this type existed. <see cref="Mirror"/> exists only for the
///     minority of hosts whose network policy blocks the public hosts every model's declared
///     <see cref="SpeechModelDownloadFile.Uri"/> points at (for example an IT department's
///     TLS-interception policy breaking <c>huggingface.co</c>), letting such a host redirect
///     every model download to its own internal mirror without any change to the models'
///     declared download descriptors.
/// </remarks>
public sealed record SpeechModelDownloaderOptions
{
    /// <summary>Initializes a new instance of the <see cref="SpeechModelDownloaderOptions"/> record.</summary>
    public SpeechModelDownloaderOptions()
    {
    }

    /// <summary>
    ///     Gets an internal mirror that every model's declared files should be fetched from
    ///     instead of their own hardcoded source URIs, or <see langword="null"/> (the default)
    ///     to fetch every file from exactly the URI its own
    ///     <see cref="SpeechModelDownloadFile.Uri"/> declares.
    /// </summary>
    /// <remarks>
    ///     When non-<see langword="null"/>, <see cref="SpeechModelDownloader"/> resolves each
    ///     file's effective request URI beneath <see cref="DownloadMirror.BaseUri"/> (see
    ///     <see cref="SpeechModelDownloader.ResolveEffectiveUri"/>) and, when the mirror declares
    ///     <see cref="DownloadMirror.Credentials"/> or <see cref="DownloadMirror.BearerToken"/>,
    ///     applies that authentication only to requests sent to the mirror.
    /// </remarks>
    public DownloadMirror? Mirror { get; init; }
}
