### OnnxExecutionProviderSelector

**Purpose**: Build an ONNX Runtime `InferenceSession`, opportunistically preferring an accelerated
execution provider when one is actually available at runtime, and always falling back to the
built-in CPU provider otherwise. This is the only unit in the SpeechOnnx package, and the one
piece of infrastructure every sibling raw-ONNX model package needs in order to construct a loaded
model session without each duplicating its own provider-fallback logic.

**Data Model**: No instance state - a stateless static class. `DefaultProviderNames` is a
read-only `IReadOnlyList<string>`, currently empty (Stage 1 is CPU-only).

**Key Methods**:

- **DefaultProviderNames**: The default, Stage-1 candidate execution provider names to try before
  falling back to CPU. Deliberately empty, matching the "start simple" design decision for the
  raw-ONNX model family; a later pass can populate it (for example with
  `"CUDAExecutionProvider"` or `"DmlExecutionProvider"`) without changing any other code in this
  class.
- **Create(modelPath, preferredProviderNames)**: Creates an `InferenceSession` for the `.onnx`
  model at `modelPath`, trying each of `preferredProviderNames` (or `DefaultProviderNames` when
  `null`) in order before falling back to the CPU provider. For each candidate provider name, the
  method constructs a fresh `SessionOptions`, appends the named provider to it, and attempts
  `new InferenceSession(modelPath, options)`. The `SessionOptions` instance is scoped to a `using`
  statement inside the loop body, so it is disposed immediately after that iteration's
  construction attempt completes - whether the attempt threw or succeeded - without the method
  ever disposing the `InferenceSession` it ultimately returns; that session is the caller's own
  responsibility to dispose. A candidate that throws `OnnxRuntimeException` or
  `DllNotFoundException` is abandoned and the next candidate is tried with its own fresh
  `SessionOptions`, so one candidate's failed native-library lookup can never leave a prior
  candidate's partially-applied configuration behind. If every preferred candidate fails, the
  method returns `new InferenceSession(modelPath)` with default `SessionOptions` - the CPU
  provider ships inside the base package and needs no explicit append - which is guaranteed to
  succeed for a well-formed model.

**Error Handling**: Throws `ArgumentException` when `modelPath` is null or empty, validated via
`ArgumentException.ThrowIfNullOrEmpty` before any provider is attempted. Never throws due to an
unavailable accelerated provider: the CPU provider is always attempted last and is guaranteed to
succeed for a well-formed model. A genuinely malformed or unreadable model file still throws from
that final CPU attempt, exactly as a direct `new InferenceSession(modelPath)` call would; that
exception propagates unchanged to the caller.

**Dependencies**: The `Microsoft.ML.OnnxRuntime` managed API (`InferenceSession`,
`SessionOptions`, `OnnxRuntimeException`); no other unit, subsystem, or shared package.

**Callers**: Any ONNX-Runtime-backed model package's own engine-construction code (for example a
model's `CreateBackend` implementation in the sibling `DemaConsulting.Speech.Onnx.Kokoro`
package), each supplying its own `.onnx` model path and its own ordered list of preferred
accelerated provider names.
