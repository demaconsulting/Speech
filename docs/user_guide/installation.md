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

Three optional packages add raw ONNX Runtime models that do not need sherpa-onnx:

- `DemaConsulting.Speech.Onnx.Kokoro` adds the Kokoro v1.0 synthesis model via `.AddKokoroModels()`.
- `DemaConsulting.Speech.Onnx.NemotronStt` adds the streaming NVIDIA Nemotron 3.5 ASR recognition
  model via `.AddNemotronSttModels()`.
- `DemaConsulting.Speech.Onnx.ChatterboxTurbo` adds the Chatterbox Turbo voice-cloning synthesis
  model via `.AddChatterboxTurboModels(options)`.

Together with the Sherpa package, the library supplies seven models through four model packages
(see "Managing speech models" below).

One further optional package is a preview and ships no speech model: `DemaConsulting.Speech.Text.En` supplies an
English text analyzer (part-of-speech and normalization annotations, sentence boundaries and the reading of
heteronyms such as "record", chosen by a small trained model, about 250 KB, that is embedded in the package) that a
model package can return from `ISynthesisModel.TextAnalyzer`; see "Text
analysis for model authors" in the usage guide. The package is a pre-release and is published only in
pre-release versions (for example `1.0.0-beta.1`, never a stable `1.0.0`): it depends on the pre-release
`DemaConsulting.PartOfSpeech` tagger (pinned to 0.1.0-beta.5 or later, which clears the working buffers in
which it keeps analyzed text, so the core `ITextAnalyzer` no-retention contract is met), and NuGet does not allow
a stable package to depend on a pre-release one. Install it with a pre-release version, for example
`dotnet add package DemaConsulting.Speech.Text.En --prerelease`. Stable versions of the other packages are
unaffected. This changes once the tagger ships a stable release.

The core package references `PortAudioSharp2`, whose transitive native runtime packages cover
`win-x64`, `linux-x64`, `linux-aarch64`, `osx-x64`, and `osx-arm64`. No `win-arm64`
PortAudio runtime package is available through that dependency chain.

`DemaConsulting.Speech.Sherpa` references the managed `org.k2fsa.sherpa.onnx` speech-engine
package. It never references a per-RID `org.k2fsa.sherpa.onnx.runtime.*` package directly; the
managed package declares those as its own dependencies, so your application restores the native
runtime for its target RID transitively. If that runtime is absent, speech recognition reports
itself unavailable rather than failing.

## Combining the raw ONNX packages with sherpa-onnx

`DemaConsulting.Speech.Onnx.Kokoro`, `DemaConsulting.Speech.Onnx.NemotronStt` and
`DemaConsulting.Speech.Onnx.ChatterboxTurbo` use Microsoft's `Microsoft.ML.OnnxRuntime`. The
sherpa-onnx native runtime packages ship their own native ONNX Runtime library with the same file
name, so an application that references one of these packages together with
`DemaConsulting.Speech.Sherpa` restores both copies into the same `runtimes/<rid>/native/` folder.
Microsoft's copy can overwrite sherpa's and break sherpa-onnx on Linux. In such an application, add
a direct reference so only sherpa's native runtime is used (the managed APIs run against it):

```xml
<PackageReference Include="Microsoft.ML.OnnxRuntime" Version="1.31.1" ExcludeAssets="native" />
```

The sherpa-onnx 1.13.8 native library is ONNX Runtime 1.28.2, while the managed API pinned by the raw
ONNX packages is 1.31.1. Microsoft's managed API requires a native library at least as new as itself,
so this override is expected to fail to initialize against an older native library: it only works once
the sherpa-onnx native runtime is at least the pinned managed version (check the version of the
sherpa-onnx native package you restore). An application that uses only the raw
ONNX packages needs no override and gets Microsoft's native runtime transitively. To use an
accelerated provider (for example DirectML or CUDA), reference the matching
`Microsoft.ML.OnnxRuntime.*` package in a host that does not also ship sherpa-onnx's runtime. The
`speech-cli` tool and the Demo application already apply this override.
