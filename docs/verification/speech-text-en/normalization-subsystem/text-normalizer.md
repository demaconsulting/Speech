### TextNormalizer Verification

#### Verification Approach

`TextNormalizer` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/TextNormalizerTests.cs`. Its collaborators (the
abbreviation resolver, the acronym recognizer and the alignment model) are real, with the embedded lexicons, so the
tests check the rule order and the period and sentence rules as they work in production. Token offsets come from the
real tokenizer of the part-of-speech package through the test helper `Fakes/NormalizationHarness.cs` (a test-only
dependency; no tagger is involved), because the rules are written against the tokenizer's real token shapes. Tests that
need exact control (cancellation, argument validation) hand-build `NormalizationToken` arrays, and cancellation is
triggered deterministically from the normalizer's token-visited observer (a test seam) at a chosen early token, without
asserting how many tokens were visited. The directive tests pass hand-built directives, placed on tokens of the real
tokenizer by token text, so the normalizer is tested without the dictionary. Most tests assert the exact spoken text
that results, obtained by applying the spans to the source. The inputs and expected texts of the symbol, mark, slash,
apostrophe, numeric-separation, shouting-sentence and abbreviation rows live in the golden files
`TestData/normalization-cases.tsv` and `TestData/number-cases.tsv`, which the subsystem tests run (see
*NormalizationSubsystem Verification*); only a small inline theory of each rule remains here.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Dependencies**: the real tokenizer through `Fakes/NormalizationHarness.cs`; no network, files or hardware
- **Isolation**: each test builds its own inputs; the cancellation tests create their own token sources

#### Acceptance Criteria

**Spans.** Each replacement lies on the exact characters of one token (with its period token where the abbreviation
rules say so), so the spans name the source characters. The numeric recognizer runs after the invisible-character and
clitic rules and before the abbreviation rule (`5 ft.` is a unit, `Ft. Worth` is still Fort), and a token a multi-token
match consumed is spoken once. An invisible-character run is dropped as one empty span while the emoji joiners stay. A
replacement is spelled exactly when its spoken text is a letter sequence, an ampersand initialism is decomposed into
spelled letter spans and an `and` span with unchanged spoken text and the possessive tail outside every span, and
adjacent replacements are separated by exactly one space. An empty text normalizes to nothing.

**Web addresses and identifiers.** A URL, an e-mail address and a bare domain are spoken as their pieces before the
clitic, abbreviation and acronym rules can take the token, an identifier of capital letters and digits has its
letters spelled and its digits read, a name joined to a number by a hyphen has its number read, and a file name or a
dotted name with an unknown ending is left as written.

**Periods and breaks.** A suffix abbreviation keeps its period at a sentence end and consumes it mid-sentence in both
token shapes, and forces a break only before a capitalized next word; a prefix title consumes its period and removes
the tokenizer break after it; the word reading of `No.` adds a break but no span; a name suffix before a credential
keeps the name in one sentence while a sentence starter still ends it; a dotted initialism needs its period token; and
each ambiguous resolution is counted once.

**Symbols and apostrophes.** Operators are spoken only where whitespace or a text edge sets them apart, marks are spoken
alone or touching a word, a slash only between two plain words; typographic apostrophes become plain ones inside clitics
and elisions while quote marks stay; the clitic roles follow the documented classification and are recorded only on
request.

**Directives.** A directive wins over every built-in rule for the spoken form of its token while the sentence-break
edits of the abbreviation rules are still made, so the reconciled breaks equal those without directives: a skip and a
reading directive emit nothing, a replace directive emits one override span, a spell directive spells ASCII letters (and
leaves anything else as written, counted as ignored), and a pronounce directive adds a mark without a span. A directive
on a consumed token is ignored and counted, applied and ignored directives are counted separately, and unordered,
repeated, negative or out-of-range directive indices are rejected.

**Contract.** A pre-canceled token and a cancellation requested mid-pass throw `OperationCanceledException`; invalid
arguments and token lists that do not describe the text are rejected; repeated normalization is deterministic and a
custom lexicon changes the outcome.

#### Test Scenarios

##### Spans, numeric rule and separation

**Tests**: `TextNormalizer_Normalize_SuffixPeriod_IsKeptAtSentenceEndAndConsumedMidSentence`,
`TextNormalizer_Normalize_EmptyTextAndTokens_ProducesNothing`,
`TextNormalizer_Normalize_NumericTokens_AreRecognizedBeforeAbbreviations`,
`TextNormalizer_Normalize_InvisibleCharacters_AreDroppedExplicitly` (a three-row theory),
`TextNormalizer_Normalize_SpelledReplacements_AreDecidedByShape`,
`TextNormalizer_Normalize_AmpersandInitialisms_AreDecomposedWithUnchangedSpokenText`,
`TextNormalizer_Normalize_AmpersandInitialismNextToReplacement_PadsExactlyOneSpace`

The spans name the exact source characters: `St` (period kept) at a sentence end and `St.` mid-sentence, `Ave.`
(consumed) and `Blvd` (kept) for the word-plus-period token shape. `5 Ft. Worth` reads `five feet. Worth` while `No. 5`
reads both readings. In a sentence with `FBI`, `U.S.`, `Dr.`, `&`, `J.`, `5 p.m.`, `$5`, `50%` and `3 kg`, exactly
`FBI`, `U.S.`, `J.` and `p.m.` are spelled and every spelled value is a letter sequence. `AT&T's` yields the spelled
span `A T`, the symbol span `" and "` and the spelled span `T`, with the spoken text `A T and T's`; with a trademark
mark directly before `AT&T` the result is `trademark A T and T` with no double space. The numeric-separation,
multi-token and invisible-character-in-number rows (`50%`, `5 million USD`, `5–10`, a number with a zero-width space)
are golden rows.

