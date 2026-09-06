## Diagnostics Subsystem Verification

### Verification Approach

The Diagnostics subsystem is verified through unit tests for `NullSpeechDiagnostics` (in
`test/DemaConsulting.Speech.Tests/Diagnostics/NullSpeechDiagnosticsTests.cs`) and through
system-level integration tests (in `SpeechTests.cs`) that supply a custom `ISpeechDiagnostics`
implementation to `AudioDeviceFactory` and assert on the events received. `ISpeechDiagnostics`
itself is an interface with no behavior of its own, so it is verified indirectly through its
implementations and through mocks used elsewhere in the test suite (via NSubstitute).

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, plus NSubstitute for mocking
  `ISpeechDiagnostics` where a subsystem/unit boundary needs to be isolated
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Dependencies**: No external services, databases, native audio backend, or network access
  required — the subsystem is pure in-memory logic

### Acceptance Criteria

A Diagnostics subsystem test run passes when `NullSpeechDiagnostics.Report` can be called with
any combination of `SpeechDiagnosticLevel`, category, and message without throwing and without
producing any observable side effect, and when the system-level diagnostics scenario correctly
receives every event reported by `AudioDeviceFactory`.

### Test Scenarios

#### Singleton: Instance Returns the Same Object

**Test**: `NullSpeechDiagnostics_Instance_ReadTwice_ReturnsSameInstance`

Verifies that two reads of `NullSpeechDiagnostics.Instance` return reference-equal objects,
confirming the singleton pattern required for safe, allocation-free default composition.

#### Null Object: Report Never Throws for a Normal Event

**Test**: `NullSpeechDiagnostics_Report_NormalEvent_DoesNotThrow`

Verifies that calling `Report` on `NullSpeechDiagnostics.Instance` with a well-formed level,
category, and message never throws.

#### Null Object: Report Never Throws for Null or Empty Arguments

**Test**: `NullSpeechDiagnostics_Report_NullOrEmptyArguments_DoesNotThrow`

Verifies that `Report` never throws even when given `null` category/message arguments,
confirming the sink can never become the reason a caller's real operation fails.

#### Null Object: Report Accepts Every Severity Level

**Test**: `NullSpeechDiagnostics_Report_AnyLevel_DoesNotThrow`

Verifies, for each `SpeechDiagnosticLevel` value (`Info`, `Warning`, `Error`), that `Report`
discards the event without throwing.

#### Integration: Sink Receives Structural Events

**Test**: `Speech_SystemIntegration_DiagnosticsSink_ReceivesStructuralEvents`

Verifies, at the system level, that a custom `ISpeechDiagnostics` implementation supplied to an
`AudioDeviceFactory` receives the expected structural diagnostic events when devices are
requested, confirming the reporting contract end-to-end.

#### Integration: No Diagnostics Supplied Uses the Null Sink Safely

**Test**: `Speech_SystemIntegration_NoDiagnosticsSupplied_UsesNullSpeechDiagnosticsSafely`

Verifies, at the system level, that omitting the `diagnostics` constructor argument to
`AudioDeviceFactory` never throws, confirming the default `NullSpeechDiagnostics` fallback works
correctly end-to-end.
