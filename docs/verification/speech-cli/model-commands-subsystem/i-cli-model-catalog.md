### ICliModelCatalog Verification

#### Verification Approach

`ICliModelCatalog` is an interface with no behavior of its own. Its contract is verified through its
production implementation, `SpeechModelCatalogAdapter`, over a real `SpeechModelCatalog` rooted at an
isolated temporary directory, and through the hand-written `FakeCliModelCatalog` that every command
test uses. The engine and audio-format members are verified with fake synthesis and recognition models
(the library's `ISynthesisModel` and `IRecognitionModel` are public interfaces).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- A unique temporary store root per test; no network, speakers, microphone or downloaded model

#### Acceptance Criteria

The contract is verified when a model of the right role yields its preferred audio format and a loaded
engine with no device bound, and a model of the wrong role is refused with an `ArgumentException` that
identifies the model, while a `null` descriptor is an `ArgumentNullException`. The operator-facing
wording of a wrong-role error is verified by the command-level tests of the Synthesis and Recognition
command subsystems, not here.

#### Test Scenarios

##### SpeechModelCatalogAdapter_CreateSynthesizerEngineAsync_WithSynthesisModel_ReturnsEngine

**Scenario**: A fake synthesis model is passed to `CreateSynthesizerEngineAsync`.

**Expected**: A loaded synthesizer engine is returned.

##### SpeechModelCatalogAdapter_GetPreferredAudioFormat_WithSynthesisModel_ReturnsFormat

**Scenario**: A fake synthesis model with a known format is passed to `GetPreferredAudioFormat`.

**Expected**: The model's audio format is returned.

##### SpeechModelCatalogAdapter_GetPreferredAudioFormat_RecognitionRoleModel_ThrowsArgumentException

**Scenario**: A recognition-role model is passed to `GetPreferredAudioFormat`.

**Expected**: An `ArgumentException` with parameter name `descriptor` is thrown and its message contains the
model id.

##### SpeechModelCatalogAdapter_CreateSynthesizerEngineAsync_RecognitionRoleModel_ThrowsArgumentException

**Scenario**: A recognition-role model is passed to `CreateSynthesizerEngineAsync`.

**Expected**: An `ArgumentException` with parameter name `descriptor` is thrown and its message contains the
model id.

##### SpeechModelCatalogAdapter_CreateRecognizerEngineAsync_WithRecognitionModel_ReturnsEngine

**Scenario**: A fake recognition model is passed to `CreateRecognizerEngineAsync`.

**Expected**: A loaded recognizer engine is returned.

##### SpeechModelCatalogAdapter_CreateRecognizerEngineAsync_SynthesisRoleModel_ThrowsArgumentException

**Scenario**: A synthesis-role model is passed to `CreateRecognizerEngineAsync`.

**Expected**: An `ArgumentException` with parameter name `descriptor` is thrown and its message contains the
model id.

##### Enumeration, download refusal, never-installed no-op, voice and pack queries

These requirements (`SpeechCli-ModelCommands-ModelEnumeration`, `-DownloadUnknownModelRefused`,
`-NeverInstalledModelNoOp`, `-VoiceEnumeration` and `-VoicePackStateQuery`) are verified by the
adapter scenarios in `speech-model-catalog-adapter.md`: the compiled-in models enumerate, an unknown id
is refused, a never-installed model or pack is a no-op, voices from both folders enumerate, and an
unknown pack id is an `ArgumentException`.

##### SpeechModelCatalogAdapter_GetPreferredAudioFormat_NullDescriptor_ThrowsArgumentNullException

##### SpeechModelCatalogAdapter_CreateSynthesizerEngineAsync_NullDescriptor_ThrowsArgumentNullException

##### SpeechModelCatalogAdapter_CreateRecognizerEngineAsync_NullDescriptor_ThrowsArgumentNullException

**Scenario**: Each engine or format member is called with a `null` descriptor.

**Expected**: An `ArgumentNullException` is thrown. These are null guards and carry no requirement.
