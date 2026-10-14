### AudioDeviceInUseException

#### Verification Approach

Verified through direct unit tests in `AudioDeviceInUseExceptionTests.cs` of the three public
constructors; no dependencies are mocked. The refusal behavior that throws this type is verified
at the AudioSubsystem level (see the _PortAudio Environment: Refresh Success and Refusal_ and
_Factory: RefreshDevices Delegation, In-Use Propagation, and Probe Re-Evaluation_ scenarios).

#### Test Environment

No additional setup beyond the standard test runner.

#### Acceptance Criteria

Tests pass when the message and inner exception passed to a constructor are exposed unchanged and
the default constructor yields a non-empty message.

#### Test Scenarios

##### Message Constructor Exposes The Message

**Test**: `AudioDeviceInUseException_Constructor_WithMessage_ExposesMessage`

##### Inner-Exception Constructor Exposes Both

**Test**: `AudioDeviceInUseException_Constructor_WithInnerException_ExposesBoth`

##### Default Constructor Has A Non-Empty Message

**Test**: `AudioDeviceInUseException_Constructor_Default_HasNonEmptyMessage`
