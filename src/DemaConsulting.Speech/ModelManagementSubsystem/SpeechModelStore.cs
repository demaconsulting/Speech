using System.Text.Json;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Owns the on-disk layout, atomic install/replace, and uninstall of downloaded models under
///     a single per-user root directory.
/// </summary>
/// <remarks>
///     Implements the atomic-swap design resolving the download-while-in-use residual risk: for
///     each model id, this type manages
///     <c>{root}/{model-id}/current/</c> (the stable, installed content, only ever replaced by a
///     directory rename), <c>{root}/{model-id}/install-manifest.json</c> (a sidecar written only
///     after a successful swap, so the store can report "installed" cheaply without re-hashing
///     gigabytes on every query), and <c>{root}/{model-id}/.tmp/{operation-id}/</c> (scratch space
///     for an in-progress download/install, never read as installed content). <c>current/</c> is
///     never touched while a download is being fetched and verified, so a model already loaded by
///     an in-use recognizer/synthesizer continues reading unaffected content until the swap
///     completes; the residual risk around in-use file handles during old-directory cleanup is
///     deferred rather than assumed away, by making that cleanup best-effort and non-blocking.
/// </remarks>
public sealed class SpeechModelStore
{
    /// <summary>The name of the stable, installed-content directory within a model's directory.</summary>
    private const string CurrentDirectoryName = "current";

    /// <summary>The name of the install-manifest sidecar file within a model's directory.</summary>
    private const string ManifestFileName = "install-manifest.json";

    /// <summary>The name of the scratch/staging directory within a model's directory.</summary>
    private const string StagingRootName = ".tmp";

    /// <summary>The prefix given to a renamed-away former <c>current/</c> directory pending best-effort cleanup.</summary>
    private const string ReplacedDirectoryPrefix = "current.replaced-";

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelStore"/> class.
    /// </summary>
    /// <param name="options">
    ///     Optional host-configured storage options, or <see langword="null"/> to use the default
    ///     per-user <see cref="Environment.SpecialFolder.LocalApplicationData"/> root.
    /// </param>
    /// <remarks>Never throws; the root directory is created lazily on first write.</remarks>
    public SpeechModelStore(SpeechModelStoreOptions? options = null)
    {
        RootPath = !string.IsNullOrEmpty(options?.RootPathOverride)
            ? options.RootPathOverride
            : Path.Join(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "DemaConsulting.Speech",
                "Models");
    }

    /// <summary>
    ///     Gets the root directory under which every model's own directory is stored. It is the
    ///     absolute per-user default, or <see cref="SpeechModelStoreOptions.RootPathOverride"/>
    ///     verbatim (so as absolute as the host made it).
    /// </summary>
    public string RootPath { get; }

    /// <summary>
    ///     Gets the directory containing every artifact (installed content, manifest, and
    ///     scratch space) for one model.
    /// </summary>
    /// <param name="modelId">The model identifier. Must not be null, empty, or an invalid directory name.</param>
    /// <returns>The absolute path <c>{RootPath}/{modelId}</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelId"/> is null, empty, or not a valid directory name.</exception>
    public string GetModelDirectory(string modelId)
    {
        ValidateModelId(modelId);
        return Path.Join(RootPath, modelId);
    }

    /// <summary>
    ///     Gets the directory holding a model's stable, installed content.
    /// </summary>
    /// <param name="modelId">The model identifier. Must not be null, empty, or an invalid directory name.</param>
    /// <returns>The absolute path to the model's <c>current/</c> directory. The directory itself may not yet exist.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelId"/> is null, empty, or not a valid directory name.</exception>
    public string GetCurrentDirectory(string modelId) =>
        Path.Join(GetModelDirectory(modelId), CurrentDirectoryName);

    /// <summary>
    ///     Determines whether a model is currently installed and ready to use.
    /// </summary>
    /// <param name="modelId">The model identifier. Must not be null, empty, or an invalid directory name.</param>
    /// <returns>
    ///     <see langword="true"/> when both <c>current/</c> exists and a valid install manifest
    ///     accompanies it; <see langword="false"/> otherwise, including when <c>current/</c>
    ///     exists but the manifest is missing or unreadable (a crash between swap and manifest
    ///     write), which is treated as not-installed/corrupt rather than falsely "installed".
    /// </returns>
    /// <remarks>Never throws for ordinary storage state; an unreadable manifest or I/O error is treated as "not installed".</remarks>
    public bool IsInstalled(string modelId) =>
        TryReadManifest(modelId) is not null && Directory.Exists(GetCurrentDirectory(modelId));

