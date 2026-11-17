### LicenseChecker Verification

#### Verification Approach

`LicenseChecker` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/LicenseCheckerTests.cs`. Each test builds the
small valid data folder of the dataset fixture, loads it with the real `TextEnDataSet`, changes one row, one file or one
name, and runs the checker with a real `CheckReport`. Files are real files in a temporary folder because the checker
enumerates the folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder

#### Acceptance Criteria

The valid fixture gives no finding and a count of the rows per license. A restricted license on a row, a generated row
that is not `CC0-1.0`, a row of the American National Corpus source that is not `CC-BY-3.0-US`, and a harvested row
without a provenance record each give a finding; a restricted term in a provenance table (including the word Misaki) and
in the source column of a data row gives a finding with its line, while the same term inside a sentence gives none; a
file named after an evaluation-only corpus gives a finding, while the mapping table and the local cache folders are not
reported; a `CC-BY-3.0-US` row without an attribution file, or with one that does not name the corpus, gives a finding,
and no such row requires no attribution file; a readme without a `License:` line, a readme with a disallowed license and
a missing audit summary each give a finding.

#### Test Scenarios

##### Allow-list

**Tests**: `LicenseChecker_Run_ValidFixture_ReportsNothingAndCountsLicenses`,
`LicenseChecker_Run_RestrictedLicenseOnRow_ReportsFinding`

##### Source policy

**Tests**: `LicenseChecker_Run_GeneratedRowNotCc0_ReportsFinding`, `LicenseChecker_Run_MascRowNotCcBy_ReportsFinding`,
`LicenseChecker_Run_HarvestRowWithoutProvenance_ReportsFinding`

##### Restricted terms

**Tests**: `LicenseChecker_Run_MisakiTermInProvenanceTsv_ReportsFindingWithLine`,
`LicenseChecker_Run_RestrictedTermInSourceColumn_ReportsFinding`,
`LicenseChecker_Run_RestrictedTermInSentence_IsNotReported`

##### Evaluation-only paths

**Tests**: `LicenseChecker_Run_EvaluationOnlyPath_ReportsFinding`,
`LicenseChecker_Run_MappingFileAndLocalCaches_AreNotReportedAsEvaluationPaths`

##### Attribution

**Tests**: `LicenseChecker_Run_AttributionFileMissing_ReportsFinding`,
`LicenseChecker_Run_AttributionWithoutMasc_ReportsFinding`,
`LicenseChecker_Run_NoAttributionRows_DoesNotRequireAttributionFile`,
`LicenseChecker_Run_ReadmeWithoutLicenseLine_ReportsFinding`,
`LicenseChecker_Run_ReadmeWithDisallowedLicense_ReportsFinding`, `LicenseChecker_Run_AuditSummaryMissing_ReportsFinding`
