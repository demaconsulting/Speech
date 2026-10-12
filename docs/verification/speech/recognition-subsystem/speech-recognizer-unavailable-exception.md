### SpeechRecognizerUnavailableException

#### Verification Approach

The exception is verified directly through its three constructors
(`UnavailableSpeechRecognizerEngineTests`); it has no dependencies to mock. Its behavioral role -
being thrown by the operational members of the unavailable session, and by a session whose capture
device fails to start - is verified with `UnavailableRecognitionSession` and `RecognitionSession`
in their own documents.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The unit is considered verified when the default constructor yields a non-empty message, the
message constructor exposes the supplied message, and the message-and-inner-exception constructor
exposes both.

#### Test Scenarios

##### Default constructor has a non-empty message

**Test**: `SpeechRecognizerUnavailableException_Constructor_Default_HasNonEmptyMessage`

##### Message constructor exposes the message

**Test**: `SpeechRecognizerUnavailableException_Constructor_WithMessage_ExposesMessage`

##### Inner-exception constructor exposes both values

**Test**: `SpeechRecognizerUnavailableException_Constructor_WithInnerException_ExposesBoth`