    /// <summary>
    ///     Begins a new install/repair operation for a model by creating a fresh, empty staging
    ///     directory that is never mistaken for installed content.
    /// </summary>
    /// <param name="modelId">The model identifier. Must not be null, empty, or an invalid directory name.</param>
    /// <returns>
    ///     A tuple of a new, unique operation id and the absolute path to its staging directory
    ///     (<c>{ModelDirectory}/.tmp/{operationId}</c>), already created and empty.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelId"/> is null, empty, or not a valid directory name.</exception>
    /// <remarks>
    ///     Called by <see cref="SpeechModelDownloader"/> before downloading; <c>current/</c> is
    ///     never touched by this call, so any model already in use is unaffected.
    /// </remarks>
    internal (string OperationId, string StagingDirectory) BeginStaging(string modelId)
    {
        var operationId = Guid.NewGuid().ToString("N");
        var stagingDirectory = Path.Join(GetModelDirectory(modelId), StagingRootName, operationId);

        // Another installer finishing may delete the empty .tmp parent between its creation and ours
        // (TryDeleteEmptyDirectory), so retry the creation a few times
        var attempt = 0;
        while (true)
        {
            try
            {
                Directory.CreateDirectory(stagingDirectory);
                break;
            }
            catch (Exception ex) when (++attempt < 5 && ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(10 * attempt);
            }
        }

        return (operationId, stagingDirectory);
    }

    /// <summary>
    ///     Atomically swaps a fully verified staging directory into place as the model's new
    ///     <c>current/</c> content, then writes the install manifest.
    /// </summary>
    /// <param name="modelId">The model identifier. Must not be null, empty, or an invalid directory name.</param>
    /// <param name="operationId">The operation id returned by the matching <see cref="BeginStaging"/> call.</param>
    /// <param name="stagingDirectory">The verified staging directory to install, as returned by <see cref="BeginStaging"/>.</param>
    /// <param name="totalSizeBytes">The total installed size in bytes, recorded in the manifest for cheap reporting.</param>
    /// <remarks>
    ///     If <c>current/</c> already exists (a repair/re-download), it is first renamed aside to
    ///     <c>current.replaced-{operationId}</c> (a metadata-only rename that does not touch file
    ///     contents), then <paramref name="stagingDirectory"/> is renamed into <c>current/</c>.
    ///     The manifest is written only after the rename succeeds, so a crash between the two
    ///     steps leaves <c>current/</c> present but the manifest missing - which
    ///     <see cref="IsInstalled"/> correctly reports as not-installed, making a repeat of this
    ///     entire sequence safe. On a repair the previous manifest is deleted before the swap (and restored if the
    ///     swap fails and the old content is put back), so it never vouches for the new content. The old
    ///     <c>current.replaced-{operationId}</c> directory (if any)
    ///     is then deleted best-effort; a failure (e.g. an open file handle held by an in-use
    ///     model) is swallowed and left for <see cref="CleanUpLeftovers"/> to retry later.
    /// </remarks>
    internal void CompleteInstall(string modelId, string operationId, string stagingDirectory, long totalSizeBytes)
    {
        var modelDirectory = GetModelDirectory(modelId);
        var currentDirectory = GetCurrentDirectory(modelId);

        // Rename any existing installed content aside first - a plain directory rename does not
        // require exclusive access to the files inside it, so an in-use model keeps working.
        string? replacedDirectory = null;
        string? previousManifest = null;
        if (Directory.Exists(currentDirectory))
        {
            // On a repair the old manifest would otherwise still vouch for the new content during the swap, so
            // remove it first (keeping its text for rollback): a crash from here on reads as not-installed
            var manifestPath = GetManifestPath(modelId);
            if (File.Exists(manifestPath))
            {
                previousManifest = File.ReadAllText(manifestPath);
                File.Delete(manifestPath);
            }

            replacedDirectory = Path.Join(modelDirectory, ReplacedDirectoryPrefix + operationId);
            try
            {
                Directory.Move(currentDirectory, replacedDirectory);
            }
            catch
            {
                RestoreManifest(modelId, previousManifest);
                throw;
            }
        }

        // The verified staging directory becomes the new current/ - also a same-volume rename,
        // atomic and effectively instantaneous on NTFS/POSIX.
        try
        {
            Directory.Move(stagingDirectory, currentDirectory);
        }
        catch
        {
            // Roll back so the previously installed content is restored rather than left
            // renamed aside (which would make a working model look not-installed)
            if (replacedDirectory is not null && !Directory.Exists(currentDirectory))
            {
                try
                {
                    Directory.Move(replacedDirectory, currentDirectory);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Best-effort: the original failure is the one worth reporting
                }

                if (Directory.Exists(currentDirectory))
                {
                    RestoreManifest(modelId, previousManifest);
                }
            }

            throw;
        }

        // The staging root is now usually empty; remove it so no stray .tmp folder is left behind
        TryDeleteEmptyDirectory(Path.Join(modelDirectory, StagingRootName));

        // Write the manifest last: only a crash after this point is indistinguishable from a
        // fully successful install, and that is the safe direction for the failure to lean.
        var manifest = new InstallManifest(totalSizeBytes, DateTimeOffset.UtcNow);
        File.WriteAllText(GetManifestPath(modelId), JsonSerializer.Serialize(manifest));

        // Best-effort cleanup of the now-superseded directory; never blocks or throws on failure.
        if (replacedDirectory is not null)
        {
            TryDeleteDirectoryRecursively(replacedDirectory);
        }
    }

