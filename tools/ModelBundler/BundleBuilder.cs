using System.Globalization;
using System.Security.Cryptography;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.ModelBundler;

/// <summary>
///     Builds deterministic bundle zips from verified vendor files and proves a set of zips reproduces byte for byte.
/// </summary>
/// <remarks>
///     <para>
///     A bundle is built in a fixed sequence: check the definition against the compiled contents and the runtime's own
///     <see cref="SpeechModelBundle"/> rules; resolve and verify every vendor file (see <see cref="VendorFileSource"/>);
///     create the metadata entries; refuse an archive that would not stay below the size limit (GitHub's 2 GiB release
///     asset limit); then write <c>README.txt</c>, <c>LICENSE</c>, <c>NOTICE</c>, <c>model.json</c> and the vendor
///     files in ordinal path order through <see cref="StoredZipWriter"/>, re-checking each vendor file's SHA-256 as it
///     is written. The zip is written under a <c>.partial</c> name and renamed only when complete, so a failed build
///     never leaves a zip that looks finished.
///     </para>
///     <para>
///     Every input is fixed (definitions, compiled pins and the vendor bytes the pins prove), so a rebuild anywhere
///     produces the same bytes, which <see cref="VerifyAsync"/> checks. Not thread-safe: run one build at a time.
///     </para>
/// </remarks>
internal sealed class BundleBuilder
{
    /// <summary>The exclusive size limit of a GitHub release asset (2 GiB).</summary>
    internal const long GitHubAssetLimit = 2L * 1024 * 1024 * 1024;

    /// <summary>The console status of a zip whose SHA-256 equals the pin the packages publish.</summary>
    internal const string MatchStatus = "MATCH";

    /// <summary>The console status of a zip the packages do not pin yet, or pin with another SHA-256.</summary>
    internal const string UpdateCatalogStatus = "UPDATE CATALOG";

    /// <summary>A syntactically valid placeholder zip pin, used only to run the runtime's bundle validation.</summary>
    private static readonly string PlaceholderChecksum = new('0', 64);

    /// <summary>The copy buffer size for reading vendor files and comparing zips.</summary>
    private const int BufferSize = 1024 * 1024;

    /// <summary>The compiled vendor files of each bundle, keyed by bundle name.</summary>
    private readonly IReadOnlyDictionary<string, SpeechModelDownloadDescriptor> _contents;

    /// <summary>The zip pins the packages publish, keyed by bundle name.</summary>
    private readonly IReadOnlyDictionary<string, string> _publishedChecksums;

    /// <summary>The verified source of vendor files.</summary>
    private readonly VendorFileSource _source;

    /// <summary>The console output for progress and results.</summary>
    private readonly TextWriter _log;

