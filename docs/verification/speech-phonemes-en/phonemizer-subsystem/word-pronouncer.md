### WordPronouncer Verification

#### Verification Approach

`WordPronouncer` is verified by unit tests in `test/DemaConsulting.Speech.Phonemes.En.Tests/PhonemizerSubsystemTests.cs`
(the `WordPronouncer_*` tests) that construct it over the real lexicon, reading tables and letter-to-sound converter,
which its design documents as its collaborators, and call `Pronounce` for single words, asserting the pronunciation and
the source.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Isolation**: no files or network; the pronouncer is immutable

#### Acceptance Criteria

`record` with the reading `record.noun` is `R EH1 K ER0 D` from the reading, without a reading `R AH0 K AO1 R D` from
the lexicon; `unfollowed` is `AH0 N F AA1 L OW0 D` from the morphology; `Zorblax` comes from the letter-to-sound model;
`unfollow` is derived by prefix and stem with the source morphology and `under` stays a dictionary word; `café` equals
`cafe`; `full-time` is `F UH1 L T AY2 M` (a primary stress on the first part only, the compound break after `full`),
`state-of-the-art` has no primary stress on `of` and `the`, `wi-fi` is the closed dictionary spelling; `co-operate` and
`non-fiction` take the stress of the closed spelling (the primary stress on the root) and `re-enter`, `pre-war`,
`ex-wife` keep the primary stress on the root after the weak prefix (the compound golden of the rendering subsystem pins
all of them); `stronger` is `S T R AO1 NG G ER0` (the dictionary lists it with and without the `G`) and `singer` is
`S IH1 NG ER0`; and a word with characters outside the Latin alphabet has no pronunciation; `nobly` is `N OW1 B L IY0`
with the source lexicon where the dictionary lists `N AA1 B L IY0`, which a pronouncer built without corrections still
returns.

#### Test Scenarios

##### Priority list

**Tests**: `WordPronouncer_Pronounce_ByPriority_ReportsTheSourceUsed`,
`WordPronouncer_Pronounce_ComparativeInNg_TakesTheVariantWithTheVelarStop`,
`WordPronouncer_Pronounce_LexiconCorrection_BeatsTheDictionaryEntry`

##### Productive prefix

**Test**: `WordPronouncer_Pronounce_ProductivePrefix_IsPrefixPlusKnownWord`

##### Diacritics, compounds and non-words

**Test**: `WordPronouncer_Pronounce_DiacriticsCompoundsAndNonWords_FollowTheDocumentedRules`
