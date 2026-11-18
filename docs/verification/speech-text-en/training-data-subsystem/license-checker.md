### LicenseChecker Verification

#### Verification Approach

`LicenseChecker` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingDataSubsystem/LicenseCheckerTests.cs`. Each test builds the
small valid data folder of the dataset fixture, loads it with the real `TextEnDataSet`, changes rows, files or names,
and runs the checker with a real `CheckReport`. Files are real files in a temporary folder because the checker
enumerates the folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds and deletes its own temporary data folder

#### Acceptance Criteria

**Allowed licenses** (`AllowedLicenses`). The valid fixture gives no finding and a count of the rows per license. A
restricted license on a row, a generated row that is not `CC0-1.0`, a row of the American National Corpus source that is
not `CC-BY-3.0-US` and a harvested row without a provenance record each give a finding, while an unknown source is left
to the schema check and gives no license finding.

**Restricted content** (`RestrictedContent`). A restricted term (including the word Misaki) in a provenance table or in
the source column of a data row gives a finding with its line, while the same term inside a sentence gives none; a file
named after an evaluation-only corpus gives a finding, while the mapping table and the local cache folders are not
reported.

**Attribution** (`Attribution`). A `CC-BY-3.0-US` row without an attribution file, or with one that does not name the
corpus, gives a finding, and no such row requires no attribution file; a readme without a `License:` line, a readme with
a disallowed license and a missing audit summary each give a finding.

#### Test Scenarios

##### Allowed licenses

**Tests**: `LicenseChecker_Run_ValidFixture_ReportsNothingAndCountsLicenses`,
`LicenseChecker_Run_RestrictedLicenseOnRow_ReportsFinding`, `LicenseChecker_Run_GeneratedRowNotCc0_ReportsFinding`,
`LicenseChecker_Run_HarvestRowWithoutProvenance_ReportsFinding`,
`LicenseChecker_Run_UnknownSource_IsReportedOnlyBySchemaCheck`

The generated-row test changes two rows whose new licenses are on the allow-list, a generated row and an American
National Corpus row, so only the source policy rejects them.

##### Restricted content

**Tests**: `LicenseChecker_Run_MisakiTermInProvenanceTsv_ReportsFindingWithLine`,
`LicenseChecker_Run_RestrictedTermInSentence_IsNotReported`, `LicenseChecker_Run_EvaluationOnlyPath_ReportsFinding`,
`LicenseChecker_Run_MappingFileAndLocalCaches_AreNotReportedAsEvaluationPaths`

##### Attribution

**Tests**: `LicenseChecker_Run_AttributionFileMissing_ReportsFinding`,
`LicenseChecker_Run_NoAttributionRows_DoesNotRequireAttributionFile`,
`LicenseChecker_Run_ReadmeWithoutLicenseLine_ReportsFinding`, `LicenseChecker_Run_AuditSummaryMissing_ReportsFinding`

The attribution test checks a missing file and a file that does not name the corpus; the readme test checks a readme
without a license line and one with a disallowed license.
