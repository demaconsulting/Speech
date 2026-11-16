### TextNormalizer Verification

#### Verification Approach

`TextNormalizer` is verified by automated unit tests in
`test/DemaConsulting.Speech.Text.En.Tests/NormalizationSubsystem/TextNormalizerTests.cs`. Its collaborators (the
abbreviation resolver, the acronym recognizer and the alignment model) are real, with the embedded lexicons, so
the tests check the rule order and the period and sentence rules as they work in production. Token offsets come
from the real tokenizer of the part-of-speech package through the test helper `Fakes/NormalizationHarness.cs`
(a test-only dependency; no tagger is involved), because the rules are written against the tokenizer's real
token shapes. Tests that need exact control (cancellation, argument validation, the observer) hand-build
`NormalizationToken` arrays, and the two that need a different lexicon build a `TextNormalizer` over custom
tables parsed from a string. The directive tests pass hand-built directives, placed on tokens of the real
tokenizer by token text, so the normalizer is tested without the dictionary. Most tests assert the exact spoken
text that results, obtained by applying the spans to the source.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Project**: `test/DemaConsulting.Speech.Text.En.Tests`
- **Dependencies**: the real tokenizer through `Fakes/NormalizationHarness.cs`; no network, files or hardware
- **Isolation**: each test builds its own inputs; the cancellation tests create their own token sources

#### Acceptance Criteria

Standalone operators are spoken only when whitespace or a text edge sets them apart on both sides; marks are
spoken whether they stand alone or touch a word, with a separating space where a letter touches; a slash is
spoken only between two plain words of at least two letters; invisible characters are dropped with one empty
span per run while the emoji joiners are kept, and a token that contains one gets no other rule; typographic
apostrophes become plain ones inside clitics and elisions but quote marks and a trailing apostrophe stay; the
clitic roles follow the documented classification and are recorded only on request; a suffix abbreviation
keeps its period at a sentence end and consumes it mid-sentence in both token shapes; a prefix title consumes
its period and removes the tokenizer break after it; a suffix forces a break only before a capitalized next
word; the word reading of `No.` adds a break but no span; each ambiguous resolution is counted once; the
shouting guard applies per sentence; a dotted initialism needs its period token; a pre-canceled token and a
cancellation observed mid-pass throw `OperationCanceledException`; the observer sees every processed token in
order; invalid arguments are rejected; an empty text normalizes to nothing; and repeated normalization is
deterministic while a custom lexicon changes the outcome. The numeric rule runs after the invisible-character
and clitic rules and before the abbreviation rule (so `5 ft.` is a unit and `Jan. 5` leaves the number to the
numeric rule), emits the spans of a match in order and does not visit a consumed token again, separates
adjacent numeric replacements with exactly one space, applies the suffix period rules to a unit with a period
(consumed mid-sentence, kept with a forced break at a sentence end or before a capitalized word), reads no
token that contains an invisible character as a number, observes cancellation in the middle of a numeric
pass, and allocates nothing per token for lower-case text without digits. A user directive on a token wins over every
built-in rule for the spoken form of that token, while the sentence-break edits of the abbreviation and acronym rules
are still made (see _Directive sentence breaks_): a skip directive emits nothing, a replace directive emits one span of the override
kind padded by at most one space, a spell directive spells ASCII letters (and leaves anything else as written, counted
as ignored), and a pronounce directive adds a pronunciation mark without a span or a change of the spoken text. A
directive on a token that an earlier multi-token match consumed is ignored, counted and leaves the match unchanged;
every directive is counted exactly once as applied or ignored; directives that are out of order, repeated, negative
or beyond the tokens are rejected without arithmetic on the index; the cancellation cadence counts every visited
token with directives present; and thousands of directives over a large text complete within the time budget. The
shape of a replacement decides whether it is spelled (letter-name replacements are, word, symbol and number
replacements are not), an ampersand initialism is decomposed into spelled letter spans and an `and` span with
unchanged spoken text and the possessive tail outside every span, and adjacent replacements are separated by
exactly one space.

