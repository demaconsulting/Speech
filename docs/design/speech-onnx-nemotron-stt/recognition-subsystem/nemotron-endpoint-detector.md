### NemotronEndpointDetector

**Purpose**: Decide when an utterance ends from what the decoder produced rather than from the
input loudness, with the trailing-silence and maximum-length rules of the sherpa-onnx online
recognizer applied on the decode clock. "Silence" for endpointing means "the recognizer emitted
no token": steady background noise that never reads as quiet still yields blank frames, and a
quiet word still yields a token, so counting decoded blank frames ends an utterance where the
recognizer heard the speech end. The unit is pure bookkeeping: it never touches audio, ONNX
Runtime, or the model state, and it is fully deterministic.

**Data Model**: The source file holds three internal types.

- **NemotronEndpointRules** (record): `TrailingSilenceMs` (rule 1, default 2400: trailing silence
  that ends a segment whether or not it holds speech), `TrailingSilenceAfterSpeechMs` (rule 2,
  default 1200: trailing silence after the last token that ends an utterance holding speech), and
  `MaxUtteranceMs` (rule 3, default 20000: utterance length at which an utterance ends even while
  speech continues). The defaults equal the sherpa-onnx online recognizer defaults, so the ONNX
  and Sherpa Nemotron backends segment speech alike. Zero disables a rule; a negative value is
  invalid.
- **NemotronEndpointReason** (enumeration): `None`, `TrailingSilence` (rule 1),
  `TrailingSilenceAfterSpeech` (rule 2), and `MaxUtteranceLength` (rule 3).
- **NemotronEndpointDetector** (class): the `Rules` in effect; the three rule thresholds
  converted once to samples (zero for a disabled rule); `HasSpeech` (the current segment holds a
  decoded token); `TrailingSilenceSamples` (input samples since the last token or the segment
  start); and `UtteranceSamples` (input samples of the current segment, padding excluded).
  Invariants: both clocks are non-negative; `TrailingSilenceSamples` is zero right after a token;
  padding never advances either clock. Not thread-safe; owned by one engine.

**Key Methods**:

- **NemotronEndpointDetector(rules, sampleRate)**: `rules` defaults to the Sherpa-equivalent
  `NemotronEndpointRules()`, `sampleRate` to `NemotronFeatureExtractor.SampleRate` (16000).
  Validates both, then converts each rule to samples as `milliseconds * sampleRate / 1000` in
  64-bit arithmetic, so every later update is integer arithmetic. Postcondition: a new, empty
  segment (no speech, both clocks zero).
- **AdvanceFrame(frameSamples, emittedToken)**: records one decoded encoder frame of real audio
  that stands for `frameSamples` input samples (1280, 80 ms, for the real model). The frame always
  lengthens the utterance; a frame with a token marks speech and restarts the trailing silence,
  and a blank frame lengthens the trailing silence by `frameSamples`.
- **AdvancePaddingFrame(emittedToken)**: records one decoded frame of synthetic padding: a token
  still marks speech and restarts the trailing silence, but neither clock advances, so padding
  alone can never satisfy a rule and flush or quiet-cap padding never shifts an endpoint.
- **AdvanceSilence(droppedSamples)**: records quiet input that the `SilenceRunLimiter` removed
  before feature extraction; it lengthens both the trailing silence and the utterance exactly as
  decoded blank frames of the same duration would, so a pause ends an utterance after the same
  input time whether or not the limiter shortened it.
- **Check()**: pure; returns the first satisfied rule in the order rule 2
  (`HasSpeech` and trailing silence reached `TrailingSilenceAfterSpeechMs`), rule 1 (trailing
  silence reached `TrailingSilenceMs`), rule 3 (utterance reached `MaxUtteranceMs`), or `None`. A
  disabled rule is never satisfied. Rule 2 is checked first so a pause after speech is reported
  as the speech rule, which is the rule that finalizes ordinary utterances.
- **Reset()** and **Reset(carriesSpeech)**: start a new segment with both clocks at zero;
  `carriesSpeech` sets `HasSpeech` for a segment that already holds the word the engine carried
  over from a maximum-length split, so rule 2 applies to it from the start.

How the engine uses the decision (see *SpeechOnnxNemotronStt RecognitionSubsystem Design*): with
no tokens any rule only restarts the detector; rules 1 and 2 finalize the utterance and restart
it; rule 3 finalizes the tokens before the last word start and restarts it with
`Reset(carriesSpeech: true)`. The model state is never reset at an endpoint.

**Error Handling**: the constructor throws `ArgumentOutOfRangeException` when `sampleRate` is not
positive or any rule duration is negative; `AdvanceFrame` and `AdvanceSilence` throw
`ArgumentOutOfRangeException` for a negative sample count. These are configuration or programming
errors and propagate to the caller (the engine constructor, and through it the model's
`CreateBackend`); nothing is handled locally.

**Dependencies**: `NemotronFeatureExtractor.SampleRate` (the default sample rate); the .NET base
class library only otherwise. No ONNX Runtime, model file, or audio dependency.

**Callers**: `OnnxNemotronRecognitionEngine` constructs it from `NemotronEngineOptions.Endpoint`,
reports every decoded frame and every credited removal of quiet input to it, applies its
`Check()` decision, and resets it with the engine; `NemotronEngineOptions` carries its
`NemotronEndpointRules`; the evaluation tool's engine-option overrides build the rules from
`--engine-option` values.
