using System.Text.RegularExpressions;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Describes one model bundle: a single SHA-256-pinned zip archive, published on the project's <c>models</c>
///     release (or any HTTPS host), that carries a model's unmodified vendor files together with their license,
///     notice, readme and <c>model.json</c> metadata.
/// </summary>
/// <remarks>
///     <para>
///     A bundle lets a model be downloaded from one firewall-friendly host as one verified file, while keeping the
///     original per-file pins as a second, compiled-in proof that every extracted vendor file is exactly the
///     upstream file. The bundle is downloaded through the ordinary downloader as its <see cref="DownloadFile"/>
///     (pinned by <see cref="Sha256Checksum"/>), so a configured mirror serves it at
///     <c>{mirror}/{model-id}/{FileName}</c>. The model's install hook then calls <see cref="InstallAsync"/>, which
///     extracts it into the staging directory, verifies every vendor file against both the bundle's own
///     <c>model.json</c> and the compiled <see cref="Contents"/> pins, copies the metadata files to
///     <c>licenses/{Name}/</c> and deletes the zip. Any failure leaves the staging directory to be discarded, so
///     nothing is installed and a prior install stays intact.
///     </para>
///     <para>
///     <see cref="Contents"/> holds the vendor files exactly as a model would declare them for a direct download:
///     their original upstream URI (recorded for provenance; never fetched by the installer), their SHA-256 pin and
///     their install path, which is also their path inside the zip. A model whose files are split across several
///     bundles (one per component) passes all of them to <see cref="CreateDescriptor"/> and
///     <see cref="InstallAsync"/>, which reject any collision between the bundles.
///     </para>
///     <para>
///     Instances are immutable and safe to share across threads.
///     </para>
/// </remarks>
public sealed partial class SpeechModelBundle
{
    /// <summary>
    ///     The name of the directory, at the root of a model's installed content, that holds each bundle's metadata
    ///     files under a <c>{Name}</c> subdirectory.
    /// </summary>
    internal const string LicensesDirectoryName = "licenses";

    /// <summary>
    ///     Initializes a new instance of the <see cref="SpeechModelBundle"/> class, validating every field eagerly.
    /// </summary>
    /// <param name="uri">
    ///     The absolute HTTPS location of the bundle zip, with no query or fragment. Its last path segment is the
    ///     bundle's <see cref="FileName"/> and must match
    ///     <c>^[a-z0-9]+([.-][a-z0-9]+)*-(fp32|fp16|int8|int4)-b[1-9][0-9]*\.zip$</c> (for example
    ///     <c>kokoro-en-v1.0-int8-b1.zip</c>): a lowercase model and upstream-release token, a quantization token and a
    ///     bundle revision. Typically built as <c>new Uri(SpeechModelBundle.ModelsReleaseBaseUri, fileName)</c>.
    /// </param>
    /// <param name="sha256Checksum">The 64-character hexadecimal SHA-256 checksum of the whole zip.</param>
    /// <param name="contents">
    ///     The vendor files inside the zip: each one's original upstream URI, SHA-256 pin and install path (which is
    ///     also its path inside the zip). No path may lie under <c>licenses/</c>, equal a metadata entry name
    ///     (<c>model.json</c>, <c>README.txt</c>, <c>LICENSE</c>, <c>NOTICE</c>) or <see cref="FileName"/>, differ from
    ///     another only by letter case, be a parent directory of another, contain a colon, or contain a segment that is
    ///     not valid on every platform (all compared without regard to case). These are the same entry-name rules the
    ///     installer applies to the zip, so a bundle that constructs on any platform installs on every platform.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="uri"/> is not an absolute HTTPS URI, carries a query or fragment, or its file
    ///     name does not match the bundle naming rule; when <paramref name="sha256Checksum"/> is not 64 hexadecimal
    ///     characters; or when a <paramref name="contents"/> path breaks one of the rules above.
    /// </exception>
    public SpeechModelBundle(Uri uri, string sha256Checksum, SpeechModelDownloadDescriptor contents)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(sha256Checksum);
        ArgumentNullException.ThrowIfNull(contents);