#### Test Scenarios

##### Symbols and marks

**Tests**: `TextNormalizer_Normalize_Operators_AreSpokenOnlyWhenSetApart` (a theory over all fifteen
operators and the cases `&` alone, `a&b`, `a=b`, `(+)` and `a +b`),
`TextNormalizer_Normalize_Marks_AreSpoken` (a theory),
`TextNormalizer_Normalize_AdjacentMarks_PadExactlyOneSpaceAndKeepOffsets`,
`TextNormalizer_Normalize_Slash_IsSpokenOnlyBetweenWords` (a theory)

Examples: `a & b` becomes `a and b` but `a=b` and `(+)` stay; `Brand™ rocks` becomes `Brand trademark rocks` and
`A™B` becomes `A trademark B`; adjacent marks are separate words (`A™®B` becomes `A trademark registered B`,
`§§ 5` becomes `section section five`, `© 2025` becomes `copyright twenty twenty five` (the digits are read by the
numeric rule), `x™©2025` becomes `x trademark copyright 2025` (the number is part of the token and is left
alone), the two replacements of
`A™®B` being `trademark` and ` registered ` at offsets 1 and 2 with no double space; `he/she` becomes `he slash she` while `km/h`, `a/b`, `24/7`, `a/b/cc` and
`he/sh3` stay, and `and/or` and `w/o` are the lexicon forms `and or` and `without`.

##### Invisible characters

**Tests**: `TextNormalizer_Normalize_InvisibleCharacters_AreDroppedExplicitly` (a theory),
`TextNormalizer_Normalize_TokenWithInvisibleCharacter_GetsNoOtherRule`

A run of zero-width spaces is one span, two runs are two spans, a byte-order mark, a bullet and the
bidirectional controls are dropped, and the zero-width joiner and non-joiner are kept; `AT&T` followed by a
zero-width space produces only the dropped span, not the ampersand initialism.

##### Apostrophes and clitics

**Tests**: `TextNormalizer_Normalize_Apostrophes_AreCanonicalizedOnlyInsideWords` (a theory),
`TextNormalizer_Normalize_Clitics_AreClassifiedFromContext` (a theory over 23 sentences),
`TextNormalizer_Normalize_CliticsNotRequested_RecordsNoCliticsButStillCanonicalizes`

Contractions, elisions (`y’all`, `o’clock`, `rock’n’roll`, `’Tis`, `’til`, `’cause`) become plain apostrophes
while `dogs’` and `‘quoted’` stay. The classification theory asserts exactly one clitic mark per sentence, on
the expected token, with the role of the documented table: `n't` Not, `'re` Are, `'ve` Have, `'ll` Will, `'m`
Am, `'d` Would or Had (`I'd better go`, `I'd never been`, `they'd gone`), `'s` as Us (`Let's go`), Is, Has and
Possessive (`What's up`, `It's been cold`, `John's book`, `The company's new plan`), and the documented
heuristic cases `John's going home` and `The dog's barking loudly` (Is). When roles are not requested no mark
is recorded but the apostrophes are still canonicalized.

##### Period and sentence rules

**Tests**: `TextNormalizer_Normalize_SuffixPeriod_IsKeptAtSentenceEndAndConsumedMidSentence`,
`TextNormalizer_Normalize_PrefixTitle_ConsumesPeriodAndSuppressesTokenizerBreak`,
`TextNormalizer_Normalize_SuffixBeforeNextWord_ForcesBreakOnlyBeforeCapitalizedWord`,
`TextNormalizer_Normalize_WordReading_ForcesBreakWithoutSpan`,
`TextNormalizer_Normalize_NameSuffixBeforeCredential_ConsumesPeriodAndKeepsNameTogether`,
`TextNormalizer_Normalize_NameSuffixBeforeSentenceStarter_StillForcesBreak`,
`TextNormalizer_Normalize_AmbiguousResolutions_AreCountedOnce`

The spans name the exact source characters: `St` (period kept) at a sentence end and `St.` mid-sentence, `Ave.`
(consumed) and `Blvd` (kept) for the word-plus-period token shape. For `Sen. Smith spoke.` the tokenizer
break is the suppressed break and nothing is left after reconciliation. `Maple Dr. He likes it.` forces the
break before `He` while `Maple Dr. is nice.` and `Maple Dr. 5 times.` force none. `No. I don't know.` has no
span and one forced break. In `John Smith Jr. Ph.D. spoke.` the span of `Jr.` includes its period and no
break is forced; in `Ann Lee Ph.D. M.D. spoke.` the tokenizer's break before `M.D.` is the suppressed break and
nothing is left after reconciliation; in `I met John Smith Jr. He left.` the only break is before `He`. The ambiguity count is 0 for clear readings and 1 per inconclusive `They St.`
(2 for two of them).

##### Initialisms

**Tests**: `TextNormalizer_Normalize_AllCapitalSentence_SuspendsAmbiguousInitialisms`,
`TextNormalizer_Normalize_DottedInitialismWithSeparatePeriod_IsHandled`,
`TextNormalizer_Normalize_DottedFormWithoutPeriodToken_IsIdentity`

`The US team won. LET US GO NOW.` speaks the first `US` letter by letter and leaves the shouted one; `5 p.m.
Then sleep.` yields the number span `five` and one `Initialism` span `P M`; `x.y` yields none.

##### Numeric rule

**Tests**: `TextNormalizer_Normalize_NumericTokens_AreRecognizedBeforeAbbreviations`,
`TextNormalizer_Normalize_ConsumedFollowUpToken_IsNotVisitedAgain`,
`TextNormalizer_Normalize_NumericToken_WithInvisibleCharacter_StaysIdentity`,
`TextNormalizer_Normalize_AdjacentNumericSpans_PadExactlyOneSpace`,
`TextNormalizer_Normalize_UnitWithPeriod_ConsumesMidSentenceAndKeepsAtSentenceEnd`

`Meet at 5 p.m. Then sleep.` yields `five`, then the acronym rule's `P M` with its period kept; `6 ft. tall` (the
unit with its period consumed, the span covering `ft.`), `6 ft.` and `6 ft. Then he left.` (the span covering `ft`
only and a forced break before `Then`) and `180 lbs.` (the unit token and its separate period token) check the
period rules, and `5 Ft. Worth` reads `five feet. Worth`. `5 million USD` has the spans `5` and `USD` and the
observer, hand-wired to the normalizer, never sees the index of `million` as a started token, but counts it as
visited. `50%` produces `fifty` and `percent` with one space between the two replacements and no moved
offset, and `5–10` produces `five`, ` to ` and `ten`; a numeric token with a zero-width space inside produces
only the dropped span.

