# SpeechOnnx System Design

This document provides the system-level design for the SpeechOnnx library.

![SpeechOnnx Structure](SpeechOnnxView.svg)

## Architecture

SpeechOnnx is a thin, dependency-free .NET library, published as the `DemaConsulting.Speech.Onnx`
NuGet package, that supplies shared ONNX Runtime infrastructure reusable by any ONNX-Runtime-backed
model package - today, execution-provider probing and selection. It is a sibling system to Speech
rather than a subsystem of it, for the same reasons SpeechSherpa is: it is a separately built,
separately packaged software item with its own dependencies, and the Speech library must never
depend on it. Unlike SpeechSherpa, this package's one shipped unit also does not depend on Speech
at all: `OnnxExecutionProviderSelector` references only the `Microsoft.ML.OnnxRuntime` package and
names no Speech type, since it is purely an ONNX Runtime helper with no knowledge of Speech's
model/catalog contracts. (The project file still carries a `ProjectReference` to the Speech
library, left over for the currently-empty `SynthesisSubsystem` folder this pass does not cover -
see the Scope note below.) SpeechOnnx declares zero models itself; each concrete model family (for
example the sibling `DemaConsulting.Speech.Onnx.Kokoro` package) references this package and adds
its own `AddXxxModels()` extension method and model classes.

The library consists of one subsystem:

- **OnnxRuntimeSubsystem**: `OnnxExecutionProviderSelector`, a static helper that builds an ONNX
  Runtime `InferenceSession`, trying each of a caller-supplied ordered list of accelerated
  execution provider names before falling back to the always-available CPU provider - see
  _SpeechOnnx OnnxRuntimeSubsystem Design_

**Scope note**: the project also has an empty `SynthesisSubsystem` folder with no source files.
It is out of scope for this design document - it has no implementation to describe - and is
documented only once it ships a unit.

## External Interfaces

SpeechOnnx exposes one small public API surface: the `OnnxExecutionProviderSelector` static class
and its `DefaultProviderNames`/`Create` members.

| Interface | Direction | Format | Constraints |
| --- | --- | --- | --- |
| `DefaultProviderNames` | Outbound | Public static property | Read-only; empty (CPU-only Stage 1) |
| `Create(modelPath, providerNames)` | Inbound/Outbound | Static method | Never throws for a missing provider |
| ONNX Runtime managed API | Outbound | Method call/return | Accelerated provider needs its own runtime from consumer |

## Dependencies

SpeechOnnx has one NuGet dependency:

- **Microsoft.ML.OnnxRuntime** supplies the managed ONNX Runtime API this package's one unit
  builds `InferenceSession`/`SessionOptions` instances against, including the CPU execution
  provider built into the base package. This package deliberately references only the base
  package, never an accelerated provider package such as `Microsoft.ML.OnnxRuntime.Gpu` or
  `Microsoft.ML.OnnxRuntime.DirectML`: a consuming application that wants accelerated inference
  adds that provider-specific package itself for its own target RID(s), and
  `OnnxExecutionProviderSelector` attempts to append the matching provider purely by name through
  ONNX Runtime's generic, dynamically-loaded `SessionOptions.AppendExecutionProvider(string,
  IReadOnlyDictionary<string,string>?)` overload - this mirrors the sibling SpeechSherpa system's
  "consuming application brings its own native runtime package" convention.

See _OTS Integration Design_ and _Microsoft.ML.OnnxRuntime Design_ for details.

## Risk Control Measures

N/A - SpeechOnnx provides no safety-critical functionality requiring risk control measures
(IEC 62304 §5.3.3). It supplies generic ONNX Runtime session-construction infrastructure with no
clinical or safety role.

## Data Flow

**Provider probing/selection path:**

1. **Input**: A model package's `CreateBackend` implementation calls
   `OnnxExecutionProviderSelector.Create(modelPath, preferredProviderNames)` with the absolute
   path of its `.onnx` model file and its own ordered list of preferred accelerated provider
   names (or `null` to use the empty `DefaultProviderNames`)
2. **Probing**: for each preferred provider name in order, the method constructs a fresh
   `SessionOptions`, appends the named provider, and attempts to construct an `InferenceSession`
   from it; a candidate that throws `OnnxRuntimeException` or `DllNotFoundException` (its native
   shared library is unavailable in this process) is abandoned and the next candidate is tried
3. **Disposal**: every candidate's `SessionOptions` is disposed immediately after its own
   construction attempt, whether that attempt succeeds or fails, so a failed candidate never
   leaves a native handle behind and never carries configuration into the next candidate
4. **Fallback**: if every preferred candidate fails, the method constructs an `InferenceSession`
   with default `SessionOptions` (no explicit provider), which always uses the CPU provider
   shipped inside the base package and is guaranteed to succeed for a well-formed model
5. **Output**: the first successfully loaded `InferenceSession` is returned to the caller, which
   owns its disposal; the method itself never disposes the session it returns

## Design Constraints

- **No accelerated provider package reference**: this library references only the base
  `Microsoft.ML.OnnxRuntime` package; accelerated providers are resolved dynamically by name at
  runtime, never at compile time
- **Per-candidate resource ownership**: every candidate `SessionOptions` is owned and disposed by
  `OnnxExecutionProviderSelector.Create` itself - win or lose - without disposing the
  `InferenceSession` it returns on success, which remains the caller's responsibility
- **Never fails for an unavailable accelerated provider**: the CPU fallback candidate is always
  attempted last and always succeeds for a well-formed model, so this unit's own logic never
  turns a missing accelerated runtime into an exception
- **Declares zero models**: this package owns no model identity, download descriptor, or catalog
  registration; that responsibility belongs entirely to each sibling model package
- **Compliance**: all functionality must be traceable to requirements
- **Quality**: zero warnings, full targeted tests, complete documentation

### Platform Support

| Target Framework | Runtime / Environment |
| --- | --- |
| `net8.0` | .NET 8 LTS |
| `net9.0` | .NET 9 |
| `net10.0` | .NET 10 |

The library runs wherever the base `Microsoft.ML.OnnxRuntime` package's CPU provider is available

- effectively anywhere the targeted .NET runtime itself runs. Accelerated providers require the
consuming application to additionally supply the matching native runtime for its own target RID;
their absence degrades silently to the CPU fallback rather than preventing the library from
working at all.

### Integration Patterns

- **NuGet Packaging**: published as a separate package, referenced transitively by a concrete
  model package rather than directly by a host application
- **Static helper, no composition root**: `OnnxExecutionProviderSelector` is a stateless static
  class; a model package calls it directly from its own `CreateBackend`/engine-construction code,
  with no dependency-injection or catalog registration step of its own
