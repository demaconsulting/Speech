### OnnxNemotronRecognitionEngine Verification

#### Verification Approach

Verified by deterministic unit tests that drive the engine through its `IRecognitionBackend`
surface with managed fakes of the encoder and prediction network (`Fakes.cs`), so chunking,
result reporting, endpointing, padding, and lifetime behavior are tested without ONNX Runtime or
model files. The real limiter, dither, feature extractor, greedy decoder, endpoint detector, and
vocabulary are used. Three kinds of fakes are used:

- a scripted encoder and network (two output frames per chunk) that emit a fixed word sequence,
  for result reporting, flushing, reset, and disposal;
- a loudness-reacting seven-frame encoder (like the real model) and a network that emits a word on
  each quiet-to-loud transition of the input features, so pauses, resumed speech, and padding
  have observable effects - an all-zero feature row would read as loud and produce an extra word,
  while silence features do not;
- a network that emits the two-piece word "hel" + "lo" on every frame (the second piece does not
  start a word), with a shortened maximum utterance length, for the maximum-length split; and a
  vocabulary holding a period token, for closing punctuation after an endpoint.

The fakes record the encoder inputs and the reset counts of the encoder and network, so the tests
observe the features fed to the model and whether the model state was kept.

#### Test Environment

xUnit v3 under the .NET SDK, in `test/DemaConsulting.Speech.Onnx.NemotronStt.Tests`
(`OnnxNemotronRecognitionEngineTests.cs`), on every target framework.

#### Acceptance Criteria

The engine buffers audio without decoding; reports provisional text only on change and only for
complete 56-frame chunks; flushes to a final result, decoding the configured silence tail chunks;
pads a partial chunk with silence features once the limiter's quiet run after speech reaches its
cap, so text is reported before the endpoint; finalizes an utterance after 1200 ms without new
words (counting quiet the limiter removed) without resetting the encoder or decoder, and not at a
0.6 s pause; reports one final per utterance within one block and keeps every word of resumed
speech exactly once; splits at the maximum utterance length between words; drops closing
punctuation that would open a segment after an endpoint; never reports a result for silence alone
and never adds words from padding; rejects negative endpoint rules and tail chunk counts; and
resets, disposes, and validates arguments as documented. The ONNX-backed `NemotronEncoder` and the
pure `NemotronEndpointDetector` are verified in their own unit verification documents.

#### Test Scenarios

##### Speech chunk reports provisional text

**Test**: `TryDecode_SpeechChunk_ReportsProvisionalText`

##### Unchanged text reports nothing

**Scenario**: after "hello" was reported, another 0.5 s of audio (below the 1200 ms endpoint rule)
produces only blanks.

**Test**: `TryDecode_NoNewTokens_ReportsNothing`

##### Less than a chunk reports nothing

**Test**: `TryDecode_LessThanChunk_ReportsNothing`

##### Empty input is ignored

**Test**: `AcceptSamples_Empty_IsIgnored`

##### Flush returns final text

**Test**: `TryFlush_PartialAudio_ReturnsFinalText`

##### Flush without audio returns false

**Test**: `TryFlush_NoAudio_ReturnsFalse`

##### Flush with audio but no tokens returns false

**Test**: `TryFlush_AudioWithoutTokens_ReturnsFalse`

##### Flush without tail chunks decodes only the audio

**Expected**: the engine with the default single tail chunk encodes exactly one more chunk than
the engine configured with none.

**Test**: `TryFlush_NoTailChunks_DecodesOnlyAudio`

##### Quiet after short speech reports before the endpoint

**Scenario**: 0.3 s of speech (a partial chunk) and then 0.8 s of silence.

**Expected**: the text is reported once as provisional, before the 1200 ms endpoint.

**Test**: `TryDecode_QuietAfterShortSpeech_ReportsBeforeEndpoint`

##### Padding follows the configured limiter cap

**Test**: `TryDecode_CustomLimiterCap_PadsAtThatCap`

##### Silence alone never produces a result

**Scenario**: five seconds of digital silence only, decoded, and in a second test also flushed.

**Expected**: no result at all, no word predicted, and no model reset.

**Tests**: `TryDecode_OnlySilence_NeverEndpoints`, `OnnxNemotronRecognitionEngine_TryFlush_SilenceOnly_ReportsNoText`

##### Silence after speech finalizes without a model reset

**Scenario**: one second of speech followed by three seconds of digital silence.

**Expected**: a provisional and then a final result; the encoder and network reset counts are
unchanged.

**Test**: `OnnxNemotronRecognitionEngine_TryDecode_SilenceAfterSpeech_FinalizesWithoutModelReset`

##### Paused speech in one block yields one final per utterance

**Scenario**: two half-second utterances, each followed by a 1.5 s pause, accepted as one block.