##### Web addresses and identifiers

**Tests**: `TextNormalizer_Normalize_WebAddresses_AreSpokenAsTheirPieces` (a five-row theory),
`TextNormalizer_Normalize_LettersAndDigits_AreSpelledAndRead` (a three-row theory)

`Visit https://www.example.com/docs?id=7 today.` becomes
`Visit H T T P S:W W W dot example dot com slash docs question mark id equals 7 today.` (the colon after the scheme
stays, the two slashes are dropped); `Mail john.smith@example.com, now.` becomes
`Mail john dot smith at example dot com, now.`; the final period of `example.com.` stays the sentence's own;
`report.pdf` and `Node.js` have no span. `A4`, `B12`, `H2O` and `MP3` are read `A four`, `B twelve`, `H two O` and
`M P three`, `COVID-19` and `Catch-22` become `COVID nineteen` and `Catch twenty two`, `SCP-184` and `F-16` become
`S C P one hundred eighty four` and `F sixteen`, and `COVID19` is left alone; every spelled span is a letter sequence
and every other span is not. The remaining address and identifier rows are golden rows.

##### Period and sentence rules

**Tests**: `TextNormalizer_Normalize_PrefixTitle_ConsumesPeriodAndSuppressesTokenizerBreak`,
`TextNormalizer_Normalize_SuffixBeforeNextWord_ForcesBreakOnlyBeforeCapitalizedWord`,
`TextNormalizer_Normalize_WordReading_ForcesBreakWithoutSpan`,
`TextNormalizer_Normalize_DottedInitialismWithSeparatePeriod_IsHandled`,
`TextNormalizer_Normalize_NameSuffixBeforeCredential_ConsumesPeriodAndKeepsNameTogether`,
`TextNormalizer_Normalize_AmbiguousResolutions_AreCountedOnce`

For `Sen. Smith spoke.` the tokenizer break is suppressed and nothing is left after reconciliation.
`Maple Dr. He likes it.` forces the break before `He`, while `Maple Dr. is nice.` and `Maple Dr. 5 times.` force none.
`No. I don't know.` has no span and one forced break. In `John Smith Jr. Ph.D. spoke.` the span of `Jr.` includes its
period and no break is forced; in `Ann Lee Ph.D. M.D. spoke.` the tokenizer's break between the credentials is
suppressed; in `I met John Smith Jr. He left.` the only break is before `He`. `5 p.m. Then sleep.` yields the number
span `five` and one `Initialism` span `P M`, and `x.y` yields none. The ambiguity count is 0 for clear readings and 1
per inconclusive `They St.`. The unit-period rows (`6 ft. tall`, `He is 6 ft. Then he left.`, `180 lbs.`) and the
shouting rows (`The US team won. LET US GO NOW.`) are golden rows.

##### Symbols and apostrophes

**Tests**: `TextNormalizer_Normalize_Operators_AreSpokenOnlyWhenSetApart` (a four-row theory),
`TextNormalizer_Normalize_Marks_AreSpoken` (a three-row theory),
`TextNormalizer_Normalize_AdjacentMarks_PadExactlyOneSpaceAndKeepOffsets`,
`TextNormalizer_Normalize_Slash_IsSpokenOnlyBetweenWords` (a three-row theory),
`TextNormalizer_Normalize_NumberSign_IsTheWordNumberOnlyAfterAWordBeforeAColonOrNumber` (a seven-row theory),
`TextNormalizer_Normalize_Apostrophes_AreCanonicalizedOnlyInsideWords` (a four-row theory),
`TextNormalizer_Normalize_Clitics_AreClassifiedFromContext` (one row per role plus negatives),
`TextNormalizer_Normalize_CliticsNotRequested_RecordsNoCliticsButStillCanonicalizes`

Examples: `Item #: SCP-184` becomes `Item number: S C P one hundred eighty four` and `Case # 12` `Case number twelve`,
while `# 5 Reasons`, `Item # Title`, a `#` at the start of a line and `5 # 6` keep the sign; `a & b` becomes `a and b`
but `a=b` and `(+)` stay; `Brand™ rocks` becomes `Brand trademark rocks`; the two replacements of `A™®B` are
`" trademark"` and `" registered "` at offsets 1 and 2 with no double space; `he/she` becomes `he slash she` while
`km/h` and `24/7` stay. Contractions and elisions get the plain apostrophe while a trailing `dogs’` and `‘quoted’` stay.
The classification theory asserts exactly one clitic mark per row, on the expected token, with the documented role
(`n't` Not, `'re` Are, `'ve` Have, `'ll` Will, `'m` Am, `'d` Would or Had, `'s` as Us, Is, Has and Possessive). When
roles are not requested no mark is recorded but the apostrophes are still canonicalized. The remaining operator, mark,
slash and apostrophe forms are golden rows.

