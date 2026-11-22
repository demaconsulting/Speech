using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     The extraction and verification engine behind <see cref="SpeechModelBundle.InstallAsync"/>: turns one verified,
///     staged bundle zip into the model's vendor files plus a <c>licenses/{Name}/</c> metadata folder, then deletes
///     the zip.
/// </summary>
/// <remarks>
///     <para>
///     <b>Bundle layout</b>: a bundle zip holds exactly the vendor files its <c>model.json</c> lists (each at its
///     install path, <c>/</c>-separated) plus the metadata entries <c>model.json</c>, <c>README.txt</c> and
///     <c>LICENSE</c> at the root and, where the license requires one, <c>NOTICE</c>. Directory entries are
///     allowed and ignored. Any other entry, or a missing one, fails the install.
///     </para>
///     <para>
///     <b><c>model.json</c></b> (UTF-8 JSON, at most <see cref="MaximumMetadataBytes"/>) must be an object with
///     <c>"schemaVersion": 1</c>, <c>"bundle"</c> equal to the bundle's <see cref="SpeechModelBundle.Name"/>, and a
///     non-empty <c>"files"</c> array whose items carry <c>"path"</c> (string), <c>"size"</c> (non-negative integer)
///     and <c>"sha256"</c> (64 hexadecimal characters). Every other property (for example <c>"sourceUrl"</c>,
///     <c>"licenses"</c>, <c>"upstream"</c>) is accepted and not interpreted. The <c>files</c> set must equal the
///     bundle's compiled <see cref="SpeechModelBundle.Contents"/> exactly, path for path and SHA-256 for SHA-256.
///     </para>
///     <para>
///     <b>Safety</b>: every entry name passes the shared <see cref="ZipEntryPathValidator"/> and resolves inside the
///     staging directory before anything is written; names differing only by letter case are duplicates; each
///     vendor file is written with <see cref="FileMode.CreateNew"/> and hashed while it is copied, and the copy stops
///     as soon as it exceeds the size <c>model.json</c> declares (the zip-bomb cap); each metadata file is capped at
///     <see cref="MaximumMetadataBytes"/>. After the copy the size and SHA-256 must equal <c>model.json</c> and the
///     SHA-256 must equal the compiled pin. The zip is read with <see cref="System.IO.Compression"/> only.
///     </para>
///     <para>
///     Every archive problem is an <see cref="InvalidDataException"/>, which the downloader classifies as an
///     install-hook failure (<see cref="SpeechModelDownloadOutcome.Failed"/>) before discarding the staging directory,
///     so nothing is installed. The zip is deleted only after the whole bundle is extracted. Stateless and
///     thread-safe.
///     </para>
/// </remarks>
internal static class SpeechModelBundleInstaller
{
    /// <summary>The name of the bundle's machine-readable metadata entry.</summary>
    internal const string ManifestEntryName = "model.json";

    /// <summary>The name of the bundle's human-readable provenance entry.</summary>
    internal const string ReadmeEntryName = "README.txt";

    /// <summary>The name of the bundle's license-text entry.</summary>
    internal const string LicenseEntryName = "LICENSE";

    /// <summary>The name of the bundle's optional attribution and notices entry.</summary>
    internal const string NoticeEntryName = "NOTICE";

    /// <summary>The only <c>schemaVersion</c> this installer understands.</summary>
    internal const int SupportedSchemaVersion = 1;

    /// <summary>The largest accepted size of each metadata entry (1 MiB), including <c>model.json</c>.</summary>
    internal const long MaximumMetadataBytes = 1024L * 1024;

    /// <summary>The metadata entries every bundle must carry.</summary>
    internal static readonly IReadOnlyList<string> RequiredMetadataEntryNames = [ManifestEntryName, ReadmeEntryName, LicenseEntryName];

    /// <summary>Every metadata entry a bundle may carry (the required ones plus the optional <c>NOTICE</c>).</summary>
    internal static readonly IReadOnlyList<string> MetadataEntryNames = [ManifestEntryName, ReadmeEntryName, LicenseEntryName, NoticeEntryName];

    /// <summary>The copy buffer size.</summary>
    private const int BufferSize = 81920;