**Expected**: two finals, one per utterance, and no model reset.

**Test**: `OnnxNemotronRecognitionEngine_TryDecode_PausedSpeechInOneBlock_ReportsOneFinalPerUtterance`

##### A pause below the rule keeps one utterance

**Scenario**: two short utterances separated by a 0.6 s pause, then a flush.

**Expected**: nothing is finalized at the pause; the flush returns both words as one final.

**Test**: `OnnxNemotronRecognitionEngine_TryDecode_ShortPauseBelowRule_KeepsOneUtterance`

##### Steady audio without new words is finalized

**Scenario**: "hello" once and then four seconds of a steady tone that never reads as quiet.

**Expected**: the text is finalized (frames without new words are trailing silence) and the
encoder state is kept.

**Test**: `OnnxNemotronRecognitionEngine_TryDecode_SteadyAudioWithoutNewTokens_FinalizesAfterTrailingBlanks`

##### Custom endpoint rules apply

**Scenario**: one engine with a 400 ms after-speech rule and one with the defaults each receive
0.3 s of speech and 0.6 s of silence.

**Expected**: only the engine with the short rule has finalized.

**Test**: `OnnxNemotronRecognitionEngine_TryDecode_CustomEndpointRules_FinalizesAtConfiguredSilence`

##### Speech after an endpoint keeps every word

**Scenario**: an utterance and a 2 s pause, drained; then another utterance and pause.

**Expected**: each utterance's word appears exactly once in its own final, with no model reset.

**Test**: `OnnxNemotronRecognitionEngine_TryDecode_SpeechAfterEndpoint_KeepsEveryToken`

##### Late closing punctuation is not a final of its own

**Scenario**: "hello" is finalized by the endpoint, then the network emits a late "." while the
flush decodes the silence tail.

**Expected**: only the sentence was finalized; the late punctuation produced no result.

**Test**: `OnnxNemotronRecognitionEngine_TryFlush_PunctuationAfterEndpoint_ReportsNoPunctuationOnlyFinal`

##### Late closing punctuation does not open the next segment

**Scenario**: "hello" is finalized, then a late "." followed by the next word, then a flush.

**Expected**: the next final starts with its word, without the previous sentence's period.

**Test**: `OnnxNemotronRecognitionEngine_TryFlush_PunctuationBeforeNextWords_StartsSegmentWithWord`

##### Continuous words beyond the maximum are split

**Scenario**: a 3 s maximum utterance length and the two-piece word "hel" + "lo" on every frame for
ten seconds, then a flush.

**Expected**: at least three finals of at most twelve words each, and every word kept exactly once.

**Test**: `OnnxNemotronRecognitionEngine_TryDecode_ContinuousTokensBeyondMaximum_SplitsUtterance`

##### The maximum-length split keeps words whole

**Scenario**: a 1 s maximum and the two-piece word "hel" + "lo" on every frame for five seconds.

**Expected**: several finals, and every word in them is the whole word.

**Test**: `OnnxNemotronRecognitionEngine_TryDecode_MaximumLengthSplit_KeepsWordsWhole`

##### Speech then silence adds no words from padding

**Scenario**: half a second of speech, a 0.3 s pause (below the limiter cap), then a flush.

**Expected**: the final holds only the spoken word; all-zero padding would have added one.

**Test**: `OnnxNemotronRecognitionEngine_TryFlush_SpeechThenSilence_AddsNoWordsFromPadding`

##### A partial chunk is padded with silence features

**Scenario**: a fifth of a second of speech (a partial chunk), then a flush.

**Expected**: two chunks are encoded and every feature row after the speech is at silence level,
never an all-zero row.

**Test**: `OnnxNemotronRecognitionEngine_TryFlush_PartialChunk_PadsWithSilenceFeatures`

##### Invalid options are rejected

**Expected**: a negative flush tail chunk count and a negative endpoint rule duration each throw
`ArgumentOutOfRangeException`.

**Test**: `OnnxNemotronRecognitionEngine_Constructor_InvalidOptions_ThrowsArgumentOutOfRangeException`

##### A reset engine behaves like a fresh engine

**Scenario**: An engine that has consumed a decoded chunk and holds half a second of not-yet-encoded audio is
reset, then given the same new audio as a fresh engine with the same script.

**Expected**: Both report the same results and encode the same amount of audio.

**Test**: `Reset_AfterAudio_NextStreamMatchesFreshEngine`

##### Dispose disposes collaborators and blocks use

**Test**: `Dispose_DisposesCollaboratorsAndBlocksUse`

##### Null constructor arguments throw

**Test**: `Constructor_NullArguments_Throw`

##### The engine declares the 16 kHz sample rate

**Test**: `SampleRate_Is16kHz`
