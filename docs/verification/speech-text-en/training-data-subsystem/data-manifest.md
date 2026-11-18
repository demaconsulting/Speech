### DataManifest Verification

#### Verification Approach

`DataManifest` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/DataManifestTests.cs`. Each test builds the
small valid data folder of the dataset fixture, with a manifest computed from it, and changes one thing: one byte of a
file, a recorded size or row count, a deleted file or manifest, an extra file, or malformed manifest entries. The real
`TsvTable` and `CheckReport` are used.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary folder

#### Acceptance Criteria

**Hash verification** (`HashVerification`). A matching folder gives no finding. A changed byte, a wrong byte count, a
wrong row count, a missing listed file and a missing manifest each give a finding that names the file; the line counter
counts terminated and unterminated lines; an unlisted file is a finding, while the manifest itself and the two local
cache folders are ignored.

**Format** (`Format`). Unsorted and duplicate entries, an uppercase hash, a path that is not relative with forward
slashes (a backslash, a parent folder or a rooted path), a line with the wrong number of columns and non-integer counts
are findings.

#### Test Scenarios

##### Hash verification

**Tests**: `DataManifest_Verify_MatchingManifest_ReportsNothing`, `DataManifest_Verify_ChangedByte_ReportsHashMismatch`,
`DataManifest_Verify_WrongBytes_ReportsSizeMismatch`, `DataManifest_Verify_MissingFile_ReportsListedFileMissing`,
`DataManifest_Verify_MissingManifest_ReportsFinding`, `DataManifest_CountLines_CountsTerminatedAndUnterminatedLines`,
`DataManifest_Verify_UnlistedFile_IsReported`, `DataManifest_Verify_LocalCaches_AreIgnored`

The size test changes a byte count and a row count in one manifest and expects both findings. A file in the folders
`external/` and `work/` does not count as unlisted.

##### Format

**Tests**: `DataManifest_Verify_UnsortedEntries_ReportsOrder`,
`DataManifest_Verify_UpperCaseDigest_ReportsMalformedDigest`, `DataManifest_Verify_NonRelativePath_IsRejected`,
`DataManifest_Verify_MalformedLine_ReportsColumns`

The order test swaps two entries and repeats another, and the malformed-line test adds a one-column line and a
non-integer size; each defect must give its own finding.
