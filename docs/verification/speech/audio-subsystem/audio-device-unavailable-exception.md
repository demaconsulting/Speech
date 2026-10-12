### AudioDeviceUnavailableException

#### Verification Approach

Verified through direct unit tests of the three public constructors; no dependencies are mocked.

#### Test Environment

No additional setup beyond the standard test runner.

#### Acceptance Criteria

Tests pass when the message and inner exception passed to a constructor are exposed unchanged and
the default constructor yields a non-empty message.

#### Test Scenarios

##### Message Constructor Exposes The Message

**Test**: `AudioDeviceUnavailableException_Constructor_WithMessage_ExposesMessage`

##### Inner-Exception Constructor Exposes Both

**Test**: `AudioDeviceUnavailableException_Constructor_WithInnerException_ExposesBoth`

##### Default Constructor Has A Non-Empty Message

**Test**: `AudioDeviceUnavailableException_Constructor_Default_HasNonEmptyMessage`
