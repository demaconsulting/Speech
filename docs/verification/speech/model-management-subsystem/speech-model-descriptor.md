### SpeechModelDescriptor Verification

#### Verification Approach

`SpeechModelDescriptor` is verified through deterministic unit tests constructing it with a fake
`ISpeechModel`, proving its passthrough properties mirror the wrapped model, that a null model
is rejected, and that two descriptors for the same model in different states are unequal
(since it is a record, `State` participates in structural equality).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Fakes**: `test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/Fakes/`

#### Acceptance Criteria

`Id`, `DisplayName`, and `Role` always mirror the wrapped `Model`'s own properties verbatim; a
null `model` throws `ArgumentNullException`; two descriptors differing only in `State` are not
equal.

#### Test Scenarios

##### A valid model produces a descriptor exposing the model and its passthrough properties

**Test**: `SpeechModelDescriptor_Constructor_ValidModel_ExposesModelAndPassthroughProperties`

##### A null model throws ArgumentNullException

**Test**: `SpeechModelDescriptor_Constructor_NullModel_ThrowsArgumentNullException`

##### Descriptors for the same model in different states are not equal

**Test**: `SpeechModelDescriptor_StateTransition_DifferentState_ProducesUnequalDescriptor`
