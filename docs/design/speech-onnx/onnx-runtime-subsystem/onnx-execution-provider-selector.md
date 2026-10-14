### OnnxExecutionProviderSelector

**Purpose**: Build an ONNX Runtime `InferenceSession`, opportunistically preferring an accelerated
execution provider when one is actually available at runtime, and always falling back to the
built-in CPU provider otherwise. This is the only unit in the SpeechOnnx package, and the one
piece of infrastructure every sibling raw-ONNX model package needs in order to construct a loaded
model session without each duplicating its own provider-fallback logic.

**Data Model**: No instance state - a stateless static class. `DefaultProviderNames` is a
read-only `IReadOnlyList<string>`, empty (CPU-only by default).

**Key Methods**:

- **DefaultProviderNames**: The default candidate execution provider names to try before falling
  back to CPU. Deliberately empty, matching the "start simple" design decision for the raw-ONNX
  model family; callers that want acceleration pass their own list (for example
  `"CUDAExecutionProvider"` or `"DmlExecutionProvider"`) to `Create` without any other change to
  this class.
- **Create(modelPath, preferredProviderNames, validateSession)**: Creates an `InferenceSession`
  for the `.onnx` model at `modelPath`, trying each of `preferredProviderNames` (or
  `DefaultProviderNames` when `null`) in order before falling back to the CPU provider. For each
  candidate provider name, the method constructs a fresh `SessionOptions`, appends the named
  provider to it, and attempts `new InferenceSession(modelPath, options)` inside a `try` block; if
  that succeeds and `validateSession` was supplied, it is invoked on the freshly constructed
  session before the candidate is accepted - a probe that lets a provider which loads and
  constructs successfully but fails on the actual graph at `Run()` time (observed with DirectML
  and Kokoro's `ConvTranspose` operator) be caught here instead of surfacing from a caller's first
  real inference. The `SessionOptions` instance is scoped to a `using` statement inside the loop
  body, so it is disposed immediately after that iteration's construction attempt completes -
  whether the attempt threw or succeeded. The `InferenceSession` itself is disposed in a `finally`
  block guarded by a `succeeded` flag set immediately before `return`, so it is disposed on every
  path except the one that returns it to the caller - covering not only the two expected "try next
  candidate" exceptions below but any other exception type a candidate's construction or probe
  might throw, which is never silently swallowed: it propagates to the caller once that
  candidate's session has been disposed. A candidate that throws `OnnxRuntimeException` or
  `DllNotFoundException` from either construction or the probe is abandoned and the next candidate
  is tried with its own fresh `SessionOptions`, so one candidate's failed native-library lookup or
  failed probe can never leave a prior candidate's partially-applied configuration behind. The CPU
  fallback is never probed - it is trusted unconditionally. If every preferred candidate fails,
  the method returns `new InferenceSession(modelPath)` with default `SessionOptions` - the CPU
  provider ships inside the base package and needs no explicit append - which is guaranteed to
  succeed for a well-formed model.

**Error Handling**: Exception contract (identical to the system API table and data flow): only a
provider's _unavailability_ is swallowed into the CPU fallback.

- A null or empty `modelPath` throws `ArgumentException` (`ArgumentNullException` for null,
  naming `modelPath`), validated via `ArgumentException.ThrowIfNullOrEmpty` before any provider is
  attempted.
- An unavailable or non-functional accelerated provider never causes a throw: the CPU provider is
  always attempted last and is guaranteed to succeed for a well-formed model.
- A missing, unreadable, or malformed model file throws `OnnxRuntimeException` from that final CPU
  attempt, exactly as a direct `new InferenceSession(modelPath)` call would (a preferred
  candidate's `OnnxRuntimeException` is only "provider unavailable"; the genuine model error is
  re-raised by the CPU attempt); that exception propagates unchanged to the caller.
- An exception thrown by `validateSession` of a type other than
  `OnnxRuntimeException`/`DllNotFoundException` propagates to the caller unchanged (after the
  probed candidate's session is disposed), rather than being treated as "try the next candidate" -
  a probe is expected to signal "this provider cannot run this model" only via those two types,
  matching session-construction's own failure modes.

**Test Hook**: `OnCandidateOptionsCreated` is an `internal static` test-only callback (visible to
the test project through `InternalsVisibleTo`) invoked with each candidate's `SessionOptions`. As
process-wide static state it is only used by tests that run in a serialized xUnit collection and
install it through a disposable scope that restores the previous value.

**Dependencies**: The `Microsoft.ML.OnnxRuntime` managed API (`InferenceSession`,
`SessionOptions`, `OnnxRuntimeException`); no other unit, subsystem, or shared package.

**Callers**: Any ONNX-Runtime-backed model package's own engine-construction code (for example a
model's `CreateBackend` implementation in the sibling `DemaConsulting.Speech.Onnx.Kokoro`
package), each supplying its own `.onnx` model path, its own ordered list of preferred accelerated
provider names, and optionally a probe delegate exercising a representative inference. The
`DemaConsulting.Speech.Onnx.Kokoro` package's `OnnxKokoroEnglishSynthesisModel.CreateBackend` and
the `tools/KokoroOnnxBenchmark` development tool both supply
`OnnxKokoroSynthesisEngine.RunProbeInference` as this probe.
