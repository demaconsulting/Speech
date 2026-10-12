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
consumer as `RecognitionSessionFaultedException` rather than being swallowed.

#### Test Scenarios

##### A session fault surfaces to the results consumer

**Test**: `RecognitionSession_GetResultsAsync_SessionFaulted_ThrowsRecognitionSessionFaultedException`
