# The Listening-Improvement Loop

Measurements against reference engines and held-out data say how often the front end agrees with a label, but only a
listener hears what a voice actually says. The quality of the spoken output is therefore improved in a loop that starts
and ends with listening. Every step of the loop exists in the repository today, and every step after the export is done
by a developer by hand.

**Scope.** The loop improves the DEMA English front end, `DemaConsulting.Speech.Text.En` and
`DemaConsulting.Speech.Phonemes.En`. It therefore applies only to the models that speak through that front end, which
today are the direct ONNX Piper and Kokoro models. Other synthesis models do not use the front end, and speech-to-text
has no text front end at all, so a fix made in this loop does not change them.

The figure below shows the six steps. Follow the arrows from listening to shipping; the dashed arrow back to the start
is the point of the loop: a shipped fix is listened to again, with new text.

![The listening-improvement loop: listen in the Proofreader, flag and export a TSV row with the category, the readings
with their confidence and the phonemes, triage by category, fix the Text.En heteronym classifier with training data or a
cue prior, the Text.En normalization rules, the Phonemes.En lexicon additions and corrections tables or the Phonemes.En
rendering rules, add the flagged sentence to the regression overlay or a golden test, run the quality gate, golden
tests, regression overlay and check commands, ship, and listen again with new
text](technical-overview-listening-loop.svg)

## Listen

The listening tool is `tools/DemaConsulting.Speech.Proofreader`, a desktop application for developers. It is never
packed or shipped, it works offline, and it lists only the models that are already installed. It splits a pasted text,
a `.txt` file or a simple Markdown file into sentences with the library's own English sentence analysis, so the
sentences match what the models speak, and plays them one by one. The next two sentences are synthesized ahead, and
every sentence is cached per model and settings, so a replay or a comparison is instant. In A/B mode each sentence
plays through two models, or one model with two settings, one after the other.

Two tabs follow the current sentence and separate front-end errors from voice errors:

- **Analysis** shows the Text.En analysis: each normalization (the written and the spoken form), each heteronym reading
  with its confidence (low-confidence readings are marked), the pronunciation overrides and the part of speech of each
  token.
- **Phonemes** shows, for Piper and Kokoro, the phoneme string of every text that the backend received, and for each
  word where its pronunciation came from: the lexicon, a heteronym reading, spelling, or the letter-to-sound model.

## Flag and Export

When a sentence sounds wrong, the listener presses `F`, optionally after selecting the offending word, picks a category
and adds a note. A flag records what was actually heard: the model, its settings, and a snapshot of the readings and
the phonemes of that sentence, captured when its audio was played. The categories are `wrong-heteronym`,
`mispronounced-word`, `normalization`, `missing-or-extra-word`, `prosody-or-pause`, `voice-glitch` and `other`.

A session is saved as JSON, and the flags are exported as a tab-separated file with one row per flag. The columns are
the flag number, the sentence, the span and the word that was selected, the category, the note, the model and its
settings, the readings (`word=reading(confidence)` for each heteronym in the span or sentence), the phonemes that the
model consumed and the sentence number. The readings and phonemes columns are what make triage possible without
synthesizing the sentence again.

## Triage by Category

The category says which component made the wrong decision, and so where the fix belongs. The readings and phonemes of
the row confirm it: if the phonemes are right but the audio is wrong, the front end is not at fault.

- **`wrong-heteronym`**: a heteronym read with the wrong reading (`read` as the present instead of the past). It is
  fixed in the Text.En heteronym classifier, with new reviewed rows in its training data in `data/text-en/` and
  retraining, or with a hand-written cue prior. The heteronym flags fixed so far were fixed by cue priors: for one of
  them a round of new training rows was tried and reverted. A retrained model is now accepted by the statistical gate:
  the goldens follow it, and the flip report shows every golden or flagged sentence whose reading it changes.
- **`normalization`**: a number, unit, date, symbol or abbreviation expanded wrongly. It is fixed in the normalization
  rules of Text.En and their abbreviation and initialism tables.
- **`mispronounced-word`**: one word pronounced wrongly. It is fixed in the Phonemes.En lexicon tables in
  `data/phonemes-en/`: the corrections table for a wrong dictionary entry, the additions table for a word the
  dictionary lacks. A wrong reading pronunciation or morphology rule is fixed in its own table or rule.
- **`prosody-or-pause`**: wrong stress, rhythm or pausing. It is fixed in the rendering rules of Phonemes.En (stress,
  weak forms and punctuation); a pause caused by where the text is split into utterances belongs to the chunking
  profile of the model package.

The other categories (`missing-or-extra-word`, `voice-glitch` and `other`) are investigated one by one. Past cases
were fixed in the model package (for example the way the Piper engine framed the start of an utterance) or found to be
the behavior of the voice itself: a flagged `live` whose phonemes were right was traced to the voice's own sampling,
not to the front end, and the settings were kept.

## Fix and Lock It In

A fix is made in the component that the triage named, and the flagged sentence is then added to the evidence so that
the error cannot return unnoticed:

- **A heteronym sentence** becomes a row of the regression overlay, `data/text-en/regression.tsv`, with the origin
  `listening` and a reference to where the flag was recorded. The overlay holds the sentences that the model has to keep
  getting right; it is never trained on, and `check-data` verifies every reference and recomputes whether the sentence
  was seen in training. The data manifest is updated after the edit.
- **Any other sentence** becomes a row of a golden test, such as the sentence golden of Phonemes.En or the pipeline
  goldens of the Piper and Kokoro packages, with its reviewed expected phonemes, or a unit test of the rule that was
  changed. A changed golden line is a behavior change and is reviewed line by line; wrong output of a rule is fixed in
  the code, never in the golden. For a heteronym the goldens pin the committed model's reading, right or wrong: a model
  change that passes the gate regenerates them, and each changed reading is reviewed against the flip report. A golden
  sentence that allows two readings is split into one unambiguous sentence per reading, and a golden or listening
  sentence may never become a training row.

## Run the Gates

Before a change is accepted, the gates must pass:

- **The quality gate**: `evaluate --gate` of the Text.En trainer: the regression overlay, both holdouts and the
  generalization set must each reach their threshold, and the flip report lists every scored sentence that changed (see
  the English front end).
- **The golden tests**: the readings golden (one sentence per reading id, end to end), the sentence, renderer and
  letter-to-sound goldens of Phonemes.En and the pipeline goldens of the Piper and Kokoro packages, run by `build.ps1`
  with the rest of the test suite.
- **The regression overlay**: `evaluate --set regression` reports the overlay with and without the cue priors, and the
  committed evaluation record holds every overlay row, so a changed reading on a flagged sentence changes the record.
- **Reproducibility**: `check-data`, `train --check` and `evaluate --check` verify the data folder, retrain the
  heteronym model and recompute the evaluation record byte for byte; CI runs them on Windows, Linux and macOS. The
  Phonemes.En tools have their own `--check` modes (`build-lexicon`, `train-lts` and `propose-readings`), run by the
  developer after a change to their inputs.

## Ship and Listen Again

The fixed front end reaches users with the next pre-release of the Piper and Kokoro packages, which depend on it. The
loop then starts again with new text, because the regression overlay and the goldens guard only the sentences that
were already heard; new errors are found only by listening to sentences that nobody has heard yet.
