## TrainingDataSubsystem Verification

### Verification Approach

The SpeechTextEn TrainingDataSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests` that need no network, no hardware and no evaluation corpus. The
subsystem tests in `TrainingDataSubsystemTests.cs` run the real `CheckDataCommand` with its real units over a small
valid data folder that the fixture (`Fakes/DatasetFixture.cs`) writes in code into a temporary folder: a few readings
including a noun and verb pair, their forms, the mapping, rows in all four splits with their provenance records, the
documents with their `License:` lines, the attribution and a computed manifest. No fixture table is checked in. Each
negative test changes exactly one thing in a fresh copy and, where the manifest would otherwise fail first, rewrites the
manifest, so each test shows one check failing for its own reason. A further test runs the command over the real
committed `data/text-en` folder, found by walking up from the test assembly to the solution file; it is skipped, and
says so, only when the repository root or the data folder is absent from the checkout. The units are verified in
isolation by their own test files (see the unit verification documents); the subsystem tests verify that they run in the
stated order, share one report and agree on the exit codes.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline; the command itself is
  also run by `lint.ps1` and by the build job on every operating system of the matrix
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: every test builds and deletes its own temporary data folder; output is captured by string writers and
  the environment is injected

### Acceptance Criteria

- **Command contract** (`CheckCommand`, `FileFormat`, `SchemaValidation`, `LicenseGate`, `LeakageGate`): a valid fixture
  and the real committed data pass every check with exit code 0 and the summary `check-data: OK (0 findings)` (the
  fixture run also says that the optional external check was skipped); each of five single violations (a missing final
  newline, an unknown reading, a restricted license, an evaluation-only file name and a duplicate sentence) gives exit
  code 1 with a line that names its check.
- **Manifest** (`FileFormat`): a changed file is named in a manifest finding until the manifest is regenerated, and then
  the data passes.
- **External sets** (`LeakageGate`): an evaluation folder with a sentence equal to a row gives an exact-match finding.

### Test Scenarios

#### Valid data passes

**Tests**: `TrainingDataSubsystem_CheckData_ValidFixture_PassesAllChecks`,
`TrainingDataSubsystem_CheckData_CommittedData_PassesAllChecks`

The command runs over the valid fixture and over the committed folder; both give exit code 0 and no finding, and the
fixture run says that the optional external check was skipped. The committed-folder test is the system-level evidence
that the repository's data is open, consistent, separated and documented.

#### Single violations

**Test**: `TrainingDataSubsystem_CheckData_SingleViolation_ExitsOneNamingTheCheck`

A theory over five named mutations of the valid fixture, one per check family: the format, the schema, the licenses (a
share-alike license and a file named after an evaluation-only corpus) and the separation of the splits (a duplicate
sentence). Each mutation recomputes the manifest and runs the command; the exit code is 1 and the output contains a line
that names the expected check and the failure summary. The finer cases of each check are unit-level tests.

#### Manifest drift

**Test**: `TrainingDataSubsystem_CheckData_ManifestDrift_NamesFileAndPassesAfterRegeneration`

One byte of the readme is changed without recomputing the manifest; the command exits 1 with a manifest finding that
names the file and its hash; after the manifest is recomputed the same data passes.

#### External evaluation sets

**Test**: `TrainingDataSubsystem_CheckData_ExternalOverlap_IsReportedWhenSetsSupplied`

A temporary evaluation folder with a text file that contains a sentence of the data gives an exact-match finding of the
external check.

See also *SpeechTextEn System Verification* and the unit documents `tsv-table.md`, `text-en-data-set.md`,
`data-manifest.md`, `span-validator.md`, `check-report.md`, `schema-checker.md`, `license-checker.md`,
`leakage-checker.md` and `check-data-command.md`.
