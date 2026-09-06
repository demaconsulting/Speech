<!-- cspell:ignore ALSA portaudio -->
# Speech

[![GitHub forks][badge-forks]][link-forks]
[![GitHub stars][badge-stars]][link-stars]
[![GitHub contributors][badge-contributors]][link-contributors]
[![License][badge-license]][link-license]
[![Build][badge-build]][link-build]
[![Quality Gate][badge-quality]][link-quality]
[![Security][badge-security]][link-security]
[![NuGet][badge-nuget]][link-nuget]

DemaConsulting.Speech is a cross-platform .NET library providing local, offline speech-to-text
(STT) and text-to-speech (TTS) services for desktop applications.

## Features

This pre-release increment now includes a real PortAudio-based audio layer in addition to the
existing diagnostics and contract abstractions:

- **Diagnostics Subsystem**: An `ISpeechDiagnostics` contract for reporting structural
  diagnostic events to an optional host-supplied sink, with a default no-op
  (`NullSpeechDiagnostics`) implementation
- **Real Audio Input/Output**: `AudioDeviceFactory` now composes PortAudio-backed capture and
  playback devices by default when the PortAudio runtime initializes successfully
- **Preferred Host API Selection**: Device enumeration is restricted to one host API per
  platform (`WASAPI` on Windows, `ALSA` on Linux, `CoreAudio` on macOS) to avoid duplicate
  physical-device listings across PortAudio backends
- **Graceful Degradation**: If PortAudio cannot initialize, composition still succeeds and the
  library falls back to honest unavailable devices/probes instead of throwing
- **Persisted Name-Based Selection**: `AudioDeviceSelection` still uses stable device names and
  falls back to the host API's default device if a saved name no longer resolves
- **Streaming Speech Recognition**: `SpeechRecognizerFactory` composes an `ISpeechRecognizer`
  over an installed recognition model and a capture device, converts captured audio to the
  format the model requires, and raises progressive provisional and final text results while
  audio is arriving
- **Casing/Contraction/Punctuation Restoration**: `IRecognitionModel.NormalizeText` lets a
  recognition model post-process its own raw text before it reaches consumers. The shipped
  `SherpaOnnxZipformerEnRecognitionModel` uses this hook, via `UppercaseTranscriptRestorer`, to
  turn its shouted, unpunctuated raw output (for example `I DON'T THINK THAT'S WORKING`) into
  readable prose (`I don't think that's working.`) with a conservative, deterministic
  contraction table, deliberately leaving genuinely ambiguous forms (`were`, `well`, `its`, and
  similar) untouched. `SherpaOnnxNemotronStreamingEnRecognitionModel` already outputs
  properly-cased, punctuated prose and keeps the interface's pass-through default
- **Per-User Model Management**: `SpeechModelStore`, `SpeechModelDownloader`, and
  `SpeechModelCatalog` download, SHA-256 verify, and atomically install speech models into a
  per-user store, and report each known model's install state
- **Natural Language Audio Tag Vocabulary**: `AudioTagParser`/`AudioTagCatalog` recognize a
  closed, fixed set of inline bracket tags (for example `[excited]`, `[whispers]`,
  `[short pause]`) grouped by kind, passing any unrecognized or malformed bracket text through
  as plain narration rather than dropping or rejecting it
- **Streaming Speech Synthesis**: `SpeechSynthesizerFactory` composes an `ISpeechSynthesizer`
  over an installed synthesis model and a playback device, renders Natural Language Audio Tags
  per the model's own declared capability (native control token, approximated pause/parameter,
  or silent strip), chunks narration into sentence/clause-sized pieces, and plays each chunk
  while the next one synthesizes
- **Multi-Platform Support**: Builds and runs on Windows, Linux, and macOS
- **Multi-Runtime Support**: Targets .NET 8, 9, and 10
- **Continuous Compliance**: Compliance evidence is generated automatically on every CI run,
  following the [Continuous Compliance][link-continuous-compliance] methodology

## Installation

Install the library using the .NET CLI:

```bash
dotnet add package DemaConsulting.Speech
```

### PortAudio runtime support

The library references `PortAudioSharp2`, which in turn brings the following native runtime
packages transitively at restore time:

