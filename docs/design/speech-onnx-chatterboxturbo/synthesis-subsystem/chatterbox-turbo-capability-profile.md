### ChatterboxTurboCapabilityProfile

**Purpose**: The model-specific audio-tag rendering policy, implementing Speech's
`IModelCapabilityProfile`.

**Data Model**: A treatment table (`ChatterboxTurboTagTreatment` per `ChatterboxTurboTagKind`) and
the configured `ChatterboxTurboEndOfSentenceTagPolicy`. Native tags are `[laugh]` (with a `...`
suffix form), `[clear throat]` and `[gasp]`. Sighs and short and long pauses become silence segments
of 300, 300 and 900 ms; every other tag is omitted. No emotion tokens are used.

**Key Methods**:

- **Render(...)**: replaces tags with private-use placeholder characters while splitting text into
  sentences (digit-flanked punctuation is not a boundary), applies the end-of-sentence policy (move
  the tag before the final word, else a per-tag suffix, else strip), attaches a tag between
  sentences to the following sentence, adds a 500 ms pause after ellipsis-ending sentences, and splits
  sentences over 200 characters at `,;:` and then by word budget. Output never contains
  bracketed text for unsupported tags.

**Error Handling**: empty or invalid input produces an empty, safe result.

**Dependencies**: Speech's public capability-profile and rendering types.

**Callers**: `OnnxChatterboxTurboEnglishSynthesisModel` (exposes it) and Speech's renderer.
