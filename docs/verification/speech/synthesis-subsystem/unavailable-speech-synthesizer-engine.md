### UnavailableSpeechSynthesizerEngine

#### Verification Approach

`UnavailableSpeechSynthesizerEngine` is verified directly against its shared instance; it has no
dependencies to mock. `CreateSessionAsync` is verified to succeed, returning the shared
unavailable session, since binding a device to an already unavailable engine is an ordinary (if
useless) composition, not an error. The safe-dispose test deliberately disposes the shared instance
twice, proving that a host wrapping its engine in a disposal scope cannot invalidate the fallback
for anyone else. `SpeechSynthesizerUnavailableException` is verified in its own document
(_SpeechSynthesizerUnavailableException_).

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The unit is considered verified when the shared instance reports `false` availability, always
succeeds at `CreateSessionAsync` (rejecting only a null device), throws
`SpeechSynthesizerUnavailableException` from `SpeakAsync`/`SynthesizeAsync`, and treats repeated
disposal as a no-op.

#### Test Scenarios

##### Unavailable Fallback: Availability and Session Creation

**Tests**: `UnavailableSpeechSynthesizerEngine_IsAvailable_Read_ReturnsFalse`,
`UnavailableSpeechSynthesizerEngine_CreateSessionAsync_Always_ReturnsUnavailableSession`,
`UnavailableSpeechSynthesizerEngine_CreateSessionAsync_NullDevice_ThrowsArgumentNullException`

The engine reports unavailable, always returns the shared unavailable session, and rejects only
a null device.

##### Unavailable Fallback: Operations Throw and Disposal Is Safe

**Tests**: `UnavailableSpeechSynthesizerEngine_SpeakAsync_Always_ThrowsSpeechSynthesizerUnavailableException`,
`UnavailableSpeechSynthesizerEngine_SynthesizeAsync_Always_ThrowsSpeechSynthesizerUnavailableException`,
`UnavailableSpeechSynthesizerEngine_DisposeAsync_CalledTwice_DoesNotThrow`

One-shot operations throw the documented exception; repeated disposal is a no-op.
