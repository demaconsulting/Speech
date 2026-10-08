# SpeechSherpa System Design

This document provides the system-level design for the SpeechSherpa library.

![SpeechSherpa Structure](SpeechSherpaView.svg)

## Architecture

SpeechSherpa is a .NET library, published as the `DemaConsulting.Speech.Sherpa` NuGet package,
that supplies concrete, sherpa-onnx-backed speech models and inference backends for the Speech
library. It is a sibling system to Speech rather than a subsystem of it, because it is a
separately built, separately packaged software item with its own dependencies, and because the
Speech library must never depend on it. Unlike the other sibling systems, SpeechSherpa both
consumes Speech and extends it: it references the Speech library and implements its
`IRecognitionModel`/`ISynthesisModel` extension seam, so a host that wants these models adds this
package alongside Speech and registers them with one call.

The library consists of three subsystems, each named after the Speech subsystem it extends:

- **ModelManagementSubsystem**: the `AddSherpaModels()` catalog extension method, the four
  concrete sherpa-onnx models (two recognition models and two synthesis models) implementing
  Speech's `IRecognitionModel`/`ISynthesisModel` contract, and the shared `.tar.bz2`
  archive-extraction and uppercase-transcript-restoration helpers they use — see _SpeechSherpa
  ModelManagementSubsystem Design_
- **RecognitionSubsystem**: `SherpaOnnxRecognitionEngine`, the real implementation of Speech's
  internal `IRecognitionBackend` seam over sherpa-onnx's streaming recognizer — see _SpeechSherpa
  RecognitionSubsystem Design_
- **SynthesisSubsystem**: `SherpaOnnxSynthesisEngine`, the real implementation of Speech's
  internal `ISynthesisBackend` seam over sherpa-onnx's offline text-to-speech API — see
  _SpeechSherpa SynthesisSubsystem Design_

Every unit in this system previously lived inside the Speech library itself. They were moved
here, unchanged in behavior, so the Speech library carries no speech-inference engine dependency
and names no sherpa-onnx type, even internally.

## External Interfaces

SpeechSherpa exposes a small public API — the catalog extension method and the four model
classes — and implements Speech's internal backend and model seams, which it can reach because
the Speech library grants this assembly `InternalsVisibleTo`.

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| `SpeechModelCatalogSherpaExtensions.AddSherpaModels()` | Inbound | Extension method | Throws only for a null catalog |
| `SherpaOnnxZipformerEnRecognitionModel` | Inbound | Public class | Stateless model declaration |
| `SherpaOnnxNemotronStreamingEnRecognitionModel` | Inbound | Public class | Stateless model declaration |
| `SherpaOnnxVitsLibriTtsEnglishSynthesisModel` | Inbound | Public class | Stateless model declaration |
| `SherpaOnnxKokoroEnglishSynthesisModel` | Inbound | Public class | Stateless model declaration |
| `IRecognitionModel.CreateBackend(...)` | Inbound/Outbound | Internal method call | Throws when unusable |
| `ISynthesisModel.CreateBackend(...)` | Inbound/Outbound | Internal method call | Throws when unusable |
| `IRecognitionBackend` | Inbound | Internal interface | Not thread-safe; one caller at a time |
| `ISynthesisBackend` | Inbound | Internal interface | Not thread-safe; one caller at a time |
| `SpeechModelCatalog.AddModels(...)` | Outbound | Method call/return | Consumed; builder-phase only |
| sherpa-onnx managed API | Outbound | Method call/return | Requires a consumer-supplied native runtime |
| SharpCompress reader API | Outbound | Method call/return | Consumed during model install only |

## Dependencies

SpeechSherpa has one project dependency — the Speech library — and the following NuGet
dependencies:

- **SherpaOnnx** (`org.k2fsa.sherpa.onnx`) supplies the managed local speech-inference API for
  both streaming recognition and offline text-to-speech. Per the decision not to bundle native
  speech runtimes, this library references only the managed package; the consuming application
  declares the `org.k2fsa.sherpa.onnx.runtime.{RID}` package for each RID it intends to run on
- **SharpCompress** supplies the managed BZip2-compressed tar (`.tar.bz2`) archive reader that
  `TarBz2ArchiveExtractor` uses to unpack every model's declared archive payload after checksum
  verification, since the BCL has no BZip2 decoder

See _OTS Integration Design_, _SherpaOnnx Design_, and _SharpCompress Design_ for details.

## Risk Control Measures

N/A - SpeechSherpa provides no safety-critical functionality requiring risk control measures
(IEC 62304 §5.3.3). It supplies speech models and inference backends with no clinical or safety
role.

