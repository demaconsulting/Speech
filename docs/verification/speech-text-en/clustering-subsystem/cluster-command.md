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

**Usage.** `--write` alone selects the write mode with no cluster count given (512 is the default), the processor count
of threads and a 2,000 ms pause; `--audit --write` writes removals and inline values are accepted; every invalid
combination or value is refused with its reason; help exits 0 and a usage error exits 2; a cache in the repository
outside the ignored folder is refused before any request; `cluster` is dispatched and listed by the trainer.

**Fetch.** `--write-pins` records each book's pin with the requested pause; without it the list is unchanged, the
missing pins are reported and a failed book exits 1.

**Audit and preflight.** The audit exits 1 while the removals are missing and 0 once written, printing the counts; the
preflight prints the coverage of each size and each word's count, rank and cuts or "too rare".

**Write and check.** A write without removals exits 1 and writes no table; a check without a record exits 2; a write
with explicit settings prints progress, the total time with the thread count and the table hash, records the settings,
and a following check is current until the record is edited, then stale. A check with a current snapshot reports it
current and leaves every file of the cache with the same bytes and time; a check with the snapshot deleted reports it
missing and with one byte of it changed reports it stale (exit 1 both times, the table and record current), and neither
check creates, replaces or repairs a file of the cache.

#### Test Scenarios

##### Usage

**Tests**: `ClusterCommand_TryParse_Write_UsesDefaults`, `ClusterCommand_TryParse_AuditWrite_WritesRemovals`,
`ClusterCommand_TryParse_InvalidArguments_ReportReason`, `ClusterCommand_Run_HelpAndUsageError`,
`ClusterCommand_Run_CacheInsideRepository_IsRefused`, `Program_Run_Cluster_IsDispatched`

The theory covers no mode, two modes, `--write-pins` without `--fetch`, `--words` without `--preflight`, `--clusters`
and `--min-count` with `--check`, a 500 ms pause, zero threads, one cluster, a disallowed mirror, a missing value and an
unknown option, each with its reason.

##### Fetch, audit and preflight

**Tests**: `ClusterCommand_Run_FetchWritePins_RecordsPins`,
`ClusterCommand_Run_FetchWithoutPins_LeavesListAndReportsFailure`, `ClusterCommand_Run_Audit_ReportsMissingThenCurrent`,
`ClusterCommand_Run_Preflight_ReportsWords`

The pin of book 202 equals the extracted body's; every pause is 1,500 ms. Without book 303 on the mirror the run exits 1
with "1 failed" and the list is unchanged. The audit prints `dev.tsv exact: 0 corpus sentences`. The preflight prints
100.00 percent coverage, `word the: count 180, rank 1` in every cut and `word zebra: count 0, too rare`.

##### Write and check

**Tests**: `ClusterCommand_Run_WriteWithoutRemovals_Exits1`, `ClusterCommand_Run_CheckWithoutRecord_Exits2`,
`ClusterCommand_Run_WriteThenCheck_CurrentThenStale`, `ClusterCommand_Run_CheckCurrentSnapshot_LeavesCacheUnchanged`,
`ClusterCommand_Run_CheckMissingOrTamperedSnapshot_ReportsWithoutRepairing`

A write before `--audit --write` prints the instruction and writes nothing. A write with 3 clusters, minimum count 2,
limit 12 and two threads prints `induce: 12/12 words (100.0%)`, the total time `with 2 threads` and the table hash, and
records those settings; the check exits 0, and after the used-book count in the record is edited it exits 1 reporting
the record stale. After a write with 3 clusters, the cache files (relative path, SHA-256 and last write time) are
listed: a check prints `snapshot current` and the list is unchanged; with `cluster-corpus.tar` deleted a check prints
`clustering.json current, snapshot missing` and the list is unchanged (no archive appears); with one byte in the middle
of the archive flipped a check prints `clustering.json current, snapshot stale`, and the list and the archive's bytes
are unchanged.
