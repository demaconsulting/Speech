using System.Collections.Concurrent;
using System.Text.Json;
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

    /// <summary>The name prefix of the sibling folder an upgrade renames the installed <c>current</c> folder to.</summary>
    private const string UpgradeBackupPrefix = "current.upgrade-backup-";

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

    /// <summary>Reference counts of install calls (running or queued) per pack id; guarded by <see cref="_installingGate"/>.</summary>
    private readonly Dictionary<string, int> _installing = new(StringComparer.Ordinal);

    /// <summary>Guards <see cref="_installing"/>.</summary>
    private readonly object _installingGate = new();

    /// <summary>One semaphore per pack id that serializes whole install calls for that pack.</summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _installLocks = new(StringComparer.Ordinal);

    /// <summary>Pack ids whose most recent install attempt failed.</summary>
    private readonly ConcurrentDictionary<string, byte> _failed = new(StringComparer.Ordinal);

    /// <summary>The packs this instance can install (the built-in list unless a test supplies its own).</summary>
    private readonly IReadOnlyList<ReferenceVoicePack> _packs;

    /// <summary>The lazily created downloader.</summary>
    private SpeechModelDownloader? _downloader;

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
        lock (_installingGate)
        {
            if (_installing.ContainsKey(pack.Id))
            {
                return SpeechModelState.Downloading;
            }
        }

        if (_packStore.IsInstalled(pack.Id))
        {
            return SpeechModelState.Downloaded;
        }

        return _failed.ContainsKey(pack.Id) ? SpeechModelState.FailedOrCorrupt : SpeechModelState.NotDownloaded;
    }

    /// <summary>
    ///     Downloads, verifies and installs a pack, optionally with its emotion (mood) clips; a no-op when it is already
    ///     installed with everything requested.
    /// </summary>
    /// <param name="packId">The pack id.</param>
    /// <param name="includeEmotions">
    ///     <see langword="true"/> (the default) to also extract the pack's <c>voices-emotion</c> clips; <see langword="false"/>
    ///     for the base voices only. The archive is the same single download either way.
    /// </param>
    /// <param name="progress">An optional progress sink for the archive transfer.</param>
    /// <param name="cancellationToken">A token that aborts the install.</param>
    /// <returns>The classified result; only <see cref="SpeechModelDownloadOutcome.Installed"/> means voices are available.</returns>
    /// <remarks>
    ///     <b>Upgrade</b>: when the pack is installed without emotions and <paramref name="includeEmotions"/> is
    ///     <see langword="true"/>, the installed <c>current</c> folder is renamed to a sibling backup folder and the
    ///     pack is installed again (the downloader never replaces an installed id). The backup is deleted on success
    ///     and renamed back on any other outcome or exception, so a failed or canceled re-download leaves the previous
    ///     voices-only install intact. An installed pack that already has emotions is never downgraded by
    ///     <c>includeEmotions: false</c>; uninstall it first. Calls for the same pack are serialized (a second caller
    ///     waits for the first and then observes its result), and <see cref="GetPackState"/> reports
    ///     <see cref="SpeechModelState.Downloading"/> until the last concurrent caller finishes.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> is not a known pack.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this instance is disposed.</exception>
    /// <exception cref="SpeechModelStoreException">Thrown when an upgrade cannot remove the existing install because its files are in use.</exception>
    public async Task<SpeechModelDownloadResult> InstallPackAsync(
        string packId,
        bool includeEmotions = true,
        IProgress<SpeechModelDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var pack = FindPack(packId);
        var downloader = GetDownloader();

        lock (_installingGate)
        {
            _installing[pack.Id] = _installing.GetValueOrDefault(pack.Id) + 1;
        }

        var gate = _installLocks.GetOrAdd(pack.Id, _ => new SemaphoreSlim(1, 1));
        var acquired = false;
        try
        {
            // Serialize the whole call (upgrade check included) so concurrent callers cannot interleave
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;
            return await InstallSerializedAsync(pack, downloader, includeEmotions, progress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (acquired)
            {
                gate.Release();
            }

            // Only the last concurrent caller clears the in-progress state
            lock (_installingGate)
            {
                var remaining = _installing.GetValueOrDefault(pack.Id) - 1;
                if (remaining <= 0)
                {
                    _installing.Remove(pack.Id);
                }
                else
                {
                    _installing[pack.Id] = remaining;
                }
            }
        }
    }

    /// <summary>
    ///     Downloads, verifies and installs a pack with its emotion clips; equivalent to
    ///     <see cref="InstallPackAsync(string, bool, IProgress{SpeechModelDownloadProgress}, CancellationToken)"/> with
    ///     <c>includeEmotions: true</c>.
    /// </summary>
    /// <param name="packId">The pack id.</param>
    /// <param name="progress">A progress sink for the archive transfer, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token that aborts the install.</param>
    /// <returns>The classified result.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> is not a known pack.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    /// <exception cref="ObjectDisposedException">Thrown when this instance is disposed.</exception>
    public Task<SpeechModelDownloadResult> InstallPackAsync(
        string packId,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken) =>
        InstallPackAsync(packId, includeEmotions: true, progress, cancellationToken);

    /// <summary>Runs one install while holding the pack's install lock, backing up the installed copy during an upgrade.</summary>
    private async Task<SpeechModelDownloadResult> InstallSerializedAsync(
        ReferenceVoicePack pack,
        SpeechModelDownloader downloader,
        bool includeEmotions,
        IProgress<SpeechModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        // A crash during an earlier upgrade may have left the install renamed away
        await RecoverAbandonedBackupsAsync(pack.Id).ConfigureAwait(false);

        string? backup = null;
        var installed = false;
        try
        {
            if (includeEmotions && _packStore.IsInstalled(pack.Id) && !await PackHasEmotionsAsync(pack.Id, cancellationToken).ConfigureAwait(false))
            {
                // Upgrade: the downloader fast-paths an installed id, so move the voices-only install aside
                // (not delete it) so any failure can put it back
                backup = MoveCurrentAside(pack.Id);
            }

            var result = await downloader
                .DownloadAsync(
                    pack.Id,
                    pack.DownloadDescriptor,
                    (stagingDirectory, token) => ReferenceVoicePackInstaller.InstallAsync(pack, includeEmotions, stagingDirectory, token),
                    progress,
                    cancellationToken)
                .ConfigureAwait(false);

            installed = result.Outcome == SpeechModelDownloadOutcome.Installed;
            if (installed)
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
            if (backup is not null)
            {
                // Cleanup must finish even when the install was canceled, so it takes no token
                if (installed)
                {
                    await TryDeleteDirectoryAsync(backup).ConfigureAwait(false);
                }
                else
                {
                    await RestoreBackupAsync(pack.Id, backup).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>Renames the pack's <c>current</c> folder to a unique sibling backup folder and returns the backup path.</summary>
    /// <exception cref="SpeechModelStoreException">Thrown when the rename fails because the files are in use.</exception>
    private string MoveCurrentAside(string packId)
    {
        var backup = Path.Join(_packStore.GetModelDirectory(packId), UpgradeBackupPrefix + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.Move(_packStore.GetCurrentDirectory(packId), backup);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SpeechModelStoreException(
                $"Failed to upgrade reference voice pack '{packId}': its installed directory could not be moved aside, " +
                "most likely because another process still has an open file handle into it.",
                ex);
        }

        return backup;
    }

    /// <summary>Puts a backup back as the pack's <c>current</c> folder, discarding any partial replacement.</summary>
    /// <remarks>Directory deletion has no asynchronous API, so it runs on the thread pool.</remarks>
    private Task RestoreBackupAsync(string packId, string backup) => Task.Run(() => RestoreBackup(packId, backup));

    /// <summary>Synchronous body of <see cref="RestoreBackupAsync"/>.</summary>
    private void RestoreBackup(string packId, string backup)
    {
        var current = _packStore.GetCurrentDirectory(packId);
        try
        {
            if (Directory.Exists(current))
            {
                Directory.Delete(current, recursive: true);
            }

            Directory.Move(backup, current);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Left in place; RecoverAbandonedBackups retries on the next install
            _diagnostics.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                $"Could not restore reference voice pack '{packId}' after a failed upgrade: {ex.Message}");
        }
    }

    /// <summary>Restores or deletes backup folders left behind by an interrupted upgrade.</summary>
    /// <remarks>Directory deletion has no asynchronous API, so it runs on the thread pool.</remarks>
    private Task RecoverAbandonedBackupsAsync(string packId) => Task.Run(() => RecoverAbandonedBackups(packId));

    /// <summary>Synchronous body of <see cref="RecoverAbandonedBackupsAsync"/>.</summary>
    private void RecoverAbandonedBackups(string packId)
    {
        var modelDirectory = _packStore.GetModelDirectory(packId);
        if (!Directory.Exists(modelDirectory))
        {
            return;
        }

        foreach (var backup in Directory.EnumerateDirectories(modelDirectory, UpgradeBackupPrefix + "*"))
        {
            if (Directory.Exists(_packStore.GetCurrentDirectory(packId)))
            {
                TryDeleteDirectory(backup);
            }
            else
            {
                RestoreBackup(packId, backup);
            }
        }
    }

    /// <summary>Deletes a directory tree on the thread pool, ignoring failures.</summary>
    private static Task TryDeleteDirectoryAsync(string path) => Task.Run(() => TryDeleteDirectory(path));

    /// <summary>Deletes a directory tree, ignoring failures.</summary>
    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: RecoverAbandonedBackups retries on the next install.
        }
    }

    /// <summary>
    ///     Gets the variants an installed pack was extracted with, as recorded in its <c>pack.json</c>.
    /// </summary>
    /// <param name="packId">The pack id.</param>
    /// <returns>
    ///     The variant names (<c>voices</c>, and <c>voices-emotion</c> when the emotion clips were extracted); empty
    ///     when the pack is not installed or its metadata is missing or unreadable. Never throws for a known id.
    /// </returns>
    /// <param name="cancellationToken">A token that aborts the read.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> is not a known pack.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public async Task<IReadOnlyList<string>> GetPackInstalledVariantsAsync(string packId, CancellationToken cancellationToken = default)
    {
        var pack = FindPack(packId);
        try
        {
            var path = Path.Join(_packStore.GetCurrentDirectory(pack.Id), ReferenceVoicePackInstaller.PackMetadataFileName);
            if (!File.Exists(path))
            {
                return [];
            }

            var text = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(text);
            if (!document.RootElement.TryGetProperty("variants", out var variants) || variants.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return [.. variants.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString()!)];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            return [];
        }
    }

    /// <summary>
    ///     Determines whether an installed pack was extracted with its emotion (mood) clips.
    /// </summary>
    /// <param name="packId">The pack id.</param>
    /// <returns><see langword="true"/> when the pack is installed with emotion clips; otherwise <see langword="false"/>. Never throws for a known id.</returns>
    /// <param name="cancellationToken">A token that aborts the read.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> is not a known pack.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public async Task<bool> PackHasEmotionsAsync(string packId, CancellationToken cancellationToken = default) =>
        (await GetPackInstalledVariantsAsync(packId, cancellationToken).ConfigureAwait(false))
        .Contains(ReferenceVoicePackInstaller.EmotionsDirectoryName, StringComparer.Ordinal);

    /// <summary>
    ///     Removes an installed pack; a no-op when it is not installed. The user folder is never touched.
    /// </summary>
    /// <param name="packId">The pack id.</param>
    /// <param name="cancellationToken">
    ///     A token that aborts the wait for the pack's operation lock. Once the removal starts it runs to completion.
    /// </param>
    /// <returns>A task that completes when the pack and any upgrade backup are removed.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="packId"/> is not a known pack.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled while waiting for an in-flight install or uninstall of the same pack, or before the removal starts.</exception>
    /// <exception cref="SpeechModelStoreException">Thrown when the pack's files are still in use and cannot be removed.</exception>
    /// <remarks>
    ///     Takes the same per-pack operation lock as <see cref="InstallPackAsync(string, bool, IProgress{SpeechModelDownloadProgress}, CancellationToken)"/>
    ///     and awaits it: when an install (or another uninstall) of the pack is in flight this call waits for it to
    ///     finish, then removes the pack, so the two never interleave. Directory deletion has no asynchronous
    ///     file-system API and runs on the thread pool.
    /// </remarks>
    public async Task UninstallPackAsync(string packId, CancellationToken cancellationToken = default)
    {
        var pack = FindPack(packId);
        var gate = _installLocks.GetOrAdd(pack.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _packStore.UninstallAsync(pack.Id, cancellationToken).ConfigureAwait(false);
            _failed.TryRemove(pack.Id, out _);

            // An uninstall also discards any upgrade backup so it cannot resurrect the pack
            await Task.Run(() => DeleteUpgradeBackups(pack.Id), CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Deletes every upgrade backup folder of a pack, ignoring failures.</summary>
    private void DeleteUpgradeBackups(string packId)
    {
        var modelDirectory = _packStore.GetModelDirectory(packId);
        if (Directory.Exists(modelDirectory))
        {
            foreach (var backup in Directory.EnumerateDirectories(modelDirectory, UpgradeBackupPrefix + "*"))
            {
                TryDeleteDirectory(backup);
            }
        }
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
            var current = _packStore.GetCurrentDirectory(pack.Id);
            var voicesDirectory = Path.Join(current, ReferenceVoicePackInstaller.VoicesDirectoryName);
            AddVoicesFrom(voicesDirectory, ReferenceVoiceSource.Pack, pack, voices, seen, Path.Join(current, ReferenceVoicePackInstaller.EmotionsDirectoryName));
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

    /// <summary>
    ///     Finds the mood clips of one speaker under <c>{emotionsDirectory}\{speaker}\{mood}.flac</c>, keyed by lower-case
    ///     mood and sorted ordinally; <see langword="null"/> when there are none or the folder cannot be read.
    /// </summary>
    private SortedDictionary<string, string>? FindMoodClips(string emotionsDirectory, string speaker)
    {
        try
        {
            var speakerDirectory = Path.Join(emotionsDirectory, speaker);
            if (!Directory.Exists(speakerDirectory))
            {
                return null;
            }

            var clips = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(speakerDirectory)
                .Where(file => AudioFileReader.SupportedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)))
            {
                var mood = Path.GetFileNameWithoutExtension(file).Trim().ToLowerInvariant();
                if (mood.Length > 0)
                {
                    clips.TryAdd(mood, file);
                }
            }

            return clips.Count == 0 ? null : clips;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _diagnostics.Report(SpeechDiagnosticLevel.Warning, DiagnosticsCategory, $"Reference voice mood folder for '{speaker}' could not be read: {ex.Message}");
            return null;
        }
    }

    /// <summary>Adds the supported audio files of one directory, skipping ids that are already present.</summary>
    private void AddVoicesFrom(
        string directory,
        ReferenceVoiceSource source,
        ReferenceVoicePack? pack,
        List<ReferenceVoiceInfo> voices,
        HashSet<string> seen,
        string? emotionsDirectory = null)
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
                Attribution: pack?.Attribution,
                MoodClips: emotionsDirectory is null ? null : FindMoodClips(emotionsDirectory, id)));
        }
    }
}