## Data Flow

**Model registration path:**

1. **Input**: A host constructs a Speech `SpeechModelCatalog` and calls `AddSherpaModels()` on it
2. **Registration**: The extension method passes the Zipformer, Nemotron, VITS, and Kokoro models
   to `SpeechModelCatalog.AddModels(...)`, in that order
3. **Output**: The same catalog instance is returned and now enumerates the four models alongside
   their install state

**Model install path:**

1. **Input**: Speech's `SpeechModelDownloader` has fetched and checksum-verified a model's
   declared `.tar.bz2` archive into a staging directory
2. **Extraction**: The model's `InstallAsync` delegates to `TarBz2ArchiveExtractor`, which unpacks
   every archive entry in place and then deletes the archive
3. **Output**: The staging directory holds the model's own top-level folder, which Speech
   atomically swaps into the installed location

**Backend construction path:**

1. **Input**: Speech's `SpeechRecognizerFactory` or `SpeechSynthesizerFactory` loads an engine for
   an installed model through its model-driven default backend factory
2. **Configuration**: The model's `CreateBackend` builds its own sherpa-onnx configuration from
   the installed directory through its internal `BuildEngineConfig`
3. **Construction**: The model constructs `SherpaOnnxRecognitionEngine` or
   `SherpaOnnxSynthesisEngine`, loading the model into native memory
4. **Output**: The engine is returned to Speech as an engine-neutral backend; a load failure is
   thrown back to Speech's factory, which degrades it to an honest unavailable engine

**Recognition path:**

1. **Input**: Speech's `RecognitionSession` pushes resampled mono audio into
   `SherpaOnnxRecognitionEngine.AcceptSamples`
2. **Decoding**: `TryDecode` decodes buffered audio and reports provisional or final results;
   for a model that opts in, buffered pre-endpoint audio is silently replayed after an endpoint
3. **Normalization**: Speech passes each result's text through the model's `NormalizeText`, which
   for the Zipformer model restores casing, contractions, and punctuation
4. **Output**: Speech surfaces the normalized result to its consumer; at session end `TryFlush`
   recovers trailing words and `Reset` recreates the stream

**Synthesis path:**

1. **Input**: Speech's `SynthesisSession` passes one rendered text chunk, speed, and the
   model-resolved speaker id to `SherpaOnnxSynthesisEngine.Generate`
2. **Inference**: The engine calls sherpa-onnx's offline text-to-speech
3. **Output**: Samples and their sample rate are returned to Speech as `EngineAudio`, which
   Speech resamples to the playback device's format when needed

## Design Constraints

- **Speech must never depend on SpeechSherpa**: The dependency direction is one-way. Speech
  carries no `ProjectReference` and no sherpa-onnx or SharpCompress package, and names no type
  from this system, so it remains independently publishable and consumable with other model
  providers
- **Sherpa-onnx types stay inside this system**: Every sherpa-onnx type is confined to the
  models' own `BuildEngineConfig`/`CreateBackend` implementations and the two engines; the Speech
  library sees only its own `IRecognitionBackend`/`ISynthesisBackend` interfaces
- **No bundled native runtime**: This library references only the managed sherpa-onnx package.
  A machine or publish target whose native runtime is absent composes successfully and reports
  recognition or synthesis as unavailable through Speech's honest fallback
- **Metadata and download descriptors only**: No model weights are bundled or redistributed; a
  consumer fetches each model through Speech's download machinery on their own explicit action,
  and each model visibly declares its own license
- **Unchanged behavior across the move**: Every unit moved from the Speech library keeps its
  original behavior and namespace; only its assembly changed
- **Compliance**: All functionality must be traceable to requirements
- **Quality**: Zero warnings, full targeted tests, complete documentation

### Platform Support

The library targets the same frameworks as the Speech library:

| Target Framework | Runtime / Environment |
| --- | --- |
| `net8.0` | .NET 8 LTS |
| `net9.0` | .NET 9 |
| `net10.0` | .NET 10 |

The library runs wherever the Speech library and a sherpa-onnx native runtime package for the
current RID are available. Other RIDs compose successfully and report speech features as
unavailable.

### Integration Patterns

- **NuGet Packaging**: Published as a separate package that depends on the Speech package
- **Extension-method registration**: A host opts in with `new SpeechModelCatalog().AddSherpaModels()`
- **Model-owned backend construction**: Each model constructs its own backend, so the Speech
  library's default backend factories need no knowledge of this system