        // A bundle is only ever fetched over HTTPS, and its identity is its file name, so a query or fragment
        // (which a mirror path and the staged file name would both silently drop) is rejected outright
        if (!uri.IsAbsoluteUri || !string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"A model bundle URI must be an absolute HTTPS URI, but was '{uri}'.", nameof(uri));
        }

        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            throw new ArgumentException($"A model bundle URI must not carry a query or fragment, but was '{uri}'.", nameof(uri));
        }

        // The file name names the bundle everywhere (staging, mirror path, licenses folder), so it must follow the
        // published naming rule exactly
        var fileName = Uri.UnescapeDataString(uri.Segments[^1]);
        if (!FileNamePattern().IsMatch(fileName))
        {
            throw new ArgumentException(
                $"The model bundle file name '{fileName}' does not match the naming rule '{{model}}-{{release}}-{{fp32|fp16|int8|int4}}-b{{N}}.zip' (lowercase letters, digits, '.' and '-').",
                nameof(uri));
        }

        // The download file enforces the 64-hex checksum shape (and repeats the HTTPS check)
        DownloadFile = new SpeechModelDownloadFile(uri, sha256Checksum, fileName);

        // The vendor paths are compared case-insensitively, because the installer and Windows file systems do so
        ValidateContents(contents, fileName);

        Uri = uri;
        Sha256Checksum = sha256Checksum;
        FileName = fileName;
        Name = fileName[..^".zip".Length];
        Contents = contents;
    }

    /// <summary>
    ///     Gets the base URI of the project's long-lived <c>models</c> GitHub release, under which every bundle this
    ///     library's model packages use is published as <c>{ModelsReleaseBaseUri}{FileName}</c>.
    /// </summary>
    /// <remarks>
    ///     Ends with a slash, so <c>new Uri(ModelsReleaseBaseUri, fileName)</c> appends the file name. The release is
    ///     append-only: a published asset is never replaced, and a changed bundle gets a new revision (<c>bN</c>).
    /// </remarks>
#pragma warning disable S1075 // The release location is intentionally a compiled-in, reviewed literal.
    public static Uri ModelsReleaseBaseUri { get; } = new("https://github.com/demaconsulting/Speech/releases/download/models/");
