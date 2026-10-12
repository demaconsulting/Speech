### SpeechSynthesizerUnavailableException

#### Verification Approach

The exception is verified directly through its three constructors
(`UnavailableSpeechSynthesizerEngineTests`); it has no dependencies to mock. Its behavioral role -
being thrown by the operational members of the unavailable engine and session - is verified with
`UnavailableSpeechSynthesizerEngine` and `UnavailableSynthesisSession` in their own documents.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The unit is considered verified when the default constructor yields a non-empty message, the
message constructor exposes the supplied message, and the message-and-inner-exception constructor
exposes both.

#### Test Scenarios

##### Default constructor has a non-empty message

**Test**: `SpeechSynthesizerUnavailableException_Constructor_Default_HasNonEmptyMessage`

##### Message constructor exposes the message

**Test**: `SpeechSynthesizerUnavailableException_Constructor_WithMessage_ExposesMessage`

##### Inner-exception constructor exposes both values

**Test**: `SpeechSynthesizerUnavailableException_Constructor_WithInnerException_ExposesBoth`