- `org.k2fsa.portaudio.runtime.win-x64`
- `org.k2fsa.portaudio.runtime.linux-x64`
- `org.k2fsa.portaudio.runtime.linux-aarch64`
- `org.k2fsa.portaudio.runtime.osx-x64`
- `org.k2fsa.portaudio.runtime.osx-arm64`

No `win-arm64` PortAudio runtime package is available through this dependency chain as of this
phase. On an unsupported RID, or if PortAudio fails to initialize on a machine, the library
still composes safely but reports audio devices as unavailable.

### Speech engine runtime support

The library references only the managed `org.k2fsa.sherpa.onnx` package and never a per-RID
`org.k2fsa.sherpa.onnx.runtime.*` package directly. The managed package declares those native
runtime packages as its own dependencies, so a consuming application restores the one it needs
for its target RID transitively. If that native runtime is absent - for example because a publish
profile trimmed it, or the target RID is not supported upstream - composition still succeeds and
`SpeechRecognizerFactory` reports the recognizer as unavailable instead of crashing.

Recognition additionally requires an installed `IRecognitionModel`, and synthesis additionally
requires an installed `ISynthesisModel`. This release ships four real, production models,
registered in `SpeechModelCatalog.KnownModels` and downloaded on demand (never bundled with the
library):

- **`SherpaOnnxZipformerEnRecognitionModel`** (`streaming-zipformer-en-2023-06-26`) - a
  streaming Zipformer2 transducer recognition model, Apache-2.0 licensed (LIKELY - corroborated
  via the icefall training toolkit's own license, not independently confirmed against an
  unreachable HuggingFace model card).
- **`SherpaOnnxNemotronStreamingEnRecognitionModel`**
  (`nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25`) - NVIDIA's cache-aware
  FastConformer-RNNT streaming recognition model, released under the **NVIDIA Open Model
  License** - a custom, non-OSI license materially different from the other two models' terms.
  Review that license's field-of-use and redistribution terms yourself before relying on this
  model in your own application.
- **`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`** (`vits-piper-en_US-libritts_r-medium`) - a
  904-speaker VITS/Piper text-to-speech voice trained on LibriTTS-R, licensed **CC BY 4.0** (an
  attribution license: crediting the LibriTTS-R dataset and the Piper project is required for
  redistributed model/audio content, but downstream relicensing is not otherwise restricted).
  All 904 speakers are selectable by plain numeric index (`0`-`903`) through its declared
  `NumericParameter`: LibriTTS-R's speaker embeddings have no published human-readable name
  mapping, so speakers are identified only by their numeric id, either via the demo's
  Text-to-Speech panel slider or `SpeechSynthesizerFactory.Create(...)`'s `parameterValues`
  argument - see "Usage" below.
- **`SherpaOnnxKokoroEnglishSynthesisModel`** (`kokoro-int8-en-v0_19`) - an English-only,
  int8-quantized, StyleTTS2-derived text-to-speech model, licensed **Apache-2.0** (confirmed
  directly from the archive's own `LICENSE` file). Like the VITS/Piper model above, **voice
  selection is fully available**: all 11 of this model's genuinely distinct voices (`af`,
  `af_bella`, `af_nicole`, `af_sarah`, `af_sky`, `am_adam`, `am_michael`, `bf_emma`,
  `bf_isabella`, `bm_george`, `bm_lewis` - differing in accent, gender, and vocal character) can
  be selected through its declared `ChoiceParameter`, either via the demo's Text-to-Speech panel
  dropdown or via `SpeechSynthesizerFactory.Create(...)`'s `parameterValues` argument for library
  consumers not using the demo - see "Usage" below.

None of these four models' bytes are bundled with this library - `SpeechModelCatalog.DownloadAsync`
fetches each one directly from its own official GitHub Releases URL only when explicitly
requested, verifying its SHA-256 checksum before installing it.

## Usage

Typical composition uses `AudioDeviceFactory` and the library-owned device abstractions:

```csharp
using DemaConsulting.Speech.AudioSubsystem;

var factory = new AudioDeviceFactory();
var captureDevices = factory.CaptureProbe.Enumerate();
var captureDevice = factory.CreateCaptureDevice(AudioDeviceSelection.SystemDefault);

if (captureDevice.IsAvailable)
{
    captureDevice.FrameCaptured += (_, args) =>
    {
        // Consume args.Samples (normalized float samples, interleaved by channel)
    };

    captureDevice.Start();
}
```

