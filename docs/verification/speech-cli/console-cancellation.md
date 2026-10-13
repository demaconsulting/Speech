## ConsoleCancellation Verification

### Verification Approach

`ConsoleCancellation` is verified with unit tests that create registrations and drive the
cancellation work through the registration's internal `Raise` seam, because the console's
`ConsoleCancelEventArgs` has no public constructor and `Ctrl+C` cannot be raised from a test.
`Raise` runs the same work as the signal handler and returns whether termination would be
suppressed. The assignment of `Cancel` inside the real event handler and the actual unsubscription
from `Console.CancelKeyPress` are therefore not asserted by these tests; they are small and
exercised only when an operator presses `Ctrl+C` during a command.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Registrations subscribe the process-global `Console.CancelKeyPress` event, so the
  class runs in the sequential collection; each test disposes what it creates
- **Test doubles**: None; a real `CancellationTokenSource` and simple callbacks

### Acceptance Criteria

The unit passes when `Register` cancels its source, runs the optional callback after the
cancellation and reports termination as suppressed; `Observe` runs its callback and reports
termination as not suppressed; repeated disposal does not throw; and null arguments are rejected.

### Test Scenarios

#### ConsoleCancellation_Register_Raise_CancelsSourceRunsCallbackAndSuppressesTermination

**Scenario**: A registration is created with a source and a callback that records whether the source
was already canceled, then raised.

**Expected**: The source is canceled, the callback ran and saw the cancellation, and `Raise` reports
termination suppressed.

#### ConsoleCancellation_Register_NoCallback_CancelsSource

**Scenario**: A registration is created with a source and no callback, then raised.

**Expected**: The source is canceled.

#### ConsoleCancellation_Observe_Raise_RunsCallbackWithoutSuppressingTermination

**Scenario**: An observing registration with a counting callback is raised.

**Expected**: The callback ran once and `Raise` reports termination not suppressed.

#### ConsoleCancellation_Dispose_CalledTwice_DoesNotThrow

**Scenario**: A registration is disposed twice.

**Expected**: No exception.

#### ConsoleCancellation_Register_NullSource_ThrowsArgumentNullException

**Scenario**: `Register` is called with a null source.

**Expected**: `ArgumentNullException`.

#### ConsoleCancellation_Observe_NullCallback_ThrowsArgumentNullException

**Scenario**: `Observe` is called with a null callback.

**Expected**: `ArgumentNullException`.

### Not Covered By A Dedicated Test

- The event handler's own `e.Cancel` assignment, and the removal of the handler from
  `Console.CancelKeyPress` on disposal (only idempotence is asserted).
- The propagation of exceptions from `source.Cancel()` or the callback.
- Commands' end-to-end reaction to a real `Ctrl+C` is verified in the command subsystems' own tests
  where they exist; it is not verified here.
