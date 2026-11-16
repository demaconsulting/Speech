### Layer2Rendering (IModelCapabilityProfile and DefaultModelCapabilityProfile)

**Purpose**: Decide, per Layer 1 span, how a Natural Language Audio Tag is realized for a
specific model - pass-through, approximation, or strip - turning a model-independent span
sequence into an ordered, model-appropriate `SpeechPlan`.

**Data Model**: `IModelCapabilityProfile` declares one method,
`Render(IReadOnlyList<TaggedTextSpan> spans, ISpeechModel model)`, returning a `SpeechPlan` (an
ordered `IReadOnlyList<SpeechSegment>`). Each `SpeechSegment` carries the text to synthesize (or
empty, for a pure-silence pause segment), the pre/post silence durations to play alongside it,
any numeric parameter overrides (speed/volume) to apply when synthesizing it, and an
`Annotations` list (default empty) of `SegmentAnnotation`s whose offsets are relative to that
segment's text. `DefaultModelCapabilityProfile` is a **public**, stateless, sealed singleton
(`Instance`, private constructor) requiring no per-model configuration, since every decision it
makes is read from the `model` argument at call time; it is public so a model package with
bespoke tag syntax can build on it, wrapping or delegating to it, instead of copying it. Its three
pause durations are public constants with unchanged values: `ShortPauseMilliseconds` (300),
`LongPauseMilliseconds` (900), and `EllipsisPauseMilliseconds` (500).

`SpeechSegment.Annotations` is validated like the other segment members: it is a private
read-only copy (null list and null element rejected), every annotation must end at or before the
segment's text length, and the same check runs when `with` replaces either `Annotations` or `Text`,
so a segment can never be shortened underneath an annotation. A segment with empty text can only
have no annotations.

**Key Methods**:

- **Render(spans, model)**: For each `TaggedTextSpanKind.Tag` span: if the tag is a pause, always
  renders a silence-only `SpeechSegment` (300ms short / 900ms long) regardless of
  `model.AudioTagSupport`. Otherwise, if `model.AudioTagSupport` is `Native`, passes the tag
  through as its canonical bracket text so the model's own inference sees the control token. If
  `ParameterMapped`, consults `SpeechParameterConventions` for a matching numeric parameter on
  `model.Parameters` and attaches it as an override on the text that follows the tag when one exists, otherwise strips
  the tag. Narration accumulated before the tag is flushed as its own segment first, so an override
  never alters text spoken before the tag; consecutive tags accumulate onto the next text. If
  `None`, strips every non-pause tag. Plain text is accumulated in a buffer and flushed, at a
  pause tag, a parameter-mapped tag, and the end of the span list, through `RenderText(...)`. The
  buffer now also carries the annotations and sentence breaks of the spans it holds:
  - On a `PlainText` span appended at buffer offset `offset`, each span annotation with
    `0 <= Start` and `End <= span.Text.Length` is added as `annotation.Shifted(offset)`
    (out-of-range entries are ignored defensively, since `TaggedTextSpan` does not bound them
    against its text). When `span.SentenceBreaks` is not `null`, the buffer is marked
    *authoritative* and each break `k` in `[0, span.Text.Length]` is added as `offset + k`.
  - On a `Tag` span, a `SentenceBreaks` entry is read as a tag-relative offset: `0` is processed
    immediately **before** the tag contributes and `1` immediately **after** it (other values are
    ignored). In both cases, when the buffer is non-empty, the buffer is marked authoritative and
    the buffer's *current length* is added as a break. The buffer length at that moment is the
    exact position of the gap in every mode: a stripped tag adds nothing, so the break sits at the
    end of the preceding text; a native tag appends its canonical `[alias]` text first, so a break
    after tag `j` falls between tag `j`'s text and tag `j+1`'s text; a parameter-mapped or pause
    tag flushes the buffer, so the break is already a segment boundary (a break at the end of the
    flushed text is ignored by the chunker). The non-empty-buffer guard keeps stale breaks out of
    an empty buffer. A duplicate offset (for example a plain-span break touching the same
    position) is de-duplicated by `SentenceChunker`'s break handling. A custom profile that ignores
    tag-span breaks only loses those breaks and never faults.
  - Native tag text is appended to the buffer without annotations or breaks. Its characters
    occupy buffer offsets, so the offsets of every later span shift accordingly - this *is* the
    re-basing across stripped and passed-through tags. A stripped tag adds no characters, so
    later spans shift by nothing.
  - A flush calls `RenderText(text, annotations, authoritative ? breaks : null, overrides)` and
    then clears the text, annotations, breaks, and the authoritative flag. Pause and
    parameter-mapped tags already flush first, so annotations and breaks never cross a
    segment-override boundary: a break exactly at a tag seam splits there, and an annotation on one
    side stays with its own segment.
  - When no span carries annotations or breaks, the buffer holds none, `RenderText` receives an
    empty list and `null` breaks, and the output - including `SpeechSegment` equality, since the
    empty annotation list is a shared instance - is identical to the output before annotations
    existed.
