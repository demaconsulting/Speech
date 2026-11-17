### DedicatedWorker

#### Verification Approach

The worker is verified in isolation (`DedicatedWorkerTests`) with delegates the test controls:
a cooperative delegate that honors cancellation, a non-cooperative delegate that blocks until
released, and a gated delegate that proves a blocked call neither blocks the caller nor
unrelated work. The abandon timeout is an injectable override so the bounded wait is verified
deterministically rather than with the multi-second default; diagnostics are an NSubstitute sink.
The integrated policy through the session is verified by
`RecognitionSession_NativeCallExceedsAbandonTimeout_TaskCompletesAndDiagnosticsReportsWarning`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Dependencies**: No external services, native runtime, or audio hardware
- **Test doubles**: NSubstitute diagnostics sink

#### Acceptance Criteria

The unit is considered verified when a blocked delegate does not block the caller or unrelated
work, a cooperative delegate completes promptly on cancellation, and a non-cooperative delegate
is abandoned after the timeout with a diagnostic reported.

#### Test Scenarios

##### Blocked delegate isolation

**Stimulus**: A delegate that blocks on a gate until the test releases it.

**Expected**: The call returns an incomplete task while the delegate is blocked, unrelated work
completes in the meantime, and the task completes once the delegate is released.

**Test**: `DedicatedWorker_Run_BlockedDelegate_DoesNotBlockCallerOrUnrelatedWork`

##### Cooperative cancellation

**Test**: `DedicatedWorker_Run_CooperativeCancellation_CompletesPromptly`

##### Abandon after timeout

**Test**: `DedicatedWorker_Run_NonCooperativeDelegate_AbandonsAfterTimeoutAndReportsDiagnostics`

##### Integration with the session

**Test**: `RecognitionSession_NativeCallExceedsAbandonTimeout_TaskCompletesAndDiagnosticsReportsWarning`