    /// <summary>
    ///     Abandons a failed or canceled install/repair operation by deleting its staging
    ///     directory, without touching <c>current/</c>.
    /// </summary>
    /// <param name="stagingDirectory">The staging directory returned by <see cref="BeginStaging"/> to discard.</param>
    /// <remarks>
    ///     Best-effort: a deletion failure is swallowed and left for <see cref="CleanUpLeftovers"/>
    ///     to retry later, consistent with this store's non-blocking cleanup policy.
    /// </remarks>
    internal static void AbandonStaging(string stagingDirectory) => TryDeleteDirectoryRecursively(stagingDirectory);

    /// <summary>
    ///     Removes a model's installed content, manifest, and any superseded <c>current.replaced-*</c> directories.
    /// </summary>
    /// <param name="modelId">The model identifier. Must not be null, empty, or an invalid directory name.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelId"/> is null, empty, or not a valid directory name.</exception>
    /// <exception cref="SpeechModelStoreException">
    ///     Thrown when <c>current/</c> cannot be removed, most commonly because another process
    ///     (e.g. an in-use recognizer/synthesizer) still holds an open file handle into it.
    /// </exception>
    /// <remarks>
    ///     This is an explicit, user-invoked operation (never called during composition or
    ///     catalog enumeration), so throwing on failure here is consistent with this library's
    ///     "nothing throws at composition" decision - it does not apply to explicit, first-use
    ///     actions like an intentional uninstall.
    /// </remarks>
    public void Uninstall(string modelId)
    {
        var currentDirectory = GetCurrentDirectory(modelId);
        if (Directory.Exists(currentDirectory))
        {
            try
            {
                Directory.Delete(currentDirectory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new SpeechModelStoreException(
                    $"Failed to uninstall model '{modelId}': its installed directory could not be removed, " +
                    "most likely because another process still has an open file handle into it.",
                    ex);
            }
        }

        var manifestPath = GetManifestPath(modelId);
        if (File.Exists(manifestPath))
        {
            try
            {
                File.Delete(manifestPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new SpeechModelStoreException(
                    $"Failed to uninstall model '{modelId}': its install manifest could not be removed, " +
                    "most likely because another process still has an open file handle into it.",
                    ex);
            }
        }

        // Only superseded current.replaced-* directories are removed here; .tmp/* may be a concurrent installer's
        // live staging directory, so it is left for CleanUpLeftovers
        var modelDirectory = GetModelDirectory(modelId);
        if (Directory.Exists(modelDirectory))
        {
            foreach (var directory in Directory.EnumerateDirectories(modelDirectory, ReplacedDirectoryPrefix + "*"))
            {
                TryDeleteDirectoryRecursively(directory);
            }
        }
    }

    /// <summary>Writes back a manifest removed earlier in a repair, best-effort.</summary>
    /// <param name="modelId">The model identifier.</param>
    /// <param name="manifestText">The previous manifest text, or <see langword="null"/> when there was none.</param>
    private void RestoreManifest(string modelId, string? manifestText)
    {
        if (manifestText is null)
        {
            return;
        }

        try
        {
            File.WriteAllText(GetManifestPath(modelId), manifestText);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: without it the model reads as not-installed and a repeat install repairs it
        }
    }

    /// <summary>
    ///     Best-effort deletes any leftover <c>current.replaced-*</c> or <c>.tmp/*</c> directories
    ///     for a model, left behind by a prior interrupted install or an uninstall that could not
    ///     immediately remove them.
    /// </summary>
    /// <param name="modelId">The model identifier. Must not be null, empty, or an invalid directory name.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelId"/> is null, empty, or not a valid directory name.</exception>
    /// <remarks>
    ///     Never throws for a deletion that still fails (e.g. an open file handle) - the leftover
    ///     directory is simply left in place for a future call to retry, per this store's
    ///     non-blocking cleanup policy. Safe to call at any time, including when nothing is left
    ///     to clean up.
    /// </remarks>
    public void CleanUpLeftovers(string modelId)
    {
        var modelDirectory = GetModelDirectory(modelId);
        if (!Directory.Exists(modelDirectory))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(modelDirectory, ReplacedDirectoryPrefix + "*"))
        {
            TryDeleteDirectoryRecursively(directory);
        }

        var stagingRoot = Path.Join(modelDirectory, StagingRootName);
        if (Directory.Exists(stagingRoot))
        {
            foreach (var directory in Directory.EnumerateDirectories(stagingRoot))
            {
                TryDeleteDirectoryRecursively(directory);
            }
        }
    }

    /// <summary>
    ///     Reads and parses a model's install manifest, if present and well-formed.
    /// </summary>
    /// <param name="modelId">The model identifier. Must not be null, empty, or an invalid directory name.</param>
    /// <returns>
    ///     The parsed manifest, or <see langword="null"/> when the manifest file does not exist,
    ///     cannot be read, or is not valid JSON - all of which are treated identically by
    ///     <see cref="IsInstalled"/> as "not installed" rather than as an error.
    /// </returns>
    internal InstallManifest? TryReadManifest(string modelId)
    {
        var manifestPath = GetManifestPath(modelId);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<InstallManifest>(File.ReadAllText(manifestPath));

            // A "{}" (or otherwise field-less) manifest deserializes to default values; a genuine
            // manifest always records a real install timestamp, so treat defaults as malformed
            return manifest is not null && manifest.InstalledAtUtc != default ? manifest : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    ///     Gets the absolute path to a model's install-manifest sidecar file.
    /// </summary>
    /// <param name="modelId">The model identifier. Must not be null, empty, or an invalid directory name.</param>
    private string GetManifestPath(string modelId) => Path.Join(GetModelDirectory(modelId), ManifestFileName);

    /// <summary>
    ///     Validates that a model identifier is non-empty and safe to use as a single directory
    ///     name segment.
    /// </summary>
    /// <param name="modelId">The model identifier to validate.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="modelId"/> is null, empty, whitespace-only, contains a
    ///     character invalid in a file name (which would otherwise let a model id escape its own
    ///     directory or collide with the store's reserved directory/file names), or is exactly
    ///     <c>"."</c> or <c>".."</c> (a relative-path segment that <see cref="Path.Join(string,string)"/>
    ///     and <see cref="Uri"/> construction would both silently normalize into traversing out
    ///     of this model's own directory/mirror subtree, bypassing every other character check).
    /// </exception>
    private static void ValidateModelId(string modelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelId);

        if (modelId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException($"Model id '{modelId}' is not a valid directory name.", nameof(modelId));
        }

        // Neither "." nor ".." contains an invalid file name character, so the check above alone
        // never rejects them - yet both are reserved relative-path segments that Path.Join (for
        // the on-disk model directory) and Uri construction (for a resolved mirror request URI)
        // both normalize away, letting a model id escape its own directory/mirror subtree
        // entirely rather than naming a literal "." or ".." subdirectory within it.
        if (modelId is "." or "..")
        {
            throw new ArgumentException(
                $"Model id '{modelId}' must not be a relative-path segment ('.' or '..').",
                nameof(modelId));
        }
    }

    /// <summary>
    ///     Deletes a directory only when it is empty, swallowing any failure so cleanup never blocks or throws.
    /// </summary>
    /// <param name="path">The directory to delete, if it exists and is empty.</param>
    private static void TryDeleteEmptyDirectory(string path)
    {
        try
        {
            // A non-recursive delete fails (and is ignored) when another operation still has a staging directory in it
            if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
            {
                Directory.Delete(path, recursive: false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort: a concurrent operation may be using it; leave it for a later cleanup.
        }
    }

    /// <summary>
    ///     Deletes a directory tree, swallowing any failure so cleanup never blocks or throws.
    /// </summary>
    /// <param name="path">The directory to delete, if it exists.</param>
    /// <remarks>
    ///     A failure here is almost always an open file handle held by another process (most
    ///     commonly seen on Windows); the directory is simply left for a later cleanup attempt
    ///     rather than treated as an error, per this store's non-blocking cleanup policy.
    /// </remarks>
    private static void TryDeleteDirectoryRecursively(string path)
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
            // Best-effort: leave the directory for a future CleanUpLeftovers call to retry.
        }
    }

    /// <summary>
    ///     The on-disk sidecar recorded alongside a successful atomic swap, letting
    ///     <see cref="IsInstalled"/> report "installed" cheaply without re-hashing gigabytes of
    ///     model content on every query.
    /// </summary>
    /// <param name="TotalSizeBytes">The total installed size in bytes, for cheap reporting.</param>
    /// <param name="InstalledAtUtc">The UTC timestamp at which the install/repair completed.</param>
    internal sealed record InstallManifest(long TotalSizeBytes, DateTimeOffset InstalledAtUtc);
}
