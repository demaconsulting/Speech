### RecognitionBackend

#### Verification Approach

The backend seam (`IRecognitionBackend`, `IRecognitionBackendFactory`,
`DefaultRecognitionBackendFactory`) is verified by replacing it with a deterministic fake that
records the mono samples it was fed and yields a scripted sequence of results, so no downloaded
model and no platform-specific native speech-inference binary is ever needed. The model-owned
configuration is verified by loading an installed model through `SpeechRecognizerFactory` and by
asking an `IRecognitionModel` for its backend from an installed directory.

The real, native-backed implementation, `SherpaOnnxRecognitionEngine`, is not part of the Speech
library; it is verified in the sibling SpeechSherpa library (see _SpeechSherpa RecognitionSubsystem
Verification_).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Dependencies**: No external services, no downloaded model, no native runtime, and no audio
  hardware
- **Test doubles**: Fake `IRecognitionBackend`/`IRecognitionBackendFactory` implementations

#### Acceptance Criteria

The unit is considered verified when a mocked backend drives a session through start and result
delivery, and when a model supplies its own backend configuration so the factory returns a real
engine for an installed model.

#### Test Scenarios

##### The seam is mockable

**Test**: `RecognitionSession_StartAsync_FromCreated_TransitionsToRunning`

**Test**: `RecognitionSession_FrameCaptured_FinalResult_AppliesModelNormalizeTextWithIsFinalTrue`

##### The model owns backend configuration

**Test**: `SpeechRecognizerFactory_LoadAsync_ModelInstalled_ReturnsRealEngine`

**Test**: `IRecognitionModel_CreateBackend_InstalledDirectory_ReturnsRecognitionBackend`
