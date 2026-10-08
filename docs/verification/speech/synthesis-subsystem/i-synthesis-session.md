### ISynthesisSession

#### Verification Approach

`ISynthesisSession` is verified indirectly through both shipped implementations:
`UnavailableSynthesisSession` proves the honest-unavailable path, and `SynthesisSession`
proves the state machine, the overlap rule, hot reuse across calls, the terminal `Faulted` state,
`SynthesizeAsync`'s full-fidelity segment list, cancellation, and fault containment.
`SynthesisSessionState` and `SessionStateChangedEventArgs` are verified through the `StateChanged`
transition sequence observed during real operations, since neither carries independent behavior
of its own.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner; no model, speakers, or native
  speech-inference runtime is required.

#### Acceptance Criteria

The contract is considered verified when the unavailable implementation reports `false`
availability, always reports `State` as `Created`, and throws on every operational member; and
when the real implementation reports itself available until disposed/faulted, never allows two
operations to overlap, reuses correctly across repeated calls, raises the documented state
sequence via `StateChanged`, transitions to and stays `Faulted` after a non-cancellation failure
(with every subsequent call throwing `SynthesisSessionFaultedException`), and
`SynthesizeAsync`/`SpeakAsync` behave correctly under cancellation, playback failure, and
playback drain.

#### Test Scenarios

See the SynthesisSubsystem-level scenarios "Session: Lifecycle State Machine and StateChanged
Event", "Session: Overlap Rule - No Concurrent Operations", "Session: Hot Reuse Across Repeated
Calls", "Session: Faulted Is Terminal", "Session: SynthesizeAsync Full-Fidelity Segment List",
"Session: Chunked Synthesis and Ordered Playback", "Session: Fault Containment", "Session:
Genuine Playback Drain Before Stopping", "Session: Cancellation and Disposal Lifecycle", "Session:
Voice/Speaker Selection", and "Unavailable Fallback: Honest Degradation".
