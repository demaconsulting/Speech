using DemaConsulting.Speech.ModelManagementSubsystem;

// CI-only bootstrap tool. Downloads one or more known models by id into the shared
// %LocalAppData%\DemaConsulting.Speech\Models store, so tests that are otherwise
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
    if (catalog.GetState(modelId) == SpeechModelState.Downloaded)
    {
        Console.WriteLine($"Model '{modelId}' is already installed - skipping download.");
        continue;
    }

    Console.WriteLine($"Downloading model '{modelId}'...");
    var lastReportedPercent = -1;
    var progress = new Progress<SpeechModelDownloadProgress>(p =>
    {
        var percent = p.FractionComplete is { } fraction ? (int)(fraction * 100) : -1;
        if (percent == lastReportedPercent)
        {
            return;
        }

        lastReportedPercent = percent;
        Console.WriteLine(percent >= 0
            ? $"  '{modelId}' file {p.FileIndex + 1}/{p.FileCount}: {percent}%"
            : $"  '{modelId}' file {p.FileIndex + 1}/{p.FileCount}: {p.BytesTransferred:N0} bytes");
    });

    try
    {
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
