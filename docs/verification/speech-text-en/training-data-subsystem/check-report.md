### CheckReport Verification

#### Verification Approach

`CheckReport` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/CheckReportTests.cs`. The tests add findings and
information lines to a real report and write it to a string writer, then compare the exact text and the exit code.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test creates its own report and writer

#### Acceptance Criteria

**Report** (`Report`). No finding gives exit code 0 and `check-data: OK (0 findings)`; findings give exit code 1 and
`check-data: FAILED (n findings)` counting all of them; findings are written in the order added with check name,
location and message; more findings in one check than the cap write only the cap followed by the count of the others,
and the other checks are not affected; a cap below one is rejected.

#### Test Scenarios

##### Report

**Tests**: `CheckReport_ExitCode_NoFindings_IsZeroAndSummaryIsOk`,
`CheckReport_ExitCode_AnyFinding_IsOneAndSummaryCountsFindings`,
`CheckReport_WriteTo_Findings_AreWrittenInInsertionOrderWithLocation`,
`CheckReport_WriteTo_MoreFindingsThanCap_PrintsCapAndCountsSuppressed`,
`CheckReport_Constructor_CapBelowOne_ThrowsArgumentOutOfRangeException`