##### Numeric cancellation and allocation

**Tests**: `TextNormalizer_Normalize_CanceledDuringNumericPass_ThrowsOperationCanceledException`,
`TextNormalizer_Normalize_NonNumericLowercaseText_AllocatesNothingPerToken`

For a long hand-built numeric text, an observer cancels the token mid-pass and the pass stops at the next periodic
check (at most 256 tokens later). For 100,000 lower-case word tokens the bytes allocated by the pass do not
depend on the number of tokens beyond the builder's fixed lists (a generous threshold calibrated by the test), so
the numeric rule adds no per-token allocation to text without numbers.

##### Cancellation and observer

**Tests**: `TextNormalizer_Normalize_PreCanceledToken_ThrowsOperationCanceledException`,
`TextNormalizer_Normalize_CanceledMidPass_ThrowsOperationCanceledException`,
`TextNormalizer_Normalize_Observer_SeesEveryTokenInOrder`,
`TextNormalizer_Normalize_RepeatedMultiTokenMatches_CheckCancellationEvery256VisitedTokens`

A canceled token throws before any work. For 1000 hand-built one-letter tokens, an observer cancels the
token while token 300 is visited, and the pass stops at the next periodic check: more than 300 and at most
600 tokens are visited, not all 1000. The observer sees the indexes 0, 1 and 2 of a three-token text, in order.
For 400 repeated `5 kg` matches (800 tokens) an observer cancels at the first token and the check fires at the
256th visited token, inside the 128th match: the observer has been called 128 times, because each token a
match consumed counts as visited.

