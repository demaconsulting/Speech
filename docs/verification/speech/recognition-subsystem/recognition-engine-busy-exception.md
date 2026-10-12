### RecognitionEngineBusyException

#### Verification Approach

The exception is verified in two ways. Its three constructors are verified directly
(`RecognitionEngineBusyExceptionTests`). Its behavioral role - being thrown, and only thrown, by
`SpeechRecognizerEngine.CreateSessionAsync` while the lease is held - is verified through the
engine, driven by a deterministic fake backend and NSubstitute capture devices, so no downloaded
model, native runtime, or audio hardware is needed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The unit is considered verified when `CreateSessionAsync` throws `RecognitionEngineBusyException`
while a prior session holds the lease (including while it is still disposing), and each constructor
exposes the message and inner exception supplied to it, with a non-empty default message.

#### Test Scenarios

##### A leased engine throws the busy exception

**Test**: `SpeechRecognizerEngine_CreateSessionAsync_SessionAlreadyLeased_ThrowsRecognitionEngineBusyException`

##### A disposing prior session still holds the lease

**Test**: `SpeechRecognizerEngine_CreateSessionAsync_PriorSessionDisposing_ThrowsRecognitionEngineBusyException`

##### Default constructor has a non-empty message

**Test**: `RecognitionEngineBusyException_Constructor_Default_HasNonEmptyMessage`

##### Message constructor exposes the message

**Test**: `RecognitionEngineBusyException_Constructor_WithMessage_ExposesMessage`

##### Inner-exception constructor exposes both values

**Test**: `RecognitionEngineBusyException_Constructor_WithInnerException_ExposesBoth`
