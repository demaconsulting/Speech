## ConsoleCancellation Verification

### Verification Approach

`ConsoleCancellation` is verified with unit tests that create registrations and exercise them two
ways. The registration's internal `Raise` seam runs the cancellation work and reports whether
termination would be suppressed. The registration's real subscribed `Handler` delegate is also
invoked directly with a `ConsoleCancelEventArgs` created reflectively (the type has no public
constructor), so the assignment of `Cancel` inside the real handler is asserted. An internal
`IsSubscribed` state records whether the handler is still attached, so detachment on disposal is
asserted. A real `Ctrl+C` signal cannot be raised portably in process, and the runtime's own
unsubscription from `Console.CancelKeyPress` is trusted rather than inspected; the real-signal path
is covered by design review and manual verification (press `Ctrl+C` during `download`).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: Registrations subscribe the process-global `Console.CancelKeyPress` event, so the
  class runs in the sequential collection; each test disposes what it creates
- **Test doubles**: None; a real `CancellationTokenSource` and simple callbacks

### Acceptance Criteria

The unit passes when `Register` cancels its source, runs the optional callback after the
cancellation and keeps the process alive (its real handler sets `Cancel`); `Observe` runs its
callback and leaves `Cancel` unset; a disposed registration is detached and repeated disposal does
not throw; and null arguments are rejected with the offending parameter name.

### Test Scenarios

#### ConsoleCancellation_Register_Handler_SetsCancelAndCancelsSource

**Scenario**: A `Register` registration's real handler is invoked with reflectively created
`ConsoleCancelEventArgs`.

**Expected**: `Cancel` is `true` and the source is canceled.

#### ConsoleCancellation_Observe_Handler_RunsCallbackAndLeavesCancelUnset

**Scenario**: An `Observe` registration's real handler is invoked with the same kind of arguments.

**Expected**: The callback ran once and `Cancel` remains `false`.

#### ConsoleCancellation_Dispose_DetachesHandler

**Scenario**: A registration is checked, disposed twice, and checked again.

**Expected**: It reports subscribed before disposal and not subscribed afterward.

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

- The runtime's removal of the handler from `Console.CancelKeyPress` on disposal (the unit asserts
  its own subscribed state and idempotence).
- The propagation of exceptions from `source.Cancel()` or the callback.
- Commands' end-to-end reaction to a real `Ctrl+C` is verified in the command subsystems' own tests
  where they exist; it is not verified here.