##### Arguments, empty text and determinism

**Tests**: `TextNormalizer_NormalizeAndConstructor_InvalidArguments_Throw`,
`TextNormalizer_Normalize_EmptyTextAndTokens_ProducesNothing`,
`TextNormalizer_Normalize_RepeatedAndCustomLexicons_AreDeterministic`

A null collaborator or text throws `ArgumentNullException`; an empty token, overlapping tokens and a token
outside the text throw `ArgumentException`. An empty text and token list yield no span, no clitic and a zero
ambiguity count. Two runs over one text agree, and a normalizer over a custom lexicon in which only `Dr.` is
listed reads `Doc Smith and Mr. Jones live on Main St.` while the default reads `Doctor Smith and Mister Jones
live on Main Street.`

##### User directives

**Tests**: `TextNormalizer_Normalize_SkipDirective_EmitsNothingAndCountsApplied`,
`TextNormalizer_Normalize_ReplaceDirective_EmitsOverrideSpanWithSinglePadding`,
`TextNormalizer_Normalize_SpellDirective_SpellsAsciiLettersOnly`,
`TextNormalizer_Normalize_PronounceDirective_AddsMarkWithoutSpan`

A skip directive on `US` in `US troops` leaves no span and no mark, counts one applied override and leaves the text
as written. A replace directive on `Nginx` in `use Nginx now` yields one span of the `Override` kind at offset 4,
length 5, not spelled, with the value `engine ex`; with a trademark mark directly before the word (two hand-built
tokens) the replacement gets exactly one separating space and the spoken text is `trademark engine ex`. A spell
directive on `gif` yields the spelled span `G I F` (padded the same way after a mark); on `café` it yields no span
and one ignored override, because the letter names exist only for ASCII letters. A pronounce directive on `Polish`
yields a pronunciation mark with the exact token range and value, no span, an unchanged text and one applied
override.

##### Directive precedence and consumed tokens

**Tests**: `TextNormalizer_Normalize_DirectiveOnBuiltInToken_BeatsEveryBuiltInRule` (a theory over eleven texts:
a clitic stem, a slash form, a symbol-bearing token, a lexicon abbreviation, listed and vowel-less acronyms, an
elision, an ampersand initialism with and without a possessive, and an initial),
`TextNormalizer_Normalize_DirectiveOnConsumedToken_IsIgnoredAndCounted` (a theory over a unit at the end and in
the middle, a magnitude and a meridiem),
`TextNormalizer_Normalize_MixedDirectives_CountAppliedAndIgnoredSeparately`

With a skip directive on the token, no built-in span lies inside it; with a replace directive, the only span on the
token is the override span with exactly the token's range. For a token consumed by a number match the spans and the
spoken text equal those without the directive, nothing is applied and one override is ignored. Four skip directives
over `US 5 kg FBI and $3 million` (two on free tokens, two on consumed ones) count two applied and two ignored.

##### Directive sentence breaks

