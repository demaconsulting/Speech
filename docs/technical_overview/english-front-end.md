# The English Front End

A voice graph of the direct ONNX Runtime models takes a sequence of phoneme symbols, not text. The English front end
produces that sequence in two stages: `DemaConsulting.Speech.Text.En` decides what is said, and
`DemaConsulting.Speech.Phonemes.En` decides how each word sounds and how the voice writes it.

In a synthesis session the library runs the model's text analyzer once over the whole request, before the text is cut
into chunks, so every decision sees the full sentence. Each chunk then reaches the model's backend with the annotations
that cover it, and the backend phonemizes the chunk with them. The text itself is never rewritten: the analysis
produces annotations over spans of the original text, and the phonemizer applies them. A host that calls a backend's
plain `Generate(text, ...)` directly, outside a session, bypasses the analysis and gets the default reading of each word
and no normalization.

## Text Normalization

The normalization of `DemaConsulting.Speech.Text.En` annotates only what is spoken differently from how it is written.
Each annotation names a span of the text and the words to say instead (`en.norm`) or the letters to spell (`en.spell`):

- **Abbreviations and symbols**: `Dr.` is `Doctor`, `St.` is `Saint` or `Street` from the words around it, `&` is `and`.
- **Initialisms**: `FBI` is spelled `F B I`, from a reviewed table of initialisms; acronyms that are said as words
  (`NASA`) are left as written.
- **Numbers in US English**: cardinals, decimals, ordinals, percentages, fractions, years and decades, currency
  (`$5.50`), a closed list of units (`5 kg`, `72°F`), dimensions, clock times, dates, ranges, phone numbers, the number
  of a hyphenated compound (`12-year-old`) and a number with a glued capital (`10K`, `3D`).
- **Web addresses**: URLs, e-mail addresses and bare domains are spoken with their separators as words.
- **Identifiers**: letter and digit identifiers (`A4`, `MP3`) and names joined to a number (`COVID-19`).
- **Host overrides**: a host can supply a pronunciation override dictionary for words the built-in rules get wrong.

The rules are deterministic code and two small embedded tables (abbreviations and acronyms). Normalization uses the
tokens alone; it does not need the part-of-speech tagger. The complete list, with the limitations, is in the text
analysis chapter of the user guide.

## The Heteronym Classifier

A heteronym is a word with more than one pronunciation for one spelling: `record` the noun and the verb, `read` in the
present and the past, `wound` the injury and the past of `wind`. The analyzer tags each word with its Universal part of
speech (through the separate `DemaConsulting.PartOfSpeech` tagger) and then lets an embedded model choose the reading of
every heteronym in its sentence. The choice is an annotation (`en.reading`, for example `record.noun`) with a confidence
(`en.reading-confidence`, from 0 to 1).

The model is a candidate-restricted averaged perceptron. It scores only the readings that the inventory allows for the
written form, using hashed features of the word, its neighbors and their parts of speech. The weights are integers, and
a temperature chosen on the development set turns the scores into calibrated confidences. Four hand-written cue priors
(adjective, noun-form, past-time and context cues) add a fixed bonus to a reading in constructions that listening found
the model getting wrong. The inventory holds 395 readings of 207 words; a heteronym outside it gets no reading, and the
model sees one sentence at a time.

Training is deterministic: one thread, integer arithmetic, a fixed random generator and ordinal sorting. The trainer
(`tools/DemaConsulting.Speech.Text.En.Trainer`) reads only the training, development, inventory and form files, so the
holdout sets cannot influence a model, and `train --check` retrains from the committed data and fails if the embedded
model differs by a single byte.

### The Quality Gate

A model is accepted only if it passes a quality gate that was fixed before any holdout data was read. The gate is
evaluated end to end through the real analyzer on the natural holdout, 1,167 labeled words from 266 source documents
that are never used for training or selection. It requires both:

- at least 93.0 percent pooled accuracy, and
- an improvement over the reading that the part-of-speech tag alone would give, whose 95 percent interval (a paired
  bootstrap that resamples whole source documents) lies above zero.

The command is `evaluate --gate` of the trainer. Reading a holdout set is treated as an irreversible act, so every run
of the gate is counted in the trainer's run log. The current figures are in the section on accuracy and evaluation.

## The Pronunciation Lexicon

`DemaConsulting.Speech.Phonemes.En` gives each word a pronunciation in ARPAbet phones with stress digits. It tries
these sources in order and uses the first that answers:

1. **The chosen reading.** For a heteronym, the reviewed pronunciation of the reading that the analysis chose (one row
   per reading id; inflected forms add the sound of their ending).
2. **Lexicon corrections.** A small reviewed table of CMU Pronouncing Dictionary entries that are transcription errors.
3. **The dictionary.** The first variant of the word in the embedded lexicon, which is derived from the CMU Pronouncing
   Dictionary: 124,926 words and 133,971 pronunciations.
4. **Lexicon additions.** A small reviewed table of words the dictionary lacks whose automatic pronunciation a listener
   flagged. An addition never shadows a dictionary word.
5. **Morphology.** Rules that derive a word from a known one: inflections (`-s`, `-ed`, `-ing`, `-er`, `-est`, `-ly`),
   productive prefixes (`un-`, `re-`, `over-`) and hyphenated compounds with one primary stress.
6. **Letter-to-sound.** A trained model for any word that nothing above knows.

A host pronunciation override comes before all of these, because it replaces the word entirely. The corrections and
additions tables are the place where a mispronounced word is fixed without touching the dictionary image or its pins.

## The Letter-To-Sound Model

The letter-to-sound model pronounces words that the dictionary and the morphology rules do not know, so an unknown name
or a new word is spoken instead of skipped. It holds three small neural networks, one for the phones and one each for
the primary and the secondary stress, quantized to integers and trained from the pinned dictionary alone. The training
is deterministic, so `train-lts --check` of the Phonemes.En tools retrains the model and compares it with the embedded
one.

## The Renderers

The last stage writes the pronounced words in the symbol set of the voice:

- **Piper** takes espeak-style IPA. Its renderer is a rule-based approximation of the style that the voice was trained
  with; it is not espeak-ng and uses no espeak-ng data.
- **Kokoro** takes the symbol set of its own vocabulary. Its renderer speaks function words in their weak forms by
  default. The British Kokoro voices receive General American phonemes, so they speak with an American pronunciation.

The renderers own the decisions that a listener hears as stress and rhythm: where a primary and a secondary stress
fall, which function words are reduced, how punctuation is passed to the voice as pause symbols, and where a long text
is split between words into parts that fit the voice graph. Every symbol that a renderer can emit is proved to be in
the voice's symbol table by tests.
