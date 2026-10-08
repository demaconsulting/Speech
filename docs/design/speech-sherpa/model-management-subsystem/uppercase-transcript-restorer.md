### UppercaseTranscriptRestorer

**Purpose**: Turn a streaming recognizer's UPPERCASE, unpunctuated raw output into readable
prose, so recognition consumers see cased, punctuated text instead of a shout. Shared by any
`IRecognitionModel` whose raw output empirically matches this style via that model's own
`IRecognitionModel.NormalizeText` override.

**Data Model**: N/A - a stateless internal static helper class with two entry points and no
mutable shared state; every regular expression is a compiled, immutable `static readonly`
field.

**Key Methods**:

- **RestoreFinal(rawText)**: the full restoration pipeline for a finalized recognition result -
  (1) lowercase the raw text, (2) restore a closed, conservative set of 28 contractions (whole
  word, case-insensitive - see below), (3) capitalize the standalone word "I" and the first
  alphabetic character, (4) append a terminal period if the text does not already end in `.`,
  `?`, or `!`. Ported verbatim (contraction table and exclusion list) from the reference
  `HiArc.AI.Speech.DictationRestorer` implementation, deliberately dropping that reference's
  glossary-casing stage and optional trained-punctuation-model stage entirely: this generic
  library has no doc-set/glossary concept, and no punctuation model is being integrated in this
  phase - matching the reference's own shipping default of heuristics-only.
- **RestoreProvisional(rawText)**: the cheap path for a still-forming provisional result -
  lowercase, then capitalize the standalone word "I" and the first letter only. No contraction
  restoration and no terminal-punctuation insertion, since a provisional hypothesis may still be
  revised or superseded many times before it settles.
- **DeliberatelyAmbiguousForms()**: enumerates the 12 forms deliberately left unchanged because
  their non-contraction reading is common in ordinary prose (see below) - exposed so tests can
  assert every excluded form is genuinely left alone.

**Contraction table** (28 entries, keyed by the apostrophe-free lowercase form): `im`→`I'm`,
`ive`→`I've`, `dont`→`don't`, `cant`→`can't`, `wont`→`won't`, `didnt`→`didn't`,
`doesnt`→`doesn't`, `isnt`→`isn't`, `arent`→`aren't`, `wasnt`→`wasn't`, `werent`→`weren't`,
`havent`→`haven't`, `hasnt`→`hasn't`, `hadnt`→`hadn't`, `wouldnt`→`wouldn't`,
`couldnt`→`couldn't`, `shouldnt`→`shouldn't`, `thats`→`that's`, `whats`→`what's`,
`theres`→`there's`, `heres`→`here's`, `youre`→`you're`, `youll`→`you'll`, `youve`→`you've`,
`theyre`→`they're`, `theyll`→`they'll`, `theyve`→`they've`, `weve`→`we've`.

**Deliberately ambiguous exclusions** (12 forms, left alone): `were` (past-tense "be" vs.
we're), `well` (noun/adverb vs. we'll), `ill` (sick vs. I'll), `its` (possessive vs. it's,
extremely common), `id` (identifier vs. I'd), `lets` (allows vs. let's), `shell`/`shed`
(the noun/past-tense verb vs. she'll/she'd), `hell`/`hed` (the noun/past-tense verb vs.
he'll/he'd), `wed` (the noun vs. we'd), `whos` (rare, but ambiguous with "who's").

**Error Handling**: Throws `ArgumentNullException` for a null `rawText`. Empty or
whitespace-only input returns the empty string from both entry points without throwing. Both
methods are pure and idempotent for already-restored input (re-running `RestoreFinal` on its own
output is a no-op beyond possible whitespace collapsing).

**Thread Safety**: Fully stateless - every pattern is a compiled, immutable `Regex` held in a
`static readonly` field, and every member is a pure function of its input. Both entry points are
safe to call concurrently from any number of threads.

**Dependencies**: `System.Text.RegularExpressions.Regex`, `System.Globalization.CultureInfo`.

**Callers**: `SherpaOnnxZipformerEnRecognitionModel.NormalizeText` (explicit
`IRecognitionModel` override, delegating `isFinal: true` to `RestoreFinal` and
`isFinal: false` to `RestoreProvisional`); any future `IRecognitionModel` whose raw output is
empirically confirmed to match this "yelling" style may reuse it the same way (see
`SherpaOnnxNemotronStreamingEnRecognitionModel`'s XML remarks for the empirical check that
decided it does *not* need this restorer).
