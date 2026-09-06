### SpeechModelDescriptorEnums Verification

#### Verification Approach

`SpeechModelRole`, `SpeechModelState`, and `SpeechModelAudioTagSupport` are plain enums verified
by locking down their exact fixed value sets via `Enum.GetValues<T>()`, guarding against a
silent, unintentional addition/removal/reordering of a declared state.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline

#### Acceptance Criteria

Each enum exposes exactly its documented fixed value set and no others.

#### Test Scenarios

##### SpeechModelRole declares exactly Recognition and Synthesis

**Test**: `SpeechModelRole_Values_DeclaresRecognitionAndSynthesisOnly`

##### SpeechModelState declares exactly the fixed lifecycle state set

**Test**: `SpeechModelState_Values_DeclaresFixedStateSet`

##### SpeechModelAudioTagSupport declares exactly None, ParameterMapped, and Native

**Test**: `SpeechModelAudioTagSupport_Values_DeclaresNoneParameterMappedAndNative`
