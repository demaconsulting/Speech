# Recognizing Speech

The library also provides the `RecognitionSubsystem`
(`DemaConsulting.Speech.RecognitionSubsystem`), which turns captured audio into text:

- **`ISpeechRecognizerEngine`**: the loaded, expensive, native-backed recognition model, with no
  capture device bound yet — `IsAvailable` and `CreateSessionAsync(device, cancellationToken)`.
  No speech-engine type appears anywhere in this contract, so a future engine change cannot break
  your code.
- **`IRecognitionSession`**: the cheap, single-use streaming speech-to-text session bound to
  exactly one capture device for its entire life — `IsAvailable`, `State`, a `StateChanged`
  event, `StartAsync(cancellationToken)`, `StopAsync(cancellationToken)`,
  `GetResultsAsync(cancellationToken)` (an `IAsyncEnumerable<SpeechRecognitionEvent>`), and
  `DisposeAsync()`.
- **`SpeechRecognitionResult`**: the recognized `Text` of the current utterance plus an `IsFinal`
  flag. `Text` is always the full utterance so far, never a fragment, so you can render it
  directly and simply replace it when the next result arrives.
- **`SpeechRecognizerFactory`**: the composition entry point. `LoadAsync(...)` returns a working
  engine only when the model is installed, declares the recognition role, and the speech engine
  loads; otherwise it returns **`UnavailableSpeechRecognizerEngine`**, which reports
  `IsAvailable == false` and whose `CreateSessionAsync` returns
  **`UnavailableRecognitionSession`**. Neither layer throws for any of these ordinary machine
  states.

Typical recognition composition:

```csharp
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.Sherpa;

// 1. The catalog is the library's only "what models exist" entry point - nothing below names a
//    concrete model class, so new models added in a future release show up automatically.
using var catalog = new SpeechModelCatalog().AddSherpaModels();

// 2. Pick a recognition model. Any model with the recognition role will do - this is the
//    idiomatic pattern for an app that just wants "a" speech-to-text model:
var descriptor = catalog.Enumerate().First(d => d.Role == SpeechModelRole.Recognition);
// To pick a *specific* model when more than one of the same role is installed, match on name
// instead: catalog.Enumerate().First(d => d.DisplayName.Contains("Zipformer"));
var model = (IRecognitionModel)descriptor.Model;

// 3. Ensure the chosen model is downloaded before first use. Safe to call unconditionally on
//    every launch - it's a cheap no-op once installed (see above for details).
await catalog.DownloadAsync(model.Id);

// 4. Create a capture device matching the model's own required audio format.
var captureDevice = new AudioDeviceFactory().CreateCaptureDevice(
    AudioDeviceSelection.SystemDefault,
    model.AudioFormat);

// 5. Load the engine once, create a session bound to the capture device, and stream
//    recognized text as it arrives.
await using var engine = await SpeechRecognizerFactory.LoadAsync(model, catalog);

if (engine.IsAvailable)
{
    await using var session = await engine.CreateSessionAsync(captureDevice);

    await session.StartAsync();

    var resultsTask = Task.Run(async () =>
    {
        await foreach (var evt in session.GetResultsAsync())
        {
            var status = evt.Result.IsFinal ? "final" : "partial";
            Console.WriteLine($"{status}: {evt.Result.Text}");
        }
    });

    // ... capture speech ...
    await session.StopAsync();
    await resultsTask;
}
```

Points worth knowing:

- **You usually do not need to match the microphone to the model manually.** The recognizer
  reads the capture device's reported `ChannelCount` and `SampleRate` and converts each captured
  block to the mono rate the model declares. When you already know the chosen model, prefer
  creating the capture device with `model.AudioFormat` first so the backend can open closer to
  the target format and the recognizer often stays on its no-op equal-rate fast path. When
  resampling is still required, the downsampling path applies anti-alias filtering before
  decimation.
- **Results are delivered through a dedicated worker thread, not the audio callback thread.**
  `GetResultsAsync` streams events produced by the session's own dedicated decoding thread, which
  pumps captured audio through the recognition backend independently of the audio device's own
  callback thread. Two bounded buffers isolate a slow consumer from that pump: captured audio
  waiting to be decoded queues in a bounded, drop-oldest buffer, and converted results queue in a
  backpressure buffer that coalesces unread provisional results into a single "latest" slot while
  keeping every final result in an unbounded FIFO — a final result is never dropped or evicted.
  A single concurrent
  `GetResultsAsync` enumeration is supported per session; a second overlapping call throws
  `InvalidOperationException`.
- **`StopAsync` does not lose the tail of an utterance.** It drains audio already captured before
  the call, so every result derived from it is enumerable via `GetResultsAsync` by the time
  `StopAsync` returns.
- **Dispose the session (and eventually the engine).** A real session holds a capture-device
  subscription and the engine's exclusivity lease; disposal releases both and implies `StopAsync`.
  Disposal is idempotent, and disposing the unavailable fallback is a safe no-op. A session is
  single-use: once `State` reaches `Stopped`, `StartAsync` throws `InvalidOperationException`
  rather than restarting — create a new session via `CreateSessionAsync` for another run.
