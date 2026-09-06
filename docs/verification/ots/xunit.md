## xUnit Verification

This document provides the verification evidence for the xUnit OTS software item. Requirements
for this OTS item are defined in the xUnit OTS Software Requirements document.

### Required Functionality

xUnit v3 (xunit.v3 and xunit.runner.visualstudio) is the unit-testing framework used by the
project. It discovers and runs all test methods and writes TRX result files that feed into coverage
reporting and requirements traceability. Passing tests confirm the framework is functioning
correctly.

### Verification Approach

xUnit is verified by self-validation evidence from the CI pipeline. Each scenario names a specific
test method that xUnit must discover, execute, and record in a TRX result file. A passing pipeline
run for all scenarios constitutes evidence that both requirements are satisfied.

### Test Scenarios

#### NullSpeechDiagnostics_Instance_ReadTwice_ReturnsSameInstance

**Scenario**: xUnit discovers and runs this test; the test verifies that
`NullSpeechDiagnostics.Instance` returns the identical shared object on two successive reads,
confirming the documented singleton contract.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Template-OTS-xUnit-Execute`, `Template-OTS-xUnit-Report`.

#### NullSpeechDiagnostics_Report_NormalEvent_DoesNotThrow

**Scenario**: xUnit discovers and runs this test; the test verifies that
`NullSpeechDiagnostics.Report` is a true no-op for a normal, well-formed diagnostic event and
never throws.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Template-OTS-xUnit-Execute`, `Template-OTS-xUnit-Report`.

#### NullSpeechDiagnostics_Report_NullOrEmptyArguments_DoesNotThrow

**Scenario**: xUnit discovers and runs this test; the test verifies that
`NullSpeechDiagnostics.Report` never throws even when given null category/message arguments,
confirming the sink cannot become the reason a caller's real operation fails.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Template-OTS-xUnit-Execute`, `Template-OTS-xUnit-Report`.

#### NullSpeechDiagnostics_Report_AnyLevel_DoesNotThrow

**Scenario**: xUnit discovers and runs this data-driven `[Theory]` test across every
`SpeechDiagnosticLevel` value (`Info`, `Warning`, `Error`); the test verifies that the sink
discards events regardless of severity without throwing.

**Expected**: xUnit executes the test for each inline data case, all cases pass, and the results
appear in the TRX output.

**Requirement coverage**: `Template-OTS-xUnit-Execute`, `Template-OTS-xUnit-Report`.

#### AudioDeviceSelection_Resolve_MatchingName_ReturnsMatchingDevice

**Scenario**: xUnit discovers and runs this test; the test verifies that resolving a selection
whose name matches an enumerated device returns that matching device.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Template-OTS-xUnit-Execute`, `Template-OTS-xUnit-Report`.

#### AudioDeviceSelection_Resolve_NoLongerPresent_ReturnsNull

**Scenario**: xUnit discovers and runs this test; the test verifies that resolving a selection
whose named device is no longer present in the enumeration falls back to `null` (system default)
rather than throwing.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Template-OTS-xUnit-Execute`, `Template-OTS-xUnit-Report`.

#### AudioDeviceSelection_Resolve_SystemDefault_ReturnsNull

**Scenario**: xUnit discovers and runs this test; the test verifies that
`AudioDeviceSelection.SystemDefault` always resolves to `null`, regardless of what devices are
enumerated.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Template-OTS-xUnit-Execute`, `Template-OTS-xUnit-Report`.

#### AudioDeviceSelection_Resolve_EmptyDeviceList_ReturnsNull

**Scenario**: xUnit discovers and runs this test; the test verifies that resolving against an
empty device list never throws and falls back to `null`.

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Template-OTS-xUnit-Execute`, `Template-OTS-xUnit-Report`.

#### AudioDeviceSelection_Resolve_NullDeviceList_ThrowsArgumentNullException

**Scenario**: xUnit discovers and runs this test; the test verifies that
`AudioDeviceSelection.Resolve` rejects a `null` device list with `ArgumentNullException`,
distinguishing "not asked" from "no devices".

**Expected**: xUnit executes the test, the test passes, and the result appears in the TRX output.

**Requirement coverage**: `Template-OTS-xUnit-Execute`, `Template-OTS-xUnit-Report`.

### Requirements Coverage

- **`Template-OTS-xUnit-Execute`**: NullSpeechDiagnostics_Instance_ReadTwice_ReturnsSameInstance,
  NullSpeechDiagnostics_Report_NormalEvent_DoesNotThrow,
  NullSpeechDiagnostics_Report_NullOrEmptyArguments_DoesNotThrow,
  NullSpeechDiagnostics_Report_AnyLevel_DoesNotThrow,
  AudioDeviceSelection_Resolve_MatchingName_ReturnsMatchingDevice,
  AudioDeviceSelection_Resolve_NoLongerPresent_ReturnsNull,
  AudioDeviceSelection_Resolve_SystemDefault_ReturnsNull,
  AudioDeviceSelection_Resolve_EmptyDeviceList_ReturnsNull,
  AudioDeviceSelection_Resolve_NullDeviceList_ThrowsArgumentNullException
- **`Template-OTS-xUnit-Report`**: NullSpeechDiagnostics_Instance_ReadTwice_ReturnsSameInstance,
  NullSpeechDiagnostics_Report_NormalEvent_DoesNotThrow,
  NullSpeechDiagnostics_Report_NullOrEmptyArguments_DoesNotThrow,
  NullSpeechDiagnostics_Report_AnyLevel_DoesNotThrow,
  AudioDeviceSelection_Resolve_MatchingName_ReturnsMatchingDevice,
  AudioDeviceSelection_Resolve_NoLongerPresent_ReturnsNull,
  AudioDeviceSelection_Resolve_SystemDefault_ReturnsNull,
  AudioDeviceSelection_Resolve_EmptyDeviceList_ReturnsNull,
  AudioDeviceSelection_Resolve_NullDeviceList_ThrowsArgumentNullException
