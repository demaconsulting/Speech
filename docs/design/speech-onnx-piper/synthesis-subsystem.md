## SynthesisSubsystem Design

![SynthesisSubsystem Structure](PiperSynthesisSubsystemView.svg)

### Overview

The SpeechOnnxPiper SynthesisSubsystem turns text into audio with the Piper voice. The Piper graph takes phoneme ids, so
the subsystem owns the conversion of text into them as well as the inference. It contains the following units:

- **PiperPhonemePipeline**: converts the text of one speech segment, with the annotations of the English text analysis
  when there are any, into framed phoneme-id sequences (with its result type `PiperUtterance`)
- **OnnxPiperSynthesisEngine**: the production `IAnnotatedSynthesisBackend` that runs the voice's ONNX graph over those
  sequences and joins the audio

### Interfaces

Both units are internal. The engine is exposed to the Speech library only as its public `ISynthesisBackend` and
`IAnnotatedSynthesisBackend` seams, returned by `OnnxPiperLibriTtsEnSynthesisModel.CreateBackend`. The subsystem
consumes `PiperVoiceConfig` of the ModelManagementSubsystem (the symbol table, the sample rate, the scales),
`EnglishPhonemizer` and `EspeakIpaRenderer` of SpeechPhonemesEn, `SegmentAnnotation` of Speech, and the ONNX Runtime
managed API.

### Design

**Text to ids.** The pipeline phonemizes the segment with `EnglishPhonemizer` (with the annotations of the English text
analysis, or as written when there are none), renders the result with `EspeakIpaRenderer(EspeakIpaProfile.EnUs)` as
espeak-style IPA, and encodes each part with the voice's symbol table in Piper's framing: the begin symbol, each symbol
followed by the pad symbol, the end symbol. The renderer cuts a long text into parts of at most 256 symbols
(`RenderParts`, between words and after a clause mark where one fits), so one segment can give several utterances. A
symbol outside the table is skipped and listed in the utterance; a part with no encodable symbol gives no utterance.

**Inference.** The engine runs the graph once per utterance with the inputs `input`, `input_lengths`, `scales` and `sid`
(see *SpeechOnnxPiper System Design*), the scales being the voice's noise scale, its length scale divided by the speed
(a speed that is not a positive finite number counts as 1) and its noise width, and the speaker clamped into the voice's
range. The waveforms of the utterances are concatenated in order. A segment with no utterance returns empty audio at the
voice's sample rate without running the graph.

**Reporting.** The engine counts the skipped symbols over its lifetime and lists the distinct ones, so that a loss is
visible to a host or a test. The renderer is tested to emit only symbols of this voice's table, so the count stays zero
for the text the shipped phonemizer produces.

**Session ownership.** The engine owns the session it is given and disposes it when it is disposed; disposal is
idempotent and safe against concurrent synthesis (it waits for the runs in progress before releasing the session, once),
and synthesis after disposal throws `ObjectDisposedException`. The engine also provides the probe inference
(`RunProbeInference`, one sentence-sized run) that `OnnxExecutionProviderSelector` uses to reject an accelerated
provider that loads the graph but cannot run it.

**Cross-reference.** The agreement of the rendered phonemes with espeak-ng and the symbol coverage of the renderer are
measured and recorded in `docs/evaluation/phonemes-en/renderers.md` and `docs/evaluation/phonemes-en/summary.md`; this
subsystem relies on those results and does not repeat them.
