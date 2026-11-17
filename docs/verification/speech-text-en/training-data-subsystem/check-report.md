### CheckReport Verification

#### Verification Approach

`CheckReport` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/CheckReportTests.cs`. The tests add findings and
information lines to a real report and write it to a string writer, then compare the exact text and the exit code.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test creates its own report and writer

#### Acceptance Criteria

A report with no finding gives exit code 0 and the summary `check-data: OK (0 findings)`; findings give exit code 1 and
the summary `check-data: FAILED (n findings)` with the count of all findings; findings are written in the order they
were added with their check name, location and message; more findings in one check than the cap write only the cap
followed by the count of the others; a cap below one is rejected; and a finding renders in the documented one-line
format.

#### Test Scenarios

##### Findings fail the run

**Tests**: `CheckReport_ExitCode_NoFindings_IsZeroAndSummaryIsOk`,
`CheckReport_ExitCode_AnyFinding_IsOneAndSummaryCountsFindings`

##### Stable output

**Tests**: `CheckReport_WriteTo_Findings_AreWrittenInInsertionOrderWithLocation`,
`CheckReport_WriteTo_MoreFindingsThanCap_PrintsCapAndCountsSuppressed`,
`CheckReport_Finding_ToString_UsesTheDocumentedFormat`,
`CheckReport_Constructor_CapBelowOne_ThrowsArgumentOutOfRangeException`