#pragma warning restore S1075

    /// <summary>Gets the HTTPS location of the bundle zip.</summary>
    public Uri Uri { get; }

    /// <summary>Gets the expected SHA-256 checksum of the whole bundle zip, as given to the constructor.</summary>
    public string Sha256Checksum { get; }

    /// <summary>Gets the bundle's file name (the last segment of <see cref="Uri"/>, for example <c>kokoro-en-v1.0-int8-b1.zip</c>).</summary>
    public string FileName { get; }

    /// <summary>Gets the bundle's name: <see cref="FileName"/> without the <c>.zip</c> extension, as recorded in its <c>model.json</c>.</summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the vendor files the bundle carries: their original upstream URIs (provenance only), their SHA-256 pins
    ///     and their install paths (which are also their paths inside the zip).
    /// </summary>
    public SpeechModelDownloadDescriptor Contents { get; }

    /// <summary>
    ///     Gets the file the downloader fetches for this bundle: <see cref="Uri"/>, pinned by
    ///     <see cref="Sha256Checksum"/>, staged as <see cref="FileName"/>.
    /// </summary>
    public SpeechModelDownloadFile DownloadFile { get; }

    /// <summary>
    ///     Creates the download descriptor of a model distributed as one or more bundles: one entry per bundle zip, in
    ///     order.
    /// </summary>
    /// <remarks>
    ///     A model returns this as its <see cref="ISpeechModel.DownloadDescriptor"/> and calls <see cref="InstallAsync"/>
    ///     with the same bundles from its <see cref="ISpeechModel.InstallAsync"/> hook. Pure and thread-safe.
    /// </remarks>
    /// <param name="bundles">The model's bundles. Must contain at least one bundle and no <see langword="null"/> element.</param>
    /// <returns>A descriptor listing each bundle's <see cref="DownloadFile"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bundles"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="bundles"/> is empty or has a <see langword="null"/> element, when two bundles
    ///     share a name, when a vendor path appears in two bundles or one bundle's vendor path is a parent directory of
    ///     another's, or when a bundle's file name equals another bundle's vendor path (all compared without regard
    ///     to case).
    /// </exception>
    public static SpeechModelDownloadDescriptor CreateDescriptor(IReadOnlyList<SpeechModelBundle> bundles)
    {
        ValidateBundleSet(bundles);
        return new SpeechModelDownloadDescriptor([.. bundles.Select(bundle => bundle.DownloadFile)]);
    }

    /// <summary>
    ///     Extracts and verifies every bundle of a model from the staging directory the downloader filled, leaving the
    ///     vendor files at their install paths and each bundle's metadata under <c>licenses/{Name}/</c>, and deleting
    ///     each zip.
    /// </summary>
    /// <remarks>
    ///     Intended to be the whole body of a bundled model's <see cref="ISpeechModel.InstallAsync"/> override. Each
    ///     bundle zip must already be staged at <c>{stagedFilesDirectory}/{FileName}</c> and checksum-verified, which
    ///     the downloader guarantees before it runs the install hook. Every entry name is validated before anything is
    ///     written, and every vendor file is verified against both the bundle's <c>model.json</c> and the compiled
    ///     <see cref="Contents"/> pin. Bundles are processed one at a time in order. On failure the staging directory
    ///     may hold partly extracted files; the downloader discards it, so nothing reaches the installed model.
    /// </remarks>
    /// <param name="bundles">The model's bundles, as passed to <see cref="CreateDescriptor"/>.</param>
    /// <param name="stagedFilesDirectory">The staging directory holding the verified bundle zips; also the extraction target.</param>
    /// <param name="cancellationToken">A token that aborts the extraction between and during file copies.</param>
    /// <returns>A task that completes when every bundle is extracted, verified and deleted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bundles"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="bundles"/> breaks a <see cref="CreateDescriptor"/> rule, or
    ///     <paramref name="stagedFilesDirectory"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a zip is not a valid archive, has an unsafe, non-portable or duplicate entry name, lacks or has a
    ///     malformed <c>model.json</c>, holds a different set of entries than <c>model.json</c> declares, or a vendor
    ///     file's size or SHA-256 differs from <c>model.json</c> or from its compiled pin.
    /// </exception>
    /// <exception cref="IOException">Thrown when a staged zip is missing or a file cannot be read or written.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static async Task InstallAsync(
        IReadOnlyList<SpeechModelBundle> bundles,
        string stagedFilesDirectory,
        CancellationToken cancellationToken)
    {
        ValidateBundleSet(bundles);
        ArgumentException.ThrowIfNullOrEmpty(stagedFilesDirectory);

        // One bundle at a time keeps peak disk use to one extraction beyond the staged zips
        foreach (var bundle in bundles)
        {
            await SpeechModelBundleInstaller.InstallAsync(bundle, stagedFilesDirectory, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Normalizes a declared install path to <c>/</c>-separated segments, the form used inside a zip and in
    ///     <c>model.json</c>.
    /// </summary>
    /// <param name="file">The declared vendor file.</param>
    /// <returns>The install path with each separator replaced by <c>/</c> and empty segments removed.</returns>
    internal static string NormalizePath(SpeechModelDownloadFile file) =>
        string.Join('/', SpeechModelDownloadFile.SplitRelativeInstallPathSegments(file.RelativeInstallPath));

    /// <summary>
    ///     Reports whether a set of <c>/</c>-separated paths collide when compared without regard to case: two equal
    ///     paths, or one path that is a parent directory of another (which could not both be created as files).
    /// </summary>
    /// <param name="paths">The normalized paths to check.</param>
    /// <returns>The first colliding path found, or <see langword="null"/> when the paths are disjoint.</returns>
    internal static string? FindPathCollision(IEnumerable<string> paths)
    {
        var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var all = paths.ToList();

        // Record every path and each of its parent directories
        foreach (var path in all)
        {
            if (!files.Add(path))
            {
                return path;
            }

            for (var index = path.IndexOf('/'); index > 0; index = path.IndexOf('/', index + 1))
            {
                directories.Add(path[..index]);
            }
        }

        // A file path that is also some other path's parent directory cannot coexist with it
        return all.FirstOrDefault(directories.Contains);
    }

    /// <summary>
    ///     Validates the rules a bundle set must satisfy for <see cref="CreateDescriptor"/> and <see cref="InstallAsync"/>.
    /// </summary>
    /// <param name="bundles">The bundle set to validate.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="bundles"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the set is empty, has a null element, or two bundles collide.</exception>
    private static void ValidateBundleSet(IReadOnlyList<SpeechModelBundle> bundles)
    {
        ArgumentNullException.ThrowIfNull(bundles);
        if (bundles.Count == 0)
        {
            throw new ArgumentException("A bundled model must declare at least one bundle.", nameof(bundles));
        }

        if (bundles.Any(bundle => bundle is null))
        {
            throw new ArgumentException("A bundled model must not declare a null bundle.", nameof(bundles));
        }

        // Each bundle's metadata lands in licenses/{Name}/, so two bundles with one name would overwrite each other
        var duplicateName = bundles
            .GroupBy(bundle => bundle.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateName is not null)
        {
            throw new ArgumentException($"The bundle '{duplicateName.Key}' is declared more than once.", nameof(bundles));
        }

        // Every vendor file of every bundle lands in one installed directory, so their paths must be disjoint
        var collision = FindPathCollision(bundles.SelectMany(bundle => bundle.Contents.Files.Select(NormalizePath)));
        if (collision is not null)
        {
            throw new ArgumentException($"The vendor path '{collision}' collides with another bundle's vendor path.", nameof(bundles));
        }

        // Each zip is staged beside the extracted files, so a zip name must not equal any bundle's vendor path
        var allPaths = new HashSet<string>(bundles.SelectMany(bundle => bundle.Contents.Files.Select(NormalizePath)), StringComparer.OrdinalIgnoreCase);
        var clash = bundles.FirstOrDefault(bundle => allPaths.Contains(bundle.FileName));
        if (clash is not null)
        {
            throw new ArgumentException($"The bundle file name '{clash.FileName}' equals a vendor path of another bundle.", nameof(bundles));
        }
    }

    /// <summary>
    ///     Validates the vendor paths of one bundle (see the constructor's <c>contents</c> parameter for the rules).
    /// </summary>
    /// <param name="contents">The bundle's vendor files.</param>
    /// <param name="fileName">The bundle's zip file name.</param>
    /// <exception cref="ArgumentException">Thrown when a vendor path breaks a rule.</exception>
    private static void ValidateContents(SpeechModelDownloadDescriptor contents, string fileName)
    {
        foreach (var file in contents.Files)
        {
            var path = NormalizePath(file);
            var segments = path.Split('/');

            // licenses/ belongs to the metadata copies, and the zip and metadata names are taken by the bundle itself
            if (string.Equals(segments[0], LicensesDirectoryName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(path, fileName, StringComparison.OrdinalIgnoreCase)
                || SpeechModelBundleInstaller.MetadataEntryNames.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"The vendor path '{path}' is reserved (it is under '{LicensesDirectoryName}/', a metadata file name, or the bundle file name).",
                    nameof(contents));
            }

            // The path is also a zip entry name, so it must pass the installer's full entry-name check (including the
            // colon rejection, which the platform's invalid-character set omits on Unix) on every platform
            try
            {
                ZipEntryPathValidator.Validate(path);
            }
            catch (InvalidDataException ex)
            {
                throw new ArgumentException($"The vendor path '{path}' is not valid on every platform.", nameof(contents), ex);
            }
        }

        var collision = FindPathCollision(contents.Files.Select(NormalizePath));
        if (collision is not null)
        {
            throw new ArgumentException($"The vendor path '{collision}' collides with another vendor path of the bundle.", nameof(contents));
        }
    }

    /// <summary>
    ///     The bundle file-name rule: lowercase alphanumeric tokens joined by <c>.</c> or <c>-</c>, a quantization
    ///     token, a bundle revision <c>b{N}</c> (N from 1, no leading zero) and the <c>.zip</c> extension.
    /// </summary>
    /// <returns>The compiled, culture-invariant, case-sensitive pattern.</returns>
    [GeneratedRegex(@"^[a-z0-9]+([.-][a-z0-9]+)*-(fp32|fp16|int8|int4)-b[1-9][0-9]*\.zip\z", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();
}
