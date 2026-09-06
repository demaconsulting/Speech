### SpeechModelStoreException

#### Verification Approach

Verified through direct unit tests in `SpeechModelStoreExceptionTests.cs` proving standard
exception-constructor conformance, plus `SpeechModelStoreTests.cs`'s Windows-only blocked-handle
scenario proving this exception is actually thrown by `SpeechModelStore.Uninstall` when
`current/` cannot be removed.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK

#### Acceptance Criteria

Tests pass when all three standard constructors expose their message/inner-exception correctly,
and when `Uninstall` throws this exception (rather than an unrelated I/O exception) for a blocked
`current/` directory.

#### Test Scenarios

##### Constructor: With Message Exposes Message

**Test**: `SpeechModelStoreException_Constructor_WithMessage_ExposesMessage`

##### Constructor: With Inner Exception Exposes Both

**Test**: `SpeechModelStoreException_Constructor_WithInnerException_ExposesBoth`

##### Constructor: Default Has Non-Empty Message

**Test**: `SpeechModelStoreException_Constructor_Default_HasNonEmptyMessage`

##### Uninstall: Open File Handle Throws This Exception (Windows only)

**Test**: `SpeechModelStore_Uninstall_OpenFileHandleInCurrent_ThrowsSpeechModelStoreException`

Windows-specific; calls `Assert.Skip(...)` on non-Windows platforms.
