### ClusterCorpusReader Verification

#### Verification Approach

`ClusterCorpusReader` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ClusteringSubsystem/ClusterCorpusReaderTests.cs`. The tests tokenize
short bodies with the real analyzer tokenizer (the unit's documented dependency, since matching it is the point) and
compare the counts, the vocabulary and the hashes with values worked out by hand. Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Not used**: files and the network

#### Acceptance Criteria

**Tokenization.** Paragraphs split at blank lines with lines joined by spaces; the normalization maps the curly
apostrophe, lower-cases only ASCII capitals and gives `<num>` for digits without letters; forms over 40 characters or
with a character outside the eligible set are not words; an ineligible token breaks adjacency.

**Runtime keys.** Every vocabulary word of a sentence with curly quotes, an apostrophe, a number, a dash, an accented
word and an ampersand hashes to the analyzer's key of itself.

**Counts.** Words and within-sentence pairs are counted as integers in the documented order; the minimum count and the
limit cut the vocabulary and the counts of cut forms are kept; the coverage is in ten-thousandths, rounded down.

**Removals and determinism.** A skipped sentence is not counted and is reported; a stale removal (changed text, missing
sentence, unknown book) throws; one and four threads give the same counts.

#### Test Scenarios

##### Tokenization

**Tests**: `ClusterCorpusReader_Paragraphs_BlankLines_SplitAndJoin`, `ClusterCorpusReader_Normalize_RuntimeRule`,
`ClusterCorpusReader_NormalizeEligible_Boundaries`, `ClusterCorpusReader_Count_IneligibleToken_BreaksAdjacency`

`Don’t` becomes `don't`, a word that starts with the capital E with acute accent keeps that capital, `1,234` becomes
`<num>` and `4TH` becomes `4th`. The 41-letter form, U+0101 and U+00A0 are not eligible; the 40-letter form and U+00FF,
U+2010, U+2027, `!` and `~` are. A Greek word between "cat" and "dog" is counted as a token but leaves no pair.

##### Runtime keys and counts

**Tests**: `ClusterCorpusReader_Count_Words_HashToRuntimeKey`,
`ClusterCorpusReader_Count_SimpleCorpus_CountsWordsAndBigrams`,
`ClusterCorpusReader_Count_MinCountAndMaxWords_CutVocabulary`, `ClusterCorpusReader_CoveragePerTenThousand_FirstWords`

"The dog runs. The cat runs." gives the words `.`, `runs`, `the`, `cat`, `dog` with counts 2, 2, 2, 1, 1, eight tokens,
two sentences, six pair tokens of five pair types. In "a a a b b c a b" a minimum count of 2 keeps `a` and `b`, a limit
of 1 keeps `a` with two pairs, and `c` keeps its count of 1. In "a a a b b c" the coverage of 1, 2 and 100 words is
5,000, 8,333 and 10,000.

##### Removals and determinism

**Tests**: `ClusterCorpusReader_Count_SkippedSentence_IsNotCounted`,
`ClusterCorpusReader_Count_StaleSkippedSentence_Throws`, `ClusterCorpusReader_Count_DifferentThreadCounts_SameCounts`

Skipping "Skip that one." removes one sentence of four tokens and the word "skip". A wrong hash, a paragraph that does
not exist and an unknown book each throw `InvalidDataException`. Eight books tokenized and counted with one and four
threads give equal words, counts and pair lists.