##### Directives

**Tests**: `TextNormalizer_Normalize_SkipDirective_EmitsNothingAndCountsApplied`,
`TextNormalizer_Normalize_ReadingDirective_SpokenAsWrittenAndCountsApplied`,
`TextNormalizer_Normalize_ReplaceDirective_EmitsOverrideSpanWithSinglePadding`,
`TextNormalizer_Normalize_SpellDirective_SpellsAsciiLettersOnly`,
`TextNormalizer_Normalize_PronounceDirective_AddsMarkWithoutSpan`,
`TextNormalizer_Normalize_DirectiveOnBuiltInToken_BeatsEveryBuiltInRule` (a four-row theory),
`TextNormalizer_Normalize_DirectiveOnConsumedToken_IsIgnoredAndCounted` (a two-row theory),
`TextNormalizer_Normalize_MixedDirectives_CountAppliedAndIgnoredSeparately`,
`TextNormalizer_Normalize_InvalidDirectiveIndices_ThrowArgumentException` (a five-row theory)

A skip directive on `US` in `US troops` leaves no span and no mark, counts one applied override and leaves the text as
written; a reading directive (value `record.noun`) behaves exactly like a skip, so the reading id never reaches the
spoken text. A replace directive on `Nginx` yields one `Override` span at offset 4, length 5, not spelled, with the
value `engine ex`, and after a trademark mark it gets exactly one separating space. A spell directive on `gif` yields
the spelled span `G I F`; on `café` it yields no span and one ignored override. A pronounce directive on `Polish` yields
a mark with the exact token range and value, no span and an unchanged text. On a symbol-bearing token, a lexicon
abbreviation, a listed acronym and an ampersand initialism a skip directive leaves no built-in span inside the token.
For a token consumed by a number match (a unit, a magnitude word) the spans equal those without the directive and one
override is ignored. Four skip directives over `US 5 kg FBI and $3 million` count two applied and two ignored. The
indices of the invalid-index theory (repeated, descending, negative, too large, extreme) are rejected without arithmetic
on the index.

##### Directive sentence breaks

**Tests**: `TextNormalizer_Normalize_DirectiveOnAbbreviation_KeepsBuiltInSentenceBreaks` (a five-row theory),
`TextNormalizer_Normalize_DirectiveOnFig_ReviewExampleHasNoSentenceBreak`,
`TextNormalizer_Normalize_DefectiveDirective_StillKeepsBuiltInSentenceBreaks` (a three-row theory)

For every row and for each of the four directive kinds (skip, replace, spell, pronounce) the reconciled breaks equal
those of the text without the directive and the sentences match the pinned literals (`See Fig. 5 for details.` is one
sentence; `We saw the plot in Fig. It was good.` splits after `Fig.`; `Hello—Dr. Smith left.` keeps its suppression).
The spans come from the directive alone and never cover a separate period token. The review example has the tokenizer
break suppressed with and without a `Say` directive. A defective directive (a spell-out of a token that is not ASCII
letters, a replacement or pronunciation without a value) is ignored and counted and still keeps the built-in breaks. The
seeded differential test over random abbreviation-rich text lives at subsystem level (see *NormalizationSubsystem
Verification*).

##### Contract: cancellation, arguments and determinism

**Tests**: `TextNormalizer_Normalize_PreCanceledToken_ThrowsOperationCanceledException`,
`TextNormalizer_Normalize_CanceledMidPass_ThrowsOperationCanceledException`,
`TextNormalizer_NormalizeAndConstructor_InvalidArguments_Throw`,
`TextNormalizer_Normalize_TokenRangeOverflowingOrOutsideText_ThrowsArgumentException` (a three-row theory),
`TextNormalizer_Normalize_SecondTokenOverflowing_ThrowsArgumentException`,
`TextNormalizer_Normalize_RepeatedAndCustomLexicons_AreDeterministic`

A canceled token throws before any work. For 1000 hand-built one-letter tokens an observer cancels the token while an
early token is visited, and the pass throws `OperationCanceledException` instead of returning a result; how many tokens
are visited is not asserted. A null collaborator or text throws `ArgumentNullException`; an empty token, overlapping
tokens, a token outside the text and a token whose end would overflow throw `ArgumentException`. Two runs over one text
agree, and a normalizer over a custom lexicon in which only `Dr.` is listed reads
`Doc Smith and Mr. Jones live on Main St.` while the default reads `Doctor Smith and Mister Jones live on Main Street.`
Cancellation during a numeric pass and under many directives is observed at the subsystem level.
