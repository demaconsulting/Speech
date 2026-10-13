## DemaConsulting.TestResults Verification

This document provides the verification evidence for the DemaConsulting.TestResults OTS software
item. Requirements for this OTS item are defined in the DemaConsulting.TestResults OTS Software
Requirements document.

### Required Functionality

DemaConsulting.TestResults provides the result model and the TRX and JUnit XML serializers used by
the SpeechCli `Validation` unit when `--validate --results <file>` is run.

### Verification Approach

Verification runs the real self-validation and inspects the file it writes, so the package is
exercised exactly as the tool uses it:

- The out-of-process integration test runs the built tool with a `.trx` results path and asserts
  that a TRX file containing a `TestRun` element is produced
- The in-process test runs `Program.Main` with a `.xml` results path and asserts that a JUnit file
  containing a `testsuite` element is produced

The tests do not claim to validate every element of either format, only that the package yields a
document of the expected kind for the results this tool produces.

### Test Scenarios

#### SpeechCli_ValidateWithTrxResults_Requested_GeneratesTrxFile

**Scenario**: The built tool is run as a child process with `--validate --results <path>.trx`.

**Expected**: Exit code 0 and a TRX results file containing a `TestRun` element.

#### Program_Main_ValidateWithXmlResults_WritesJUnitFile

**Scenario**: `Main` receives `--validate --results <path>.xml`.

**Expected**: Exit code 0 and a JUnit file containing a `testsuite` element.
