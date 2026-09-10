using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.ModelDownloader;

/// <summary>
///     Hosts the CI-only model download command so automated runs can provision real speech
///     models before integration tests execute.
/// </summary>
/// <remarks>
///     This executable exists outside the library's public API because CI needs a narrow,
///     behavior-preserving bootstrap step that downloads known models into the shared per-user
///     model store. The implementation keeps each requested model isolated from the others so one
///     failed download never prevents later requests from running, which preserves the original
///     command-line tool contract.
/// </remarks>
internal static class Program
{
    /// <summary>
    ///     The extended transfer timeout used for CI model downloads.
    /// </summary>
    /// <remarks>
    ///     The default <see cref="HttpClient.Timeout"/> covers the entire response body read and
    ///     is too short for the repository's large model archives on slower CI runners, so this
    ///     command deliberately opts into a longer budget.
    /// </remarks>
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(15);

    /// <summary>
    ///     Executes the CI bootstrap download flow for every requested model identifier.
    /// </summary>
    /// <param name="args">
    ///     The model identifiers supplied on the command line. At least one identifier is
    ///     required for the tool to do useful work.
    /// </param>
    /// <returns>
    ///     <c>0</c> when every requested model was already installed or installed successfully;
    ///     otherwise <c>1</c> after all requested model ids have been attempted.
    /// </returns>
    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            await Console.Error.WriteLineAsync(
                    "Usage: DemaConsulting.Speech.ModelDownloader <modelId> [modelId...]")
                .ConfigureAwait(false);
            return 1;
        }

        using var httpClient = CreateHttpClient();
        using var downloadClient = new HttpModelDownloadClient(httpClient);
        var store = new SpeechModelStore();
        using var downloader = new SpeechModelDownloader(store, downloadClient);

        return await DownloadRequestedModelsAsync(args, store, downloader).ConfigureAwait(false);
    }

    /// <summary>
    ///     Creates the dedicated HTTP client that keeps large CI downloads from timing out
    ///     prematurely.
    /// </summary>
    /// <returns>
    ///     A new <see cref="HttpClient"/> configured with the longer timeout this bootstrap tool
    ///     requires.
    /// </returns>
    /// <remarks>
    ///     This tool constructs its own download stack instead of routing through
    ///     <see cref="SpeechModelCatalog"/> so it can adjust transfer policy without changing the
    ///     library's production defaults.
    /// </remarks>
    private static HttpClient CreateHttpClient() => new() { Timeout = DownloadTimeout };

    /// <summary>
    ///     Processes each requested model id while preserving the original "try them all" command
    ///     semantics.
    /// </summary>
    /// <param name="modelIds">The model identifiers requested on the command line. Never null.</param>
    /// <param name="store">
    ///     The shared model store used to detect whether a requested model is already installed.
    ///     Never null.
    /// </param>
    /// <param name="downloader">
    ///     The downloader that fetches, verifies, and installs missing models into
    ///     <paramref name="store"/>. Never null.
    /// </param>
    /// <returns>
    ///     <c>0</c> when every model succeeds; otherwise <c>1</c> after all requests have been
    ///     attempted.
    /// </returns>
    /// <remarks>
    ///     CI wants one aggregate exit code, but it also needs every requested model attempt to
    ///     run so logs show the full success/failure set in a single invocation.
    /// </remarks>
    private static async Task<int> DownloadRequestedModelsAsync(
        IEnumerable<string> modelIds,
        SpeechModelStore store,
        SpeechModelDownloader downloader)
    {
        var failed = false;
        foreach (var modelId in modelIds)
        {
            failed |= await TryDownloadModelAsync(modelId, store, downloader).ConfigureAwait(false);
        }

        return failed ? 1 : 0;
    }

    /// <summary>
    ///     Resolves one requested model id, skips redundant work for installed models, and reports
    ///     a per-model success or failure outcome.
    /// </summary>
    /// <param name="modelId">The requested model identifier. Never null or empty.</param>
    /// <param name="store">
    ///     The model store used to detect existing installations before download work begins.
    ///     Never null.
    /// </param>
    /// <param name="downloader">
    ///     The downloader that performs the actual fetch/verify/install operation. Never null.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the request failed and should contribute to a non-zero
    ///     process exit code; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///     This helper exists to keep the entry point focused on command orchestration while the
    ///     model-specific decision tree remains isolated and easier to audit.
    /// </remarks>
    private static async Task<bool> TryDownloadModelAsync(
        string modelId,
        SpeechModelStore store,
        SpeechModelDownloader downloader)
    {
        try
        {
            // Resolve the requested id up front so the operator gets a clear, model-specific
            // error before any download work or progress reporting begins.
            var model = FindKnownModel(modelId);
            if (model is null)
            {
                await Console.Error.WriteLineAsync(
                        $"Model '{modelId}' is not a known model.")
                    .ConfigureAwait(false);
                return true;
            }

            // Skip work when the shared store already contains the requested model so repeated
            // CI retries stay idempotent and fast.
            if (store.IsInstalled(modelId))
            {
                Console.WriteLine($"Model '{modelId}' is already installed - skipping download.");
                return false;
            }

            return await DownloadModelAsync(modelId, model, downloader).ConfigureAwait(false);
        }
        // Intentionally broad: this CLI treats each requested model as an independent work item,
        // so any unexpected timeout, network, checksum, or filesystem failure must be reported
        // for that model without aborting later requested downloads in the same invocation.
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"Model '{modelId}' failed: {ex.Message}").ConfigureAwait(false);
            return true;
        }
    }

    /// <summary>
    ///     Looks up a requested id in the compiled-in model catalog so the tool only downloads
    ///     supported models.
    /// </summary>
    /// <param name="modelId">The requested model identifier. Never null.</param>
    /// <returns>
    ///     The matching known model when the id is supported; otherwise <see langword="null"/>.
    /// </returns>
    /// <remarks>
    ///     Keeping the lookup in one helper centralizes the command's strict
    ///     ordinal-comparison policy, which avoids accidental case-folding differences between
    ///     platforms and store implementations.
    /// </remarks>
    private static ISpeechModel? FindKnownModel(string modelId) =>
        SpeechModelCatalog.KnownModels
            .FirstOrDefault(candidate => string.Equals(candidate.Id, modelId, StringComparison.Ordinal));

    /// <summary>
    ///     Downloads one missing model and translates the downloader's result into the tool's
    ///     console-oriented success/failure reporting.
    /// </summary>
    /// <param name="modelId">The requested model identifier. Never null or empty.</param>
    /// <param name="model">The resolved known model metadata for <paramref name="modelId"/>. Never null.</param>
    /// <param name="downloader">
    ///     The downloader that performs fetch, verification, and installation. Never null.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when installation failed and should contribute to a non-zero
    ///     exit code; otherwise <see langword="false"/>.
    /// </returns>
    /// <remarks>
    ///     This helper isolates the downloader result translation so the entry point does not
    ///     need to know the details of installed-versus-failed outcomes.
    /// </remarks>
    private static async Task<bool> DownloadModelAsync(
        string modelId,
        ISpeechModel model,
        SpeechModelDownloader downloader)
    {
        Console.WriteLine($"Downloading model '{modelId}'...");

        var progress = CreateProgressReporter(modelId);
        var result = await downloader.DownloadAsync(model, progress).ConfigureAwait(false);
        if (result.Outcome == SpeechModelDownloadOutcome.Installed)
        {
            Console.WriteLine($"Model '{modelId}' installed successfully.");
            return false;
        }

        await Console.Error.WriteLineAsync(
                $"Model '{modelId}' failed to install: {result.Outcome} ({result.Error?.Message})")
            .ConfigureAwait(false);
        return true;
    }

    /// <summary>
    ///     Creates a throttled progress reporter so large downloads remain observable without
    ///     flooding CI logs.
    /// </summary>
    /// <param name="modelId">The model identifier whose transfer progress is being reported.</param>
    /// <returns>
    ///     An <see cref="IProgress{T}"/> instance that emits percentage updates when the total
    ///     size is known, or whole-megabyte updates otherwise.
    /// </returns>
    /// <remarks>
    ///     Throttling to changed percentages or changed whole-megabyte counts preserves the
    ///     original operator experience while dramatically reducing redundant log lines for large
    ///     transfers.
    /// </remarks>
    private static IProgress<SpeechModelDownloadProgress> CreateProgressReporter(string modelId)
    {
        var lastReportedPercent = -1;
        var lastReportedMegabytes = -1L;

        return new Progress<SpeechModelDownloadProgress>(progress =>
        {
            // Prefer percentage progress when Content-Length is known because it is easier to
            // compare across files; otherwise fall back to transferred megabytes so the log still
            // shows forward motion for chunked or unknown-length responses.
            if (progress.FractionComplete is { } fraction)
            {
                var percent = (int)(fraction * 100);
                if (percent == lastReportedPercent)
                {
                    return;
                }

                lastReportedPercent = percent;
                Console.WriteLine(
                    $"  '{modelId}' file {progress.FileIndex + 1}/{progress.FileCount}: {percent}%");
                return;
            }

            var megabytes = progress.BytesTransferred / (1024 * 1024);
            if (megabytes == lastReportedMegabytes)
            {
                return;
            }

            lastReportedMegabytes = megabytes;
            Console.WriteLine(
                $"  '{modelId}' file {progress.FileIndex + 1}/{progress.FileCount}: {megabytes:N0} MB");
        });
    }
}
