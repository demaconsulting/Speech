### WordClusterTable Verification

#### Verification Approach

`WordClusterTable` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/ClusteringSubsystem/WordClusterTableTests.cs`. The tests build tables
from word lists written in code, including two words that share a 32-bit lookup hash (the first collision of FNV-1a
over UTF-16 code units among the words `w0`, `w1`, `w2` and so on, found by a deterministic search in the test and
asserted there), and parse hand-written table texts. Nothing is
mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Not used**: files and the network

#### Acceptance Criteria

**Collisions.** Both words of a shared hash are left out and counted as two, the other words are kept in ordinal order;
an ineligible word is left out and counted.

**Format.** The written text is the exact header and ordinal rows and parses back equal; each broken table (header,
order, path alphabet, prefix, path count, count minimum, shared hash, CR, eligibility) throws; inconsistent build input
throws.

#### Test Scenarios

##### Collisions

**Tests**: `WordClusterTable_Build_SharedHash_DropsEveryWordOfThatHash`,
`WordClusterTable_Build_IneligibleWord_IsDroppedAndCounted`

From `the`, `cat` and the two colliding words only `cat` and `the` remain, with two collisions and no ineligible word.
From `dog` and a word with U+0101 only `dog` remains, with one ineligible word.

##### Format

**Tests**: `WordClusterTable_WriteAndParse_RoundTrip`, `WordClusterTable_Parse_BrokenTables_Throw`,
`WordClusterTable_Build_InconsistentInput_Throws`

`the`, `<num>`, `Zebra` and `don't` are written as
`word\tpath\tcount\n<num>\t01\t30\nZebra\t10\t20\ndon't\t11\t10\nthe\t00\t40\n` and parse back equal. Nine broken texts
each throw `InvalidDataException`. Lists of different lengths and a repeated word throw `ArgumentException`.
