### RecognitionSessionFaultedException

#### Verification Approach

The exception is verified through `RecognitionSession`, the unit that surfaces it. The backend is
a deterministic fake made to fault and the capture device is an NSubstitute `IAudioCaptureDevice`,
so no downloaded model, native runtime, or audio hardware is needed. A consumer enumerating
`GetResultsAsync` while the session faults is asserted to receive the exception.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- No additional setup beyond the standard test runner.

#### Acceptance Criteria

The unit is considered verified when a fault during `GetResultsAsync` enumeration surfaces to the
consumer as `RecognitionSessionFaultedException`
rather than being swallowed,
and stopping a faulted session does not raise it.

#### Test Scenarios

##### A session fault surfaces to the results consumer

**Test**: `RecognitionSession_GetResultsAsync_SessionFaulted_ThrowsRecognitionSessionFaultedException`

##### Stopping a faulted session does not throw

**Test**: `RecognitionSession_StopAsync_AfterFault_DoesNotThrowFaultedException`

Verifies that the fault is reported only to a reader of the results: stopping a session that has
already faulted completes without throwing the session-faulted exception.