Streaming speech recognition composes a recognizer over an installed model and that capture
device:

```csharp
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

var store = new SpeechModelStore();
var captureDevice = new AudioDeviceFactory().CreateCaptureDevice();

using var recognizer = SpeechRecognizerFactory.Create(
    model,                                          // your installed IRecognitionModel
    store.GetCurrentDirectory(model.Id),
    captureDevice);

if (recognizer.IsAvailable)
{
    recognizer.ResultReceived += (_, args) =>
    {
        // args.Result.Text is the full text of the current utterance;
        // args.Result.IsFinal distinguishes a committed result from a provisional one.
        Console.WriteLine($"{(args.Result.IsFinal ? "final" : "partial")}: {args.Result.Text}");
    };

    recognizer.Start();
    // ... speak ...
    recognizer.Stop();
}
```

`SpeechRecognizerFactory.Create(...)` never throws for an ordinary machine state: a model that is
not installed, a machine with no microphone, and a missing speech-engine native runtime all
return a recognizer reporting `IsAvailable == false`.

Streaming speech synthesis composes a synthesizer over an installed model and a playback device,
then plays narration text - optionally containing Natural Language Audio Tags - as it streams in:

```csharp
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

var store = new SpeechModelStore();
var playbackDevice = new AudioDeviceFactory().CreatePlaybackDevice();

using var synthesizer = SpeechSynthesizerFactory.Create(
    model,                                          // your installed ISynthesisModel
    store.GetCurrentDirectory(model.Id),
    playbackDevice);

if (synthesizer.IsAvailable)
{
    await synthesizer.SpeakAsync("Welcome. [short pause] Let's get started!");
}
```

For a model that declares tunable parameters - for example
`SherpaOnnxKokoroEnglishSynthesisModel`'s `voice` selection, or
`SherpaOnnxVitsLibriTtsEnglishSynthesisModel`'s numeric `speaker` selection - pass a
`parameterValues` bag keyed by each parameter's `Id` to select a non-default value, such as a
specific voice or speaker index:

```csharp
using var synthesizer = SpeechSynthesizerFactory.Create(
    model,
    store.GetCurrentDirectory(model.Id),
    playbackDevice,
    parameterValues: new Dictionary<string, object> { ["voice"] = "af_bella" });
```

Or, for the numeric `SherpaOnnxVitsLibriTtsEnglishSynthesisModel` speaker parameter:

```csharp
using var synthesizer = SpeechSynthesizerFactory.Create(
    model,
    store.GetCurrentDirectory(model.Id),
    playbackDevice,
    parameterValues: new Dictionary<string, object> { ["speaker"] = 450 });
```

A `null` (or omitted) `parameterValues` bag resolves to every model's own declared default value
for each parameter it declares.

`SpeakAsync` normalizes the text, parses any Natural Language Audio Tags, Layer 2 renders each
tag per the model's own declared capability, chunks the result into sentence/clause-sized pieces,
and pipelines synthesis with playback: an earlier chunk plays while a later chunk is still being
synthesized, rather than waiting for the entire utterance to finish inference before any sound is
heard. Calling `Stop()` cancels an in-flight `SpeakAsync` call deterministically (for example, in
response to a user interruption) and is a safe no-op when nothing is speaking.

`SpeechSynthesizerFactory.Create(...)` never throws for an ordinary machine state: a model that
is not installed, a machine with no speakers, and a missing speech-engine native runtime all
return a synthesizer reporting `IsAvailable == false`.

Automated tests in this repository verify selection logic, preferred-host filtering, fallback to
host-API defaults, and degradation when PortAudio cannot initialize. They do **not** prove true
end-to-end hardware I/O in CI, because CI runners cannot guarantee access to a real microphone or
speaker. Opening a real device and moving audio through it remains a manual/local verification step.
The recognition and synthesis pipelines are likewise verified against deterministic test engines
rather than real speech, so recognition accuracy and synthesized speech quality are manual/local
verification steps too.

## Demo Application

This repository includes an Avalonia desktop demo application that exercises the library through
its public API only. Run it from a clone of this repository:

```bash
dotnet run --project src/DemaConsulting.Speech.Demo
```

The demo currently demonstrates:

