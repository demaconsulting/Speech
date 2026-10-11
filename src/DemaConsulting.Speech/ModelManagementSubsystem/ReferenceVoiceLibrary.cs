using System.Collections.Concurrent;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Manages reference voices for voice-cloning models: installs and removes downloadable
///     <see cref="ReferenceVoicePack"/>s into the per-user store and lists every available voice from the user
///     folder, caller-named directories and installed packs as one de-duplicated list.
/// </summary>
/// <remarks>
///     <b>Layout</b> (under the model store root <c>R</c>):
///     <c>R\reference-voices\{pack-id}\current\</c> holds an installed pack (<c>voices\*.flac</c>, license and
///     readme files, <c>pack.json</c>) beside the store's <c>install-manifest.json</c>, and
///     <c>R\reference-voices\user\</c> is where users drop their own <c>.wav</c>/<c>.flac</c> clips. Packs are
///     installed through a second <see cref="SpeechModelStore"/> rooted at <c>R\reference-voices</c>, so staging,
///     atomic replace, uninstall and leftover cleanup behave exactly as for models; the download itself uses the
///     mirror-aware, hash-verified <see cref="SpeechModelDownloader"/>.
///     <para>
///     <b>Merge order</b> of <see cref="EnumerateVoices"/>: caller-named directories (in the order given), then the
///     user folder, then installed packs; within one directory voices are sorted by file name (ordinal). The first
///     voice with a given id wins; later duplicates are skipped and reported at
///     <see cref="SpeechDiagnosticLevel.Info"/>. Enumeration never creates folders and never throws for a missing
///     folder or an unreadable one.
///     </para>
///     <para>
///     <b>Consent</b>: cloning a voice reproduces a person's vocal identity - show <see cref="ConsentNotice"/> next to
///     any voice-selection UI. Instances are thread-safe. Dispose to release the internal downloader (created
///     lazily on the first install).
///     </para>
/// </remarks>
public sealed class ReferenceVoiceLibrary : IDisposable
{
    /// <summary>The directory name, directly under the store root, that holds packs and the user folder.</summary>
    public const string RootDirectoryName = "reference-voices";

    /// <summary>The name of the user folder inside <see cref="RootPath"/>.</summary>
    public const string UserDirectoryName = "user";

    /// <summary>The notice hosts should show wherever reference voices can be chosen.</summary>
    public const string ConsentNotice =
        "Only clone voices you have permission to use. Reference voices clone a speaker's vocal identity.";

    /// <summary>The diagnostics category used by this type.</summary>
    private const string DiagnosticsCategory = "ModelManagementSubsystem";

    /// <summary>The store that holds installed packs.</summary>
    private readonly SpeechModelStore _packStore;

    /// <summary>The diagnostics sink.</summary>
    private readonly ISpeechDiagnostics _diagnostics;

    /// <summary>The downloader options (mirror) used when the downloader is created.</summary>
    private readonly SpeechModelDownloaderOptions? _downloaderOptions;

    /// <summary>The caller-supplied download client, or <see langword="null"/> to let the downloader create its own.</summary>
    private readonly IModelDownloadClient? _client;

    /// <summary>Guards creation and disposal of <see cref="_downloader"/>.</summary>
    private readonly object _gate = new();

    /// <summary>Pack ids with an install currently running.</summary>
    private readonly ConcurrentDictionary<string, byte> _installing = new(StringComparer.Ordinal);

    /// <summary>Pack ids whose most recent install attempt failed.</summary>
    private readonly ConcurrentDictionary<string, byte> _failed = new(StringComparer.Ordinal);

    /// <summary>The lazily created downloader.</summary>
    private SpeechModelDownloader? _downloader;

    /// <summary>The packs this instance can install (the built-in list unless a test supplies its own).</summary>
    private readonly IReadOnlyList<ReferenceVoicePack> _packs;

    /// <summary>Whether this instance has been disposed.</summary>
    private bool _disposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReferenceVoiceLibrary"/> class.
    /// </summary>
    /// <param name="store">The model store whose root also hosts the <c>reference-voices</c> folder (for example <c>catalog.Store</c>).</param>
    /// <param name="downloaderOptions">Download options such as a mirror, or <see langword="null"/> for none.</param>
    /// <param name="diagnostics">The diagnostics sink, or <see langword="null"/> for none.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is <see langword="null"/>.</exception>
    public ReferenceVoiceLibrary(
        SpeechModelStore store,
        SpeechModelDownloaderOptions? downloaderOptions = null,
        ISpeechDiagnostics? diagnostics = null)
        : this(store, downloaderOptions, diagnostics, client: null, packs: null)
    {
    }

