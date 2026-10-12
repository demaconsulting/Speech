### Layer2Rendering (IModelCapabilityProfile and DefaultModelCapabilityProfile)

**Purpose**: Decide, per Layer 1 span, how a Natural Language Audio Tag is realized for a
specific model - pass-through, approximation, or strip - turning a model-independent span
sequence into an ordered, model-appropriate `SpeechPlan`.

**Data Model**: `IModelCapabilityProfile` declares one method,
`Render(IReadOnlyList<TaggedTextSpan> spans, ISpeechModel model)`, returning a `SpeechPlan` (an
ordered `IReadOnlyList<SpeechSegment>`). Each `SpeechSegment` carries the text to synthesize (or
empty, for a pure-silence pause segment), the pre/post silence durations to play alongside it,
and any numeric parameter overrides (speed/volume) to apply when synthesizing it.
`DefaultModelCapabilityProfile` is a stateless singleton (`Instance`) requiring no per-model
configuration, since every decision it makes is read from the `model` argument at call time.

**Key Methods**:

- **Render(spans, model)**: For each `TaggedTextSpanKind.Tag` span: if the tag is a pause, always
  renders a silence-only `SpeechSegment` (300ms short / 900ms long) regardless of
  `model.AudioTagSupport`. Otherwise, if `model.AudioTagSupport` is `Native`, passes the tag
  through as its canonical bracket text so the model's own inference sees the control token. If
  `ParameterMapped`, consults `SpeechParameterConventions` for a matching numeric parameter on
  `model.Parameters` and attaches it as an override on the surrounding segment when one exists,
  otherwise strips the tag. If `None`, strips every non-pause tag. For each
  `TaggedTextSpanKind.PlainText` span, delegates to `SentenceChunker.ChunkWithMetadata(...)` and
  emits one `SpeechSegment` per resulting chunk, setting that segment's `PostSilenceMs` to a new
  `EllipsisPauseMilliseconds` constant (500ms) when the chunk's `EndsWithEllipsis` flag is set, or
  `0` otherwise - a chunk-boundary-triggered pause distinct from, and never reusing, the
  tag-triggered short/long pause durations above.

**Error Handling**: Rejects a null `spans` or `model` with `ArgumentNullException`. Every other
input - any tag, any support level, any parameter set - is handled without throwing, per this
library's "never worse than plain narration" guarantee extended to Layer 2.

**Dependencies**: `TaggedTextSpan`, `TaggedTextSpanKind`, `NaturalLanguageAudioTagKind` from this
subsystem's unchanged units; `SentenceChunker`; `SpeechParameterConventions`; `SpeechSegment`,
`SpeechPlan`; `ISpeechModel`, `SpeechModelAudioTagSupport`, `NumericParameter` from the
ModelManagementSubsystem.

**Callers**: `SynthesisSession.GenerateAndOptionallyPlayAsync(...)` via
`ISynthesisModel.CapabilityProfile`.
