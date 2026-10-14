### SpeechModelDescriptor Verification

#### Verification Approach

`SpeechModelDescriptor` is verified through deterministic unit tests constructing it with a fake
`ISpeechModel` (hand-written fakes and NSubstitute substitutes where a test must change the model's values
after construction), proving its passthrough properties mirror the wrapped model, that a null model
is rejected, and that two descriptors for the same model in different states are unequal
(since it is a record, `State` participates in structural equality).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Test doubles**: NSubstitute substitutes of `ISpeechModel`, and the hand-written fakes in `test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/Fakes/`

#### Acceptance Criteria

`Id`, `DisplayName`, and `Role` always mirror the wrapped `Model`'s own properties verbatim;
likewise `LicenseName` and `LicenseUrl` always mirror the wrapped `Model`'s declared license
name and URL (including a model that declares a non-default license); a null `model` throws
`ArgumentNullException`; two descriptors for the same model and state are equal; two descriptors differing only in
`State` are not equal; two descriptors for different model instances are not equal even with the same state;
the descriptor's public properties have no setters and its state is unaffected by later changes to the model.

#### Test Scenarios

##### A valid model produces a descriptor exposing the model and its passthrough properties

**Test**: `SpeechModelDescriptor_Constructor_ValidModel_ExposesModelAndPassthroughProperties`

##### Passthrough mirrors a non-default identity, live

**Scenario**: A substituted model reports a non-default id, name and role, which then change.

**Expected**: The descriptor returns the first values, then the changed values (no caching).

**Test**: `SpeechModelDescriptor_Passthrough_NonDefaultIdentity_MirrorsModelLive`

##### Passthrough mirrors a non-default license

**Test**: `SpeechModelDescriptor_Passthrough_NonDefaultLicense_MirrorsModel`

##### The same model and state are equal

**Test**: `SpeechModelDescriptor_SameModelAndState_AreEqual`

##### Different model instances with the same state are not equal

**Test**: `SpeechModelDescriptor_DifferentModelInstance_SameState_AreNotEqual`

Two distinct but identically named fake models are wrapped with the same state; equality is by model instance.

##### Distinct models that report themselves equal are still not equal

**Test**: `SpeechModelDescriptor_DistinctModelsThatAreAlwaysEqual_AreNotEqual`

Two distinct substitute models whose own `Equals` always returns `true` yield unequal descriptors; the same
instance and state yield equal descriptors with consistent hash codes.

##### Immutability: no public setters

**Test**: `SpeechModelDescriptor_Snapshot_PublicProperties_HaveNoSetters`

##### Immutability: state stays the observed value when the model changes later

**Test**: `SpeechModelDescriptor_Snapshot_ModelChangesLater_StateRemainsObservedValue`

##### A null model throws ArgumentNullException

**Test**: `SpeechModelDescriptor_Constructor_NullModel_ThrowsArgumentNullException`

##### Descriptors for the same model in different states are not equal

**Test**: `SpeechModelDescriptor_StateTransition_DifferentState_ProducesUnequalDescriptor`
