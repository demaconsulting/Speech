# SpeechOnnxChatterboxTurbo System Design

This document provides the system-level design for the SpeechOnnxChatterboxTurbo library.

![SpeechOnnxChatterboxTurbo Structure](SpeechOnnxChatterboxTurboView.svg)

## Architecture

SpeechOnnxChatterboxTurbo is a .NET library, published as the
`DemaConsulting.Speech.Onnx.ChatterboxTurbo` NuGet package, that supplies a concrete,
ONNX-Runtime-backed text-to-speech model and inference engine for the `ResembleAI/chatterbox-turbo-ONNX`
export implementing the Speech library's `ISynthesisModel`/`ISynthesisBackend` contracts. It is a
sibling system to Speech, like SpeechOnnxKokoro: a separately built, separately packaged software
item with its own dependencies, and the Speech library must never depend on it. The package's name
carries the `ChatterboxTurbo` suffix so that a separate package for the original Chatterbox model
family can be added later without a naming clash.

Chatterbox Turbo is a voice-cloning model: it has no built-in voices and instead conditions
generation on a short reference clip. This package therefore accepts caller-supplied reference
voices through its options; it bundles no voice audio and the model store carries no voice
downloads. The model is distributed as four ONNX graphs (speech encoder, token embedder, language
model, conditional decoder), each with an external-data file, which this package runs directly
through ONNX Runtime on the CPU. The package owns its text tokenizer (an embedded byte-level BPE
vocabulary), its autoregressive generation loop, and its audio-tag rendering policy.

The library consists of two subsystems:

- **ModelManagementSubsystem**: the `AddChatterboxTurboModels()` catalog extension method, the
  `OnnxChatterboxTurboEnglishSynthesisModel` concrete `ISynthesisModel`, the model-family-neutral
  `ByteLevelBpeTokenizer`, and the `ChatterboxTurboReferenceVoice` type - see _SpeechOnnxChatterboxTurbo
  ModelManagementSubsystem Design_
- **SynthesisSubsystem**: `OnnxChatterboxTurboSynthesisEngine`, `OnnxChatterboxTurboNetworks`,
  `ChatterboxTurboTokenSelector` and `ChatterboxTurboCapabilityProfile` - see _SpeechOnnxChatterboxTurbo
  SynthesisSubsystem Design_

## External Interfaces

SpeechOnnxChatterboxTurbo exposes the catalog extension method, the model class with its options,
and the reference-voice type. It implements Speech's public `ISynthesisModel` and
`ISynthesisBackend` seams and behaves as an ordinary external consumer of Speech's public API.

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| `AddChatterboxTurboModels()` | Inbound | Extension method | Throws for a null catalog |
| `OnnxChatterboxTurboEnglishSynthesisModel` | Inbound | Public class | Holds options and voices |
| `ChatterboxTurboReferenceVoice.FromSamples/FromFile` | Inbound | Static factory | 16-bit PCM WAV or samples |
| `ISynthesisModel.CreateBackend(...)` | Inbound/Outbound | Public method call | Throws when unusable |
| `ISynthesisBackend` | Inbound | Public interface | Not thread-safe |
| `SpeechModelCatalog.AddModels(...)` | Outbound | Method call/return | Builder-phase only |
| Microsoft.ML.OnnxRuntime managed API | Outbound | Method call/return | CPU provider only |

## Dependencies

SpeechOnnxChatterboxTurbo depends on the Speech library (project reference) and on
**Microsoft.ML.OnnxRuntime** (NuGet), which runs the four graphs. Unlike SpeechOnnxKokoro it does
not use the SpeechOnnx execution-provider selector: it is CPU-only. See _OTS Integration Design_ and
_Microsoft.ML.OnnxRuntime Design_.

## Risk Control Measures

N/A - SpeechOnnxChatterboxTurbo provides no safety-critical functionality requiring risk control
measures (IEC 62304 §5.3.3). It supplies a speech-synthesis model and backend with no clinical or
safety role.

## Data Flow

**Model registration path:** a host calls `AddChatterboxTurboModels(options)` on a
`SpeechModelCatalog`; the method registers the one model instance and returns the same catalog.

**Model install path:** Speech's downloader fetches and checksum-verifies the files of the selected
precision variants (eight by default) directly into the install directory; no extraction step is
needed.

**Backend construction path:** `CreateBackend` verifies the files exist, opens the four ONNX
sessions, constructs the tokenizer and engine, and returns the engine; failures dispose any
already-opened sessions before rethrowing.

**Synthesis path:**

1. Speech's session renders audio tags through the package's capability profile and passes one
   text segment, speed and speaker id to `Generate`
2. The engine tokenizes the text, embeds the ids, and prefixes the selected voice's conditioning
3. The language model is stepped autoregressively with a repetition-penalized greedy selector until
   the stop token or a bound proportional to the text length
4. The conditional decoder converts the prompt and speech tokens into a 24 kHz waveform, which is
   returned to Speech as `EngineAudio`

## Design Constraints

- **Speech must never depend on SpeechOnnxChatterboxTurbo**: the dependency is one-way
- **No bundled audio or weights**: voices are caller-supplied and model files are downloaded on the
  host's explicit action
- **CPU only**: the model is run on the ONNX Runtime CPU provider; no accelerated provider is
  referenced
- **Bounded generation**: generation never loops unboundedly and never throws for hitting a bound
- **Compliance**: all functionality must be traceable to requirements

### Platform Support

| Target Framework | Runtime / Environment |
| --- | --- |
| `net8.0` | .NET 8 LTS |
| `net9.0` | .NET 9 |
| `net10.0` | .NET 10 |

### Integration Patterns

- **NuGet Packaging**: published as a separate package that depends on the Speech package
- **Extension-method registration**: `new SpeechModelCatalog().AddChatterboxTurboModels(options)`
- **Model-owned backend construction**: the model constructs its own backend