- **Audio Devices**: capture and playback device pickers built over `AudioDeviceFactory`'s
  probes, with a refresh button for hot-plugged devices. On a machine with no audio backend the
  pickers explain the absence rather than showing a silently empty list.
- **Model Catalog**: a list of the models `SpeechModelCatalog` knows about, each with its
  installation state, plus a download action that reports progress and honest failure.
- **Text-to-Speech**: a panel that lists installed synthesis models, offers a text box with
  inline hints for a few of the library's Natural Language Audio Tags (such as `[whispers]`,
  `[short pause]`, and `[excited]`), and Play/Stop controls built over `ISpeechSynthesizer`. The
  panel reports its playback status honestly and explains itself if no synthesis model is
  installed or no playback device is available. The model picker and its voice/speaker settings
  lock while audio is playing, so you cannot switch models or parameters mid-playback.
- **Speech-to-Text**: a panel with Start/Stop streaming transcription built over
  `ISpeechRecognizer`, showing committed final results alongside a live-updating partial line.
  The panel explains itself if no recognition model is installed or no capture device is
  available. The model picker locks while listening is active, so you cannot switch models
  mid-session.
- **Model Settings**: embedded in the Text-to-Speech and Speech-to-Text panels, a generic
  settings view that renders whichever numeric, choice, and boolean parameters the selected
  model declares as sliders/numeric up-downs, combo boxes, and checkboxes, with no per-model
  code in the demo.

Note that the model catalog now ships four real models (see "Speech engine runtime support"
above for their identities and licenses). The catalog panel — and, in turn, the Text-to-Speech
and Speech-to-Text panels — populate rows for all four as soon as the library composes, and each
panel's own guidance covers downloading and installing a model before use. Selecting a voice for
a model that declares one (such as Kokoro's 11 voices) is done through the embedded Model
Settings panel's combo box, which is genuinely wired into the Play action.

## Documentation

Generated documentation includes:

- **Build Notes**: Release information and changes
- **User Guide**: Installation and usage guidance
- **Code Quality Report**: CodeQL and SonarCloud analysis results
- **Requirements**: Functional and non-functional requirements
- **Requirements Justifications**: Detailed requirement rationale
- **Trace Matrix**: Requirements-to-test traceability

## Contributing

Contributions are welcome. See [CONTRIBUTING.md][link-contributing] for development setup, coding
standards, and the pull request process.

## License

Copyright (c) DEMA Consulting. Licensed under the MIT License. See [LICENSE][link-license] for
details.

By contributing to this project, you agree that your contributions will be licensed under the MIT License.

<!-- Badge References -->
[badge-forks]: https://img.shields.io/github/forks/demaconsulting/Speech?style=plastic
[badge-stars]: https://img.shields.io/github/stars/demaconsulting/Speech?style=plastic
[badge-contributors]: https://img.shields.io/github/contributors/demaconsulting/Speech?style=plastic
[badge-license]: https://img.shields.io/github/license/demaconsulting/Speech?style=plastic
[badge-build]: https://img.shields.io/github/actions/workflow/status/demaconsulting/Speech/build_on_push.yaml?style=plastic
[badge-quality]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_Speech&metric=alert_status
[badge-security]: https://sonarcloud.io/api/project_badges/measure?project=demaconsulting_Speech&metric=security_rating
[badge-nuget]: https://img.shields.io/nuget/v/DemaConsulting.Speech?style=plastic

<!-- Link References -->
[link-forks]: https://github.com/demaconsulting/Speech/network/members
[link-stars]: https://github.com/demaconsulting/Speech/stargazers
[link-contributors]: https://github.com/demaconsulting/Speech/graphs/contributors
[link-license]: https://github.com/demaconsulting/Speech/blob/main/LICENSE
[link-build]: https://github.com/demaconsulting/Speech/actions/workflows/build_on_push.yaml
[link-quality]: https://sonarcloud.io/dashboard?id=demaconsulting_Speech
[link-security]: https://sonarcloud.io/dashboard?id=demaconsulting_Speech
[link-nuget]: https://www.nuget.org/packages/DemaConsulting.Speech
[link-continuous-compliance]: https://github.com/demaconsulting/ContinuousCompliance
[link-contributing]: https://github.com/demaconsulting/Speech/blob/main/CONTRIBUTING.md
