### DedicatedWorker

#### Verification Approach

The worker is verified in isolation (`DedicatedWorkerTests`) with delegates the test controls:
a cooperative delegate that honors cancellation, and a non-cooperative delegate that blocks until
released. The abandon timeout is an injectable override so the bounded wait is verified
deterministically rather than with the multi-second default; diagnostics are an NSubstitute sink.
The integrated policy through the session is verified by
`RecognitionSession_NativeCallExceedsAbandonTimeout_TaskCompletesAndDiagnosticsReportsWarning`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Dependencies**: No external services, native runtime, or audio hardware
- **Test doubles**: NSubstitute diagnostics sink

#### Acceptance Criteria

The unit is considered verified when work runs on a dedicated long-running thread, a cooperative
delegate completes promptly on cancellation, and a non-cooperative delegate is abandoned after
the timeout with a diagnostic reported.

#### Test Scenarios

##### Dedicated long-running thread

**Test**: `DedicatedWorker_Run_UsesLongRunningTaskCreationOption`

##### Cooperative cancellation

**Test**: `DedicatedWorker_Run_CooperativeCancellation_CompletesPromptly`

##### Abandon after timeout

**Test**: `DedicatedWorker_Run_NonCooperativeDelegate_AbandonsAfterTimeoutAndReportsDiagnostics`

##### Integration with the session

**Test**: `RecognitionSession_NativeCallExceedsAbandonTimeout_TaskCompletesAndDiagnosticsReportsWarning`
