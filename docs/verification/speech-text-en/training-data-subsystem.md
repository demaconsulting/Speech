## TrainingDataSubsystem Verification

### Verification Approach

The SpeechTextEn TrainingDataSubsystem is verified through deterministic tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests` that need no network, no hardware and no evaluation corpus. The
subsystem-level tests in `TrainingDataSubsystemTests.cs` run the real `CheckDataCommand` with its real units over a
small valid data folder that the fixture (`Fakes/DatasetFixture.cs`) writes in code into a temporary folder: a few
readings including a noun and verb pair, their forms, the mapping, rows in all four splits with their provenance
records, the documents with their `License:` lines and the attribution, and a computed manifest. No fixture table is
checked in, so the test data needs no review of its own. Each negative test changes exactly one thing in a fresh copy
(one byte, one cell, one file) and, where the manifest would otherwise fail first, rewrites the manifest, so each test
shows one check failing for its own reason. A further test runs the command over the real committed `data/text-en`
folder, found by walking up from the test assembly to the solution file, which proves that the data of the repository
passes every check; it is skipped, and says so, only when the repository root or the data folder is absent from the
checkout. Each unit is verified in isolation by its own test file (see the unit verification documents); the subsystem
tests verify that the units run in the stated order, share one report and agree on the exit codes.

### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline; the command itself is also run by `lint.ps1`
  and by the build job on every operating system of the matrix
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: every test builds its own temporary data folder and deletes it afterwards; output is captured by
  string writers; the environment is injected, so no test reads the real environment variables
- **Fixtures**: the dataset fixture, which can edit one row or file and recompute the manifest, defined in the test
  project

### Acceptance Criteria

The subsystem tests pass when: a valid fixture passes every check with exit code 0 and the summary
`check-data: OK (0 findings)`; the real committed data folder passes with the same result; each of twelve single
violations (a changed file, a missing final newline, an unknown reading, a span off by one, a duplicate identifier, a
restricted license, a generated row that is not CC0, a missing attribution file, an evaluation-only file name, a
duplicate sentence, a document across splits and a template across files) gives exit code 1 with a line that names its
check; a changed file is named in a manifest finding until the manifest is regenerated and then passes; supplying an
evaluation folder with a sentence equal to a row gives an exact-match finding of the external leakage check; and running
without an evaluation folder passes and says that the external check was skipped.

### Test Scenarios

#### Valid data passes

**Tests**: `TrainingDataSubsystem_CheckData_ValidFixture_PassesAllChecks`,
`TrainingDataSubsystem_CheckData_CommittedData_PassesAllChecks`

The command runs over the valid fixture and over the committed folder; both give exit code 0, no finding and the
summary of success. The committed-folder test is the system-level evidence that the repository's data is open,
consistent, separated and documented.

#### Single violations

**Test**: `TrainingDataSubsystem_CheckData_SingleViolation_ExitsOneNamingTheCheck`

A theory over twelve named mutations of the valid fixture. Each mutation changes one thing, recomputes the manifest
where the manifest is not the subject, and runs the command; the exit code is 1, the output contains a line that names
the expected check (`manifest`, `format`, `schema`, `license` or `leakage`) and the failure summary. The mutations cover
the requirements of the subsystem as follows: the format (a missing final newline), the manifest (a changed file), the
schema (an unknown reading, a span off by one, a duplicate identifier), the licenses (a share-alike license, a generated
row that is not CC0, a missing attribution file, a file named after an evaluation-only corpus) and the separation of the
splits (a duplicate sentence, a document across splits, a template across files).

#### Manifest drift

**Test**: `TrainingDataSubsystem_CheckData_ManifestDrift_NamesFileAndPassesAfterRegeneration`

One byte of the readme is changed without recomputing the manifest; the command exits 1 with a manifest finding that
names the file and its hash; after the manifest is recomputed the same data passes.

#### External evaluation sets

**Tests**: `TrainingDataSubsystem_CheckData_ExternalOverlap_IsReportedWhenSetsSupplied`,
`TrainingDataSubsystem_CheckData_NoEvaluationSets_PassesAndSaysSkipped`

A temporary evaluation folder with a text file that contains a sentence of the data gives an exact-match finding of the
external check; without an evaluation folder the run passes and its output says that the external check was skipped.

See also *SpeechTextEn System Verification* and the unit documents `tsv-table.md`, `text-en-data-set.md`,
`data-manifest.md`, `span-validator.md`, `check-report.md`, `schema-checker.md`, `license-checker.md`,
`leakage-checker.md` and `check-data-command.md`.
