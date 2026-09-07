using DemaConsulting.Speech.ModelManagementSubsystem;

// CI-only bootstrap tool. Downloads one or more known models by id into the shared, per-user
// model store (Environment.SpecialFolder.LocalApplicationData\DemaConsulting.Speech\Models -
// an OS-dependent path: %LOCALAPPDATA% on Windows, ~/.local/share on Linux, and
// ~/Library/Application Support on macOS as of .NET 8), so tests that are otherwise
// deliberately skipped when a real model is not installed (see the class-level remarks on
// SherpaOnnxRecognitionEngineTests and SherpaOnnxRecognitionEngineAccuracyTests) can exercise
// a real, downloaded model instead. Not part of the library's public surface, never packed or
// shipped - see ModelDownloader.csproj.
if (args.Length == 0)
{
    await Console.Error.WriteLineAsync("Usage: DemaConsulting.Speech.ModelDownloader <modelId> [modelId...]").ConfigureAwait(false);
    return 1;
}

using var catalog = new SpeechModelCatalog();
var failed = false;

foreach (var modelId in args)
{
    try
    {
        if (catalog.GetState(modelId) == SpeechModelState.Downloaded)
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
                Console.WriteLine($"  '{modelId}' file {p.FileIndex + 1}/{p.FileCount}: {p.BytesTransferred:N0} bytes");
            }
        });

        var result = await catalog.DownloadAsync(modelId, progress).ConfigureAwait(false);
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
    catch (ArgumentException ex)
    {
        await Console.Error.WriteLineAsync($"Model '{modelId}' is not a known model: {ex.Message}").ConfigureAwait(false);
        failed = true;
    }
}

return failed ? 1 : 0;
