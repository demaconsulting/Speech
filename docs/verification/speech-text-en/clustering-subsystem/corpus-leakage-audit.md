### CorpusLeakageAudit Verification

#### Verification Approach

`CorpusLeakageAudit` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ClusteringSubsystem/CorpusLeakageAuditTests.cs`. The tests tokenize
short bodies with the real reader (its documented dependency) and pass evaluation sentences directly, so each rule is
exercised by a sentence written for it. The loading of evaluation sentences uses the data fixture
(`Fakes/DatasetFixture.cs`). Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Isolation**: the data fixture is a temporary folder, deleted on disposal

#### Acceptance Criteria

**Rules.** An exact repeat in other case and punctuation is removed with its location and text hash; a near repeat of a
13-word sentence with one extra word is removed and a short sentence sharing its start is not; a 16-word sentence quoted
across two corpus sentences removes both by the contains rule and leaves the unrelated one; a 6-word sentence inside a
longer corpus sentence is removed by the contains rule.

**Attribution.** A sentence matching several evaluation sentences names the development file and the ordinally first id;
one and four threads give equal removals.

**Counts, file and evaluation sentences.** The counts are per file and rule with distinct evaluation sentences and
books; the removals file round-trips and malformed or unsorted files are refused; the evaluation sentences of the
fixture are the eight rows of the five evaluation files and no training row, and the listing hash changes when a file
changes.

#### Test Scenarios

##### Rules

**Tests**: `CorpusLeakageAudit_Run_ExactRepeat_IsRemoved`, `CorpusLeakageAudit_Run_NearRepeat_IsRemoved`,
`CorpusLeakageAudit_Run_QuotedAcrossSentences_ContainsRemovesBoth`,
`CorpusLeakageAudit_Run_ShortEvaluationInsideSentence_IsRemoved`

"THE WIND HOWLED, ALL NIGHT!" in paragraph 1 is removed as exact, attributed to `dev.tsv` d1, with the SHA-256 of its
text. The near, contains-across-sentences and short-contains cases each give exactly the expected sentences and rules.

##### Attribution

**Tests**: `CorpusLeakageAudit_Run_SeveralMatches_FirstFileThenLowestId`,
`CorpusLeakageAudit_Run_DifferentThreadCounts_SameRemovals`

With the same sentence in the generalization, development (ids d9 and d10) and regression files, the removal names
`dev.tsv` d10. Six books each with an exact repeat in two paragraphs give 12 equal removals with one and four threads.

##### Counts, file and evaluation sentences

**Tests**: `CorpusLeakageAudit_Counts_PerFileAndRule`, `CorpusLeakageAudit_WriteAndParse_RoundTripAndValidation`,
`CorpusLeakageAudit_LoadEvaluationSentences_FixtureData_FiveFilesNoTraining`

Four removals give 15 counts with (3 sentences, 2 evaluation sentences, 2 books) for `dev.tsv` exact. Written removals
parse back equal; unsorted rows, an unknown rule, a training file and a wrong header throw. The fixture yields eight
sentences starting with the two development rows, none from `train.tsv`, and adding a development row changes the
listing hash.
