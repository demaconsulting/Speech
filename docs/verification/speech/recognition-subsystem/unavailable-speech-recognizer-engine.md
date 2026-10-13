### UnavailableSpeechRecognizerEngine

#### Verification Approach

`UnavailableSpeechRecognizerEngine` is verified directly against its shared instance; it has no
dependencies to mock.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The unit is considered verified when the shared instance reports `false` availability, always
returns the shared `UnavailableRecognitionSession.Instance` from `CreateSessionAsync` regardless
of the supplied device, rejects a null device with `ArgumentNullException`, and treats repeated
disposal as a safe no-op.

#### Test Scenarios

##### Unavailable Fallback: Availability and Session Creation

**Tests**: `UnavailableSpeechRecognizerEngine_IsAvailable_Read_ReturnsFalse`,
`UnavailableSpeechRecognizerEngine_CreateSessionAsync_Always_ReturnsUnavailableSession`,
`UnavailableSpeechRecognizerEngine_CreateSessionAsync_NullDevice_ThrowsArgumentNullException`

The engine reports unavailable, always returns the shared unavailable session, and rejects only
a null device.

##### Unavailable Fallback: Canceled Token Is a Caller Error

**Test**: `UnavailableSpeechRecognizerEngine_CreateSessionAsync_CanceledToken_ThrowsOperationCanceledException`

An already-canceled token still throws `OperationCanceledException`, honoring the
`ISpeechRecognizerEngine.CreateSessionAsync` contract.

##### Unavailable Fallback: Safe Disposal

**Test**: `UnavailableSpeechRecognizerEngine_DisposeAsync_CalledTwice_DoesNotThrow`

Repeated disposal is a no-op that never invalidates the shared instance.
