# Installation

Prerequisites: a .NET SDK that targets .NET 8, 9, 10, or 11 (the packages multi-target all four), and
for live audio a machine with a capture or playback device. Without a device or runtime, the
library still composes and reports the affected capability as unavailable.

Install the core library and the sherpa-onnx-backed models package using the .NET CLI:

```bash
dotnet add package DemaConsulting.Speech
dotnet add package DemaConsulting.Speech.Sherpa
```

`DemaConsulting.Speech` is the engine-neutral core library: it ships zero built-in models and no
dependency on any speech-engine package. `DemaConsulting.Speech.Sherpa` is a separate, sibling
package supplying the sherpa-onnx-backed models this guide uses; calling `.AddSherpaModels()`
on a `SpeechModelCatalog` registers them (see "Managing speech models" below). A host that only
needs the catalog/contract seam - for example to ship its own `IRecognitionModel`/
`ISynthesisModel` implementation - can depend on the core package alone.

Four optional packages add raw ONNX Runtime models that do not need sherpa-onnx:

- `DemaConsulting.Speech.Onnx.Kokoro` adds the Kokoro v1.0 synthesis model via `.AddKokoroModels()`.
- `DemaConsulting.Speech.Onnx.Piper` adds the Piper LibriTTS-R English (US, 904 speakers) synthesis
  model via `.AddPiperModels()`.
- `DemaConsulting.Speech.Onnx.NemotronStt` adds the streaming NVIDIA Nemotron 3.5 ASR recognition
  model via `.AddNemotronSttModels()`.
- `DemaConsulting.Speech.Onnx.ChatterboxTurbo` adds the Chatterbox Turbo voice-cloning synthesis
  model via `.AddChatterboxTurboModels(options)`.

Together with the Sherpa package, the library supplies eight models through five model packages
(see "Managing speech models" below).

One further optional package is a preview and ships no speech model: `DemaConsulting.Speech.Text.En` supplies an English
text analyzer (part of speech, normalization, sentence boundaries and heteronym readings) that a model package can
return from `ISynthesisModel.TextAnalyzer`; see "English Text Analysis". The package is a pre-release and is published
only in pre-release versions (for example `1.0.0-beta.1`, never a stable `1.0.0`): it depends on the pre-release
`DemaConsulting.PartOfSpeech` tagger, and NuGet does not allow a stable package to depend on a pre-release one. Install
it with a pre-release version, for example `dotnet add package DemaConsulting.Speech.Text.En --prerelease`. Stable
versions of the packages that do not depend on a preview package are unaffected. This changes once the tagger ships a
stable release.

`DemaConsulting.Speech.Phonemes.En` is a second preview package that ships no speech model: it turns English text (with
the annotations of `DemaConsulting.Speech.Text.En`) into the phoneme input of a voice, from the embedded CMU
Pronouncing Dictionary, a trained letter-to-sound model for unknown words, and renderers for the Piper and Kokoro
phoneme symbols. The Kokoro and Piper packages depend on it and on `DemaConsulting.Speech.Text.En`, so referencing
either of them restores both; a host does not install them separately. Like `DemaConsulting.Speech.Text.En` it is
published only in pre-release versions, and so are the Kokoro and Piper packages that depend on it.

The core package references `PortAudioSharp2`, whose transitive native runtime packages cover
`win-x64`, `linux-x64`, `linux-aarch64`, `osx-x64`, and `osx-arm64`. No `win-arm64`
PortAudio runtime package is available through that dependency chain.

`DemaConsulting.Speech.Sherpa` references the managed `org.k2fsa.sherpa.onnx` speech-engine
package. It never references a per-RID `org.k2fsa.sherpa.onnx.runtime.*` package directly; the
managed package declares those as its own dependencies, so your application restores the native
runtime for its target RID transitively. If that runtime is absent, speech recognition reports
itself unavailable rather than failing.

## Combining the raw ONNX packages with sherpa-onnx

`DemaConsulting.Speech.Onnx.Kokoro`, `DemaConsulting.Speech.Onnx.Piper`,
`DemaConsulting.Speech.Onnx.NemotronStt` and `DemaConsulting.Speech.Onnx.ChatterboxTurbo` use
Microsoft's `Microsoft.ML.OnnxRuntime` (they require 1.28.0 or newer, the lowest release they are built and
tested against, so you may choose a version). The sherpa-onnx native runtime packages ship their own native
ONNX Runtime library (1.28.2 for sherpa-onnx 1.13.8) with the same file name, so an application that
references one of these packages together with `DemaConsulting.Speech.Sherpa` would restore both copies
into the same `runtimes/<rid>/native/` folder, and only one can be loaded. Two rules decide which one has to
stay:

- On Linux, sherpa-onnx's native library is linked against the versioned symbols of its own ONNX Runtime
  (`VERS_1.28.2`), so sherpa's copy must be the one that stays: with Microsoft's copy in its place (any
  version, including 1.28.0) sherpa fails to load with "version 'VERS_1.28.2' not found".
- Microsoft's managed API requires a native library at least as new as itself (an older one fails with
  "The requested API version [31] is not available"), so with sherpa's 1.28.2 the managed API must not be
  newer than 1.28.

Reference `Microsoft.ML.OnnxRuntime` directly at `1.28.0` with `ExcludeAssets="native"`:

```xml
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.28.0" ExcludeAssets="native" />
```

That leaves sherpa's native library as the only one in the output on every platform. The `speech-cli` tool and
the Demo application do exactly this and run both model families on `win-x64` and `linux-x64` (verified on
both). With a newer sherpa-onnx, keep the managed version equal to the ONNX Runtime release that its native
runtime package bundles. An application that does not use sherpa-onnx needs no such pin: the raw ONNX packages
then bring Microsoft's managed API and native runtime together, and for an accelerated provider (for example
DirectML or CUDA) it references the matching `Microsoft.ML.OnnxRuntime.*` package instead of the CPU one.
