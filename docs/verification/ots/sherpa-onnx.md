<!-- cspell:ignore Kaldi -->
## SherpaOnnx Verification

This document provides the verification evidence for the SherpaOnnx (`org.k2fsa.sherpa.onnx`) OTS
software item. Requirements for this OTS item are defined in the SherpaOnnx OTS Software
Requirements document.

### Required Functionality

SherpaOnnx provides the managed streaming speech-recognition API used by the SpeechSherpa
library's internal `SherpaOnnxRecognitionEngine` adapter, and the managed configuration types each
SpeechSherpa recognition model populates to describe its own architecture, file locations, and
required input sample rate. The Speech library itself no longer references this package.

### Verification Approach

Automated verification for this OTS item is intentionally limited to deterministic integration
behaviors that require neither a downloaded speech model nor the platform-specific native
inference binary:

- A SpeechSherpa recognition model can populate a sherpa-onnx recognizer configuration resolved
  against its installed-files directory, and can declare the input sample rate that configuration
  carries; these tests live in `test/DemaConsulting.Speech.Sherpa.Tests`, the test project that
  references the package
- The Speech library's recognition composition path consumes a model-owned backend through its
  own engine-neutral seam, and degrades honestly when loading the engine fails; these tests live
  in `test/DemaConsulting.Speech.Tests` and use a fake model, since the seam names no sherpa-onnx
  type

Where the real native runtime and an installed model are available, the SpeechSherpa
`SherpaOnnxRecognitionEngineTests`/`SherpaOnnxRecognitionEngineAccuracyTests` additionally
exercise real native inference (see _SpeechSherpa RecognitionSubsystem Verification_); they
self-skip when the model is not installed, so they are not relied on as this OTS item's
requirement evidence.

Automated tests do **not** claim proof of real speech-to-text accuracy or of successful native
inference. Loading a real model through the native runtime and recognizing real speech requires
manual/local verification on a machine with a downloaded model and a supported platform runtime.

### Test Scenarios

#### SherpaOnnxZipformerEnRecognitionModel_BuildEngineConfig_ResolvesInt8FilesAndFeatureConfig

**Scenario**: A SpeechSherpa recognition model builds its sherpa-onnx recognizer configuration
against an installed-files directory.

**Expected**: The configuration carries the model's declared feature sample rate and file paths
resolved against that directory, proving the repository can construct the managed configuration
types correctly.

**Requirement coverage**: `Speech-OTS-SherpaOnnx-ManagedStreamingApi`.

#### SherpaOnnxZipformerEnRecognitionModel_AudioFormat_IsMono16000

**Scenario**: A SpeechSherpa recognition model's declared engine input format is read.

**Expected**: The declared mono `AudioFormat` is returned, proving the per-model input-format
declaration the streaming API requires is available to the recognition pipeline.

**Requirement coverage**: `Speech-OTS-SherpaOnnx-ManagedStreamingApi`.

#### SpeechRecognizerFactory_LoadAsync_ModelInstalled_ReturnsRealEngine

**Scenario**: Recognition is composed for an installed model and an available capture device.

**Expected**: The model's own backend is requested with its declared `AudioFormat.SampleRate` and
used to load an engine through the library's seam, proving the model-owned configuration pattern
works end to end without the Speech library naming any sherpa-onnx type.

**Requirement coverage**: `Speech-OTS-SherpaOnnx-ModelOwnedConfiguration`.

#### SpeechRecognizerFactory_LoadAsync_EngineLoadFails_ReturnsUnavailableEngineAndDoesNotFaultTask

**Scenario**: Loading the speech-inference engine fails, as it would on a machine whose native
runtime is absent.

**Expected**: Composition returns the honest unavailable engine and reports the reason,
without throwing, proving the native-runtime boundary degrades in the required honest way.

**Requirement coverage**: `Speech-OTS-SherpaOnnx-ModelOwnedConfiguration`.

### Requirements Coverage

- **`Speech-OTS-SherpaOnnx-ManagedStreamingApi`**:
  `SherpaOnnxZipformerEnRecognitionModel_BuildEngineConfig_ResolvesInt8FilesAndFeatureConfig`,
  `SherpaOnnxZipformerEnRecognitionModel_AudioFormat_IsMono16000`
- **`Speech-OTS-SherpaOnnx-ModelOwnedConfiguration`**:
  `SpeechRecognizerFactory_LoadAsync_ModelInstalled_ReturnsRealEngine`,
  `SpeechRecognizerFactory_LoadAsync_EngineLoadFails_ReturnsUnavailableEngineAndDoesNotFaultTask`
