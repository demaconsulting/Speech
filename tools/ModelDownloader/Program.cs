using DemaConsulting.Speech.ModelManagementSubsystem;

// CI-only bootstrap tool. Downloads one or more known models by id into the shared, per-user
// model store (Environment.SpecialFolder.LocalApplicationData\DemaConsulting.Speech\Models -
// an OS-dependent path: %LOCALAPPDATA% on Windows, ~/.local/share on Linux, and
// ~/Library/Application Support on macOS as of .NET 8), so tests that are otherwise
// deliberately skipped when a real model is not installed (see the class-level remarks on
// SherpaOnnxRecognitionEngineTests and SherpaOnnxRecognitionEngineAccuracyTests) can exercise
// a real, downloaded model instead. Not part of the library's public surface, never packed or
// shipped - see ModelDownloader.csproj.
//
// This constructs its own SpeechModelStore/SpeechModelDownloader (rather than going through
// SpeechModelCatalog, whose client-injecting constructor is internal) so it can supply a
// longer-than-default HttpClient.Timeout: the default 100-second timeout covers the entire
// request including reading the response body, which is too short for the ~310 MB and ~464 MB
// model archives this tool downloads in CI on a slow runner/connection.
if (args.Length == 0)
{
    await Console.Error.WriteLineAsync("Usage: DemaConsulting.Speech.ModelDownloader <modelId> [modelId...]").ConfigureAwait(false);
    return 1;
}

using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
using var downloadClient = new HttpModelDownloadClient(httpClient);
var store = new SpeechModelStore();
using var downloader = new SpeechModelDownloader(store, downloadClient);
var failed = false;

foreach (var modelId in args)
{
    try
    {
        var model = SpeechModelCatalog.KnownModels
            .FirstOrDefault(candidate => string.Equals(candidate.Id, modelId, StringComparison.Ordinal));
        if (model is null)
        {
            await Console.Error.WriteLineAsync($"Model '{modelId}' is not a known model.").ConfigureAwait(false);
            failed = true;
            continue;
        }

        if (store.IsInstalled(modelId))
        {
            Console.WriteLine($"Model '{modelId}' is already installed - skipping download.");
            continue;
        }

        Console.WriteLine($"Downloading model '{modelId}'...");
        var lastReportedPercent = -1;
        var lastReportedMegabytes = -1L;
        var progress = new Progress<SpeechModelDownloadProgress>(p =>
        {
            if (p.FractionComplete is { } fraction)
            {
                var percent = (int)(fraction * 100);
                if (percent == lastReportedPercent)
                {
                    return;
                }

                lastReportedPercent = percent;
                Console.WriteLine($"  '{modelId}' file {p.FileIndex + 1}/{p.FileCount}: {percent}%");
            }
            else
            {
                // TotalBytes is unknown (no Content-Length reported), so report progress by
                // whole megabytes transferred instead of a percentage, throttled the same way.
                var megabytes = p.BytesTransferred / (1024 * 1024);
                if (megabytes == lastReportedMegabytes)
                {
                    return;
                }

                lastReportedMegabytes = megabytes;
                Console.WriteLine($"  '{modelId}' file {p.FileIndex + 1}/{p.FileCount}: {megabytes:N0} MB");
            }
        });

        var result = await downloader.DownloadAsync(model, progress).ConfigureAwait(false);
        if (result.Outcome == SpeechModelDownloadOutcome.Installed)
        {
            Console.WriteLine($"Model '{modelId}' installed successfully.");
        }
        else
        {
            await Console.Error.WriteLineAsync(
                $"Model '{modelId}' failed to install: {result.Outcome} ({result.Error?.Message})").ConfigureAwait(false);
            failed = true;
        }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        // Any failure for one model (bad id, network/IO error, checksum mismatch, etc.) must
        // not abort the remaining requested models - report it and keep going, reflecting the
        // overall failure in the tool's exit code instead.
        await Console.Error.WriteLineAsync($"Model '{modelId}' failed: {ex.Message}").ConfigureAwait(false);
        failed = true;
    }
}

return failed ? 1 : 0;
