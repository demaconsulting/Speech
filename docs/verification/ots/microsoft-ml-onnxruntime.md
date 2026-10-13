## Microsoft.ML.OnnxRuntime Verification

This document provides the verification evidence for the Microsoft.ML.OnnxRuntime OTS software
item. Requirements for this OTS item are defined in the Microsoft.ML.OnnxRuntime OTS Software
Requirements document.

### Required Functionality

Microsoft.ML.OnnxRuntime loads and runs `.onnx` graphs for the SpeechOnnx family of libraries,
including the CPU execution provider that the shared `OnnxExecutionProviderSelector` falls back to
and the typed tensor input/output binding the Chatterbox Turbo and Nemotron network wrappers use.

### Verification Approach

Automated verification uses tiny graphs, either built in memory by the test projects or loaded from
a checked-in tiny fixture (`TestData/tiny-identity-model.onnx`, generated offline), so every
scenario runs without model downloads, a GPU, or network access:

- The execution-provider selector creates a working CPU session when no provider is requested and
  when a requested provider is unknown or fails its validation probe
- The Chatterbox Turbo network wrapper binds named and positional tensor outputs and reuses a
  key/value cache across steps over real `InferenceSession` instances
- The Nemotron streaming encoder runs consecutive chunks over a real session and feeds the caches
  of one chunk into the next

The Nemotron joint/decoder steps and the Kokoro engine also use ONNX Runtime but are not given
separate OTS scenarios: they bind tensors through the same `OrtValue` mechanism proven by the
scenarios below, and their behavior is verified by their own unit tests.

Automated tests do **not** claim proof of GPU execution providers or of the production model
graphs; they prove the session-creation, fallback, and tensor-binding behavior the libraries rely
on.

### Test Scenarios

#### OnnxExecutionProviderSelector_Create_NoPreferredProviders_CreatesWorkingCpuSession

**Scenario**: `OnnxExecutionProviderSelector.Create` is called with no preferred providers.

**Expected**: A usable CPU `InferenceSession` is returned, and running its single Identity node on a
float tensor returns that tensor unchanged.

#### OnnxExecutionProviderSelector_Create_UnknownProviderName_FallsBackToCpuSession

**Scenario**: A provider name ONNX Runtime does not recognize is requested.

**Expected**: The selector falls back to a working CPU session. ONNX Runtime's exception is caught
inside the selector, so the `OnnxRuntimeException` itself is evidenced indirectly by the fallback.

#### OnnxExecutionProviderSelector_Create_ValidateSessionProbeThrowsOnnxRuntimeException_DisposesCandidateAndFallsBackToCpuWithoutProbingFallback

**Scenario**: The validation probe for a candidate provider throws `OnnxRuntimeException`.

**Expected**: The candidate session is disposed and the selector falls back to CPU. The exception is
provoked with a wrong-typed input, standing in for a real accelerated-provider probe failure.

#### OnnxChatterboxTurboNetworks_Encode_UnnamedFourOutputs_BindsPositionally

**Scenario**: The Chatterbox Turbo network wrapper runs an in-memory graph whose four outputs are
unnamed.

**Expected**: The tensor outputs are bound by position into the conditioning result.

#### OnnxChatterboxTurboNetworks_Encode_NamedOutputs_ReturnsConditioning

**Scenario**: The Chatterbox Turbo network wrapper runs an in-memory graph with named outputs.

**Expected**: The tensor outputs are bound by name into the conditioning result.

#### OnnxChatterboxTurboNetworks_Step_TwoSteps_ReusesCacheAndReturnsLastLogits

**Scenario**: Two consecutive language-model steps run over a real session, once with a full-size
key/value cache and once with a half-size cache (the test is a theory over both variants).

**Expected**: The cache tensors from the first step feed the second and the last logits return,
for both cache variants.

#### NemotronEncoder_Encode_SecondChunk_UsesPreviousCaches

**Scenario**: The Nemotron encoder runs two chunks over a real session.

**Expected**: The second chunk consumes the caches produced by the first.
