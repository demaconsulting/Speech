using System.Text.Json;

namespace DemaConsulting.Speech.ModelBundler;

/// <summary>
///     Publishes built bundle zips to the append-only <c>models</c> GitHub release with the <c>gh</c> command-line
///     tool, never replacing or deleting an existing asset.
/// </summary>
/// <remarks>
///     <para>
///     The release is the download source of every installed bundle, and each package pins each zip's SHA-256, so a
///     published asset must never change. The uploader therefore:
///     </para>
///     <list type="number">
///         <item>reads <c>SHA256SUMS</c> and proves every local zip still has its listed SHA-256;</item>
///         <item>lists the release's assets (<c>gh release view models --json assets</c>);</item>
///         <item>skips a zip whose name is already published with the same digest;</item>
///         <item>refuses the whole upload, before uploading anything, when a name is published with another (or an
///         unknown) digest;</item>
///         <item>uploads each remaining zip with <c>gh release upload</c>, deliberately without <c>--clobber</c>, so
///         even a race cannot overwrite an asset.</item>
///     </list>
///     <para>
///     The release itself is created by hand first (see the tool's README). <c>--dry-run</c> lists the assets and
///     prints the upload commands without running them. Not thread-safe.
///     </para>
/// </remarks>
internal sealed class ReleaseUploader
{
    /// <summary>The tag of the long-lived release that hosts every bundle.</summary>
    internal const string ReleaseTag = "models";

    /// <summary>The repository the bundles are published to by default.</summary>
    internal const string DefaultRepository = "demaconsulting/Speech";

    /// <summary>The GitHub command-line tool.</summary>
    internal const string GhCommand = "gh";

    /// <summary>The prefix GitHub puts before an asset's SHA-256 digest.</summary>
    private const string DigestPrefix = "sha256:";

    /// <summary>The runner that executes <c>gh</c>.</summary>
    private readonly IProcessRunner _runner;

    /// <summary>The console output for progress and results.</summary>
    private readonly TextWriter _log;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ReleaseUploader"/> class.
    /// </summary>
    /// <param name="runner">The runner that executes <c>gh</c>.</param>
    /// <param name="log">The writer that receives progress and result lines.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public ReleaseUploader(IProcessRunner runner, TextWriter log)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(log);
        _runner = runner;
        _log = log;
    }

    /// <summary>
    ///     Uploads every zip listed in <c>{inputDirectory}/SHA256SUMS</c> that the release does not hold yet.
    /// </summary>
    /// <param name="inputDirectory">The directory a <c>build</c> wrote.</param>
    /// <param name="repository">The <c>owner/name</c> repository whose <c>models</c> release receives the zips.</param>
    /// <param name="dryRun"><see langword="true"/> to print the upload commands instead of running them.</param>
    /// <param name="cancellationToken">A token that aborts the upload.</param>
    /// <returns><see langword="true"/> when every zip is published (or would be, in a dry run); otherwise <see langword="false"/>.</returns>
    /// <exception cref="FileNotFoundException">Thrown when <c>SHA256SUMS</c> is missing.</exception>
    /// <exception cref="InvalidDataException">Thrown when <c>SHA256SUMS</c> or the asset list is malformed.</exception>
    public async Task<bool> UploadAsync(string inputDirectory, string repository, bool dryRun, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputDirectory);
        ArgumentException.ThrowIfNullOrEmpty(repository);

        // Every local zip must still be exactly what the build listed
        var lines = ReleaseNotesWriter.ParseChecksums(
            await File.ReadAllTextAsync(Path.Combine(inputDirectory, ReleaseNotesWriter.ChecksumsFileName), cancellationToken).ConfigureAwait(false));
        foreach (var line in lines)
        {
            var path = Path.Combine(inputDirectory, line.FileName);
            var actual = File.Exists(path) ? await BundleBuilder.HashFileAsync(path, cancellationToken).ConfigureAwait(false) : null;
            if (!string.Equals(actual, line.Sha256, StringComparison.Ordinal))
            {
                await _log.WriteLineAsync($"REFUSED {line.FileName}: the local file does not have the listed SHA-256 {line.Sha256}.").ConfigureAwait(false);
                return false;
            }
        }

        // List what the release already holds
        var view = await _runner.RunAsync(
                GhCommand,
                ["release", "view", ReleaseTag, "--repo", repository, "--json", "assets"],
                cancellationToken)
            .ConfigureAwait(false);
        if (view.ExitCode != 0)
        {
            await _log.WriteLineAsync(
                    $"Could not list the '{ReleaseTag}' release of {repository} (create it first with gh release create): {view.StandardError.Trim()}")
                .ConfigureAwait(false);
            return false;
        }

        var published = ParseAssets(view.StandardOutput);

        // Decide every zip before uploading any, so a conflict uploads nothing
        var uploads = new List<string>();
        var refused = false;
        foreach (var line in lines)
        {
            if (!published.TryGetValue(line.FileName, out var digest))
            {
                uploads.Add(line.FileName);
            }
            else if (string.Equals(digest, DigestPrefix + line.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                await _log.WriteLineAsync($"SKIPPED {line.FileName}: already published with the same SHA-256.").ConfigureAwait(false);
            }
            else
            {
                await _log.WriteLineAsync(
                        $"REFUSED {line.FileName}: already published with digest '{digest ?? "unknown"}', not sha256:{line.Sha256}. Publish a new bundle revision (bN) instead.")
                    .ConfigureAwait(false);
                refused = true;
            }
        }

        if (refused)
        {
            return false;
        }

        // Upload without --clobber: gh then fails rather than overwrite an asset that appeared meanwhile
        foreach (var fileName in uploads)
        {
            string[] arguments = ["release", "upload", ReleaseTag, Path.Combine(inputDirectory, fileName), "--repo", repository];
            if (dryRun)
            {
                await _log.WriteLineAsync($"DRY RUN: {GhCommand} {string.Join(' ', arguments)}").ConfigureAwait(false);
                continue;
            }

            await _log.WriteLineAsync($"Uploading {fileName}").ConfigureAwait(false);
            var upload = await _runner.RunAsync(GhCommand, arguments, cancellationToken).ConfigureAwait(false);
            if (upload.ExitCode != 0)
            {
                await _log.WriteLineAsync($"FAILED {fileName}: {upload.StandardError.Trim()}").ConfigureAwait(false);
                return false;
            }

            await _log.WriteLineAsync($"UPLOADED {fileName}").ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Parses <c>gh release view --json assets</c> output into asset digests by name.</summary>
    /// <param name="json">The JSON document <c>{"assets":[{"name":…,"digest":…},…]}</c>.</param>
    /// <returns>Each asset's digest (<c>sha256:…</c>), or <see langword="null"/> when GitHub reports none, by name.</returns>
    /// <exception cref="InvalidDataException">Thrown when the document does not have that shape.</exception>
    internal static IReadOnlyDictionary<string, string?> ParseAssets(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var assets = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var asset in document.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString()
                    ?? throw new InvalidDataException("A release asset has no name.");
                assets[name] = asset.TryGetProperty("digest", out var digest) && digest.ValueKind == JsonValueKind.String
                    ? digest.GetString()
                    : null;
            }

            return assets;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new InvalidDataException("The release asset list is not in the expected 'gh release view --json assets' form.", ex);
        }
    }
}
