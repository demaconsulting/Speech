### DedicatedWorker

#### Verification Approach

`DedicatedWorker` is verified directly and in isolation. The abandon timeout is injectable, so the
non-cooperative-abandonment test does not wait out the two-second default. A cooperative delegate
(one that honors its cancellation token) lets `Run` complete promptly on cancellation; a
non-cooperative delegate (one that ignores it) is abandoned once the shortened timeout elapses,
reporting a `Warning` through an NSubstitute diagnostics sink while still returning control to the
caller as canceled. A third test blocks a delegate on a gate and proves the call still returns, unrelated work
still progresses, and the task completes once the gate is released. `Start`'s additional raw-completion task is
exercised indirectly through the `SynthesisSession` abandonment tests that await it.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: None
- **Test doubles**: NSubstitute `ISpeechDiagnostics` sink

#### Acceptance Criteria

The unit is considered verified when `Run` completes promptly on cooperative cancellation,
abandons a non-cooperative delegate after the timeout with a `Warning` diagnostic and a canceled
result, and a delegate blocked inside the worker neither blocks the caller nor prevents unrelated
work from progressing.

#### Test Scenarios

All tests are in `test/DemaConsulting.Speech.Tests/SynthesisSubsystem/DedicatedWorkerTests.cs`.

##### Cooperative Cancellation

**Stimulus**: A delegate that observes its token is canceled mid-run.

**Expected**: `Run` completes promptly as canceled; no abandonment diagnostic.

**Test**: `DedicatedWorker_Run_CooperativeCancellation_CompletesPromptly`

##### Non-Cooperative Abandonment

**Stimulus**: A delegate that ignores cancellation, with a short abandon timeout.

**Expected**: After the timeout `Run` returns canceled to its caller and a `Warning` diagnostic
is reported.

**Test**: `DedicatedWorker_Run_NonCooperativeDelegate_AbandonsAfterTimeoutAndReportsDiagnostics`

##### Blocked Delegate Isolation

**Stimulus**: A delegate that blocks on a gate until the test releases it.

**Expected**: The call returns an incomplete task while the delegate is blocked, unrelated work
completes in the meantime, and the task completes with the delegate's result once released.

**Test**: `DedicatedWorker_Run_BlockedDelegate_DoesNotBlockCallerOrUnrelatedWork`
