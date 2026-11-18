### EvaluateExternalCommand Verification

#### Verification Approach

`EvaluateExternalCommand` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/EvaluateExternalCommandTests.cs`. The
tests call the command as a function through the command harness (`Fakes/CommandHarness.cs`) with string writers for the
output and error streams, over the evaluation rig (`Fakes/ExternalEvaluationRig.cs`): a training fixture with a trained
model, hand-written fake external sources with their pins and mapping files, and a cache folder outside the fixture's
repository folder. The real units run; only the network is replaced by an archive fetcher delegate that serves the fake
archives and counts its calls. The tests observe only the exit code, the printed lines, the files of the cache and of
the record, and the number of fetches. The fake datasets contain no real dataset text.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: each test builds its own training fixture, model folder, cache and record folder in temporary folders
  and deletes them afterwards
- **Not used**: the network and any real dataset

#### Acceptance Criteria

**Usage errors.** `--help` prints the usage and exits 0; an unknown option, an option without a value, an unknown set,
`--write` together with `--check`, a missing pins file, a missing model and a cache folder inside the repository other
than the ignored external folder each exit 2 with a message that names the problem, before any figure; the default
cache and a folder outside the repository are accepted.

**Fetch option.** A run of one set fetches and evaluates only the source that set needs, and a malformed data file exits
2 with no figure.

**Report.** The report prints, for each set, the rows read, used and skipped by reason, the accuracy with its interval,
the three baselines, the improvement with its interval and the parity, and contains no sentence, target word or label of
the fake datasets. With `--seeds` each seed and the mean, minimum and maximum of every set are printed, and two runs
with
different external content print the same model hashes.

**Write and check.** `--write` twice gives identical files; `--check` of a current record exits 0 and of a stale or
missing record exits 1 naming the file.

#### Test Scenarios

##### Usage errors

**Tests**: `EvaluateExternalCommand_Run_Help_PrintsUsageAndExitsZero`,
`EvaluateExternalCommand_Run_BadCommandLine_ExitsTwoNamingTheProblem` (a theory),
`EvaluateExternalCommand_Run_UnknownSetOrWriteWithCheck_ExitsTwo`,
`EvaluateExternalCommand_Run_MissingPinsOrModel_ExitsTwo`,
`EvaluateExternalCommand_Run_CacheInRepositoryOutsideIgnoredFolder_ExitsTwo`,
`EvaluateExternalCommand_Run_DefaultAndOutsideCache_AreAccepted`

The exit code and the text of the output or error stream are checked for each case; a refused cache folder is neither
created nor fetched into.

##### Fetch option

**Tests**: `EvaluateExternalCommand_Run_SetSelection_FetchesAndEvaluatesOnlyNeededSources`,
`EvaluateExternalCommand_Run_MalformedData_ExitsTwoWithNoFigures`

Selecting one dataset leaves the other source out of the fetches, the cache and the output. A pinned file that is not in
the expected format exits 2, names the file and prints no figure. The refusal of tampered caches and bad archives is
verified in the `ExternalSourceCache` unit and at subsystem level.

##### Report and seed study

**Tests**: `EvaluateExternalCommand_Run_Report_PrintsCountsBaselinesAndIntervalsWithoutDatasetText`,
`EvaluateExternalCommand_Run_Seeds_PrintsEachSeedAndSpread`,
`EvaluateExternalCommand_Run_Seeds_AreIndependentOfExternalContent`

The printed lines are checked for the counts and figures of the fixture, and none of the sentences or labels of the fake
datasets occurs in the output. Each seed and the mean, minimum and maximum of each set are printed; two runs over fake
datasets with different contents print the same model hash for each seed, which shows that the external rows have no
influence on the retrained models.

##### Write and check

**Tests**: `EvaluateExternalCommand_Run_WriteTwice_ProducesIdenticalFiles`,
`EvaluateExternalCommand_Run_CheckStaleOrMissingRecord_ExitsOneNamingFile`

Two writes give identical bytes; the check of the written record exits 0; after a record file is edited or removed the
check exits 1 and names that file, and a missing record names all seven files.
