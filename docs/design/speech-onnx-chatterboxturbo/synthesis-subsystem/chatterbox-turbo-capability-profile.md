### ChatterboxTurboCapabilityProfile

**Purpose**: The model-specific audio-tag rendering policy, implementing Speech's
`IModelCapabilityProfile`.

**Data Model**: A treatment table (`ChatterboxTurboTagTreatment` per `ChatterboxTurboTagKind`) and
the configured `ChatterboxTurboEndOfSentenceTagPolicy`. Native tags are `[laugh]` (with a `...`
suffix form), `[clear throat]` and `[gasp]`. Sighs and short and long pauses become silence segments
of 300, 300 and 900 ms; every other non-mood tag is omitted. No emotion tokens are used.

**Moods**: the model has no emotion tokens, so a mood tag selects a different reference clip for the
text that follows. The 15 mood tags and their aliases map to 13 reference clips: excited, enthused, happy, sad,
angry (anger), loud (shout), calm, surprised, bored (tired), worried and panicked (worried),
confused, frustrated, neutral and serious (neutral). A mood applies until the next mood tag; the last
of consecutive mood tags wins. Whispers, sarcastic, soft and breathy remain stripped. Each spoken
segment's text is prefixed with an internal private-use marker character (U+E100 plus the mood's
index in `ReferenceVoiceInfo.KnownMoods`); user text containing marker or placeholder characters has
them removed, so the marker cannot be forged. The marker is carried in `SpeechSegment.Text` so the
public plan types are unchanged, and the engine removes it before tokenization. Where two adjacent
spoken segments differ in mood, the earlier segment's post-silence becomes at least the configured
`MoodJoinSilenceMilliseconds` (default 150 ms, 0 to 1000); a longer ellipsis pause is kept.

**Limitations**: every segment pays its own prefill cost, prosody resets at each join, and mood tags
work best at sentence boundaries. The end-of-sentence tag policy is applied per segment.

**Constructor**: `(policy, moodJoinMilliseconds = 150)`. `ArgumentOutOfRangeException` for an undefined policy
or a join outside 0 to 1000 (both bounds accepted).

**Key Methods**:

- **WithMoodMarker(mood, text)** / **ReadMoodMarker(text, out mood)** (internal): add or split off the leading
  marker; the mood is matched against the known moods case-insensitively after trimming, and an unknown, null or
  blank mood leaves the text unchanged, and reading removes every marker character.
- **Render(...)**: replaces tags with private-use placeholder characters while splitting text into
  sentences (digit-flanked punctuation is not a boundary), applies the end-of-sentence policy (move
  the tag before the final word, else a per-tag suffix, else strip), attaches a tag between
  sentences to the following sentence, attaches a trailing tag-only chunk (a tag with no speech after it) to
  the previous unit so the policy places it, adds a 500 ms pause after ellipsis-ending sentences, and splits
  sentences over 200 characters at `,;:` and then by word budget. The 200-character budget is measured on
  the placeholder text (each native tag is one character) before tags expand to their model tokens, so a
  unit may exceed 200 characters by the expansion. Output never contains
  bracketed text for unsupported tags.
- **AddMoodJoinSilence(segments, moods)** (private): after rendering, for each pair of directly adjacent
  spoken segments whose moods differ (including a change from or to the base clip), raises the earlier
  segment's post-silence to at least the configured join silence, never shortening a longer pause (such as an
  ellipsis pause); pause segments are not spoken, so they are never marked or counted as a mood change. A
  join silence of 0 disables the step.

**Supporting types**: `ChatterboxTurboTagKind` (Native, Pause, Strip or Mood) classifies how a tag is
rendered, and `ChatterboxTurboTagTreatment` (kind, native token, pause milliseconds, suffix form and mood name)
is the per-tag treatment table entry.

**Error Handling**: `ArgumentNullException` for null `spans` or `model`; otherwise empty or invalid input
produces an empty, safe result.

**Dependencies**: Speech's public capability-profile and rendering types.

**Callers**: `OnnxChatterboxTurboEnglishSynthesisModel` (exposes it), Speech's renderer and
`OnnxChatterboxTurboSynthesisEngine` (which calls `ReadMoodMarker` to split the mood marker off a segment).
