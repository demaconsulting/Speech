<!-- cspell:ignore Kaldi -->
## SherpaOnnx Verification

This document provides the verification evidence for the SherpaOnnx (`org.k2fsa.sherpa.onnx`) OTS
software item. Requirements for this OTS item are defined in the SherpaOnnx OTS Software
Requirements document.

### Required Functionality

SherpaOnnx provides the managed streaming speech-recognition API used by the internal
`SherpaOnnxRecognitionEngine` adapter, and the managed configuration types each recognition model
populates to describe its own architecture, file locations, and required input sample rate.

### Verification Approach

Automated verification for this OTS item is intentionally limited to deterministic integration
behaviors that require neither a downloaded speech model nor the platform-specific native
inference binary:

- A recognition model can populate and expose a sherpa-onnx recognizer configuration resolved
  against its installed-files directory, and can declare the input sample rate that configuration
  carries
- The recognition composition path consumes that model-owned configuration through the library's
  own engine seam, and degrades honestly when loading the engine fails

Automated tests do **not** claim proof of real speech-to-text accuracy or of successful native
inference. Loading a real model through the native runtime and recognizing real speech requires
manual/local verification on a machine with a downloaded model and a supported platform runtime.

### Test Scenarios

#### IRecognitionModel_CreateEngineConfig_InstalledDirectory_ResolvesPathsAndSampleRate

**Scenario**: A recognition model builds its sherpa-onnx recognizer configuration against an
installed-files directory.

**Expected**: The configuration carries the model's declared feature sample rate and file paths
resolved against that directory, proving the repository can construct the managed configuration
types correctly.

**Requirement coverage**: `Speech-OTS-SherpaOnnx-ManagedStreamingApi`.

#### IRecognitionModel_AudioFormat_DeclaredByModel_IsExposed

**Scenario**: A recognition model's declared engine input format is read.

**Expected**: The declared mono `AudioFormat` is returned, proving the per-model input-format
declaration the streaming API requires is available to the recognition pipeline.

**Requirement coverage**: `Speech-OTS-SherpaOnnx-ManagedStreamingApi`.

#### SpeechRecognizerFactory_Create_ModelInstalledAndDeviceAvailable_ReturnsRealRecognizer

**Scenario**: Recognition is composed for an installed model and an available capture device.

**Expected**: The model's own configuration and declared `AudioFormat.SampleRate` are requested
and used to load an engine through the library's seam, proving the model-owned configuration
pattern works end to end.

**Requirement coverage**: `Speech-OTS-SherpaOnnx-ModelOwnedConfiguration`.

#### SpeechRecognizerFactory_Create_EngineLoadFails_ReturnsUnavailableRecognizerAndDoesNotThrow

**Scenario**: Loading the speech-inference engine fails, as it would on a machine whose native
runtime is absent.

**Expected**: Composition returns the honest unavailable recognizer and reports the reason,
without throwing, proving the native-runtime boundary degrades as architecture.md requires.

**Requirement coverage**: `Speech-OTS-SherpaOnnx-ModelOwnedConfiguration`.

### Requirements Coverage

- **`Speech-OTS-SherpaOnnx-ManagedStreamingApi`**:
  `IRecognitionModel_CreateEngineConfig_InstalledDirectory_ResolvesPathsAndSampleRate`,
  `IRecognitionModel_AudioFormat_DeclaredByModel_IsExposed`
- **`Speech-OTS-SherpaOnnx-ModelOwnedConfiguration`**:
  `SpeechRecognizerFactory_Create_ModelInstalledAndDeviceAvailable_ReturnsRealRecognizer`,
  `SpeechRecognizerFactory_Create_EngineLoadFails_ReturnsUnavailableRecognizerAndDoesNotThrow`