    /// <summary>Initializes a new instance with an injectable download client (tests).</summary>
    internal ReferenceVoiceLibrary(
        SpeechModelStore store,
        SpeechModelDownloaderOptions? downloaderOptions,
        ISpeechDiagnostics? diagnostics,
        IModelDownloadClient? client,
        IReadOnlyList<ReferenceVoicePack>? packs = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        RootPath = Path.Join(store.RootPath, RootDirectoryName);
        _packStore = new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = RootPath });
        _downloaderOptions = downloaderOptions;
        _diagnostics = diagnostics ?? NullSpeechDiagnostics.Instance;
        _client = client;
        _packs = packs ?? ReferenceVoicePack.All;
    }

    /// <summary>Gets the absolute path of the <c>reference-voices</c> folder that holds packs and the user folder.</summary>
    public string RootPath { get; }

    /// <summary>Gets the absolute path of the user folder (it may not exist yet; see <see cref="EnsureUserDirectory"/>).</summary>
    public string UserDirectory => Path.Join(RootPath, UserDirectoryName);

    /// <summary>Gets the packs this library can install.</summary>
    public static IReadOnlyList<ReferenceVoicePack> Packs => ReferenceVoicePack.All;

    /// <summary>
    ///     Creates the user folder if needed.
    /// </summary>
    /// <returns>The absolute path of the user folder.</returns>
    /// <exception cref="IOException">Thrown when the folder cannot be created.</exception>
    public string EnsureUserDirectory()
    {
        Directory.CreateDirectory(UserDirectory);
        return UserDirectory;
    }

    /// <summary>
    ///     Gets the directory an installed pack's content lives in (it may not exist when the pack is not installed).
    /// </summary>
    /// <param name="packId">The pack id.</param>
    /// <returns>The absolute path <c>{RootPath}\{packId}\current</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> is not a known pack.</exception>
    public string GetPackDirectory(string packId) => _packStore.GetCurrentDirectory(FindPack(packId).Id);

    /// <summary>
    ///     Gets the current install state of a pack.
    /// </summary>
    /// <param name="packId">The pack id.</param>
    /// <returns>
    ///     <see cref="SpeechModelState.Downloading"/> while an install runs, <see cref="SpeechModelState.Downloaded"/>
    ///     when installed, <see cref="SpeechModelState.FailedOrCorrupt"/> after a failed install attempt in this
    ///     process, otherwise <see cref="SpeechModelState.NotDownloaded"/>. Never throws for a known id.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> is not a known pack.</exception>
    public SpeechModelState GetPackState(string packId)
    {
        var pack = FindPack(packId);
        if (_installing.ContainsKey(pack.Id))
        {
            return SpeechModelState.Downloading;
        }

        if (_packStore.IsInstalled(pack.Id))
        {
            return SpeechModelState.Downloaded;
        }

        return _failed.ContainsKey(pack.Id) ? SpeechModelState.FailedOrCorrupt : SpeechModelState.NotDownloaded;
    }

    /// <summary>
    ///     Downloads, verifies and installs a pack; a no-op when it is already installed.
    /// </summary>
    /// <param name="packId">The pack id.</param>
    /// <param name="progress">An optional progress sink for the archive transfer.</param>
    /// <param name="cancellationToken">A token that aborts the install.</param>
    /// <returns>The classified result; only <see cref="SpeechModelDownloadOutcome.Installed"/> means voices are available.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> is not a known pack.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this instance is disposed.</exception>
    public async Task<SpeechModelDownloadResult> InstallPackAsync(
        string packId,
        IProgress<SpeechModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var pack = FindPack(packId);
        var downloader = GetDownloader();

        _installing[pack.Id] = 0;
        try
        {
            var result = await downloader
                .DownloadAsync(
                    pack.Id,
                    pack.DownloadDescriptor,
                    (stagingDirectory, token) => ReferenceVoicePackInstaller.InstallAsync(pack, stagingDirectory, token),
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            if (result.Outcome == SpeechModelDownloadOutcome.Installed)
            {
                _failed.TryRemove(pack.Id, out _);
            }
            else
            {
                _failed[pack.Id] = 0;
            }

            return result;
        }
        finally
        {
            _installing.TryRemove(pack.Id, out _);
        }
    }

    /// <summary>
    ///     Removes an installed pack; a no-op when it is not installed. The user folder is never touched.
    /// </summary>
    /// <param name="packId">The pack id.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> is not a known pack.</exception>
    /// <exception cref="SpeechModelStoreException">Thrown when the pack's files are still in use and cannot be removed.</exception>
    public void UninstallPack(string packId)
    {
        var pack = FindPack(packId);
        _packStore.Uninstall(pack.Id);
        _failed.TryRemove(pack.Id, out _);
    }

    /// <summary>
    ///     Lists every available voice, merged and de-duplicated (see the type remarks for the order).
    /// </summary>
    /// <param name="extraDirectories">
    ///     Caller-named directories (for example a <c>--voices-dir</c> option) listed first, in the order given; a
    ///     missing directory is skipped. May be <see langword="null"/>.
    /// </param>
    /// <returns>The voices in precedence order; empty when there are none. Never throws for missing folders.</returns>
    public IReadOnlyList<ReferenceVoiceInfo> EnumerateVoices(IEnumerable<string>? extraDirectories = null)
    {
        var voices = new List<ReferenceVoiceInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var directory in (extraDirectories ?? []).Where(directory => !string.IsNullOrWhiteSpace(directory)))
        {
            AddVoicesFrom(Path.GetFullPath(directory), ReferenceVoiceSource.Directory, pack: null, voices, seen);
        }

        AddVoicesFrom(UserDirectory, ReferenceVoiceSource.User, pack: null, voices, seen);

        foreach (var pack in _packs.Where(pack => _packStore.IsInstalled(pack.Id)))
        {
            var voicesDirectory = Path.Join(_packStore.GetCurrentDirectory(pack.Id), ReferenceVoicePackInstaller.VoicesDirectoryName);
            AddVoicesFrom(voicesDirectory, ReferenceVoiceSource.Pack, pack, voices, seen);
        }

        return voices;
    }

    /// <summary>
    ///     Disposes the internal downloader if it was created.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _downloader?.Dispose();
            _downloader = null;
        }
    }

    /// <summary>Converts a file-name stem such as <c>alan_davis_drake2</c> into <c>Alan Davis Drake 2</c>.</summary>
    internal static string Humanize(string id)
    {
        var words = id.Split(['_', '-', ' '], StringSplitOptions.RemoveEmptyEntries).SelectMany(SplitWord).ToList();
        return words.Count == 0 ? id : string.Join(' ', words);
    }

    /// <summary>Title-cases one word, splitting a trailing number from it so "drake2" reads "Drake 2".</summary>
    private static IEnumerable<string> SplitWord(string word)
    {
        var digitStart = word.Length;
        while (digitStart > 0 && char.IsAsciiDigit(word[digitStart - 1]))
        {
            digitStart--;
        }

        if (digitStart > 0 && digitStart < word.Length)
        {
            return [TitleCase(word[..digitStart]), word[digitStart..]];
        }

        return [TitleCase(word)];
    }
    /// <summary>Upper-cases the first character and lower-cases the rest.</summary>
    private static string TitleCase(string word) =>
        char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant();

    /// <summary>Looks up a known pack by id.</summary>
    private ReferenceVoicePack FindPack(string packId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        return _packs.FirstOrDefault(pack => string.Equals(pack.Id, packId, StringComparison.Ordinal))
            ?? throw new ArgumentException($"'{packId}' is not a known reference-voice pack.", nameof(packId));
    }

    /// <summary>Returns the downloader, creating it on first use.</summary>
    private SpeechModelDownloader GetDownloader()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _downloader ??= new SpeechModelDownloader(_packStore, _client, _diagnostics, _downloaderOptions);
        }
    }

    /// <summary>Adds the supported audio files of one directory, skipping ids that are already present.</summary>
    private void AddVoicesFrom(
        string directory,
        ReferenceVoiceSource source,
        ReferenceVoicePack? pack,
        List<ReferenceVoiceInfo> voices,
        HashSet<string> seen)
    {
        string[] files;
        try
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            files = [.. Directory.EnumerateFiles(directory)
                .Where(file => AudioFileReader.SupportedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _diagnostics.Report(SpeechDiagnosticLevel.Warning, DiagnosticsCategory, $"Reference voice folder '{directory}' could not be read: {ex.Message}");
            return;
        }

        Array.Sort(files, StringComparer.Ordinal);
        foreach (var file in files)
        {
            var id = Path.GetFileNameWithoutExtension(file).Trim();
            if (id.Length == 0)
            {
                continue;
            }

            if (!seen.Add(id))
            {
                _diagnostics.Report(SpeechDiagnosticLevel.Info, DiagnosticsCategory, $"Reference voice '{id}' from '{file}' is hidden by an earlier voice with the same id.");
                continue;
            }

            voices.Add(new ReferenceVoiceInfo(
                id,
                Humanize(id),
                file,
                source,
                pack?.Id,
                id,
                Mood: null,
                LicenseName: pack?.LicenseName,
                Attribution: pack?.Attribution));
        }
    }
}
