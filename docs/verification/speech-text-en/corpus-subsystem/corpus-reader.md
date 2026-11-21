### CorpusReader Verification

#### Verification Approach

`CorpusReader` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Trainer.Tests/CorpusSubsystem/CorpusReaderTests.cs`. The tests tokenize
short bodies with the real analyzer tokenizer (the unit's documented dependency, since matching it is the point) and
compare the sentences, the normalized forms and the eligibility with values worked out by hand. Nothing is mocked.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK, invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Trainer.Tests`
- **Not used**: files and the network

#### Acceptance Criteria

**Tokenization.** Paragraphs split at blank lines with lines joined by spaces; sentences and token spans are the
analyzer's and do not depend on the thread count; the normalization maps the curly apostrophe, lower-cases only ASCII
capitals and gives `<num>` for digits without letters; forms over 40 characters or with a character outside the
eligible set are not eligible.

#### Test Scenarios

##### Tokenization

**Tests**: `CorpusReader_Paragraphs_BlankLines_SplitAndJoin`, `CorpusReader_Normalize_RuntimeRule`,
`CorpusReader_NormalizeEligible_Boundaries`, `CorpusReader_Tokenize_DifferentThreadCounts_SameBooks`

`Don’t` becomes `don't`, a word that starts with the capital E with acute accent keeps that capital, `1,234` becomes
`<num>` and `4TH` becomes `4th`. The 41-letter form, U+0101 and U+00A0 are not eligible; the 40-letter form and U+00FF,
U+2010, U+2027, `!` and `~` are. Eight books of five two-sentence paragraphs tokenized with one and with four threads
keep the input order, split "The w0 runs. A w0 sleeps." into its two sentences, count 320 tokens and give identical
paragraph texts and token spans.
