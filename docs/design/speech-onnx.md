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
| `Create(modelPath, providerNames, validateSession)` | Inbound/Outbound | Static method | Never throws |
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
   `OnnxExecutionProviderSelector.Create(modelPath, preferredProviderNames, validateSession)` with
   the absolute path of its `.onnx` model file, its own ordered list of preferred accelerated
   provider names (or `null` to use the empty `DefaultProviderNames`), and optionally a probe
   delegate that exercises a representative inference on the candidate session
2. **Probing**: for each preferred provider name in order, the method constructs a fresh
   `SessionOptions`, appends the named provider, and attempts to construct an `InferenceSession`
   from it; when `validateSession` is supplied, it is then invoked on that freshly constructed
   session, so a provider that loads and constructs successfully but fails on the actual graph at
   `Run()` time (observed with DirectML and Kokoro's `ConvTranspose` operator) is caught here
   rather than surfacing later from a caller's first real inference. A candidate that throws
   `OnnxRuntimeException` or `DllNotFoundException` from either construction or the probe (its
   native shared library is unavailable in this process, or the probe inference itself failed)
   is abandoned and the next candidate is tried. The CPU fallback candidate is never probed - it
   is trusted unconditionally, matching this class's existing guarantee that CPU always succeeds
   for a well-formed model
3. **Disposal**: every candidate's `SessionOptions` is disposed immediately after its own
   construction attempt, whether that attempt succeeds or fails, so a failed candidate never
   leaves a native handle behind and never carries configuration into the next candidate. Each
   candidate's `InferenceSession` is likewise disposed on every path except the one that returns
   it to the caller: a `try`/`finally` with a success flag covers the two expected "try next
   candidate" exceptions above, and also any other exception type a candidate's construction or
   probe might throw - a session is never leaked, and an unexpected exception type is never
   silently swallowed, propagating to the caller after that candidate's session is disposed
4. **Fallback**: if every preferred candidate fails, the method constructs an `InferenceSession`
   with default `SessionOptions` (no explicit provider), which always uses the CPU provider
   shipped inside the base package and is guaranteed to succeed for a well-formed model
5. **Output**: the first successfully loaded (and, if a probe was supplied, successfully probed)
   `InferenceSession` is returned to the caller, which owns its disposal; the method itself never
   disposes the session it returns

## Design Constraints

- **No accelerated provider package reference**: this library references only the base
  `Microsoft.ML.OnnxRuntime` package; accelerated providers are resolved dynamically by name at
  runtime, never at compile time
- **Per-candidate resource ownership**: every candidate `SessionOptions` is owned and disposed by
  `OnnxExecutionProviderSelector.Create` itself - win or lose - without disposing the
  `InferenceSession` it returns on success, which remains the caller's responsibility. Each
  candidate's `InferenceSession` is likewise disposed on every non-winning path, including an
  unexpected exception type from a caller-supplied probe
- **Never fails for an unavailable or non-functional accelerated provider**: the CPU fallback
  candidate is always attempted last and always succeeds for a well-formed model, so this unit's
  own logic never turns a missing or construction-only-functional accelerated runtime into an
  exception; a caller-supplied `validateSession` probe is what catches a provider that loads and
  constructs a session but fails on the actual graph at `Run()` time
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
