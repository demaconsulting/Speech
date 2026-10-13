### UnavailableSynthesisSession

#### Verification Approach

`UnavailableSynthesisSession` is verified directly against its shared instance; it has no
dependencies to mock. `State` is verified to always report `Created`, since this session never
transitions. The safe-dispose test deliberately disposes the shared instance twice, proving that
a host wrapping its session in a disposal scope cannot invalidate the fallback for anyone else.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The unit is considered verified when the shared instance reports `false` availability and
`SynthesisSessionState.Created`, throws `SpeechSynthesizerUnavailableException` from
`SpeakAsync`/`SynthesizeAsync` (and `ArgumentNullException` for a null `text`), treats `StopAsync`
as a safe no-op and repeated `DisposeAsync` calls as a no-op, and subscribing/unsubscribing
`StateChanged` never throws.

#### Test Scenarios

##### Unavailable Fallback: Honest State Reporting

**Tests**: `UnavailableSynthesisSession_IsAvailable_Read_ReturnsFalse`,
`UnavailableSynthesisSession_State_Read_ReturnsCreated`,
`UnavailableSynthesisSession_StateChanged_SubscribeAndUnsubscribe_DoesNotThrow`

The session reports unavailable in the `Created` state, and event subscription is safe.

##### Unavailable Fallback: Operations Throw or No-Op

**Tests**: `UnavailableSynthesisSession_SpeakAsync_Always_ThrowsSpeechSynthesizerUnavailableException`,
`UnavailableSynthesisSession_SpeakAsync_NullText_ThrowsArgumentNullException`,
`UnavailableSynthesisSession_SynthesizeAsync_Always_ThrowsSpeechSynthesizerUnavailableException`,
`UnavailableSynthesisSession_SynthesizeAsync_NullText_ThrowsArgumentNullException`,
`UnavailableSynthesisSession_StopAsync_NoSessionInFlight_IsNoOp`,
`UnavailableSynthesisSession_DisposeAsync_CalledTwice_DoesNotThrow`

Speak and synthesize throw the documented exception (null text throws
`ArgumentNullException`); stop and repeated dispose are safe no-ops.
