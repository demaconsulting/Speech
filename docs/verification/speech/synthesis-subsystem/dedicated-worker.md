### DedicatedWorker

#### Verification Approach

`DedicatedWorker` is verified directly and in isolation. The abandon timeout is injectable, so the
non-cooperative-abandonment test does not wait out the two-second default. A cooperative delegate
(one that honors its cancellation token) lets `Run` complete promptly on cancellation; a
non-cooperative delegate (one that ignores it) is abandoned once the shortened timeout elapses,
reporting a `Warning` through an NSubstitute diagnostics sink while still returning control to the
caller as canceled. A third test inspects the running thread from inside the delegate to prove it
runs on a `TaskCreationOptions.LongRunning` task. `Start`'s additional raw-completion task is
exercised indirectly through the `SynthesisSession` abandonment tests that await it.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: None
- **Test doubles**: NSubstitute `ISpeechDiagnostics` sink

#### Acceptance Criteria

The unit is considered verified when `Run` completes promptly on cooperative cancellation,
abandons a non-cooperative delegate after the timeout with a `Warning` diagnostic and a canceled
result, and always executes its delegate on a long-running task.

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

##### Long-Running Execution

**Stimulus**: A delegate that records the characteristics of its running thread.

**Expected**: The delegate ran on a dedicated long-running (non-pool) thread.

**Test**: `DedicatedWorker_Run_UsesLongRunningTaskCreationOption`