- **RenderText(text, annotations, sentenceBreaks, parameterOverrides)** *(public static; the
  chunk-then-render building block)*: Calls `SentenceChunker.ChunkAligned(text, annotations,
  sentenceBreaks)` and emits one `SpeechSegment` per chunk with zero pre-silence, post-silence
  `EllipsisPauseMilliseconds` when the chunk's `EndsWithEllipsis` flag is set (zero otherwise),
  the supplied `parameterOverrides`, and the chunk's re-based `Annotations`. All of its parameters
  after `text` are optional (`null`). `Render` itself flushes through this method, so there is
  exactly one rendering code path for plain text. A model profile that only needs to change how
  tags are realized can call it for the narration and keep the default chunking, pauses, and
  annotation handling.
- **Plain text without annotations**: the behavior described for `RenderText` reduces to
  the original behavior: delegate to `SentenceChunker`, emit one segment per chunk, and set that
  segment's `PostSilenceMs` to `EllipsisPauseMilliseconds` (500ms) when the chunk's
  `EndsWithEllipsis` flag is set, or `0` otherwise - a chunk-boundary-triggered pause distinct
  from, and never reusing, the tag-triggered short/long pause durations above.

**Error Handling**: Rejects a null `spans` or `model` with `ArgumentNullException`, and
`RenderText` rejects a null `text` with `ArgumentNullException`. `SpeechPlan`
rejects a null segment list (`ArgumentNullException`) or a null element (`ArgumentException`) and
keeps a private read-only copy of the list. `SpeechSegment` rejects null text
(`ArgumentNullException`), negative pre/post silence (`ArgumentOutOfRangeException`), and a null
annotation list (`ArgumentNullException`), a null annotation element, or an annotation lying
outside the segment text (`ArgumentException`). Every other
input - any tag, any support level, any parameter set, any span annotation or break - is handled
without throwing, per this library's "never worse than plain narration" guarantee extended to
Layer 2: an out-of-range span annotation or break is ignored rather than faulting, and the chunker
drops any annotation it cannot represent in one chunk.

**Dependencies**: `TaggedTextSpan`, `TaggedTextSpanKind`, `NaturalLanguageAudioTagKind` from this
subsystem's AudioTagParser unit; `SegmentAnnotation` from the TextAnalysis unit; `SentenceChunker`;
`SpeechParameterConventions`; `SpeechSegment`,
`SpeechPlan`; `ISpeechModel`, `SpeechModelAudioTagSupport`, `NumericParameter` from the
ModelManagementSubsystem.

**Callers**: `SynthesisSession.GenerateAndOptionallyPlayAsync(...)` via
`ISynthesisModel.CapabilityProfile`, and external model profiles, which may wrap
`DefaultModelCapabilityProfile.Instance`, delegate to its `Render`, or call `RenderText` and the
public pause constants directly. A bespoke profile that does not understand annotations may ignore
the annotations and breaks on spans (it then behaves as before) or copy them onto the segments it
builds.