**Tests**: `TextNormalizer_Normalize_DirectiveOnAbbreviation_KeepsBuiltInSentenceBreaks` (a theory over fourteen
texts: `Fig`, `Dr`, `St`, `No`, `Jan` and `Ph.D`, mid-sentence and sentence-final, and `Dr` after an em dash),
`TextNormalizer_Normalize_DirectiveOnFig_ReviewExampleHasNoSentenceBreak`,
`TextNormalizer_Normalize_DefectiveDirective_StillKeepsBuiltInSentenceBreaks`,
`TextNormalizer_Normalize_DirectiveOnAbbreviation_VisitsEveryTokenOnce`,
`TextNormalizer_Normalize_DirectiveOnTokensWithoutPeriod_AllocatesNoMoreThanBuiltIn`,
`NormalizationSubsystem_Normalize_DirectiveAnywhere_NeverChangesSentenceBreaks`

For every row and for each of the four directive kinds (skip, replace, spell, pronounce) the reconciled breaks equal
those of the text without the directive and the sentences match the pinned literals (`See Fig. 5 for details.` is
one sentence; `We saw the plot in Fig. It was good.` splits after `Fig.`; `Hello—Dr. Smith left.` keeps its
suppression, which closes the former limitation; `John Smith Jr. Ph.D. He spoke.` keeps the name continuation).
The spans come from the directive alone: a replace or spell directive leaves exactly one override span with the
token's range, which never covers a separate period token, and a skip or pronounce directive leaves none. The review
example has the tokenizer break `[9]` suppressed with and without a `Say` directive. A defective directive (a
replacement or pronunciation without a value, a spell-out of a token that is not ASCII letters) is ignored and
counted, and still keeps the built-in breaks. With directives on abbreviations an observer sees every token index
exactly once, in order. A directive on every capitalized word of a 20,000-word text without a period allocates no
more than the built-in pass (the abbreviation lookup is not attempted). The seeded differential test (seed
20261116, 400 random abbreviation-rich texts with periods, dashes and tag placeholders) puts each of the four kinds
on every word token that a period touches and on up to 16 other word tokens, and once a directive on every sampled
word at once; the reconciled breaks always equal those without directives, every directive is counted exactly
once, spans and marks stay ascending and disjoint, and no break lies inside a span.

##### Directive validation, cancellation and size

**Tests**: `TextNormalizer_Normalize_InvalidDirectiveIndices_ThrowArgumentException` (a theory over repeated,
descending, negative, too large and extreme indices for a text of two tokens),
`TextNormalizer_Normalize_DirectivesWithCancellation_CountEveryVisitedToken`,
`TextNormalizer_Normalize_ManyDirectives_CompletesWithinBudget`

The indices are rejected with `ArgumentException`. For 1000 one-letter tokens with a skip directive on every tenth,
an observer cancels while token 300 is visited and exactly the tokens 0 to 510 are visited (the next periodic check
is at the 512th token); a second call with the canceled token throws again. For 200,000 tokens with a skip directive
on every twentieth, all 10,000 directives are applied, no span is emitted and the pass stays within a generous
time budget.

##### Spelled replacements and ampersand initialisms

**Tests**: `TextNormalizer_Normalize_SpelledReplacements_AreDecidedByShape`,
`TextNormalizer_Normalize_AmpersandInitialisms_AreDecomposedWithUnchangedSpokenText`,
`TextNormalizer_Normalize_AmpersandInitialismNextToReplacement_PadsExactlyOneSpace`

In a sentence with `FBI`, `U.S.`, `Dr.`, `&`, `J.`, `5 p.m.`, `$5`, `50%` and `3 kg`, exactly `FBI`, `U.S.`, `J.`
and `p.m.` are spelled, every spelled value is a letter sequence and no other span is. `AT&T's` yields the spelled
span `A T`, the symbol span ` and ` and the spelled span `T`, with the spoken text `A T and T's`; the typographic
possessive gives the same spoken text plus an apostrophe span; `Q&A and R&D` gives six spans, four of them spelled,
and `Q and A and R and D`. With a trademark mark directly before `AT&T` the result is `trademark A T and T` with no
double space.
