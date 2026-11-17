### DataManifest Verification

#### Verification Approach

`DataManifest` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/DataManifestTests.cs`. Each test writes a few
small files and a manifest computed from them into a temporary folder, then changes one thing: one byte of a file, a
recorded size or row count, a deleted file or manifest, an extra file, or one malformed manifest entry. The real
`TsvTable` and `CheckReport` are used.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary folder

#### Acceptance Criteria

A folder that matches its manifest gives no finding. A changed byte, a wrong byte count, a wrong row count, a missing
listed file and a missing manifest each give a finding that names the file; the line counter counts terminated and
unterminated lines. An unlisted file is a finding, while the manifest itself and the two local cache folders are
ignored. Unsorted entries, a duplicate entry, an uppercase hash, a path that is not a relative path with forward
slashes, a malformed line and non-integer counts are findings, and a valid manifest exposes its entries.

#### Test Scenarios

##### Hash verification

**Tests**: `DataManifest_Verify_MatchingManifest_ReportsNothing`, `DataManifest_Verify_ChangedByte_ReportsHashMismatch`,
`DataManifest_Verify_WrongBytes_ReportsSizeMismatch`, `DataManifest_Verify_WrongRows_ReportsRowCountMismatch`,
`DataManifest_Verify_MissingFile_ReportsListedFileMissing`, `DataManifest_Verify_MissingManifest_ReportsFinding`,
`DataManifest_CountLines_CountsTerminatedAndUnterminatedLines`

##### Complete listing

**Tests**: `DataManifest_Verify_UnlistedFile_IsReported`, `DataManifest_Verify_LocalCaches_AreIgnored`

A file in the folder `external/` and a file in `work/` do not count as unlisted.

##### Determinism

**Tests**: `DataManifest_Load_ValidManifest_ExposesEntries`, `DataManifest_Verify_UnsortedEntries_ReportsOrder`,
`DataManifest_Verify_DuplicateEntry_ReportsDuplicate`, `DataManifest_Verify_UpperCaseDigest_ReportsMalformedDigest`,
`DataManifest_Verify_NonRelativePath_IsRejected`, `DataManifest_Verify_MalformedLine_ReportsColumns`,
`DataManifest_Verify_NonIntegerBytes_ReportsMalformedNumbers`
