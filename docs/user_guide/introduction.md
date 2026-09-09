# Introduction

## Purpose

This document is the user guide for DemaConsulting.Speech, a cross-platform .NET library
providing local, offline speech-to-text (STT) and text-to-speech (TTS) services for desktop
applications.

## Scope

This user guide covers:

- Installation of the library
- Using the current diagnostics and real audio-device abstractions
- Managing per-user speech model storage and downloads
- Recognizing speech from an audio input device
- Recognizing inline Natural Language Audio Tags in text intended for text-to-speech
- Running the bundled demo application
- Runtime-support limitations and fallback behavior
- Where to find full API and design detail

# Continuous Compliance

This repository follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology,
which ensures compliance evidence is generated automatically on every CI run.

## Key Practices

- **Requirements Traceability**: Every requirement is linked to passing tests, and a trace matrix
  is auto-generated on each release
- **Linting Enforcement**: markdownlint, cspell, and yamllint are enforced before any build proceeds
- **Automated Audit Documentation**: Each release ships with generated requirements,
  justifications, trace matrix, and quality reports
- **CodeQL and SonarCloud**: Security and quality analysis runs on every build

# Installation

Install the library using the .NET CLI:

```bash
dotnet add package DemaConsulting.Speech
```

The package references `PortAudioSharp2`, whose transitive native runtime packages cover
`win-x64`, `linux-x64`, `linux-aarch64`, `osx-x64`, and `osx-arm64`. No `win-arm64`
PortAudio runtime package is available through that dependency chain in this phase.

The package also references the managed `org.k2fsa.sherpa.onnx` speech-engine package. It never
references a per-RID `org.k2fsa.sherpa.onnx.runtime.*` package directly; the managed package
declares those as its own dependencies, so your application restores the native runtime for its
target RID transitively. If that runtime is absent, speech recognition reports itself unavailable
rather than failing.

# Usage

## Current Capabilities

This release ships the `Diagnostics` subsystem and a real `AudioSubsystem` implementation:

- **`ISpeechDiagnostics`** (`DemaConsulting.Speech.Diagnostics`): a contract for reporting
  structural diagnostic events to an optional host-supplied sink. `NullSpeechDiagnostics`
  provides the default no-op implementation via `NullSpeechDiagnostics.Instance`.
- **`AudioDeviceFactory`** (`DemaConsulting.Speech.AudioSubsystem`): the composition entry point
  for creating audio capture/playback devices and probes. When PortAudio initializes
  successfully, it returns real devices backed by PortAudio; otherwise it falls back to honest
  unavailable implementations.
- **`AudioDeviceDescription`** and **`AudioDeviceSelection`**: immutable, name-only
  representations of an audio device and a persistable user device preference.

This release also ships the storage and download machinery of the `ModelManagementSubsystem`
(`DemaConsulting.Speech.ModelManagementSubsystem`), used to fetch and install speech models on
a per-user basis:

- **`SpeechModelStore`**: per-user model storage rooted under `LocalApplicationData` by default
  (or a host-configured override via `SpeechModelStoreOptions`). Installs are atomic: a
  replacement is only swapped into `current/` after being fully staged and verified, so a
  failed repair never disturbs a working installation. Also provides honest `IsInstalled(...)`
  queries and `Uninstall(...)`.
- **`SpeechModelDownloader`**: allows unlimited concurrent downloads across different models,
  while serializing concurrent download attempts for the same model id so `SpeechModelStore`'s
  atomic install is never raced; fetches each file via an injected `IModelDownloadClient`
  (`HttpModelDownloadClient` by default), verifies each file's SHA-256 checksum, and only then
  hands the verified files to `SpeechModelStore` for atomic install. A corrupted, partial, or
  checksum-mismatched download is never marked installed.
- **`SpeechModelDownloadDescriptor`** / **`SpeechModelDownloadFile`**: describe the HTTPS URL(s)
  and expected SHA-256 checksum(s) for a model's downloadable files.
