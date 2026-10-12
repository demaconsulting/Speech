## Microsoft.ML.OnnxRuntime Verification

This document provides the verification evidence for the Microsoft.ML.OnnxRuntime OTS software
item. Requirements for this OTS item are defined in the Microsoft.ML.OnnxRuntime OTS Software
Requirements document.

### Required Functionality

Microsoft.ML.OnnxRuntime loads and runs `.onnx` graphs for the SpeechOnnx family of libraries,
including the CPU execution provider that the shared `OnnxExecutionProviderSelector` falls back to
and the typed tensor input/output binding the Chatterbox Turbo and Nemotron network wrappers use.

### Verification Approach

Automated verification uses tiny graphs built in memory by the test projects, so every scenario
runs without model downloads, a GPU, or network access:

- The execution-provider selector creates a working CPU session when no provider is requested and
  when a requested provider is unknown or fails its validation probe
- The Chatterbox Turbo network wrapper binds named and positional tensor outputs and reuses a
  key/value cache across steps over real `InferenceSession` instances
- The Nemotron recurrent network wrapper runs encoder and joint steps over real sessions

Automated tests do **not** claim proof of GPU execution providers or of the production model
graphs; they prove the session-creation, fallback, and tensor-binding behavior the libraries rely
on.

### Test Scenarios

#### Create_NoPreferredProviders_CreatesWorkingCpuSession

**Scenario**: `OnnxExecutionProviderSelector.Create` is called with no preferred providers.

**Expected**: A usable CPU `InferenceSession` is returned.

**Requirement coverage**: `Speech-OTS-MicrosoftMlOnnxRuntime-CpuInference`.

#### Create_UnknownProviderName_FallsBackToCpuSession

**Scenario**: A provider name ONNX Runtime does not recognize is requested.

**Expected**: The selector falls back to a working CPU session.

**Requirement coverage**: `Speech-OTS-MicrosoftMlOnnxRuntime-ProviderFallback`.

#### Create_ValidateSessionProbeThrowsOnnxRuntimeException_DisposesCandidateAndFallsBackToCpuWithoutProbingFallback

**Scenario**: The validation probe for a candidate provider throws `OnnxRuntimeException`.

**Expected**: The candidate session is disposed and the selector falls back to CPU.

**Requirement coverage**: `Speech-OTS-MicrosoftMlOnnxRuntime-ProviderFallback`.

#### OnnxChatterboxTurboNetworks_Encode_NamedOutputs_ReturnsConditioning

**Scenario**: The Chatterbox Turbo network wrapper runs an in-memory graph with named outputs.

**Expected**: The tensor outputs are bound by name into the conditioning result.

**Requirement coverage**: `Speech-OTS-MicrosoftMlOnnxRuntime-TensorBinding`.

#### OnnxChatterboxTurboNetworks_Step_TwoSteps_ReusesCacheAndReturnsLastLogits

**Scenario**: Two consecutive language-model steps run over a real session.

**Expected**: The cache tensors from the first step feed the second and the last logits return.

**Requirement coverage**: `Speech-OTS-MicrosoftMlOnnxRuntime-TensorBinding`.

#### Encode_SecondChunk_UsesPreviousCaches

**Scenario**: The Nemotron encoder runs two chunks over a real session.

**Expected**: The second chunk consumes the caches produced by the first.

**Requirement coverage**: `Speech-OTS-MicrosoftMlOnnxRuntime-TensorBinding`.

### Requirements Coverage

- **`Speech-OTS-MicrosoftMlOnnxRuntime-CpuInference`**:
  `Create_NoPreferredProviders_CreatesWorkingCpuSession`
- **`Speech-OTS-MicrosoftMlOnnxRuntime-ProviderFallback`**:
  `Create_UnknownProviderName_FallsBackToCpuSession`,
  `Create_ValidateSessionProbeThrowsOnnxRuntimeException_DisposesCandidateAndFallsBackToCpuWithoutProbingFallback`
- **`Speech-OTS-MicrosoftMlOnnxRuntime-TensorBinding`**:
  `OnnxChatterboxTurboNetworks_Encode_NamedOutputs_ReturnsConditioning`,
  `OnnxChatterboxTurboNetworks_Step_TwoSteps_ReusesCacheAndReturnsLastLogits`,
  `Encode_SecondChunk_UsesPreviousCaches`
