### ClusterInducer Verification

#### Verification Approach

`ClusterInducer` and its input `BigramGraph` are verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ClusteringSubsystem/ClusterInducerTests.cs`. The inputs are built in
code: a golden corpus of 60 sentences over a 29-word vocabulary (determiners, animals, verbs, prepositions, places and
the full stop, chosen by a fixed integer pseudo-random generator), structured synthetic graphs of hidden word classes,
and hand-written pairs. A brute-force **oracle** in the test recomputes the total mutual information from scratch with
the library logarithm for every candidate merge, observed through the inducer's observer seam. Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Platforms**: the golden test runs in every CI job (Windows x64, Linux x64, macOS ARM64), which is how platforms are
  compared

#### Acceptance Criteria

**Classes.** For an 8-word input with three clusters, every maintained loss equals the oracle's within a relative 1e-9
and every chosen merge is an oracle minimum; paths are bit strings, prefix-free and exactly `C` distinct (or one per
word when the vocabulary is smaller).

**Determinism.** The golden corpus gives exactly the recorded paths and merge sequence, losses compared in round-trip
form; one, two, three and eight threads give identical paths and merges.

**Tie-break, progress and input.** With all losses equal the lowest slots merge first; progress reaches the vocabulary
size and the final merges; invalid inputs are refused.

#### Test Scenarios

##### Classes

**Tests**: `ClusterInducer_Induce_SmallInput_LossesMatchBruteForceOracle`,
`ClusterInducer_Induce_SyntheticInput_PathsArePrefixFreeAndBounded`,
`ClusterInducer_Induce_FewerWordsThanClusters_OnePathPerWord`

There is one snapshot per merge; every pair's maintained loss matches the oracle and the chosen pair's oracle loss is
the minimum. Sixty words with seven clusters give seven distinct prefix-free bit strings. Five words with 512 clusters
give five distinct paths built by four final merges.

##### Determinism

**Tests**: `ClusterInducer_Induce_FixedInput_MatchesGoldenPathsAndMerges`,
`ClusterInducer_Induce_DifferentThreadCounts_IdenticalOutput`

The golden corpus with four clusters gives the recorded 29 paths and 28 merges (word, slots and loss, for example the
first merge `0 4 1 3 0` and the last `27 -1 0 2 62.78977725174548`). A 160-word synthetic graph with 12 clusters gives
equal results for 1, 2, 3 and 8 threads.

##### Tie-break, progress and input

**Tests**: `ClusterInducer_Induce_AllLossesEqual_MergesLowestSlotsFirst`,
`ClusterInducer_Induce_WithProgress_ReportsWordsAndFinalMerges`, `ClusterInducer_Induce_InvalidArguments_Throw`,
`BigramGraph_FromPairs_RepeatedPairs_AddsCountsAndMarginals`, `BigramGraph_FromSortedKeys_InvalidKeys_Throw`

Four words without pairs and two clusters merge (0, 1), (0, 1) and then (0, 2), giving the paths 0, 0, 1, 0. Progress
reports include 30 words with no final merge and 30 words with 3 of 3 final merges. One word, one cluster and zero
threads throw `ArgumentException`. Pairs (0,1,2), (1,2,3), (0,1,1), (2,0,4) give three pair types, total 10 and the
expected marginals and lists; unsorted keys, an unknown word and a zero count throw.
