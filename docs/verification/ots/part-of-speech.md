## DemaConsulting.PartOfSpeech Verification

This document provides the verification evidence for the DemaConsulting.PartOfSpeech OTS software item.
Requirements for this OTS item are defined in the DemaConsulting.PartOfSpeech OTS Software Requirements
document.

### Required Functionality

DemaConsulting.PartOfSpeech provides the English tokenizer, the sentence splitter and the shared
Universal Dependencies tagger that the SpeechTextEn `PartOfSpeechAdapter` unit uses to annotate speech
requests, and whose tokenizer conventions (token shapes and sentence rules for abbreviations, clitics and
symbols) the SpeechTextEn NormalizationSubsystem relies on.

### Verification Approach

Verification runs the real package, through exactly the members the adapter uses, in integration tests in
`test/DemaConsulting.Speech.Text.En.Tests/Ots/PartOfSpeechTests.cs`. The tests do not depend on the analysis
code built on top of the package, so they are evidence about the package itself. They double as an
API-presence check: they bind every member the analysis uses to a delegate of the expected shape, so a
package version that removes or reshapes one stops the test project from compiling. One test locks the
tokenizer conventions the normalization depends on, so that moving the exact version pin to a release that
changes them fails loudly instead of silently changing which text is normalized or where sentences break.
The NormalizationSubsystem tests also call the tokenizer directly, as a test-only dependency (see
*SpeechTextEn NormalizationSubsystem Verification*). The tests do not claim to
measure the tagger's accuracy, which the package documents and measures itself; they use only sentences whose
tags it assigns with full confidence.

### Test Scenarios

#### PartOfSpeech_DefaultTagger_TagsSentence_ReturnsUniversalTagPerToken

**Scenario**: The shared default tagger tags "The cat sat on the mat.".

**Expected**: One sentence whose seven tokens are tagged DET, NOUN, VERB, ADP, DET, NOUN and PUNCT.

#### PartOfSpeech_DefaultTagger_SharedInstance_ReturnsSameTagger

**Scenario**: The shared tagger is read twice.

**Expected**: The same instance.

#### PartOfSpeech_PublicApi_MembersUsedByTextEn_ArePresent

**Scenario**: Every member the analysis uses is bound to a delegate and used on a tiny text.

**Expected**: The code compiles, the token and tagged-token properties are readable, and the tag set has the
17 documented values with the expected tag names.

#### PartOfSpeech_Tag_MultiSentenceText_TokenOffsetsIndexSourceText

**Scenario**: A two-sentence text with an abbreviation, a contraction and a line break inside the second
sentence is tagged as a whole.

**Expected**: Every token's text equals the source slice at its offset and length.

#### PartOfSpeech_Tokenizer_Tokenize_MultiSentenceText_TokenOffsetsIndexSourceText

**Scenario**: The same text is split by the tokenizer alone.

**Expected**: Sentences are in text order, every token equals its source slice, and the negation clitic is a
token of its own.

#### PartOfSpeech_TagSentence_PerSentence_MatchesWholeTextTagging

**Scenario**: The text is tagged once as a whole and once sentence by sentence after tokenizing.

**Expected**: The same offsets, lengths, tags and confidences, so sentence-by-sentence tagging (which lets
cancellation be checked between sentences) changes nothing.

#### PartOfSpeech_Tag_Tokens_ConfidenceIsProbabilityInRange

**Scenario**: Two sentences are tagged.

**Expected**: Every confidence is between 0 and 1 and the clear-cut tokens are at the top of the range.

#### PartOfSpeech_Tag_Tokens_KnownWordFlagReflectsLexicon

**Scenario**: "The qzxvbnk sat." is tagged.

**Expected**: The two ordinary words are known and the invented spelling is not.

#### PartOfSpeech_DefaultTagger_ConcurrentTagging_ReturnsSameResultsAsSequential

**Scenario**: Sixteen tasks each tag four texts ten times through the shared tagger.

**Expected**: Every result of every task equals the sequentially computed reference.

#### PartOfSpeech_Tokenizer_NormalizationRelevantConventions_HoldForPinnedVersion

**Scenario**: The tokenizer alone splits a set of probe texts that the NormalizationSubsystem's rules are
written against: 23 abbreviations (`Dr.`, `St.`, `Mr.`, `Prof.`, `Capt.`, `Sgt.`, `Mt.`, `Ft.`, `Co.`, `Ltd.`,
`Corp.`, `Sr.`, `Jr.`, `e.g.`, `i.e.`, `etc.`, `vs.`, `J.`, `J.R.R.`, `U.S.`, `U.S.A.`, `p.m.`, `a.m.`) inside
a sentence; eight address abbreviations (`Ave`, `Blvd`, `Rd`, `Ln`, `Apt`, `Ste`, `Dept`, `Bldg`) followed by
a period; contractions with a typographic and a plain apostrophe and a leading elision; standalone symbols
set apart by whitespace; joined forms such as `he/she`, `w/o`, `24/7`, `AT&T`, a mark glued to a word and
invisible characters inside a word; and four sentences that show where the tokenizer ends a sentence at an
abbreviation.

**Expected**: Each listed abbreviation is one token that keeps its period; each address abbreviation is a word
token followed by a separate period token; `I don’t think it’s ready` splits into `I`, `do`, `n’t`, `think`,
`it`, `’s`, `ready`, `I'd` yields the clitic `'d` and `wait ’til dawn` yields a lone apostrophe token and `til`;
the symbols `&`, `@`, `+`, `=`, `<`, `>` and `/` are single tokens; each joined form stays one token; and
`Dr. Smith lives on Maple Dr. He likes it.`, `I live on Main St. It is nice.` and `No. I don't know.` are each
one sentence while `Acme Inc. Smith left.` is two. A version change that alters any of these makes the test
fail, so the abbreviation, clitic and break rules cannot drift silently.

### Text Retention Evidence

Version 0.1.0-beta.5 clears the tagger's per-thread working buffers (the decoder workspace and the glossary
workspace) on every public entry point, on the normal and on the exceptional path (upstream issue
<https://github.com/demaconsulting/PartOfSpeech/issues/29>, fixed by upstream pull request
<https://github.com/demaconsulting/PartOfSpeech/pull/30>, requirement `PartOfSpeech-Lib-NoRetention`). The evidence
is the upstream package's own regression tests, cited here by version because the workspaces are private
and version 0.1.0-beta.5 offers no public API or documented test hook to inspect them (its hook,
`TagDecoder.CurrentThreadWorkspace`, is internal): the decoder after a long and then a shorter request, with and
without feature groups; the decoder after an exception half way through a request; word lookups and `IsFormOf`,
including over-long compounds; the glossary matcher; and every public `Tagger` entry point, each asserting that
the whole of each buffer is zero, and each failing when the clearing is disabled (upstream mutation check).

This repository has no test of its own over that private state, and reflection or a memory scan is not used.
That is the remaining gap, stated here for the repository owner's approval. Managed strings that the package
creates itself (for example the normalized copy of a non-ASCII token) and the result objects it returns are
not cleared, by upstream's own statement; SpeechTextEn keeps neither (its weak-reference test proves it keeps
no reference to the text or the results). The test
`SpeechTextEn_ProjectFile_Dependencies_PinTaggerAtRetentionFixOrLater` fails if the pin is moved below
0.1.0-beta.5, so the cited evidence cannot silently stop applying.