    /// <summary>The exclusive upper bound of a bundle zip's length.</summary>
    private readonly long _archiveSizeLimit;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BundleBuilder"/> class.
    /// </summary>
    /// <param name="contents">The compiled vendor files of each bundle, keyed by bundle name (see <see cref="BundleCatalog"/>).</param>
    /// <param name="publishedChecksums">The zip pins the packages publish, keyed by bundle name.</param>
    /// <param name="source">The verified source of vendor files.</param>
    /// <param name="log">The writer that receives progress and result lines.</param>
    /// <param name="archiveSizeLimit">The exclusive upper bound of a bundle zip's length; defaults to <see cref="GitHubAssetLimit"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    public BundleBuilder(
        IReadOnlyDictionary<string, SpeechModelDownloadDescriptor> contents,
        IReadOnlyDictionary<string, string> publishedChecksums,
        VendorFileSource source,
        TextWriter log,
        long archiveSizeLimit = GitHubAssetLimit)
    {
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(publishedChecksums);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(log);
        _contents = contents;
        _publishedChecksums = publishedChecksums;
        _source = source;
        _log = log;
        _archiveSizeLimit = archiveSizeLimit;
    }

    /// <summary>
    ///     Builds one bundle zip, <c>{outputDirectory}/{Name}.zip</c>, replacing any earlier local build.
    /// </summary>
    /// <param name="definition">The bundle definition.</param>
    /// <param name="outputDirectory">The directory that receives the zip (created when missing).</param>
    /// <param name="cancellationToken">A token that aborts the build.</param>
    /// <returns>The written zip with its SHA-256 and length.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the bundle has no compiled contents or breaks a runtime bundle rule, a vendor copy differs from its
    ///     pin, or the zip would not stay below the size limit.
    /// </exception>
    /// <exception cref="VendorFileUnavailableException">Thrown when a vendor file cannot be obtained from any source.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public async Task<BuiltBundle> BuildAsync(BundleDefinition definition, string outputDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);
        var contents = GetValidatedContents(definition);

        // Resolve every vendor file to a verified local copy, in the fixed ordinal entry order
        var files = new List<ResolvedVendorFile>();
        foreach (var file in contents.Files.OrderBy(VendorFileSource.GetEntryName, StringComparer.Ordinal))
        {
            var resolved = await _source.ResolveAsync(file, definition.ModelIds, cancellationToken).ConfigureAwait(false);
            await _log.WriteLineAsync($"  {resolved.Origin}: {resolved.EntryName}").ConfigureAwait(false);
            files.Add(resolved);
        }

        // Create the metadata and refuse an archive that could not be published before writing a byte
        var metadata = BundleMetadataWriter.CreateEntries(definition, files);
        var archiveSize = StoredZipWriter.ComputeArchiveSize(
            BundleMetadataWriter.EntryOrder.Select(name => (name.Length, (long)metadata[name].Length))
                .Concat(files.Select(file => (file.EntryName.Length, file.Size))));
        if (archiveSize >= _archiveSizeLimit)
        {
            throw new InvalidDataException(
                $"The bundle '{definition.Name}' would be {archiveSize:N0} bytes, which is not below the limit of {_archiveSizeLimit:N0} bytes.");
        }

        // Write under a temporary name and rename once complete
        Directory.CreateDirectory(outputDirectory);
        var path = Path.Combine(outputDirectory, definition.FileName);
        var partial = path + ".partial";
        try
        {
            await WriteZipAsync(definition, metadata, files, partial, cancellationToken).ConfigureAwait(false);
            File.Move(partial, path, overwrite: true);
        }
        catch
        {
            File.Delete(partial);
            throw;
        }

        return new BuiltBundle(definition, path, await HashFileAsync(path, cancellationToken).ConfigureAwait(false), new FileInfo(path).Length);
    }

    /// <summary>
    ///     Builds a set of bundles into one directory and writes <c>SHA256SUMS</c> and <c>release-notes.md</c> for the
    ///     bundles built, printing <c>{sha256}  {size}  {file}  {MATCH|UPDATE CATALOG}</c> for each.
    /// </summary>
    /// <param name="definitions">The bundles to build, in build order.</param>
    /// <param name="outputDirectory">The output directory (created when missing).</param>
    /// <param name="skipUnavailable">
    ///     <see langword="true"/> to skip (and report) a bundle whose vendor files cannot be obtained instead of failing.
    ///     A vendor copy that differs from its pin always fails.
    /// </param>
    /// <param name="cancellationToken">A token that aborts the build.</param>
    /// <returns>The built bundles, sorted by file name; empty when every bundle was skipped.</returns>
    /// <exception cref="InvalidDataException">Thrown when a bundle cannot be built (see <see cref="BuildAsync"/>).</exception>
    /// <exception cref="VendorFileUnavailableException">Thrown when a file is unavailable and skipping is off.</exception>
    public async Task<IReadOnlyList<BuiltBundle>> BuildSetAsync(
        IReadOnlyList<BundleDefinition> definitions,
        string outputDirectory,
        bool skipUnavailable,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        // Build each bundle; an unavailable source only skips the bundle when asked to
        var built = new List<BuiltBundle>();
        foreach (var definition in definitions)
        {
            await _log.WriteLineAsync($"Building {definition.FileName}").ConfigureAwait(false);
            try
            {
                built.Add(await BuildAsync(definition, outputDirectory, cancellationToken).ConfigureAwait(false));
            }
            catch (VendorFileUnavailableException ex) when (skipUnavailable)
            {
                await _log.WriteLineAsync($"SKIPPED {definition.Name}: {ex.Message}").ConfigureAwait(false);
            }
        }

        if (built.Count == 0)
        {
            return built;
        }

        // Write the release-level files and print the summary
        var sorted = built.OrderBy(bundle => bundle.Definition.FileName, StringComparer.Ordinal).ToList();
        await File.WriteAllTextAsync(
                Path.Combine(outputDirectory, ReleaseNotesWriter.ChecksumsFileName),
                ReleaseNotesWriter.CreateChecksums(sorted),
                cancellationToken)
            .ConfigureAwait(false);
        await File.WriteAllTextAsync(
                Path.Combine(outputDirectory, ReleaseNotesWriter.ReleaseNotesFileName),
                ReleaseNotesWriter.CreateReleaseNotes(sorted),
                cancellationToken)
            .ConfigureAwait(false);
        foreach (var bundle in sorted)
        {
            await _log.WriteLineAsync(
                    $"{bundle.Sha256}  {bundle.Size.ToString(CultureInfo.InvariantCulture)}  {bundle.Definition.FileName}  {GetCatalogStatus(bundle)}")
                .ConfigureAwait(false);
        }

        return sorted;
    }

    /// <summary>
    ///     Proves a build directory reproduces: rebuilds every bundle <c>SHA256SUMS</c> lists into a scratch directory
    ///     and compares the rebuilt zip with the listed SHA-256 and, byte for byte, with the existing zip; also compares
    ///     <c>release-notes.md</c> when present.
    /// </summary>
    /// <param name="definitions">Every declared bundle definition.</param>
    /// <param name="inputDirectory">The directory a previous <c>build</c> wrote.</param>
    /// <param name="cancellationToken">A token that aborts the rebuild.</param>
    /// <returns><see langword="true"/> when every zip (and the release notes) is identical; otherwise <see langword="false"/>.</returns>
    /// <exception cref="FileNotFoundException">Thrown when <c>SHA256SUMS</c> is missing.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>SHA256SUMS</c> is malformed or lists an undeclared bundle, or a rebuild fails.
    /// </exception>
    /// <exception cref="VendorFileUnavailableException">Thrown when a vendor file cannot be obtained.</exception>
    public async Task<bool> VerifyAsync(IReadOnlyList<BundleDefinition> definitions, string inputDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var lines = ReleaseNotesWriter.ParseChecksums(
            await File.ReadAllTextAsync(Path.Combine(inputDirectory, ReleaseNotesWriter.ChecksumsFileName), cancellationToken).ConfigureAwait(false));

        // Rebuild one bundle at a time into scratch space, so at most one extra zip is on disk
        var scratch = Directory.CreateTempSubdirectory("model-bundler-verify-");
        var identical = true;
        var rebuilt = new List<BuiltBundle>();
        try
        {
            foreach (var line in lines)
            {
                var definition = definitions.FirstOrDefault(candidate => string.Equals(candidate.FileName, line.FileName, StringComparison.Ordinal))
                    ?? throw new InvalidDataException($"'{line.FileName}' is not a bundle declared in {BundleDefinitions.FileName}.");
                await _log.WriteLineAsync($"Rebuilding {line.FileName}").ConfigureAwait(false);
                var bundle = await BuildAsync(definition, scratch.FullName, cancellationToken).ConfigureAwait(false);
                var existing = Path.Combine(inputDirectory, line.FileName);
                var same = string.Equals(bundle.Sha256, line.Sha256, StringComparison.Ordinal)
                    && await FilesEqualAsync(existing, bundle.Path, cancellationToken).ConfigureAwait(false);
                await _log.WriteLineAsync($"{(same ? "IDENTICAL" : "DIFFERENT")}  {bundle.Sha256}  {line.FileName}").ConfigureAwait(false);
                identical &= same;
                rebuilt.Add(bundle);
                File.Delete(bundle.Path);
            }

            // The release notes are a pure function of the bundles, so they must reproduce as well
            var notesPath = Path.Combine(inputDirectory, ReleaseNotesWriter.ReleaseNotesFileName);
            if (File.Exists(notesPath))
            {
                var notesSame = string.Equals(
                    await File.ReadAllTextAsync(notesPath, cancellationToken).ConfigureAwait(false),
                    ReleaseNotesWriter.CreateReleaseNotes(rebuilt),
                    StringComparison.Ordinal);
                await _log.WriteLineAsync($"{(notesSame ? "IDENTICAL" : "DIFFERENT")}  {ReleaseNotesWriter.ReleaseNotesFileName}").ConfigureAwait(false);
                identical &= notesSame;
            }
        }
        finally
        {
            scratch.Delete(recursive: true);
        }

        return identical;
    }

    /// <summary>Gets whether a built zip matches the pin the packages publish.</summary>
    /// <param name="bundle">The built bundle.</param>
    /// <returns><see cref="MatchStatus"/> or <see cref="UpdateCatalogStatus"/>.</returns>
    internal string GetCatalogStatus(BuiltBundle bundle) =>
        _publishedChecksums.TryGetValue(bundle.Definition.Name, out var pin)
        && string.Equals(pin, bundle.Sha256, StringComparison.OrdinalIgnoreCase)
            ? MatchStatus
            : UpdateCatalogStatus;

    /// <summary>
    ///     Gets a bundle's compiled contents and proves they satisfy the runtime's own bundle rules (file name and vendor
    ///     paths), so the installer will accept the zip.
    /// </summary>
    /// <param name="definition">The bundle definition.</param>
    /// <returns>The compiled vendor files.</returns>
    /// <exception cref="InvalidDataException">Thrown when there are no contents or a rule is broken.</exception>
    private SpeechModelDownloadDescriptor GetValidatedContents(BundleDefinition definition)
    {
        if (!_contents.TryGetValue(definition.Name, out var contents))
        {
            throw new InvalidDataException($"No model package declares compiled contents for the bundle '{definition.Name}'.");
        }

        try
        {
            _ = new SpeechModelBundle(new Uri(SpeechModelBundle.ModelsReleaseBaseUri, definition.FileName), PlaceholderChecksum, contents);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException($"The bundle '{definition.Name}' breaks a model bundle rule: {ex.Message}", ex);
        }

        return contents;
    }

    /// <summary>Writes the zip: the metadata entries in their fixed order, then the vendor files in ordinal order.</summary>
    /// <param name="definition">The bundle definition (its date stamps every entry).</param>
    /// <param name="metadata">The metadata entries by name.</param>
    /// <param name="files">The verified vendor files, sorted by entry name.</param>
    /// <param name="path">The file to write.</param>
    /// <param name="cancellationToken">A token that aborts the write.</param>
    /// <returns>A task that completes when the zip is written and closed.</returns>
    /// <exception cref="InvalidDataException">Thrown when a vendor file's bytes changed after verification.</exception>
    private async Task WriteZipAsync(
        BundleDefinition definition,
        IReadOnlyDictionary<string, byte[]> metadata,
        IReadOnlyList<ResolvedVendorFile> files,
        string path,
        CancellationToken cancellationToken)
    {
        var output = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None, BufferSize, useAsync: true);
        await using (output.ConfigureAwait(false))
        {
            var writer = new StoredZipWriter(output, definition.Date, _archiveSizeLimit);
            foreach (var name in BundleMetadataWriter.EntryOrder)
            {
                await writer.AddEntryAsync(name, metadata[name], cancellationToken).ConfigureAwait(false);
            }

            // Each vendor file is hashed again as it is written, so the zip provably holds the pinned bytes
            foreach (var file in files)
            {
                var input = new FileStream(file.LocalPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
                await using (input.ConfigureAwait(false))
                {
                    var written = Convert.ToHexStringLower(await writer.AddEntryAsync(file.EntryName, input, cancellationToken).ConfigureAwait(false));
                    if (!string.Equals(written, file.File.Sha256Checksum, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            $"'{file.LocalPath}' changed after it was verified: the zip received SHA-256 {written}, not {file.File.Sha256Checksum}.");
                    }
                }
            }

            await writer.FinishAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Computes the lowercase SHA-256 of a file.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">A token that aborts hashing.</param>
    /// <returns>The lowercase hexadecimal SHA-256.</returns>
    internal static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        await using (stream.ConfigureAwait(false))
        {
            return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
        }
    }

    /// <summary>Compares two files byte for byte.</summary>
    /// <param name="first">The first file; a missing file compares unequal.</param>
    /// <param name="second">The second file.</param>
    /// <param name="cancellationToken">A token that aborts the comparison.</param>
    /// <returns><see langword="true"/> when both files exist with identical bytes.</returns>
    private static async Task<bool> FilesEqualAsync(string first, string second, CancellationToken cancellationToken)
    {
        if (!File.Exists(first) || new FileInfo(first).Length != new FileInfo(second).Length)
        {
            return false;
        }

        var a = new FileStream(first, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
        await using (a.ConfigureAwait(false))
        {
            var b = new FileStream(second, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, useAsync: true);
            await using (b.ConfigureAwait(false))
            {
                var bufferA = new byte[BufferSize];
                var bufferB = new byte[BufferSize];
                int read;
                while ((read = await a.ReadAtLeastAsync(bufferA, BufferSize, throwOnEndOfStream: false, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await b.ReadExactlyAsync(bufferB.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    if (!bufferA.AsSpan(0, read).SequenceEqual(bufferB.AsSpan(0, read)))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
