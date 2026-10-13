## Building a Minimal End-to-End Application

The preceding sections introduced model download, capture/recognition, and playback/synthesis
independently. This section ties them together into one minimal console application - the kind
of program you can copy into a new project - that downloads one recognition model and one
synthesis model, then listens for speech and speaks a reply.

```csharp
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Sherpa;

// 1. Compose the per-user model store/catalog and the audio devices. None of this throws for an
//    ordinary machine state - a missing microphone, missing speakers, or a missing native
//    runtime all degrade to an honest "unavailable" device/engine/session instead.
using var catalog = new SpeechModelCatalog().AddSherpaModels();
var audioFactory = new AudioDeviceFactory();

// 2. Download one recognition model and one synthesis model on first run. DownloadAsync is a
//    safe no-op cost-wise to call every launch: skip it yourself once IsInstalled(...) is true
//    if you want to avoid the up-front installed check on every start-up.
const string recognitionModelId = "streaming-zipformer-en-2023-06-26";
const string synthesisModelId = "kokoro-int8-en-v0_19";

await catalog.DownloadAsync(recognitionModelId);
await catalog.DownloadAsync(synthesisModelId);

// 3. Resolve each downloaded id back to its ISpeechModel instance and installed directory.
var recognitionDescriptor = catalog.Enumerate().Single(d => d.Id == recognitionModelId);
var synthesisDescriptor = catalog.Enumerate().Single(d => d.Id == synthesisModelId);
var recognitionModel = (IRecognitionModel)recognitionDescriptor.Model;
var synthesisModel = (ISynthesisModel)synthesisDescriptor.Model;
var captureDevice = audioFactory.CreateCaptureDevice(
    AudioDeviceSelection.SystemDefault,
    recognitionModel.AudioFormat);
var playbackDevice = audioFactory.CreatePlaybackDevice(
    AudioDeviceSelection.SystemDefault,
    synthesisModel.PreferredAudioFormat);

// 4. Load the recognizer and synthesizer engines over the resolved models, then create a
//    session bound to each resolved device. The recognition session is single-use (one session
//    per "turn"); the synthesis session is reused for every reply.
await using var recognizerEngine = await SpeechRecognizerFactory.LoadAsync(
    recognitionModel,
    catalog);

await using var synthesizerEngine = await SpeechSynthesizerFactory.LoadAsync(
    synthesisModel,
    catalog);

if (!recognizerEngine.IsAvailable || !synthesizerEngine.IsAvailable)
{
    Console.WriteLine("No microphone/speakers (or the models failed to load) - exiting.");
    return;
}

await using var recognitionSession = await recognizerEngine.CreateSessionAsync(captureDevice);
await using var synthesisSession = await synthesizerEngine.CreateSessionAsync(playbackDevice);

// 5. Speak a greeting, then listen and echo back each final result until Enter is pressed.
await synthesisSession.SpeakAsync("Hello! [short pause] Say something and I will repeat it back.");

using var cts = new CancellationTokenSource();

var resultsTask = Task.Run(async () =>
{
    await foreach (var evt in recognitionSession.GetResultsAsync(cts.Token))
    {
        if (!evt.Result.IsFinal)
        {
            continue;
        }

        Console.WriteLine($"You said: {evt.Result.Text}");
        await synthesisSession.SpeakAsync($"You said: {evt.Result.Text}");
    }
});

await recognitionSession.StartAsync();
Console.WriteLine("Listening - press Enter to stop.");
Console.ReadLine();
await recognitionSession.StopAsync();
cts.Cancel();
```

This example deliberately keeps error handling minimal for readability; a production application
should also subscribe an `ISpeechDiagnostics` sink (see "Current Capabilities" above) to observe
composition fallbacks and runtime faults instead of relying solely on `IsAvailable` checks.
