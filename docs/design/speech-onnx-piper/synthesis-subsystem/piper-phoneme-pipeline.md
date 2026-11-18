### PiperPhonemePipeline

**Purpose**: Turn the text of one speech segment, with the annotations of the English text analysis when there are any,
into the framed phoneme-id sequences that the Piper graph takes.

**Data Model**:

- Constructor: `PiperPhonemePipeline(config)` with the voice configuration (a null configuration throws
  `ArgumentNullException`); the class holds one `EnglishPhonemizer` and one `EspeakIpaRenderer(EspeakIpaProfile.EnUs)`
  and is immutable and thread-safe
- `MaxPartSymbols` = 256, the largest number of phoneme symbols (before the pad symbols) in one utterance
- **PiperUtterance** (record, internal): `Phonemes` (the IPA string), `Ids` (the framed ids), `SymbolCount` (the encoded
  symbols, not counting pad or framing) and `UnknownSymbols` (the distinct symbols the voice's table lacks, in
  first-seen order; they are not in `Ids`)

**Key Methods**:

- **ToUtterances(text, annotations)**: the utterances of a segment in speaking order. It phonemizes the text with the
  annotations (or as written when they are `null`: default readings, no normalization), renders the result with
  `RenderParts(…, 256)`, encodes each part, and keeps the parts with at least one encoded symbol; an empty text or a
  text with no sound in the voice gives an empty list. A null text throws `ArgumentNullException`
- **Encode(phonemes)**: the framing of Piper: the begin ids, then for each Unicode scalar value of the phoneme string
  the ids of its symbol followed by the pad ids, then the end ids. A symbol that is not in the table is skipped and
  listed (once) in `UnknownSymbols`

**Policies.**

- **Never silent**: a skipped symbol is always listed, and the engine counts it. The renderer is tested to emit only
  symbols of this voice's table (all 150 sentences of the renderer's Piper golden and the 360 sentences of the
  renderer's corpora give no unknown symbol), so a non-empty list is a defect to investigate
- **Readings**: the annotations are handed to the phonemizer unchanged, so the reading that the text analysis chose for
  a heteronym is the one spoken; the text spoken without annotations uses the default reading of each word
- **Unknown words**: a word that is in no dictionary is pronounced by the letter-to-sound model of the phonemization
  package, not skipped
- **Sentence segments**: the session speaks the segments of the model's capability profile, which joins the clause
  pieces of a sentence (see *OnnxPiperLibriTtsEnSynthesisModel*), so a segment is normally a whole sentence with its
  commas, and its phoneme string keeps the `,` where the sentence has a comma; the first word of each segment, the
  start of an utterance, gets the renderer's start-of-text mark (*SpeechPhonemesEn WeakForms*)
- **Long text**: a segment that phonemizes to more than 256 symbols is cut between words, preferably after a clause
  mark, by the renderer's `RenderParts`, which is why the limit is applied there and not here

**Error Handling**: a null text or phoneme string throws `ArgumentNullException`; nothing else throws.

**Dependencies**: `PiperVoiceConfig`, `EnglishPhonemizer`, `EspeakIpaRenderer` (SpeechPhonemesEn) and
`SegmentAnnotation` (Speech). The rendering fidelity and the symbol coverage of the renderer are recorded in
`docs/evaluation/phonemes-en/renderers.md`.

**Callers**: `OnnxPiperSynthesisEngine` and the tests.