- **`Text` is already restored, not raw engine output.** Before a result reaches your
  `GetResultsAsync` consumer, the session calls the owning model's
  `IRecognitionModel.NormalizeText(text, isFinal)`. Most models simply pass text through
  unchanged (the interface's default), but `SherpaOnnxZipformerEnRecognitionModel` overrides it
  with `UppercaseTranscriptRestorer` to turn its raw shouted, unpunctuated output (for example
  `I DONT THINK THATS WORKING`) into readable prose (`I don't think that's working.`) using a
  conservative, deterministic contraction table and terminal-punctuation restoration —
  deliberately leaving genuinely ambiguous forms (`were`, `well`, `its`, and similar) untouched.
  Provisional (`isFinal: false`) results only get the cheap lowercase/capitalize pass, not the
  full contraction/punctuation restoration, so they stay stable as later words refine them.
- **Host-side post-processing of delivered text is a supported pattern.** "Already restored"
  describes what the library itself will do to `Text`, not a ceiling on what you may do to it
  afterward. Applying your own domain-specific transformation — for example, correcting the
  casing of project-glossary terms (acronyms, product names) that general-purpose recognition
  cannot know about — is an anticipated, supported use of the delivered text, not an
  undocumented workaround. The ordering is guaranteed: the owning model's `NormalizeText` has
  already run by the time `Text` reaches your `GetResultsAsync` consumer, so your transformation
  composes after the library's restoration rather than racing it. Attach that transformation to
  final (`isFinal: true`) results only — provisional results carry just the cheap pass and are
  still being revised, so running your own restoration on them would make the draft flicker
  while the user is still speaking.

The library provides three production recognition models and four production synthesis models; the
recognition models are covered here and the synthesis models under "Synthesizing Speech".

The Sherpa package supplies two recognition models:
`SherpaOnnxZipformerEnRecognitionModel` (`streaming-zipformer-en-2023-06-26`, Apache-2.0
license, LIKELY per icefall/LibriSpeech training-toolkit norms — not independently confirmed
against an explicit model-specific `LICENSE` file) and `SherpaOnnxNemotronStreamingEnRecognitionModel`
(`nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25`, released by NVIDIA under the
**NVIDIA Open Model License** — a custom, non-OSI license materially different from the other
model's Apache-2.0 terms; review that license's field-of-use and redistribution terms before
relying on this model) — both shipped by the separate `DemaConsulting.Speech.Sherpa` package and
registered via `AddSherpaModels()`. Neither model's
bytes are bundled with this library; `SpeechModelCatalog.DownloadAsync` fetches each one directly
from its own official GitHub Releases URL only when you explicitly request it. Both facts above
are also available programmatically without parsing prose or `DisplayName`, via each model's
`ISpeechModel.LicenseName`/`LicenseUrl` (for example `SherpaOnnxZipformerEnRecognitionModel`
reports `"Apache-2.0 (likely)"`, preserving the same uncertainty rating in the value itself).

The optional `DemaConsulting.Speech.Onnx.NemotronStt` package adds a third, streaming
recognition model through `AddNemotronSttModels()`: `OnnxNemotronMultilingualRecognitionModel`
(`nemotron-3.5-asr-streaming-0.6b-onnx-int4`, an int4 ONNX export of about 790 MB, dual-licensed
under the **MIT License and the NVIDIA Open Model License**, both of which apply; review the
NVIDIA license's terms before redistributing). It declares one
`language` choice parameter (`en-US` by default, or `en-GB`) that you pass like any other model
parameter, for example `--stt-param language=en-GB` with `speech-cli recognize`. Its engine runs
entirely on ONNX Runtime, keeps the decoder and joint networks on a single non-spinning CPU
thread, and mitigates leading silence by shortening any quiet run longer than 400 ms to 400 ms
(the quiet threshold is calibrated from the first 400 ms of audio) and by adding a tiny seeded
dither; it also finalizes an utterance after 2.5 seconds of quiet following speech. A download
mirror serves its files from `<mirror>/nemotron-3.5-asr-streaming-0.6b-onnx-int4/<file>`. See
"Synthesizing Speech" below for the production synthesis models.

## Reading WAV and FLAC files

The core package decodes audio files with managed code only. `AudioFileReader.Read(path)` (or
`Read(stream)`) detects WAV (8/16/24/32-bit PCM and 32-bit float) or FLAC by content and returns an
`AudioClip` holding interleaved float samples in [-1, 1] with its `SampleRate`, `ChannelCount` and
`BitsPerSample`. `AudioFileReader.ReadMono(path)` returns down-mixed samples and the native rate,
and `AudioResampler.ToMonoResampled(clip, 16000)` prepares a clip for a model. A missing file throws
`FileNotFoundException`, a malformed file `InvalidDataException` (including a FLAC file with a frame
that runs past the total sample count declared in its header) and an unsupported WAV encoding
`NotSupportedException`. `new AudioClip(...)` rejects null samples, and a sample rate or channel
count that is not positive, with an `ArgumentException`-derived exception. `AudioFileReadOptions` lets you
disable FLAC checksum verification, lower the `MaxSamples` guard against files that decode to too many
samples, or lower `MaxEncodedBytes` (default 1.25 GiB, independent of `MaxSamples`) to reject oversized
input before it is read into memory.
