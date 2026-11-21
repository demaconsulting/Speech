### ClusterCommand Verification

#### Verification Approach

`ClusterCommand` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ClusteringSubsystem/ClusterCommandTests.cs`. Argument handling is
tested on `TryParse` directly. The modes are run as a function with string writers through the clustering fixture
(`Fakes/ClusteringFixture.cs`), whose fake mirror and recording sleeper replace the network and the pause; the other
units of the subsystem are used as they are. The dispatch is tested through the trainer's `Program.Run`.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test has its own temporary repository layout, deleted on disposal
- **Not used**: the network and any real book

#### Acceptance Criteria

**Usage.** `--audit --write` writes removals with the default 2,000 ms pause, and inline values are accepted; every
invalid combination or value (the retired modes and options included) is refused with its reason; help exits 0 and
lists only the fetch and audit modes, and a usage error exits 2; a cache in the repository outside the ignored folder
is refused before any request; `cluster` is dispatched and listed by the trainer.

**Fetch.** `--write-pins` records each book's pin with the requested pause; without it the list is unchanged, the
missing pins are reported and a failed book exits 1.

**Audit.** The audit exits 1 while the removals are missing and 0 once written, printing the counts.

#### Test Scenarios

##### Usage

**Tests**: `ClusterCommand_TryParse_AuditWrite_WritesRemovals`, `ClusterCommand_TryParse_InvalidArguments_ReportReason`,
`ClusterCommand_TryParse_FromSnapshot_IsUnknownOption`, `ClusterCommand_Run_HelpAndUsageError`,
`ClusterCommand_Run_CacheInsideRepository_IsRefused`, `Program_Run_Cluster_IsDispatched`

The theory covers no mode, two modes, `--write` without a mode, `--write-pins` with `--audit`, `--write` with
`--fetch`, a 500 ms pause, zero threads, a disallowed mirror, a missing value, the retired `--clusters` option and
`--check` mode, and an unknown option, each with its reason. The retired `--from-snapshot` option is refused as an
unknown option by the parser and by a run, which exits 2 without a request.

##### Fetch and audit

**Tests**: `ClusterCommand_Run_FetchWritePins_RecordsPins`,
`ClusterCommand_Run_FetchWithoutPins_LeavesListAndReportsFailure`, `ClusterCommand_Run_Audit_ReportsMissingThenCurrent`

The pin of book 202 equals the extracted body's; every pause is 1,500 ms. Without book 303 on the mirror the run exits 1
with "1 failed" and the list is unchanged. The audit prints `dev.tsv exact: 0 corpus sentences`.