    /// <summary>
    ///     Extracts and verifies one bundle staged at <c>{stagingDirectory}/{bundle.FileName}</c>.
    /// </summary>
    /// <param name="bundle">The bundle to install. Must not be <see langword="null"/>.</param>
    /// <param name="stagingDirectory">The staging directory holding the verified zip; also the extraction target.</param>
    /// <param name="cancellationToken">A token that aborts the extraction.</param>
    /// <returns>A task that completes when the bundle is extracted, verified and its zip deleted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bundle"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stagingDirectory"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="InvalidDataException">Thrown for every archive problem (see the type remarks).</exception>
    /// <exception cref="IOException">Thrown when the staged zip is missing or a file cannot be read or written.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    internal static async Task InstallAsync(SpeechModelBundle bundle, string stagingDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentException.ThrowIfNullOrEmpty(stagingDirectory);

        // The central-directory read has no asynchronous API, so leave the caller's thread first; cancellation then
        // takes precedence over a missing or unreadable zip
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        var archivePath = Path.Combine(stagingDirectory, bundle.FileName);
        var stagingRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(stagingDirectory));

        using (var archive = OpenArchive(archivePath))
        {
            // Step 1: validate every entry name, and index the file entries, before anything is written
            var entries = IndexEntries(archive, stagingRoot);

            // Step 2: read and parse model.json, then prove the entry set and the compiled pins agree with it
            if (!entries.TryGetValue(ManifestEntryName, out var manifestEntry))
            {
                throw new InvalidDataException($"The model bundle '{bundle.FileName}' has no '{ManifestEntryName}' entry.");
            }

            var manifestBytes = await ReadCappedAsync(manifestEntry, cancellationToken).ConfigureAwait(false);
            var manifestFiles = ParseManifest(manifestBytes, bundle);
            VerifyEntrySet(bundle, entries, manifestFiles);
            var pins = VerifyPins(bundle, manifestFiles);

            // Step 3: copy each vendor file to its install path, hashing and size-capping it as it is copied
            foreach (var file in manifestFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = ZipEntryPathValidator.ResolveUnderRoot(stagingRoot, ToPlatformPath(file.Path), file.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await CopyAndVerifyAsync(bundle, entries[file.Path], destination, file, pins[file.Path], cancellationToken).ConfigureAwait(false);
            }

            // Step 4: keep the license, notice, readme and model.json with the installed model, per bundle
            var licenseDirectory = ZipEntryPathValidator.ResolveUnderRoot(
                stagingRoot, Path.Combine(SpeechModelBundle.LicensesDirectoryName, bundle.Name), bundle.Name);
            Directory.CreateDirectory(licenseDirectory);
            await WriteNewFileAsync(Path.Combine(licenseDirectory, ManifestEntryName), manifestBytes, cancellationToken).ConfigureAwait(false);
            foreach (var name in MetadataEntryNames.Where(name => name != ManifestEntryName && entries.ContainsKey(name)))
            {
                var bytes = await ReadCappedAsync(entries[name], cancellationToken).ConfigureAwait(false);
                await WriteNewFileAsync(Path.Combine(licenseDirectory, name), bytes, cancellationToken).ConfigureAwait(false);
            }
        }

        // Step 5: the zip was only an input; remove it so it does not stay in the installed model
        File.Delete(archivePath);
    }

    /// <summary>
    ///     Opens the staged zip, converting a file that is not a readable zip into an <see cref="InvalidDataException"/>.
    ///     A missing or inaccessible zip is not converted and surfaces as the file system's own exception.
    /// </summary>
    /// <param name="archivePath">The staged zip path.</param>
    /// <returns>The opened archive, owned by the caller.</returns>
    private static ZipArchive OpenArchive(string archivePath)
    {
        try
        {
            return ZipFile.OpenRead(archivePath);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is NotSupportedException or ArgumentException)
        {
            throw new InvalidDataException($"The model bundle '{Path.GetFileName(archivePath)}' is not a valid zip file.", ex);
        }
    }

    /// <summary>
    ///     Validates every entry name (directory entries included) and indexes the file entries by their
    ///     <c>/</c>-separated path.
    /// </summary>
    /// <param name="archive">The opened bundle.</param>
    /// <param name="stagingRoot">The canonical staging root the entries must resolve inside.</param>
    /// <returns>The file entries keyed by normalized path (ordinal).</returns>
    /// <exception cref="InvalidDataException">Thrown for an unsafe, non-portable, empty or case-insensitively duplicated name.</exception>
    private static Dictionary<string, ZipArchiveEntry> IndexEntries(ZipArchive archive, string stagingRoot)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var segments = ZipEntryPathValidator.Validate(entry.FullName);
            if (segments.Length == 0)
            {
                throw new InvalidDataException($"Archive entry '{entry.FullName}' has no name.");
            }

            var path = string.Join('/', segments);
            ZipEntryPathValidator.ResolveUnderRoot(stagingRoot, ToPlatformPath(path), entry.FullName);

            // Directory entries carry no content; the directories are created from the file paths
            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            if (!seen.Add(path))
            {
                throw new InvalidDataException($"Archive contains the entry '{entry.FullName}' more than once.");
            }

            entries[path] = entry;
        }

        return entries;
    }

    /// <summary>
    ///     Parses and validates <c>model.json</c> (see the type remarks for the schema).
    /// </summary>
    /// <param name="manifestBytes">The raw <c>model.json</c> bytes.</param>
    /// <param name="bundle">The bundle being installed, whose name and file name constrain the manifest.</param>
    /// <returns>The declared vendor files, in declaration order.</returns>
    /// <exception cref="InvalidDataException">Thrown when the manifest is not valid JSON or breaks a schema rule.</exception>
    private static List<ManifestFile> ParseManifest(byte[] manifestBytes, SpeechModelBundle bundle)
    {
        try
        {
            using var document = JsonDocument.Parse(manifestBytes);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Invalid(bundle, "is not a JSON object");
            }

            // The schema version gates every other rule, so an unknown future layout is refused, not misread
            if (!root.TryGetProperty("schemaVersion", out var version)
                || version.ValueKind != JsonValueKind.Number
                || !version.TryGetInt32(out var schemaVersion)
                || schemaVersion != SupportedSchemaVersion)
            {
                throw Invalid(bundle, $"does not declare \"schemaVersion\": {SupportedSchemaVersion}");
            }

            // A zip renamed to another bundle's name must not install as that bundle
            if (!root.TryGetProperty("bundle", out var name)
                || name.ValueKind != JsonValueKind.String
                || !string.Equals(name.GetString(), bundle.Name, StringComparison.Ordinal))
            {
                throw Invalid(bundle, $"does not declare \"bundle\": \"{bundle.Name}\"");
            }

            if (!root.TryGetProperty("files", out var files) || files.ValueKind != JsonValueKind.Array || files.GetArrayLength() == 0)
            {
                throw Invalid(bundle, "does not declare a non-empty \"files\" array");
            }

            var result = files.EnumerateArray().Select(item => ParseManifestFile(item, bundle)).ToList();

            // Two declared files must never land on one path, nor one inside another
            var collision = SpeechModelBundle.FindPathCollision(result.Select(file => file.Path));
            if (collision is not null)
            {
                throw Invalid(bundle, $"declares the path '{collision}' more than once or as a directory of another file");
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The '{ManifestEntryName}' of model bundle '{bundle.FileName}' is not valid JSON.", ex);
        }
    }

    /// <summary>
    ///     Parses and validates one <c>files</c> item of <c>model.json</c>.
    /// </summary>
    /// <param name="item">The JSON item.</param>
    /// <param name="bundle">The bundle being installed.</param>
    /// <returns>The declared vendor file.</returns>
    /// <exception cref="InvalidDataException">Thrown when a field is missing, malformed, or the path is unsafe or reserved.</exception>
    private static ManifestFile ParseManifestFile(JsonElement item, SpeechModelBundle bundle)
    {
        if (item.ValueKind != JsonValueKind.Object
            || !item.TryGetProperty("path", out var pathElement) || pathElement.ValueKind != JsonValueKind.String
            || !item.TryGetProperty("size", out var sizeElement) || sizeElement.ValueKind != JsonValueKind.Number
            || !sizeElement.TryGetInt64(out var size) || size < 0
            || !item.TryGetProperty("sha256", out var shaElement) || shaElement.ValueKind != JsonValueKind.String)
        {
            throw Invalid(bundle, "has a \"files\" item without a string \"path\", a non-negative integer \"size\" and a string \"sha256\"");
        }

        var path = pathElement.GetString()!;
        var sha256 = shaElement.GetString()!;
        if (sha256.Length != 64 || !sha256.All(char.IsAsciiHexDigit))
        {
            throw Invalid(bundle, $"declares a malformed SHA-256 for '{path}'");
        }

        // The path is also the zip entry name, so it must be canonical, safe and portable, and outside the names the
        // bundle itself reserves
        var segments = ZipEntryPathValidator.Validate(path);
        if (segments.Length == 0
            || !string.Equals(string.Join('/', segments), path, StringComparison.Ordinal)
            || string.Equals(segments[0], SpeechModelBundle.LicensesDirectoryName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(path, bundle.FileName, StringComparison.OrdinalIgnoreCase)
            || MetadataEntryNames.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            throw Invalid(bundle, $"declares the reserved or non-canonical path '{path}'");
        }

        return new ManifestFile(path, size, sha256);
    }

    /// <summary>
    ///     Requires the zip's file entries to be exactly the <c>model.json</c> files plus the required metadata entries,
    ///     with <c>NOTICE</c> optional.
    /// </summary>
    /// <param name="bundle">The bundle being installed.</param>
    /// <param name="entries">The zip's file entries keyed by path.</param>
    /// <param name="manifestFiles">The vendor files <c>model.json</c> declares.</param>
    /// <exception cref="InvalidDataException">Thrown for an unexpected or a missing entry.</exception>
    private static void VerifyEntrySet(SpeechModelBundle bundle, Dictionary<string, ZipArchiveEntry> entries, List<ManifestFile> manifestFiles)
    {
        var declared = new HashSet<string>(manifestFiles.Select(file => file.Path), StringComparer.Ordinal);

        var unexpected = entries.Keys.FirstOrDefault(path => !declared.Contains(path) && !MetadataEntryNames.Contains(path, StringComparer.Ordinal));
        if (unexpected is not null)
        {
            throw new InvalidDataException($"The model bundle '{bundle.FileName}' contains the unexpected entry '{unexpected}'.");
        }

        var missing = declared.Concat(RequiredMetadataEntryNames).FirstOrDefault(path => !entries.ContainsKey(path));
        if (missing is not null)
        {
            throw new InvalidDataException($"The model bundle '{bundle.FileName}' is missing the entry '{missing}'.");
        }
    }

    /// <summary>
    ///     Requires the <c>model.json</c> files to be exactly the compiled <see cref="SpeechModelBundle.Contents"/>,
    ///     each with the same SHA-256, so every extracted file has a compiled-in pin.
    /// </summary>
    /// <param name="bundle">The bundle being installed.</param>
    /// <param name="manifestFiles">The vendor files <c>model.json</c> declares.</param>
    /// <returns>The compiled pin of each declared path.</returns>
    /// <exception cref="InvalidDataException">Thrown when a pinned file is missing, differs, or a declared file has no pin.</exception>
    private static Dictionary<string, string> VerifyPins(SpeechModelBundle bundle, List<ManifestFile> manifestFiles)
    {
        var pins = bundle.Contents.Files.ToDictionary(SpeechModelBundle.NormalizePath, file => file.Sha256Checksum, StringComparer.Ordinal);
        var declared = manifestFiles.ToDictionary(file => file.Path, StringComparer.Ordinal);

        foreach (var (path, pin) in pins)
        {
            if (!declared.TryGetValue(path, out var file) || !string.Equals(file.Sha256, pin, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"The model bundle '{bundle.FileName}' does not declare the pinned file '{path}' with its pinned SHA-256.");
            }
        }

        var unpinned = manifestFiles.FirstOrDefault(file => !pins.ContainsKey(file.Path));
        if (unpinned is not null)
        {
            throw new InvalidDataException($"The model bundle '{bundle.FileName}' declares the file '{unpinned.Path}', which has no compiled pin.");
        }

        return pins;
    }

    /// <summary>
    ///     Copies one vendor entry to a new file, hashing it as it is copied and stopping once it exceeds the declared
    ///     size, then checks the size and SHA-256 against <c>model.json</c> and the SHA-256 against the compiled pin.
    /// </summary>
    /// <param name="bundle">The bundle being installed (for messages).</param>
    /// <param name="entry">The zip entry to copy.</param>
    /// <param name="destination">The new file to create.</param>
    /// <param name="file">The entry's <c>model.json</c> declaration.</param>
    /// <param name="pin">The entry's compiled SHA-256 pin.</param>
    /// <param name="cancellationToken">A token that aborts the copy.</param>
    /// <returns>A task that completes when the file is written and verified.</returns>
    /// <exception cref="InvalidDataException">Thrown when the size or SHA-256 differs.</exception>
    private static async Task CopyAndVerifyAsync(
        SpeechModelBundle bundle,
        ZipArchiveEntry entry,
        string destination,
        ManifestFile file,
        string pin,
        CancellationToken cancellationToken)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[BufferSize];
        long copied = 0;

#pragma warning disable S6966 // ZipArchiveEntry.OpenAsync exists only on net10.0+; this library also targets net8.0/net9.0
        await using (var source = entry.Open())
#pragma warning restore S6966
        await using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
        {
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                // The declared size is the zip-bomb cap: never write more than model.json promised
                copied += read;
                if (copied > file.Size)
                {
                    throw new InvalidDataException(
                        $"The entry '{file.Path}' of model bundle '{bundle.FileName}' is larger than its declared {file.Size} bytes.");
                }

                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            }
        }

        if (copied != file.Size)
        {
            throw new InvalidDataException(
                $"The entry '{file.Path}' of model bundle '{bundle.FileName}' is {copied} bytes, not its declared {file.Size} bytes.");
        }

        // Verify against both the bundle's own declaration and the compiled pin
        var actual = Convert.ToHexStringLower(hash.GetHashAndReset());
        if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"The entry '{file.Path}' of model bundle '{bundle.FileName}' does not match the SHA-256 its '{ManifestEntryName}' declares.");
        }

        if (!string.Equals(actual, pin, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"The entry '{file.Path}' of model bundle '{bundle.FileName}' does not match its pinned SHA-256.");
        }
    }

    /// <summary>
    ///     Reads a whole metadata entry into memory, refusing one larger than <see cref="MaximumMetadataBytes"/>.
    /// </summary>
    /// <param name="entry">The metadata entry.</param>
    /// <param name="cancellationToken">A token that aborts the read.</param>
    /// <returns>The entry's bytes.</returns>
    /// <exception cref="InvalidDataException">Thrown when the entry exceeds the cap.</exception>
    private static async Task<byte[]> ReadCappedAsync(ZipArchiveEntry entry, CancellationToken cancellationToken)
    {
        var buffer = new byte[BufferSize];
        using var memory = new MemoryStream();
#pragma warning disable S6966 // ZipArchiveEntry.OpenAsync exists only on net10.0+; this library also targets net8.0/net9.0
        await using var source = entry.Open();
#pragma warning restore S6966
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (memory.Length + read > MaximumMetadataBytes)
            {
                throw new InvalidDataException($"The bundle entry '{entry.FullName}' exceeds {MaximumMetadataBytes} bytes.");
            }

            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return memory.ToArray();
    }

    /// <summary>Writes bytes to a file that must not already exist.</summary>
    /// <param name="path">The new file's path.</param>
    /// <param name="bytes">The content.</param>
    /// <param name="cancellationToken">A token that aborts the write.</param>
    /// <returns>A task that completes when the file is written.</returns>
    private static async Task WriteNewFileAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);
        await target.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Converts a <c>/</c>-separated path to this platform's separator.</summary>
    /// <param name="path">The normalized path.</param>
    /// <returns>The platform path.</returns>
    private static string ToPlatformPath(string path) => path.Replace('/', Path.DirectorySeparatorChar);

    /// <summary>Creates the exception for a <c>model.json</c> schema violation.</summary>
    /// <param name="bundle">The bundle being installed.</param>
    /// <param name="problem">What is wrong, phrased to follow "its model.json".</param>
    /// <returns>The exception to throw.</returns>
    private static InvalidDataException Invalid(SpeechModelBundle bundle, string problem) =>
        new($"The '{ManifestEntryName}' of model bundle '{bundle.FileName}' {problem}.");

    /// <summary>One vendor file declared by <c>model.json</c>.</summary>
    /// <param name="Path">The <c>/</c>-separated install path (also the zip entry name).</param>
    /// <param name="Size">The declared size in bytes.</param>
    /// <param name="Sha256">The declared SHA-256, as 64 hexadecimal characters.</param>
    private sealed record ManifestFile(string Path, long Size, string Sha256);
}