- **`SpeechModelDownloadProgress`**: reports per-file transfer progress, suitable for
  `IProgress<T>` consumption by a GUI progress bar.

Typical model download composition:

```csharp
using DemaConsulting.Speech.ModelManagementSubsystem;

var store = new SpeechModelStore();
var downloader = new SpeechModelDownloader(store);

var descriptor = new SpeechModelDownloadDescriptor(
[
    new SpeechModelDownloadFile(
        new Uri("https://example.com/model.bin"),
        "…64-character SHA-256 hex checksum…",
        "model.bin")
]);

await downloader.DownloadAsync("my-model", descriptor);
```

This release also ships the model catalog/contract seam of the `ModelManagementSubsystem`,
describing *which* models are available and their tunable parameters:

- **`ISpeechModel`** (with **`IRecognitionModel`**/**`ISynthesisModel`** role markers): the
  common per-model contract — identity, `Role`, declared `Parameters`, declared
  `AudioTagSupport`, and `DownloadDescriptor`.
- **`ISpeechModelParameter`** (with **`NumericParameter`**, **`ChoiceParameter`**, and
  **`BooleanParameter`**): typed, self-validating tunable-parameter descriptors a host can use
  to render an appropriate generic control (slider, dropdown, checkbox) for any model.
- **`SpeechModelDescriptor`**: pairs one `ISpeechModel` with its current `SpeechModelState`
  (`NotDownloaded`, `Downloading`, `Downloaded`, or `FailedOrCorrupt`).
- **`SpeechModelCatalog`**: enumerates the library's known/compiled-in models alongside each
  one's current install state, and orchestrates downloading a known model by id.
  `DownloadAsync` is safe to call unconditionally on every launch - it's a cheap no-op once a
  model is installed, and otherwise returns `Failed` (transport or I/O failure, with the
  underlying exception in `SpeechModelDownloadResult.Error`) or `ChecksumMismatch` on a real
  download problem, or throws `ArgumentException` for an unrecognized model id.

This release ships **four real, production model classes** across both roles it defines —
`SpeechModelCatalog.KnownModels` is a compiled-in, non-empty list. The catalog/contract seam,
together with each model's own `CreateEngineConfig(...)` and (for synthesis) `CapabilityProfile`
members, lets a host already build a model-settings-style UI and compose a real
recognizer/synthesizer against any of the four. See "Recognizing Speech" and "Synthesizing
Speech" below for each model's identity and license.

A model may also unpack its own downloaded archive before it becomes usable: `ISpeechModel`
declares an `InstallAsync(stagedFilesDirectory, cancellationToken)` hook, defaulting to a no-op
for the common case of a single directly-usable downloaded file, but overridable by a model
whose declared payload is a zip/tar archive. `SpeechModelDownloader`/`SpeechModelCatalog` invoke
this hook on the verified staged files before they are atomically installed, so this unpacking
never touches an existing installation until it succeeds.

Typical catalog usage:

```csharp
using DemaConsulting.Speech.ModelManagementSubsystem;

var catalog = new SpeechModelCatalog();

foreach (var descriptor in catalog.Enumerate())
{
    Console.WriteLine($"{descriptor.DisplayName}: {descriptor.State}");
}
```

Typical capture composition:

```csharp
using DemaConsulting.Speech.AudioSubsystem;

var factory = new AudioDeviceFactory();
var device = factory.CreateCaptureDevice(AudioDeviceSelection.SystemDefault);
// Optionally pass a preferred AudioFormat when you already know the target format.

if (device.IsAvailable)
{
    device.FrameCaptured += (_, args) =>
    {
        // args.Samples contains normalized float audio samples.
    };

    device.Start();
}
```

## Recognizing Speech

This release also ships the `RecognitionSubsystem`
(`DemaConsulting.Speech.RecognitionSubsystem`), which turns captured audio into text:

- **`ISpeechRecognizer`**: the streaming speech-to-text contract — `IsAvailable`, `Start()`,
  `Stop()`, a `ResultReceived` event, and `Dispose()`. No speech-engine type appears anywhere in
  this contract, so a future engine change cannot break your code.
- **`SpeechRecognitionResult`**: the recognized `Text` of the current utterance plus an `IsFinal`
  flag. `Text` is always the full utterance so far, never a fragment, so you can render it
  directly and simply replace it when the next result arrives.
- **`SpeechRecognizerFactory`**: the composition entry point. It returns a working recognizer
  only when the model is installed, declares the recognition role, the capture device is
  available, and the speech engine loads; otherwise it returns
  **`UnavailableSpeechRecognizer`**, which reports `IsAvailable == false`. It never throws for
  any of these ordinary machine states.

Typical recognition composition:

```csharp
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

// 1. The catalog is the library's only "what models exist" entry point - nothing below names a
//    concrete model class, so new models added in a future release show up automatically.
using var catalog = new SpeechModelCatalog();

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

// 5. Compose the recognizer and stream recognized text as it arrives.
using var recognizer = SpeechRecognizerFactory.Create(model, catalog, captureDevice);

if (recognizer.IsAvailable)
{
    recognizer.ResultReceived += (_, args) =>
    {
        var status = args.Result.IsFinal ? "final" : "partial";
        Console.WriteLine($"{status}: {args.Result.Text}");
    };

    recognizer.Start();
    // ... capture speech ...
    recognizer.Stop();
}
```

Points worth knowing:

- **You usually do not need to match the microphone to the model manually.** The recognizer
  reads the capture device's reported `ChannelCount` and `SampleRate` and converts each captured
  block to the mono rate the model declares. When you already know the chosen model, prefer
  creating the capture device with `model.AudioFormat` first so the backend can open closer to
  the target format and the recognizer often stays on its no-op equal-rate fast path. When
  resampling is still required, the downsampling path now applies anti-alias filtering before
  decimation.
- **Results arrive off the audio thread.** `ResultReceived` is raised from the recognizer's own
  background decoding thread, never from the audio callback thread, so a handler may do moderate
  work. Handlers are invoked serially, and an exception thrown by a handler is reported through
  your diagnostics sink rather than propagated.
- **`Stop()` does not lose the tail of an utterance.** It drains audio already captured before
  the call, so every result derived from it has been delivered by the time `Stop()` returns.
- **Dispose the recognizer.** A real recognizer holds a loaded engine; disposal releases it and
  implies `Stop()`. Disposal is idempotent, and disposing the unavailable fallback is a safe
  no-op.
- **`Text` is already restored, not raw engine output.** Before a result reaches your
  `ResultReceived` handler, the recognizer calls the owning model's
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
  already run by the time `Text` reaches your `ResultReceived` handler, so your transformation
  composes after the library's restoration rather than racing it. Attach that transformation to
  final (`isFinal: true`) results only — provisional results carry just the cheap pass and are
  still being revised, so running your own restoration on them would make the draft flicker
  while the user is still speaking.

This release ships two production recognition models —
`SherpaOnnxZipformerEnRecognitionModel` (`streaming-zipformer-en-2023-06-26`, Apache-2.0
license, LIKELY per icefall/LibriSpeech training-toolkit norms — not independently confirmed
against an explicit model-specific `LICENSE` file, since `huggingface.co` is unreachable from
this project's sandboxes) and `SherpaOnnxNemotronStreamingEnRecognitionModel`
(`nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25`, released by NVIDIA under the
**NVIDIA Open Model License** — a custom, non-OSI license materially different from the other
model's Apache-2.0 terms; review that license's field-of-use and redistribution terms before
relying on this model) — both registered in `SpeechModelCatalog.KnownModels`. Neither model's
bytes are bundled with this library; `SpeechModelCatalog.DownloadAsync` fetches each one directly
from its own official GitHub Releases URL only when you explicitly request it. Both facts above
are also available programmatically without parsing prose or `DisplayName`, via each model's
`ISpeechModel.LicenseName`/`LicenseUrl` (for example `SherpaOnnxZipformerEnRecognitionModel`
reports `"Apache-2.0 (likely)"`, preserving the same uncertainty rating in the value itself). See
"Synthesizing Speech" below for this release's two production synthesis models.

## Natural Language Audio Tags

This release also ships the closed, fixed Natural Language Audio Tag vocabulary and its
model-independent Layer 1 parser from the `SynthesisSubsystem`
(`DemaConsulting.Speech.SynthesisSubsystem`), together with the Layer 2 rendering and streaming
synthesis pipeline described below. Inline bracket tags let you request expressive delivery
without SSML:

| Kind | Tags |
| --- | --- |
| Emotion | `[excited]`/`[excitedly]`, `[serious]`, `[sarcastic]` |
| Emotion | `[panicked]`/`[shocked]`, `[bored]`/`[tired]`, `[sad]`/`[crying]` |
| Pace | `[slow]`, `[very slow]`, `[fast]`, `[very fast]` |
| Pause | `[short pause]`, `[long pause]` |
| Emphasis | `[emphasis]` |
| Delivery/Volume | `[whispers]`/`[whispering]`, `[soft]`, `[loud]`/`[shouting]`/`[screams]`, `[breathy]` |
| Non-verbal | `[laughs]`/`[laughing]`/`[giggles]`, `[sighs]`/`[sigh]` |
| Non-verbal | `[gasp]`/`[inhale]`, `[clears throat]`/`[cough]`, `[snorts]` |

```csharp
using DemaConsulting.Speech.SynthesisSubsystem;

var spans = AudioTagParser.Parse("I can't believe it! [excited] That's amazing!");

foreach (var span in spans)
{
    Console.WriteLine(span.Kind == TaggedTextSpanKind.Tag
        ? $"tag: {span.Tag}"
        : $"text: {span.Text}");
}
```

Tag matching is case-insensitive and tolerant of extra internal whitespace (`[very   slow]`
resolves the same as `[very slow]`). Any bracket text this vocabulary does not recognize — a
typo, an unsupported phrase, or an unbalanced bracket — passes through unchanged as ordinary
narration rather than being dropped or rejected, so a reply is never worse than plain text.

## Synthesizing Speech

`SpeechSynthesizerFactory.Create(...)` composes an `ISpeechSynthesizer` over an installed
`ISynthesisModel` and a playback device, mirroring `SpeechRecognizerFactory`'s
nothing-throws-at-composition contract: it never throws for an ordinary machine state, and
instead returns a synthesizer reporting `IsAvailable == false` for a model that is not installed,
a machine with no speakers, or a missing speech-engine native runtime.

```csharp
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

// 1. The catalog is the library's only "what models exist" entry point - nothing below names a
//    concrete model class, so new models added in a future release show up automatically.
using var catalog = new SpeechModelCatalog();

// 2. Pick a synthesis model. Any model with the synthesis role will do - this is the idiomatic
//    pattern for an app that just wants "a" text-to-speech model:
var descriptor = catalog.Enumerate().First(d => d.Role == SpeechModelRole.Synthesis);
// To pick a *specific* model when more than one of the same role is installed, match on name
// instead: catalog.Enumerate().First(d => d.DisplayName.Contains("Kokoro"));
var model = (ISynthesisModel)descriptor.Model;

// 3. Ensure the chosen model is downloaded before first use. Safe to call unconditionally on
//    every launch - it's a cheap no-op once installed.
await catalog.DownloadAsync(model.Id);

// 4. Create a playback device matching the model's own preferred audio format hint.
var playbackDevice = new AudioDeviceFactory().CreatePlaybackDevice(
    AudioDeviceSelection.SystemDefault,
    model.PreferredAudioFormat);

// 5. Compose the synthesizer and speak.
using var synthesizer = SpeechSynthesizerFactory.Create(model, catalog, playbackDevice);

if (synthesizer.IsAvailable)
{
    await synthesizer.SpeakAsync("Welcome. [short pause] Let's get started!");
}
```

For a model that declares a `ChoiceParameter` or `NumericParameter` for voice/speaker selection
(such as `SherpaOnnxKokoroEnglishSynthesisModel`'s `voice` parameter, or
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`'s numeric `speaker` parameter), pass a
`parameterValues` bag keyed by each declared parameter's `Id` to `Create(...)` to select a
non-default value:

```csharp
using var synthesizer = SpeechSynthesizerFactory.Create(
    model,
    catalog,
    playbackDevice,
    parameterValues: new Dictionary<string, object> { ["voice"] = "bm_george" });
```

A `null` (or omitted) `parameterValues` bag - the previous behavior - resolves to every declared
parameter's own default value, including whichever speaker/voice a model's `ResolveSpeakerId`
hook treats as its default. A supplied key naming a parameter the model does not declare is
silently ignored (composition still succeeds, with only an `Info`-level diagnostic reported if a
sink is wired up) so one settings dictionary stays reusable across different models, but a
supplied value for a parameter the model *does* declare that fails that parameter's own
validation (wrong CLR type, out of range, a fractional value for a whole-number-only parameter,
or a string matching no declared `ChoiceParameterOption`) throws `ArgumentException` synchronously
from `Create()` naming the parameter, the model, and the reason the value is invalid -
`SpeechRecognizerFactory.Create`'s `parameterValues` argument follows the same rule.
`model.PreferredAudioFormat` is likewise only a best-effort playback
hint: after construction, the synthesizer always treats the loaded engine's actual `SampleRate`
as authoritative and resamples whenever needed. This is a session-level choice: it is
independent of, and does not disturb, the existing per-segment Natural Language Audio Tag
speed/volume overrides described
above, which continue to apply per rendered segment regardless of which voice is selected. The
demo's Text-to-Speech panel exposes this same choice as a combo box in its embedded Model
Settings panel for any model that declares a `ChoiceParameter`.

Under the hood, `SpeakAsync` runs the text through the same normalize/tag-parse steps as above,
then a Layer 2 rendering step decides, tag by tag, how to realize each `TaggedTextSpan` against
the selected model's own declared audio-tag support (`IModelCapabilityProfile`):

- **Native** support passes a tag through unchanged as its canonical bracket text, letting a
  model that understands the vocabulary directly consume it as a control token.
- **Parameter-mapped** support approximates a pace or delivery/volume tag as a numeric parameter
  override on the surrounding narration (for example, `[fast]` maps to a speed multiplier), and
  silently strips a tag with no such convention.
- **None** strips every tag except pauses to plain narration, so speech is never worse than
  reading the underlying text aloud.
- A pause tag (`[short pause]`/`[long pause]`) always renders as a real timed silence segment
  regardless of a model's declared support, since honoring a pause requires no model cooperation.

The rendered result is an ordered `SpeechPlan` of `SpeechSegment`s, which a sentence/clause-sized
chunker further splits so that synthesis and playback can pipeline: an earlier chunk plays on the
playback device while a later chunk is still being synthesized, rather than waiting for an entire
utterance's inference to finish before any sound is heard. Calling `Stop()` cancels an in-flight
`SpeakAsync` call deterministically (for example, in response to a user interruption) and is a
safe no-op when nothing is speaking. A synthesis-engine fault or a playback device that drops
mid-utterance fails the awaited `SpeakAsync` task honestly rather than hanging or crashing the
process.

This release ships two production synthesis models (each also reports its license
programmatically via `ISpeechModel.LicenseName`/`LicenseUrl`, without requiring you to parse
this prose or `DisplayName`):

- **`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`** (`vits-piper-en_US-libritts_r-medium`, a
  VITS/Piper voice fine-tuned on the LibriTTS-R corpus, 904 declared speakers, 22,050 Hz,
  licensed **CC BY 4.0** — an attribution license: crediting the LibriTTS-R dataset
  (`http://www.openslr.org/141/`) and the Piper text-to-speech project is required if you
  redistribute the model or audio generated by it, but downstream relicensing under different
  terms is not otherwise restricted, unlike a ShareAlike/CC BY-SA license). All 904 speakers are
  selectable through `ISpeechSynthesizer` by plain numeric index (`0`-`903`) via this model's
  declared `NumericParameter` named `speaker`: LibriTTS-R's speaker embeddings have no published
  human-readable name mapping, so speakers are identified only by their numeric id, unlike the
  named voices below.
- **`SherpaOnnxKokoroEnglishSynthesisModel`** (`kokoro-int8-en-v0_19`, an English-only,
  int8-quantized, StyleTTS2-derived text-to-speech model, 24,000 Hz, licensed **Apache-2.0**,
  confirmed directly from the archive's own `LICENSE` file). Like the model above, **voice
  selection is fully wired**, but through named voices rather than a plain numeric index: this
  model declares an 11-value `ChoiceParameter` (`af`, `af_bella`,
  `af_nicole`, `af_sarah`, `af_sky`, `am_adam`, `am_michael`, `bf_emma`, `bf_isabella`,
  `bm_george`, `bm_lewis`), each mapping through its own `ResolveSpeakerId` hook to sherpa-onnx's
  real integer speaker id for that voice, and each voice is genuinely distinct in accent, gender,
  or vocal character - not merely a numeric id with no audible difference. Kokoro has no discrete
  emotion parameter (no happy/sad/excited control); expressiveness is limited to voice choice
  plus the existing speed/length-scale and Natural Language Audio Tag mechanisms described above.

Both models are registered in `SpeechModelCatalog.KnownModels`. Neither model's bytes are bundled
with this library; `SpeechModelCatalog.DownloadAsync` fetches each one directly from its own
official GitHub Releases URL only when you explicitly request it.

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

// 1. Compose the per-user model store/catalog and the audio devices. None of this throws for an
//    ordinary machine state - a missing microphone, missing speakers, or a missing native
//    runtime all degrade to an honest "unavailable" device/recognizer/synthesizer instead.
using var catalog = new SpeechModelCatalog();
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

// 4. Compose the recognizer and synthesizer over the resolved models and devices.
using var recognizer = SpeechRecognizerFactory.Create(
    recognitionModel,
    catalog,
    captureDevice);

using var synthesizer = SpeechSynthesizerFactory.Create(
    synthesisModel,
    catalog,
    playbackDevice);

if (!recognizer.IsAvailable || !synthesizer.IsAvailable)
{
    Console.WriteLine("No microphone/speakers (or the models failed to load) - exiting.");
    return;
}

// 5. Speak a greeting, then listen and echo back each final result until Enter is pressed.
await synthesizer.SpeakAsync("Hello! [short pause] Say something and I will repeat it back.");

recognizer.ResultReceived += async (_, args) =>
{
    if (!args.Result.IsFinal)
    {
        return;
    }

    Console.WriteLine($"You said: {args.Result.Text}");
    await synthesizer.SpeakAsync($"You said: {args.Result.Text}");
};

recognizer.Start();
Console.WriteLine("Listening - press Enter to stop.");
Console.ReadLine();
recognizer.Stop();
```

This example deliberately keeps error handling minimal for readability; a production application
should also subscribe an `ISpeechDiagnostics` sink (see "Current Capabilities" above) to observe
composition fallbacks and runtime faults instead of relying solely on `IsAvailable` checks.

## Demo Application

The repository includes an Avalonia desktop demo application, `DemaConsulting.Speech.Demo`, which
uses only the library's public API. It is not published to NuGet; run it from a clone of the
repository:

```bash
dotnet run --project src/DemaConsulting.Speech.Demo
```

The application opens with four panels, one of which embeds a fifth:

- **Audio Devices**: lists the capture and playback devices reported by `AudioDeviceFactory`'s
  probes, preselects the first device in each direction, and offers a refresh button so a headset
  connected after start-up appears without restarting the application. Each picker reports how
  many devices were found; when a direction reports none, it explains that the audio backend may
  be unavailable or no device of that kind is connected, rather than showing a blank list.
- **Model Catalog**: lists the models `SpeechModelCatalog` knows about, each with its
  installation state (not downloaded, downloading, installed, or failed or corrupt), and offers a
  download action that reports progress and reports failure with an explanation.
- **Text-to-Speech**: lists installed synthesis models, offers a text box with inline hints
  showing a few of the library's Natural Language Audio Tags (such as `[whispers]`,
  `[short pause]`, and `[excited]`), and Play/Stop controls that compose an `ISpeechSynthesizer`
  through a demo-owned seam over `SpeechSynthesizerFactory`. Playback status reflects
  synthesizing, playing, idle, or an honest error; the panel explains itself if no synthesis
  model is installed or no playback device is available. A Refresh button lets a user re-check
  installed models on demand, and the panel also refreshes itself automatically the moment a
  synthesis model finishes downloading from the Model Catalog panel, with no manual click or
  application restart needed. The model picker and its Model Settings controls disable while
  audio is synthesizing or playing, so a voice/speaker cannot be changed mid-playback.
- **Speech-to-Text**: offers Start/Stop streaming transcription that composes an
  `ISpeechRecognizer` through a demo-owned seam over `SpeechRecognizerFactory`. Committed final
  results accumulate in order while a trailing partial line updates live as the recognizer
  refines it. The panel explains itself if no recognition model is installed or no capture
  device is available. A Refresh button lets a user re-check installed models on demand, and the
  panel also refreshes itself automatically the moment a recognition model finishes downloading
  from the Model Catalog panel, with no manual click or application restart needed. The model
  picker disables while transcription is actively listening, so a model cannot be switched
  mid-session.
- **Model Settings** (embedded in the Text-to-Speech and Speech-to-Text panels): renders whichever
  numeric, choice, and boolean parameters the currently selected model declares, as generic
  sliders/numeric up-downs, combo boxes, and checkboxes bound two-way to the parameter's current
  value. The demo contains no per-model or per-parameter code; presenter selection is driven
  entirely by the library's `ISpeechModelParameter` concrete type. For the Text-to-Speech panel,
  the current value bag (including a selected voice, for a model such as
  `SherpaOnnxKokoroEnglishSynthesisModel` that declares one) is genuinely forwarded to
  `SpeechSynthesizerFactory.Create(...)` on Play - selecting a different voice in the dropdown
  audibly changes the synthesized speech.

### Diagnostics: Raw Capture Recording (Temporary)

The Speech-to-Text panel supports an opt-in, temporary diagnostic aid for investigating
live-microphone streaming issues: set the `DEMASPEECH_CAPTURE_DEBUG_DIR` environment variable to
a directory path before launching the demo, and each Start/Stop listening session writes the raw
microphone audio (exactly as delivered by the capture device, before the recognizer's own
resampling) to a new timestamped `capture-{yyyyMMdd-HHmmss}-{suffix}.wav` file in that
directory. The console prints the exact path written for each session. Leave the variable unset
for normal use - this has no effect and no overhead when disabled.

The model catalog now ships four real, production models (see "Recognizing Speech" and
"Synthesizing Speech" above), so the Model Catalog, Text-to-Speech, and Speech-to-Text panels
populate rows for all four once the library composes, each with its own installation state
and download action. A future model needing a bespoke, non-generic UI beyond the generic Model
Settings surface remains out of scope for this pass.

The demo is verified at the view-model level. Its window, layout, and interactive behavior are
not covered by automated tests, because CI runners have no display; visual verification of the
application is a manual/local activity.

## SpeechCli

`speech-cli` is a cross-platform .NET global tool, packaged as `DemaConsulting.Speech.Cli`, that
exposes the library's model management, audio device inspection, text-to-speech, and
speech-to-text capabilities directly from the command line - useful for scripting, CI smoke
checks, or trying a model without writing any code. It is a sibling application to the library,
not a wrapper around the demo; it is separately packaged and versioned.

### Installing SpeechCli

```bash
dotnet tool install -g DemaConsulting.Speech.Cli
```

The tool targets .NET 10 and bundles the native inference runtime for `win-x64`, `linux-x64`,
and `osx-arm64` (the current `macos-latest` architecture) only, keeping the package a reasonable
size instead of shipping every platform's native binaries.

Once installed, the `speech-cli` command is available on the `PATH`. Run `speech-cli doctor` any
time to check that the native audio backend, native inference runtime, and model store are all in
a healthy state on the current machine.

### Command Overview

| Command | Purpose |
| --- | --- |
| `list-models` | List known models, optionally filtered by role or download state |
| `model-info <modelId>` | Show full detail for one known model |
| `download <modelId> [<modelId>...]` | Download one or more models into the local model store |
| `uninstall <modelId>` | Remove a downloaded model's files, keeping its catalog entry |
| `clean <modelId>` | Best-effort remove leftover partial-install artifacts for a model (not a full uninstall) |
| `list-devices` | List capture and/or playback audio devices |
| `devices test` | Play or record a short test tone/clip on a chosen device |
| `doctor` | Report overall environment health (audio, native runtimes, model store) |
| `speak` | Synthesize text to a real playback device or a WAV file |
| `recognize` | Recognize speech from a WAV file or the microphone |
| `ask` | Speak a prompt, then listen for the reply |

Every command's exact flags are shown by `speech-cli --help`, which always reflects the installed
version - run it locally rather than relying solely on the summary above, which is illustrative
only.

### Worked Examples

Download a recognition model, then recognize speech from a WAV file:

```bash
speech-cli download streaming-zipformer-en-2023-06-26
speech-cli recognize --stt-model streaming-zipformer-en-2023-06-26 --input meeting.wav
```

Speak text to a WAV file, without needing a playback device:

```bash
speech-cli download vits-piper-en_US-libritts_r-medium
speech-cli speak --tts-model vits-piper-en_US-libritts_r-medium --text "Hello there." --output-audio hello.wav
```

Speak text through a real playback device, selecting a specific device by name (see
`list-devices` for the exact names available on the current machine):

```bash
speech-cli speak --tts-model vits-piper-en_US-libritts_r-medium --text "Hello there." --playback-device "Speakers (Realtek)"
```

Recognize speech live from the microphone, stopping automatically after five seconds of silence
(and giving up to ten seconds to start speaking):

```bash
speech-cli recognize --stt-model streaming-zipformer-en-2023-06-26 --mic --silence-timeout 5 --start-timeout 10
```

Speak a prompt and listen for the reply in one invocation, giving up to 20 seconds to start
speaking and ending capture after 1.5 seconds of silence - the intended pattern for an AI agent
holding a two-way voice conversation with a person through this CLI:

```bash
speech-cli ask --tts-model vits-piper-en_US-libritts_r-medium --stt-model streaming-zipformer-en-2023-06-26 \
  --text "Do you want me to continue?" --start-timeout 20 --silence-timeout 1.5
```

## Hardware Verification Boundary

Automated tests in this repository verify only the seam-driven logic that is safe for CI:
preferred-host selection, name-based device matching, fallback to the host API's default device,
and degradation to unavailable devices or `AudioDeviceUnavailableException` when PortAudio
initialization or stream opening fails.

Actual end-to-end hardware I/O still requires manual/local verification on a machine with a real
microphone or speaker. CI cannot honestly claim coverage for opening a physical device and moving
audio through it. The same applies to recognition accuracy and synthesized speech quality: the
recognition and synthesis pipelines are each verified against a deterministic test engine, so
proving that real speech becomes correct text - or that rendered text becomes intelligible speech

- through a real model is also a manual/local activity.

## Where to Look for More Detail

- `docs/design/introduction.md` and the per-subsystem design documents under
  `docs/design/speech/` describe the current software structure, interfaces, and design
  rationale in full.
- The XML documentation comments on `ISpeechDiagnostics`, `AudioDeviceFactory`, and the other
  public types provide member-level API detail via IntelliSense or a generated API reference.

# References

- [REF-1] Continuous Compliance Methodology (<https://github.com/demaconsulting/ContinuousCompliance>)
