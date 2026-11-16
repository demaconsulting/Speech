## DemaConsulting.PartOfSpeech Verification

This document provides the verification evidence for the DemaConsulting.PartOfSpeech OTS software item.
Requirements for this OTS item are defined in the DemaConsulting.PartOfSpeech OTS Software Requirements
document.

### Required Functionality

DemaConsulting.PartOfSpeech provides the English tokenizer, the sentence splitter and the shared
Universal Dependencies tagger that the SpeechTextEn `PartOfSpeechAdapter` unit uses to annotate speech
requests.

### Verification Approach

Verification runs the real package, through exactly the members the adapter uses, in integration tests in
`test/DemaConsulting.Speech.Text.En.Tests/Ots/PartOfSpeechTests.cs`. The tests do not depend on the analysis
code built on top of the package, so they are evidence about the package itself. They double as an
API-presence check: they bind every member the analysis uses to a delegate of the expected shape, so a
package version that removes or reshapes one stops the test project from compiling. The tests do not claim to
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

### Known Limitation Not Covered by Verification

The tests above give no evidence about, and this repository does not claim, that the package keeps analyzed
text out of memory. Version 0.1.0-beta.4 keeps the characters of tagged tokens in a thread-static working
buffer of its tag decoder that is never cleared, so words analyzed on a thread remain recoverable from the
process memory after the request (upstream issue <https://github.com/demaconsulting/PartOfSpeech/issues/29>).
No requirement of this OTS item asserts otherwise, and no test inspects or clears that private state; the
SpeechTextEn retention requirement and its weak-reference test are scoped to SpeechTextEn's own state.
