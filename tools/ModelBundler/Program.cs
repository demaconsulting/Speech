namespace DemaConsulting.Speech.ModelBundler;

/// <summary>
///     Hosts the maintainer-only model bundling tool: builds deterministic model bundle zips for the project's
///     <c>models</c> release, verifies that a build reproduces, and uploads a build without replacing any asset.
/// </summary>
/// <remarks>
///     This executable is never packed or shipped. It exists so the bundles the model packages download are produced
///     from the packages' own compiled per-file pins, by a repeatable process anyone can rerun to prove the published
///     bytes. See <c>tools/ModelBundler/README.md</c> for usage and the publishing order.
/// </remarks>
internal static class Program
{
    /// <summary>The exit code of a successful run.</summary>
    private const int Success = 0;

    /// <summary>The exit code of a failed build, verification or upload.</summary>
    private const int Failure = 1;

    /// <summary>The exit code of an invalid command line.</summary>
    private const int UsageError = 2;

    /// <summary>The overall HTTP timeout of one vendor file download (large files on a slow link).</summary>
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(60);

    /// <summary>The connection timeout, so an unreachable host fails fast instead of waiting for the download timeout.</summary>
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Parses the command line and runs the requested command.
    /// </summary>
    /// <param name="args">The command line (see <see cref="CommandLineOptions.Usage"/>).</param>
    /// <returns><c>0</c> on success, <c>1</c> when the command fails, <c>2</c> for an invalid command line.</returns>
    private static async Task<int> Main(string[] args)
    {
        // Reject an invalid command line before doing any work
        CommandLineOptions options;
        try
        {
            options = CommandLineOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            await Console.Error.WriteLineAsync(CommandLineOptions.Usage).ConfigureAwait(false);
            return UsageError;
        }

        try
        {
            return options.Command == BundlerCommand.Upload
                ? await UploadAsync(options).ConfigureAwait(false)
                : await BuildOrVerifyAsync(options).ConfigureAwait(false);
        }
        // Intentionally broad: a maintainer tool reports any failure (I/O, network, pin mismatch, gh) as one clear
        // message and a non-zero exit code instead of a stack trace
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            await Console.Error.WriteLineAsync($"FAILED: {ex.Message}").ConfigureAwait(false);
            return Failure;
        }
    }

    /// <summary>Runs <c>build</c> or <c>verify</c>.</summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The exit code.</returns>
    private static async Task<int> BuildOrVerifyAsync(CommandLineOptions options)
    {
        // The definitions and the compiled contents are the only inputs besides the vendor bytes
        var definitions = BundleDefinitions.Load(BundleDefinitions.GetDefaultPath());
        using var handler = new SocketsHttpHandler { ConnectTimeout = ConnectTimeout };
        using var httpClient = new HttpClient(handler) { Timeout = DownloadTimeout };
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DemaConsulting.Speech.ModelBundler");
        var source = new VendorFileSource(options.SourceStore, options.Cache, httpClient);
        var builder = new BundleBuilder(BundleCatalog.GetContents(), BundleCatalog.GetPublishedChecksums(), source, Console.Out);

        if (options.Command == BundlerCommand.Verify)
        {
            var identical = await builder.VerifyAsync(definitions, options.Input!, CancellationToken.None).ConfigureAwait(false);
            Console.WriteLine(identical ? "VERIFIED: the build reproduces byte for byte." : "NOT REPRODUCED: see the DIFFERENT lines above.");
            return identical ? Success : Failure;
        }

        // Select the requested bundles; an undeclared name is a command-line error
        var selected = new List<BundleDefinition>();
        if (options.All)
        {
            selected.AddRange(definitions);
        }
        else
        {
            foreach (var name in options.Bundles)
            {
                var definition = definitions.FirstOrDefault(candidate => string.Equals(candidate.Name, name, StringComparison.Ordinal));
                if (definition is null)
                {
                    await Console.Error.WriteLineAsync($"'{name}' is not a bundle declared in {BundleDefinitions.FileName}.").ConfigureAwait(false);
                    return UsageError;
                }

                selected.Add(definition);
            }
        }

        var built = await builder.BuildSetAsync(selected, options.Output!, options.SkipUnavailable, CancellationToken.None).ConfigureAwait(false);
        if (built.Count == 0)
        {
            await Console.Error.WriteLineAsync("No bundle was built.").ConfigureAwait(false);
            return Failure;
        }

        return Success;
    }

    /// <summary>Runs <c>upload</c>.</summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The exit code.</returns>
    private static async Task<int> UploadAsync(CommandLineOptions options)
    {
        var uploader = new ReleaseUploader(new ProcessRunner(), Console.Out);
        return await uploader.UploadAsync(options.Input!, options.Repository, options.DryRun, CancellationToken.None).ConfigureAwait(false)
            ? Success
            : Failure;
    }
}
