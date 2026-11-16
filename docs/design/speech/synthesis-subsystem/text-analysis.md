### TextAnalysis (ITextAnalyzer, TextAnalysisResult, SegmentAnnotation, TextAnalysisConventions)

**Purpose**: Give a synthesis model a language-neutral, once-per-request view of the *whole* text
being spoken, so a phoneme-driven model can use sentence-level context (for example the part of
speech that selects a heteronym's pronunciation) that no chunk, and no fragment between two inline
audio tags, carries on its own. The unit defines the contract a model implements (`ITextAnalyzer`),
the value shapes the analysis returns (`SegmentAnnotation`, `TextAnalysisResult`), the one shared
convention both sides rely on (`TextAnalysisConventions.TagPlaceholder`), and the internal mapping
(`TextAnalysisMapper`) that builds the text to analyze from a parsed span sequence and then
re-bases the analysis result onto the individual spans. The core attaches no linguistic meaning
to an annotation: kinds and values are opaque, so the seam is equally usable by any model family
and any language.

**Data Model**: All offsets are **UTF-16 code unit indices** (ordinary C# string indices).

- `SegmentAnnotation(Start, Length, Kind, Value)` - a sealed record marking the half-open range
  `[Start, Start + Length)` of some container text with an opaque `Kind` (for example
  `"reading"` or `"pos"`; a case-sensitive, ordinal, non-blank string) and an opaque `Value` (may
  be empty). `End` is the exclusive end, `Start + Length`. `Start` must be `>= 0`; `Length` must be
  `> 0` and `Start + Length` must not overflow `int`; `Kind` must be non-blank; `Value` must be
  non-null. Every property validates on construction and on `with`, so a record can never be
  observed in an invalid state. The container is whatever text the annotation travels with
  (the analysis text, a `TaggedTextSpan.Text`, a `SpeechSegment.Text`). An internal `Shifted(delta)`
  helper returns a copy moved by `delta`; it is used by the mapper, chunker, and default profile,
  which guarantee the result stays non-negative. Consumers must ignore kinds they do not recognize.
- `TextAnalysisResult(Annotations, SentenceBreaks)` - a sealed record. `Annotations` is a private
  read-only copy of the supplied list (null list and null elements rejected; the shared empty
  list is used when there are none); overlapping annotations are allowed (different kinds may
  cover the same word) and order is preserved. `SentenceBreaks` is `null` when the analyzer
  expresses no opinion about sentence boundaries - the punctuation heuristic then applies - and
  otherwise a sorted, de-duplicated private copy of non-negative offsets. A non-null list, even an
  empty one, is **authoritative**. A break offset `k` means "a sentence ends immediately before
  index `k`", so `k` is the start of the next sentence; `0` and the text length are harmless
  no-ops. `TextAnalysisResult.Empty` is the shared "no analysis" value (no annotations, `null`
  breaks). Instances are immutable and thread-safe.
- `TextAnalysisConventions.TagPlaceholder` - the constant holding the Unicode OBJECT REPLACEMENT
  CHARACTER (the single character Unicode reserves for an inline object). One placeholder stands
  for exactly one recognized audio tag in the analysis text.
  Analyzers must treat it as an opaque, non-letter, non-word, whitespace-like inline object: it is
  neither a sentence terminator nor part of any word, an analyzer should not cover it with an
  annotation, and an analyzer may place a sentence break before or after it. The constant lives on
  a static class rather than on the interface, mirroring `SpeechParameterConventions`.
- `ITextAnalyzer` - one method, `Analyze(text, cancellationToken)`, returning a
  `TextAnalysisResult`. A model supplies an instance through the optional
  `ISynthesisModel.TextAnalyzer` member (see *SpeechModelContract*), defaulting to none.
- `AnnotationList` - an internal static helper shared by the annotated value types: the shared
  empty-list singleton (so default-constructed spans and segments stay record-equal to their
  pre-annotation shapes), a defensive `Copy` (rejecting a null list or null element), and a
  `RequireWithin` check that every annotation ends at or before a given text length.
- `TextAnalysisMapper` - an internal static class with two methods, described below.

**Key Methods**:

- **ITextAnalyzer.Analyze(text, cancellationToken)**: Called by `SynthesisSession` **once per
  `SpeakAsync`/`SynthesizeAsync` request** on a **thread-pool thread** (`Task.Run`), never on the
  thread that called `SpeakAsync`/`SynthesizeAsync` and not on the `DedicatedWorker` (it is managed
  CPU work). `text` is the model-normalized text
  (`ISpeechModel.NormalizeText`) in which each recognized audio tag has been replaced by one
  `TagPlaceholder`; unrecognized or malformed bracket content stays literal text, exactly as
  `AudioTagParser` keeps it. The returned offsets are relative to that exact string. An
  implementation must be thread-safe (one model instance may back several engines) or stateless per
  call, must not assume thread affinity (no `ThreadStatic`, no UI or synchronization-context
  access), must honor the token (the operation's linked token, checked before the analyzer starts
  and honored during the call; an analyzer that ignores it delays stop and dispose until it
  returns), must not retain `text`, and may return `TextAnalysisResult.Empty`. A `null` return is
  treated the same as `Empty` (no analysis). Throwing is allowed and is contained by the session
  (see *Error Handling*).
- **TextAnalysisMapper.BuildAnalysisText(spans)**: Concatenates the `Text` of every `PlainText`
  span and appends exactly one `TagPlaceholder` for every `Tag` span, preserving order. For a
  span list that contains only plain text the result equals the original text.
- **TextAnalysisMapper.Annotate(spans, analysis)**: Re-bases the analysis result from the single
  analysis string onto the plain spans that produced it, using the **offset-mapping algorithm**
  below, and returns a new span list in which each affected plain span carries its own
  `Annotations` and, when breaks were supplied, `SentenceBreaks`.

**Offset-mapping algorithm.** Let the spans be `s0..sn-1`. The analysis text `A` is built by
appending `span.Text` for each `PlainText` span and exactly one `TagPlaceholder` for each `Tag`
span. For each plain span `i`, record its interval `[a_i, b_i)` in `A`, where `b_i = a_i +
length(text_i)`. Because every tag contributes at least one placeholder character, two plain spans
never touch (`a_j >= b_i + 1`), so a closed-interval lookup `a_i <= k <= b_i` is unambiguous. The
intervals are in ascending order, so the lookup is a binary search.

1. **Annotations**: for each annotation `x` in `analysis.Annotations`: if `x.End` exceeds the
   length of `A`, drop it. Otherwise find the plain span `i` with `a_i <= x.Start` and
   `x.End <= b_i`. When found, add `x.Shifted(-a_i)` to span `i`'s list, preserving order. When
   not found - the annotation covers a placeholder, crosses two plain spans, or lies in no plain
   span - the annotation is **dropped**. It is dropped, never clipped or split, because a clipped
   annotation would misrepresent the unit it marks.
2. **Sentence breaks** (only when `analysis.SentenceBreaks` is not `null`): for each break `k`
   (already sorted and distinct, possibly beyond the text), find the plain span `i` with
   `a_i <= k <= b_i`. When found - including a break that touches a run edge, such as the end of
   the plain text before a tag run or the start of the plain text after it - add `k - a_i` to span
   `i`'s breaks. When not found, the break lies strictly inside a run of consecutive tags (or
   before a leading tag, or beyond the text). If `1 <= k <= length(A)`, position `k - 1` is
   necessarily a placeholder, and the break is attached to that **tag span** as tag offset `1`
   ("immediately after this tag"; offset `0` means "immediately before"), because no plain text
   exists there to hold it and `DefaultModelCapabilityProfile` knows the exact buffer position
   only while it processes the tag. A break with `k = 0` (before a leading tag) or `k > length(A)`
   is dropped. Every plain span then receives a **non-null** (possibly empty) break list, so the
   renderer knows the analyzer's breaks are authoritative for the whole request. When
   `analysis.SentenceBreaks` is `null`, plain spans keep `SentenceBreaks` as `null`.
3. **Result**: tag spans are returned untouched (the same instances) except a tag span that
   carries a break, which becomes `span with { SentenceBreaks = [1] }`; a plain span with
   something to attach becomes `span with { Annotations = ..., SentenceBreaks = ... }`; when
   nothing changes anywhere (no annotation kept and no breaks supplied) the **input list
   instance** is returned.

**Worked example** (a model with no audio-tag support; input `I've [whispers] read it. Dr. Smith
left.`):

- Parsing yields three spans: plain `"I've "`, a `Whispers` tag, and plain `" read it. Dr. Smith
  left."`.
- The analysis text is `"I've "` + the placeholder + `" read it. Dr. Smith left."`. The first plain span
  occupies `[0, 5)`, the placeholder sits at index 5, and the second plain span starts at `a_1 =
  6`.
- An analyzer annotates `read` at index 7 of the analysis text. It lies wholly inside the second
  plain span, so it becomes span-local offset `7 - 6 = 1`.
- The default profile for a model with no tag support buffers `"I've "` + `" read it. Dr. Smith
  left."` as `"I've  read it. Dr. Smith left."`. The second span starts at buffer offset 5, so the
  annotation lands at buffer offset 6, and `buffer[6..10]` is `read`. The chunker then re-bases it
  into the chunk (see *SentenceChunker*).
- For a model with native tag support the buffer is `"I've [whispers] read it. ..."`, the second
  span starts at buffer offset 15, and the same code path produces offset 16 - the tag text the
  profile passes through simply shifts later spans.
- Had the analyzer's annotation covered the placeholder (for example a range from index 3 to 8),
  it would not fit inside either plain span and would be dropped.
- For `First[fast][excited]Second` the analysis text is `"First"` + two placeholders + `"Second"`
  (placeholders at indices 5 and 6). A break at index 6 sits between the two tags, inside no
  plain span, so it is attached to the first tag span as offset `1`; for a model with no tag
  support the profile then buffers `FirstSecond` and splits at index 5, giving `First` and
  `Second`.

**Error Handling**: `SegmentAnnotation` throws `ArgumentOutOfRangeException` for a negative
`Start`, a non-positive `Length`, or a range whose end would overflow, `ArgumentNullException` for
a null `Kind` or `Value`, and `ArgumentException` for a blank `Kind`. `TextAnalysisResult` throws
`ArgumentNullException` for a null annotation list, `ArgumentException` for a null annotation
element, and `ArgumentOutOfRangeException` for a negative break offset; it keeps its own copy, so a
caller changing its lists later cannot alter the result. `TextAnalysisMapper.BuildAnalysisText`
and `Annotate` throw `ArgumentNullException` for a null argument. An annotation or break that
cannot be represented in one plain span is dropped silently by `Annotate` rather than throwing,
because an analyzer's best-effort output must never fault synthesis. A failing analyzer is handled
by `SynthesisSession`, not by this unit: it reports only the exception type name through
diagnostics, never the text or the exception message, and continues without annotations. This
unit never reports diagnostics itself.

**Dependencies**: `TaggedTextSpan` and `TaggedTextSpanKind` from this subsystem's AudioTagParser
unit (the mapper reads and rewrites spans). The `ISynthesisModel.TextAnalyzer` hook in the
ModelManagementSubsystem is the consumer-facing declaration of an `ITextAnalyzer`.

**Callers**: `SynthesisSession` calls `TextAnalysisMapper.BuildAnalysisText`, the model's
`ITextAnalyzer`, and `TextAnalysisMapper.Annotate` between Layer 1 parsing and Layer 2 rendering.
`DefaultModelCapabilityProfile` consumes the annotations and sentence breaks the mapper places on
spans, and `SentenceChunker` consumes `SegmentAnnotation` offsets. A model package implements
`ITextAnalyzer` and reads `SegmentAnnotation` values through `IAnnotatedSynthesisBackend`.
