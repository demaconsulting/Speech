<!-- markdownlint-disable MD013 MD060 table-too-wide -->

# Text.En Heteronym Data

License: CC0-1.0 AND CC-BY-3.0-US AND Public-Domain

Training, development and holdout data, the reading inventory and the provenance records for the English heteronym
classifier of `DemaConsulting.Speech.Text.En`. A *heteronym reading* is one pronunciation of a written word (`record` as a
noun or as a verb, `read` in the past or the present). The classifier picks the reading of every ambiguous word in context;
this folder holds what it is trained on and checked against. Everything here is open data with its license and provenance
recorded per file and per row. Nothing here is legal advice.

## Contents

| File | Rows | What | License |
| --- | ---: | --- | --- |
| [readings.tsv](readings.tsv) | 395 | inventory v1.1: one row per reading (`reading_id lemma pos_hint description status`), 207 lemmas: the 318 readings of the 162 WHD homographs plus 77 extension readings | CC0-1.0 |
| [forms.tsv](forms.tsv) | 853 | candidate readings per surface form (`form reading_id kind`): 501 forms | CC0-1.0 |
| [whd-mapping.tsv](whd-mapping.tsv) | 326 | id-to-id mapping `whd_wordid reading_id` for the external evaluation (ids only, no text, no IPA, no gloss) | CC0-1.0 |
| [bench-mapping.tsv](bench-mapping.tsv) | 60 | id-to-id mapping `bench_key reading_id` for the external evaluation: the benchmark's own reading key of a word to our reading id (ids only, no text, no IPA, no gloss) | CC0-1.0 |
| [train.tsv](train.tsv) | 19,865 | training rows: 13,064 synthetic (9,242 first round, 3,172 second round, 650 third round), 1,621 LLM-labeled natural, 5,180 UPOS-derived natural | per row (`license` column) |
| [dev.tsv](dev.tsv) | 3,235 | development rows (model selection): 1,934 synthetic, 317 LLM-labeled natural, 984 UPOS-derived natural | per row |
| [holdout-natural.tsv](holdout-natural.tsv) | 1,167 | natural rows reserved for the final accuracy gate, document-separated; never used for training or model selection (283 LLM-labeled, 884 UPOS-derived) | per row |
| [holdout-independent.tsv](holdout-independent.tsv) | 1,972 | test-only rows, 5 per reading (2 for `jesus.spanish`), written blind by a different model family than the training data | CC0-1.0 |
| [harvest-provenance.tsv](harvest-provenance.tsv) | 9,269 | per natural row: source document, PartOfSpeech corpus, sentence id, label method (`upos` or `llm`) | CC0-1.0 |
| [synthetic-provenance.tsv](synthetic-provenance.tsv) | 14,998 | per synthetic row: generation batch, template (split unit), id in the first-round spike | CC0-1.0 |
| [exclusions.tsv](exclusions.tsv) | 70 | rows and candidates removed (evaluation-set overlap, audit and holdout-review drops, template conflicts, one skipped by the labeler) with reason | CC0-1.0 |
| [gaps-v4.tsv](gaps-v4.tsv) | 395 | per reading: train natural/synthetic, dev, holdouts, synthetic and natural rows still needed | CC0-1.0 |
| [extension-candidates.tsv](extension-candidates.tsv) | 60 | the ranked proposal of heteronyms that inventory v1 did not cover (natural frequency, class Y/R/S) | CC0-1.0 |
| [extension-decisions.tsv](extension-decisions.tsv) | 60 | the decision per candidate (add or defer) with the CMUdict evidence | CC0-1.0 |
| [leakage-report.tsv](leakage-report.tsv) | 96 | per evaluation set and split: exact, near-duplicate and 5-gram overlap counts | CC0-1.0 |
| [regression.tsv](regression.tsv) | 519 | evaluation overlay: owner-flagged sentences, analyzer and golden test sentences and selected training and development rows, each referenced and marked seen or unseen in training (see "Evaluation Overlays") | per row |
| [generalization.tsv](generalization.tsv) | 0 | the blind generalization test set; header only until the blind writer has written it (see "Evaluation Overlays") | CC0-1.0 |
| [audit/](audit/audit-summary.md) | 6 files | blind relabel audit, adjudication, holdout review and final review: summary, the 85 adjudicated rows, the 13 description fixes, the consistency fixes, the holdout review, the final review | CC0-1.0 |
| [MANIFEST.tsv](MANIFEST.tsv) | | size, SHA-256 and row count of every file; verified by `check-data` | CC0-1.0 |
| [check-output.txt](check-output.txt) | | output of the Python prototype of the checks (schema, licenses, leakage) and of the gap report that built these files; kept as the historical baseline that `check-data` ports | CC0-1.0 |
| [prompts/](prompts/README.md) | 11 | the prompts that produced the generated, LLM-labeled and audited data | CC0-1.0 |
| [ATTRIBUTION.md](ATTRIBUTION.md) | | attribution for the MASC-derived sentences | |

Row format of `train.tsv`, `dev.tsv`, `holdout-natural.tsv` and `holdout-independent.tsv` (UTF-8, LF, no quoting, header row):

```text
id  lemma  reading_id  sentence  start  end  source  license  split
```

`sentence[start:end]` is the surface form of the target word (UTF-16 code units, end exclusive, NFC text). `lemma` is the
dictionary lemma of the reading (`wound` meaning "coiled" has lemma `wind`). `source` is `synthetic` (first round), `synthetic-v2`
(second round), `synthetic-v3` (third round), `independent-holdout` or `harvest-<corpus>[-llm]`. `license` is the license class of the sentence text: `CC0-1.0`,
`CC-BY-3.0-US` or `Public-Domain`. `split` is `train`, `dev`, `holdout-natural` or `test` (`holdout-independent.tsv` only). The
sentences of natural rows are tokenized by single spaces in MASC and silver text (`ca n't`), as in the PartOfSpeech corpora.

**Read these files with quoting disabled** (split on tabs): sentences contain double quotation marks and some start with one. A reader with CSV quoting
enabled drops the leading quotation mark and shifts the offsets of such a row by 1 to 3 characters (this is what made a blind review report 7 "invalid spans"; the files were
correct, see Audit). `check-data` validates every row with a strict whole-word span check (scalar-aware, UTF-16 offsets).

### TSV conventions

Every `.tsv` file of this folder follows the same conventions, and `check-data` enforces them:

- Columns are separated by one tab character. There is **no quoting and no escaping**: a C# reader must split every line on `'\t'` and must not use a CSV or
  quoting parser. A sentence that starts with `"` keeps the quotation mark, and its `start` and `end` offsets count it.
- UTF-8 without a byte-order mark, LF line endings (no CR), a final newline, no empty line, the same number of columns in every line, and text in Unicode
  normalization form NFC.
- The first line is the header row. `#` comment lines occur only in `MANIFEST.tsv`.
- Offsets are UTF-16 code units (the native .NET `string` index), `start` inclusive and `end` exclusive.

## Rules of This Folder

- **Open licenses only.** CC0, public domain and CC BY (with attribution). No share-alike, non-commercial or no-derivative
  material. `check-data` enforces it (see "Check" below): a license allow-list, a per-source license policy and a list of forbidden terms.
- **Evaluation-only sets are never committed.** WikipediaHomographData (CC BY-SA text) and heteronym-bench (CC BY-SA data)
  are fetched at evaluation time only. The only traces of them here are the id-only `whd-mapping.tsv` and `bench-mapping.tsv`. No sentence of any
  evaluation set is in these files (`check-data` with the external leakage check, see "Leakage"). Misaki data is not used anywhere in this folder.
- **Reading ids are self-owned.** `lemma.discriminator[.sub]`, lowercase ASCII `[a-z0-9-]`, immutable once released
  (a mistake is fixed by deprecating the id and adding a new one), versioned inventory, no IPA and no third-party text.
- **Model-written labels are labeled as such.** Generated sentences and LLM-chosen readings were written by language models; every
  such row in `train.tsv`, `dev.tsv` and `holdout-natural.tsv` was judged blind by a second model of another family (see Audit for the exact coverage); no human reviewed them. The row `source` and the provenance files say which rows are which.

## Provenance and Licensing

### Natural sentences (harvest): 9,269 rows in 2,526 documents

Sentences are the training sentences of the PartOfSpeech corpora (data identical to PartOfSpeech tag `0.1.0-beta.4`; `git diff
0.1.0-beta.4 HEAD -- data` is empty). Each row records its document in `harvest-provenance.tsv` (`source_id`).

| Corpus (`source`) | Sentence license class | Basis |
| --- | --- | --- |
| `harvest-masc` | `CC-BY-3.0-US` (attribution required, see [ATTRIBUTION.md](ATTRIBUTION.md)) | MASC written genres, CC BY 3.0 US |
| `harvest-open-train` | `CC0-1.0` (Tatoeba) or `Public-Domain` (US federal works, Project Gutenberg) | per document statement in PartOfSpeech `open/sources.tsv` |
| `harvest-silver` | `CC0-1.0` (Tatoeba) or `Public-Domain` (Gutenberg) | PartOfSpeech `silver/sources.tsv` |
| `harvest-imperative`, `-compound`, `-compound-2`, `-tech-*`, `-headings`, `-headings-2` | `Public-Domain` (US federal works, 17 U.S.C. 105) | PartOfSpeech `*/sources.tsv` |

`check-data` does not recompute the license class from the PartOfSpeech source tables (that recomputation was done once while the data was built; its
result is recorded in `check-output.txt`, and the PartOfSpeech data is not available to the check). It enforces a per-source license policy instead:
`harvest-masc` and `harvest-masc-llm` rows are `CC-BY-3.0-US` only, `harvest-open-train*` and `harvest-silver*` rows are `CC0-1.0` or `Public-Domain`, every
other `harvest-*` row is `Public-Domain` only, and every harvested row has an entry in `harvest-provenance.tsv`. The sentences
are not claimed by DEMA Consulting. Labels are two kinds:

- `label_method = upos` (7,048 rows, 1,847 of them for the extension readings): the reading is derived by rule from the part-of-speech tag
  in the PartOfSpeech corpus (UPOS to reading, only for words whose readings are fully decided by the part of speech; the PartOfSpeech
  tags are themselves Penn-treebank-derived for MASC and model-written elsewhere). Rule and exclusions: see "Harvest rules" below.
- `label_method = llm` (2,221 rows, `source` ends in `-llm`): the part of speech does not decide the reading (`read`, `wind`, `job`,
  `reading`, `used`, `tears`, `recount`, ...); a language model chose the reading from the sentence and skipped what it could not decide.
  First round: 1,405 rows (1,410 labeled by `claude-sonnet-5.5` on 2026-11-15, [prompt](prompts/labeling-harvest-llm.md), plus the 2 re-marked `recount` rows, minus 7 rows dropped by the audit); second round: 818
  rows for the extension forms (`claude-sonnet-5.5`, 2026-11-16, [prompt](prompts/labeling-natural-v2.md); 1 candidate skipped as garbled). The 2 `recount`
  rows that the rule had produced were checked by hand and re-marked as labeled (owner decision).

### Synthetic sentences: 14,998 rows

- **First round**: 10,528 rows, generated on 2026-11-15 by four GitHub Copilot CLI sub-agents (`general-purpose`) running Anthropic
  `claude-sonnet-5.5`, each writing from scratch for the (homograph, reading) pairs of its share, with no access to any test set, the web or
  any external corpus ([prompt](prompts/generation-synthetic.md)). Part 3 was assembled from 28 batch files by a validating script; parts 1 and
  2 left no batch records. 28 percent of the rows use an inflected or irregular form (`forms.tsv` kind `derived`; those forms usually leave no
  noun/verb ambiguity, so they add generic context signal).
- **Second round**: 3,820 rows, generated on 2026-11-16 by four sub-agents (`synth2-0` to `synth2-3`, `claude-sonnet-5.5`) for the readings that
  still lacked rows (the extension readings and the readings short of 40 rows), writing in batches of 5-10 sentences per reading
  (602 batches, `synthetic-provenance.tsv`) and seeing the reading descriptions but not the holdout, any evaluation set or the web
  ([prompt](prompts/generation-synthetic-v2.md)). The `source` is `synthetic-v2`.
- **Third round**: 650 rows (664 written, 14 dropped because their template was already a dev template of the same reading), generated by two sub-agents (`synth3-0`,
  `synth3-1`, `claude-sonnet-5.5`; 2026-11-17 00:16 to 00:20 local time, 05:16 to 05:20 UTC) to fill the remaining gap to 40 training rows per reading, all train
  ([prompt](prompts/generation-synthetic-v3.md)). The `source` is `synthetic-v3`; all 650 rows were reviewed blind afterwards (see Audit).

Every row was validated (offset slices to the target form, reading exists, no duplicate sentence, one target per sentence).

### Independent holdout: 1,972 rows

`holdout-independent.tsv` was written on 2026-11-16 by one sub-agent running OpenAI `gpt-5.6-luna`, a different model family than the authors of the
training data (`claude-sonnet-5.5`). It saw only `readings.tsv` and `forms.tsv` (never any training, development or labeled data) and wrote 5 sentences
for every reading, with realistic and difficult phrasing ([prompt](prompts/holdout-independent.md)). It is **test-only**: no model selection, no training, no
tuning on it. 3 rows (bare personal-name `jesus.spanish` sentences) were dropped in the consistency re-check, leaving 1,972 of 1,975.
Because the writer was told to cover every reading, it is balanced over readings, not over natural frequency.

### CC0 dedication

DEMA Consulting, as the owner of the generated and LLM-labeled content, dedicates to the public domain under
[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/): the synthetic sentences (both rounds), the independent holdout, the readings chosen by
language models for natural sentences, the UPOS-to-reading labels, the audit records and the selection and arrangement of all files in this folder
(including the inventory, mappings, splits and reports). This is not a claim on the underlying natural sentences, whose licenses are stated per row.
The generation, labeling and audit sub-agents' output was accepted under the terms of the GitHub Copilot service as operated by the owner (provider terms
review: owner decision D10, approved). Models: Anthropic `claude-sonnet-5.5` (generation and labeling), OpenAI `gpt-5.6-luna` (independent holdout and
blind audit) and OpenAI `gpt-6.1-sol` (adjudication).

### Inventory and mappings

`readings.tsv`, `forms.tsv`, `whd-mapping.tsv` and `bench-mapping.tsv` are written by DEMA Consulting under CC0-1.0. The WikipediaHomographData ids
(`whd_wordid`) were used only to enumerate the homographs and their number of readings; descriptions are written in our own
words and contain no IPA, no WHD gloss and no WHD sentence. `bench-mapping.tsv` pairs the reading key that heteronym-bench gives each of its 30 words
(an identifier such as `reed` or `red`) with our reading id; it was checked against the benchmark's own description of each key and contains no benchmark sentence, IPA or gloss.

## Splits

- **Natural rows**: the unit is the source document (`harvest-provenance.tsv` `source_id`; a Tatoeba sentence is its own document,
  a MASC file, Gutenberg book or federal document holds many sentences). Bucket = first 8 hex digits of
  `SHA-1("text-en-split-v1|doc|<source_id>")` mod 100: 0-9 `holdout-natural`, 10-21 `dev`, otherwise `train`. All rows of a
  document, including both targets of one sentence and the LLM-labeled rows of the second round, are in one split. Result: holdout-natural 1,167 rows
  (12.6%) in 266 documents, dev 1,301 rows (14.0%) in 318 documents, train 6,801 rows in 1,942 documents.
- **Synthetic rows**: the unit is a *template*: the sentence with the target word replaced by `_` and kept two words on each side
  (lower-cased), for example `the royal _ was`. First round: the templates of a reading are ordered by
  `SHA-1("text-en-split-v1|syn|<reading_id>|<template>")` and moved to `dev` until 12 percent of the reading's rows are in dev. Second round: the generator
  assigned whole batches (about 85 percent train, 15 percent dev; `synthetic-provenance.tsv` `batch`). A batch is not a separation unit: after the 12 `move-to-train` fixes
  below, 7 of the 602 second-round batches have rows in both train and dev. The checks enforce that a
  (reading, template) pair is not in both train and dev; 12 dev rows whose template was also in train (after the second round and the relabels) were moved to
  train (`audit/consistency-fixes.tsv`, action `move-to-train`).
- **Holdouts**: `holdout-natural.tsv` is natural only and document-separated; `holdout-independent.tsv` is synthetic by another model family. Neither is used
  for training or model selection. No sentence is in two files, and `holdout-independent.tsv` was deduplicated against train and dev (no overlap was found).
- The checks fail if a sentence, a natural document or a synthetic template is in two splits. The separation units that are enforced are the **document** (natural rows)
  and the **(reading, template) pair** (synthetic rows); the second-round batch is not enforced.

## Leakage

All rows were compared with every evaluation set: WHD train, eval and its converted dev (not committed), heteronym-bench (all 566
items), PartOfSpeech `pos-gold`, `user36` (36 adversarial sentences) and `branch-gold` (the 500-sentence gold set with its listening pack, both from a local pre-release experiment),
the PartOfSpeech open dev and test, gold v2, shape probe, STE challenge and the shapes development tranches (24 sets,
31,324 sentences, 28,708 distinct keys). Rule: the key is the lower-case alphanumerics of the sentence (exact normalized match);
a near duplicate is a sentence of at least 8 words whose word 5-grams are at least 80 percent contained in the evaluation 5-grams.

- Removed before the files were written: 7 exact matches (all synthetic; short generic sentences such as "The dove is a symbol of
  peace." that appear in heteronym-bench or the `branch-gold` set) and 31 near duplicates (30 natural: boilerplate shared with the PartOfSpeech open
  dev/test sentences, the shapes development tranche of defined terms and pos-gold; 1 synthetic of the second round). List: `exclusions.tsv`.
- Remaining: 0 exact, 0 near duplicates in all four data files. Rows that share at least one word 5-gram with some evaluation set (generic phrases such
  as "at the end of the"): train 688 of 19,865, dev 93 of 3,235, holdout-natural 68 of 1,167, holdout-independent 4 of 1,972. Counts per set: `leakage-report.tsv`.

### Last full external leakage result

The last full run of the external leakage check was the Python prototype recorded in `check-output.txt` (2026-11-17), over the 24 evaluation sets named above
(31,324 sentences, 28,708 distinct keys): **0 exact matches and 0 near duplicates** in all four data files. Rows that share at least one word 5-gram with at least
one evaluation set: train 688 of 19,865, dev 93 of 3,235, holdout-natural 68 of 1,167, holdout-independent 4 of 1,972 (per set and split in `leakage-report.tsv`).

WikipediaHomographData and heteronym-bench are **evaluation-only and never committed**; the only traces of them in this folder are the id-only `whd-mapping.tsv` and `bench-mapping.tsv`.
Because the evaluation sets are not committed, `check-data` runs only the internal separation checks by default (and so does CI). The external check must be
re-run by an operator with local copies of the evaluation sets before any change of the data (command and options in "Check" below).

## Evaluation Overlays

Two evaluation files sit beside the splits. They are not splits: they are not part of the split separation, the per-reading
counts or `gaps-v4.tsv`, nothing is trained on them, and `check-data` checks them with rules of their own (`overlay`
findings). The protocol and the pre-registered use of both are in the trainer's `RUN-LOG.md`, entry "Word-cluster
subsystem (PR A)".

### Regression overlay: 519 rows

`regression.tsv` (`id lemma reading_id sentence start end origin ref seen_in_training license`) collects the sentences the
model has to keep getting right. Every row refers to where it comes from (`origin` and `ref`):

| Origin | Rows | Seen in training | Reference |
| --- | ---: | ---: | --- |
| `listening` | 7 | 0 | the owner's listening flags and their controls: *grey and very minute and clear*, *The difference was extremely minute*, *a knife wound or a deep muscle tear* (two targets), *Yesterday I read the whole book*, *The tape wound onto the spool*, *A tear ran down her cheek* (`RUN-LOG.md:<line>` or `<Name>Tests.cs:<line>`) |
| `analyzer-test` | 11 | 0 | the analyzer tests that assert a reading with the embedded model (`<Name>Tests.cs:<line>`) |
| `golden-pipeline` | 37 | 0 | the heteronym targets of the Piper pipeline golden sentences (the Kokoro golden has the same sentences), labeled from the reviewed expected phonemes (`piper-pipeline-golden.tsv:<id>`) |
| `golden-sentences` | 61 | 0 | the heteronym targets of the sentence golden whose expected phonemes match the reviewed readings golden of exactly one candidate, plus six inflected forms labeled from their phonemes and sentence (`sentences-golden.tsv:<id>`) |
| `golden-readings` | 382 | 6 | the end-to-end rows of the readings golden, one per reading (`readings-golden.tsv:<line>`); 3 rows are already in the overlay as listening or analyzer rows |
| `data-row` | 21 | 19 | training and development rows of the flagged constructions: `wound.injury` with *knife*, *stab* or *bullet*; `tear.rip` with a word of the body or a material; `minute.tiny` after a degree adverb; `read.past` with a past-time phrase (`train.tsv:<id>` or `dev.tsv:<id>`) |

`seen_in_training` is `yes` exactly when the row refers to a training row, its sentence key equals a training sentence or
it nearly repeats one (the near-duplicate rule of "Leakage"); `check-data` recomputes it. No row refers to or repeats a
holdout sentence. A `data-row` reference must match the referenced row (lemma, reading, sentence, offsets and license);
the golden and test references are resolved by a trainer test that reads the repository's test files. Own-written and
golden rows are `CC0-1.0`; the listening sentence *To the eastward, of course, these little boats ...* is quoted from a
public-domain book and is `Public-Domain`; `data-row` rows carry the license of the referenced row. The file was built
once by a throwaway script from the golden files, the analyzer tests, the run log and the data rows.

### Generalization set (written blind)

`generalization.tsv` (`id lemma reading_id sentence start end kind family cue_forms cue_unsolvable source license`) is a
test set written blind by a model of another family that sees only `readings.tsv`, `forms.tsv` and
its prompt `prompts/generalization.md`, which is committed before the set is written. It covers 13 target words in 27 target slots. Its
rows are novel by construction, and `check-data` enforces the novelty against the data as it is now:

- **N1**: no sentence equals a sentence of the four data files or of the regression overlay, or nearly repeats a training
  or development sentence;
- **N2** (`kind` `novel-vocabulary`): every designated cue word (`cue_forms`) is absent from the training and development
  sentences of the target's lemma group (the lemmas whose readings share a form, such as `wind` and `wound`);
- **N3** (`kind` `novel-construction`): no word trigram around the target (positions -2..0, -1..+1, 0..+2, target
  replaced) occurs at the same position around the target of a training or development row of the group.

Once the set has rows it must have at least 30 families (`family`, the cue or template family, the unit of the bootstrap)
of 2 to 12 rows, 90 `novel-vocabulary` and 70 `novel-construction` rows, 60 rows that the curated cues of the classifier
cannot solve (`cue_unsolvable=yes`) over 8 lemmas, and rows for at least two readings of every target. Until the blind
writer has written it, the file has its header only and these quotas do not apply. A staged copy is checked before it is
committed with `check-data --generalization <file>`, whose novelty findings give a generic reason and never name a
training row. Every row is `CC0-1.0` and has the source `generalization`.

## Harvest rules

- Reading from UPOS only when the part of speech decides every reading of the homograph and exactly one reading covers that tag;
  lexical sense splits (tear, wind, winds, wound, bow, bass, sow, dove, row, axes) and `recount` are never derived from UPOS.
- Plural and third-person `-s/-es/-ies` forms of the noun/verb stress- or vowel-split words (`projects`, `records`) are harvested;
  `-ed/-ing` forms are not (always verbal). Voicing-only splits (house, close, mouth, excuse) are not harvested for inflections.
- Sentences are deduplicated by their alphanumeric lower-case key in the priority order masc, open-train, imperative, compound,
  compound-2, technical, headings, silver. `+foreign`, `disputed`, masked tokens and silver rows below 90 percent confidence are
  not used as rule-labeled rows; for the extension forms they went to the labeling candidates.
- Extension readings use the same rules on the forms listed in `extension-decisions.tsv` (exact lower-case form match; a token tagged
  by one UPOS that exactly one reading of the lemma covers). Forms whose reading the tag never decides (`used`, `rows`, `bows`,
  `putting`, `primer`, `sewer`) and tags that no reading covers (a proper noun, an unexpected tag) went to the labeling candidates (819 sentences from the
  TRAINING corpora only, document-split like the other natural rows, minus evaluation overlaps; 818 labeled, 1 skipped). Per-form
  exceptions were made in the one-off builder script (not part of the repository): `leads` and `tears` as verbs, `contents`, `closer`, and `does` as AUX/VERB (the NOUN tags of `does` are tagger errors).

## Inventory v1.1: mapping decisions and known quirks

Reading ids use the discriminators `noun`, `verb`, `adj`, `adj-noun` (an adjective and noun reading that share one pronunciation; owner
decision, accepted), `adv`, `adv-prep`, and meaning words (`bass.fish`, `lead.metal`, `tear.rip`) where the part of speech does not name the reading.
Plural nouns of another lemma keep their own lemma (`axis.plural`, `life.plural`, `analysis.plural`, `diagnosis.plural`; owner decision, accepted).
Descriptions are documentation: they may be improved (13 were, after the audit); ids do not change.

| Case | Decision |
| --- | --- |
| WHD `bow_nou-ship` | its pronunciation covers the bending gesture as well as the front of a ship: `bow.bend` (rhymes with "cow"); `bow_nou-knot` is `bow.weapon` (archery bow, ribbon, violin bow) |
| `lead_nou` / `lead_nou-vrb` | the suffix is misleading: `lead.metal` (the metal) and `lead.guide` (to guide, the lead position, a leash, a clue, **electrical and ECG leads**) |
| `correlate_nou-vrb`, `intrigue_nou-vrb` | the suffix says noun-verb but the pronunciation is the verb: `correlate.verb`, `intrigue.verb` |
| `read` | `read.past` (past tense, participle) and `read.present` (base, infinitive, imperative, present, **and the noun "a good read"**) |
| `wound_vrb`, `winds_*`, `wound_nou-vrb` | the form `wound` as the past of wind is `wind.coil` (lemma `wind`); injury is `wound.injury`; `winds` maps to `wind.air` and `wind.coil`; the form `wound` therefore has two candidate lemmas |
| `dove_vrb` | the past of dive: `dive.past`; `dove` is the bird `dove.bird` |
| `aged`, `blessed`, `learned`, `moped` | adjective reading under its own lemma (`aged.adj`); the participle reading under the verb lemma (`age.verb`, `bless.verb`, `learn.verb`, `mope.verb`) |
| `blessed` | audit finding: "blessed with/to/by" and "blessed enough" are one syllable and belong to `bless.verb` even when used as adjectives; `blessed.adj` is the two-syllable religious, attributive or emphatic use ("the Blessed Virgin", "a blessed relief") |
| `abuses`, `uses`, `lives_vrb` | merged into the readings of `abuse`, `use`, `live.verb` |
| `analyses`, `diagnoses`, `axes`, `lives_nou`, `confines` | plural nouns of another lemma get their own lemma: `analysis.plural` with `analyse.verb`, `diagnosis.plural` with `diagnose.verb`, `axis.plural` with `axe.tool`, `life.plural`, `confine.noun` with `confine.verb` |
| `recount_nou` / `recount_vrb` | the suffix is misleading: `recount.again` (count again; the noun "a recount") and `recount.narrate` (tell). The part of speech does not decide this pair, so `recount` is excluded from the rule-labeled harvest (owner decision). The first generator inverted the two readings for all 38 counting examples; the audit relabeled them to `recount.again`, which left `recount.narrate` with few rows until the third round filled it |
| WHD ids without suffix (`isolate`, `transform`, `compress`, `affect`, `rerelease`, `mate`, `minute`, `sow`, `polish`, `job`, ...) | each assigned from its pronunciation: `isolate.verb`, `transform.verb`, `compress.verb`, `affect.verb`, `rerelease.noun`, `mate.companion`, `minute.time`, `sow.plant`, `polish.shine`, `job.work` |
| `expose_nou`, `resume_nou` | the nouns are exposé and résumé (`expose.noun`, `resume.noun`); "do not expose" is the verb |
| `desert_nou` / `desert_vrb` | the dry region, including the plural "deserts" in that sense, is `desert.noun`; "(just) deserts" (what one deserves) shares the verb pronunciation and is `desert.verb` (rule: any `desert.noun` row with "just desert(s)" is relabeled; none existed). **Proper name Mount Desert Island (Maine)** is pronounced like "dessert", so it is labeled `desert.verb` although it is a noun (audit decision, 4 rows kept; 2 broken catalog sentences containing it were dropped); "Desert View" and "Desert Inn" stay `desert.noun` |
| `rodeo_geo` / `rodeo` | **Rodeo Drive and the town of Rodeo, California** share the second-syllable-stress pronunciation and are `rodeo.drive`; the cowboy event is `rodeo.event` |
| `jesus_es` / `jesus` | a bare personal name gives no cue for English versus Spanish pronunciation, so such rows were dropped (the audit dropped 1, the consistency check 7 in all files); rows with a Spanish cue (surname, place, band) stay `jesus.spanish` |
| `use` / `used` | `use.habitual` is "used to" for a former habit and accustomed ("be/get used to"); the participle "used to" of purpose ("a tool used to cut") and ordinary "used" are `use.verb` |
| `close_adj-nou` | includes the adverb; the noun meaning the end ("close of business", "at the close of the day") has the final z sound and is `close.verb` (6 natural rows relabeled); the extension form `closer` is `close.adj-noun` (nearer) or `close.verb` (one who closes) |
| `primer` | `primer.paint` includes an ignition or priming device ("engine primer"); `primer.textbook` is the elementary textbook |
| `content` | `content.satisfied` (adjective, noun contentment, verb "to content") and `content.noun` (the contents); not decidable by UPOS; the extension form `contents` is `content.noun` or `content.satisfied` |
| `reading_en` / `reading_geo` | a bare "Reading ..." with no cue was dropped (activity versus the town) |

Every WHD id maps to exactly one reading (`whd-mapping.tsv`); 326 ids map to 318 readings (8 ids share a reading). The 77 extension readings have no WHD id.

## Inventory Extension (owner decision, 2026-11-16)

`extension-candidates.tsv` (60 proposals, ranked by natural frequency in the training corpora) was decided in `extension-decisions.tsv`:
every candidate of class Y (both readings common) or R (one reading rare, word very frequent) is added; a class S candidate (stress-only
difference) is added only when CMUdict lists at least two entries for the word whose stress patterns differ. Result: 45 candidates added
(35 Y, 6 R, 4 S: `research`, `inside`, `address`, `essay`), 15 class S candidates deferred (`nearby`, `overall`, `underground`, `overhead`,
`overweight`, `overlap`, `overlook`, `convoy`, `overlay`, `reboot`, `recoil`, `overdose`, `overload`, `overturn`, `overhaul`).
The 45 add 77 readings (38 new lemmas with two readings each plus `use.habitual`) and the forms of six existing lemmas (`tears`, `leads`,
`closer`, `contents`, `bows`, `rows`; no new reading). The `incorporate` candidate drops the form `incorporated` (both readings sound alike).

Evidence: CMUdict, the Carnegie Mellon University Pronouncing Dictionary (`cmudict.dict`, BSD-style license, copyright 1993-2015 Carnegie Mellon
University), read from `external/cmudict/` (a local cache that is not part of the data and not in the manifest; fetched from
<https://github.com/cmusphinx/cmudict>, SHA-256 `81917843c7f44ce2b094ac63873c2c7a4cf802040792c455ba3ca406891c3d22` at the time of use). It is used only to decide the class S
candidates; no CMUdict pronunciation is copied into the data. No other pronunciation dictionary is used (not misaki). The natural frequencies come
from the corpora. Descriptions of the new readings are written in our own words.

## Audit

Every model-written or model-labeled row of the train and dev files (16,397 rows: 10,532 first-round and 3,823 second-round synthetic rows, and
2,042 natural LLM-labeled rows) was relabeled blind by four `gpt-5.6-luna` sub-agents (a different family than the authors) on 2026-11-16, and the
85 disagreements (99.48 percent agreement) were adjudicated by `gpt-6.1-sol`: 39 `keep`, 38 `relabel` (all `recount`), 8 `drop`. 13 reading descriptions were
fixed and 15 further rows were corrected or dropped in a consistency re-check of the systematic readings. Method, figures, systematic issues and files:
[audit/audit-summary.md](audit/audit-summary.md).

**Holdout review.** The LLM-labeled natural rows of `holdout-natural.tsv` that were in the reviewed file (268 of the 285 then present) were judged blind by one
`gpt-6.1-sol` sub-agent (2026-11-17, local time 00:16 to 00:20; [prompt](prompts/holdout-review.md)): 266 agreed with the label (99.25 percent), 2 were `unsure`
(`harvest-u00640`, `harvest-u00641`: the tense is not fixed) and were dropped. The reviewer also recommended dropping 7 rows for "invalid target spans"; **the spans in the files are
valid**. Root cause: the reviewer's reader parsed the TSV with CSV quoting enabled, and each of the 7 sentences starts with a double quotation mark, which a quoting reader consumes,
shifting the offsets by 1 to 3 characters (a default CSV reader misreads 293 rows of the four files the same way). A strict whole-word span validator (the slice is a listed form of the
reading, case-insensitive, bounded by non-letter/non-digit characters or the string edges, UTF-16 offsets, scalar-aware) finds 0 defects in all 26,239 rows of the four data files, so nothing
needed fixing or dropping; the 7 rows were kept (the reviewer's pronunciation judgment agrees with the label in all 7). The validator is now part of `check-data` and the quoting
hazard is documented above. 17 LLM-labeled first-round holdout rows (`harvest-u*` ids, e.g. `wind`, `mate`) were not in the reviewed file; they were covered by the final review below.

**Final review.** A last blind review by `gpt-6.1-sol` (2026-11-17, local time 00:28 to 00:30) judged all 650 `synthetic-v3` rows of `train.tsv` and the 17 remaining LLM-labeled `holdout-natural.tsv` rows,
without the original labels and with its judgments frozen (hashed) before comparison: 667 of 667 agree (100 percent), no `unsure`, all kept (`audit/final-review.tsv`).

**Coverage, stated precisely.** Every row in `train.tsv`, `dev.tsv` and `holdout-natural.tsv` that a language model wrote (`source` `synthetic`, `synthetic-v2`, `synthetic-v3`) or whose reading a language model
chose (`source` ends in `-llm`) has been judged blind by a second model of another family than its author: the relabel audit (16,397 rows), the holdout review (268 rows) and the final review (667 rows)
together cover all of them, and the build script (not part of the repository) stopped if one was missing. Not covered: the 7,048 rule-labeled natural rows (`label_method = upos`; no model chose their reading) and
`holdout-independent.tsv` (an independent model-written test set that no third model reviewed). All reviews are second-model judgments, not human review; agreement is agreement with the label, not an
estimate of true pronunciation accuracy; the reviews preceded the later adjudication and consistency corrections (`audit/consistency-fixes.tsv`).

## Known Limitations

- Natural labels of `label_method = upos` are as good as the PartOfSpeech tags (MASC from the treebank; the others
  model-written) and as the rule above.
- 14 of 395 readings have 39 training rows, one short of 40 (14 rows in total; `gaps-v4.tsv`, after the third round). 115 readings have no natural row in training and 106 none in any split; 295 have
  fewer than 10 natural training rows. 2 readings have no dev row and 227 no holdout-natural row; every reading has holdout-independent rows.
- Synthetic sentences come from one model family and are more regular than real text; they carry 66 percent of the training rows. The independent
  holdout is also synthetic (another model family): it tests generalization across writers, not natural text; `holdout-natural.tsv` is the natural check.
- The extension rows follow the natural distribution: `do.verb` (about 530 rows) and `number.noun` (about 300) dominate their forms.
- No human reviewed any model-chosen reading or generated sentence; the audits are second-model blind judgments (full coverage of model-written and model-labeled rows in the three splits, see Audit).
- The majority class dominates natural text (for example `house.noun` far above `house.verb`); do not read natural counts as priors.

## Check

The checks of this folder are implemented by the `check-data` command of the Text.En trainer tool. From the repository root:

```text
dotnet run --project tools/DemaConsulting.Speech.Text.En.Trainer -- check-data
```

Options:

- `--data <dir>`: the data folder to check (default: the first `data/text-en` folder found walking up from the current directory, then from the tool's folder).
- `--eval-sets <dir>` (or the environment variable `TEXTEN_EVAL_SETS`; the option wins): a folder of neutral evaluation sets for the optional external leakage check
  (see below).
- `--leakage-report <file>`: requires evaluation sets supplied via `--eval-sets` or `TEXTEN_EVAL_SETS`; writes the per-set and per-split counts in the format of `leakage-report.tsv`.
- `--generalization <file>`: checks a staged copy of the generalization set in place of the committed `generalization.tsv` (see "Evaluation Overlays");
  its novelty findings give a generic reason only.
- `--max-findings <n>`: the number of findings printed per check (the rest is counted).
- `--counts`: also prints the per-reading count table.
- `--help`: prints the usage.

Exit codes: 0 when all checks pass, 1 when there are findings, 2 for a usage or I/O error (including an evaluation-set folder that is missing or empty).
Every finding names the check, the file and the line.

What the command enforces, in this order:

1. **Manifest**: every file listed in `MANIFEST.tsv` exists and has the listed SHA-256, byte count and row count, every file of the folder is listed, and the entries
   are unique and sorted. `MANIFEST.tsv` itself, `external/` and `work/` are not listed.
2. **File format** of every `.tsv` file (see "TSV conventions"): no byte-order mark, no CR, final newline, no empty line, constant column count, NFC text.
3. **Schema**: header columns, reading id grammar, unique ids, lemma equal to the id prefix, part-of-speech hints, status, no IPA in the inventory, the mapping and
   the forms, the extension decisions, the four data files (known reading, lemma, split, source vocabulary, integer offsets), the strict whole-word span check,
   the provenance ids, and that `gaps-v4.tsv` equals the per-reading counts recomputed from the data files.
4. **Licenses**: every row `license` is one of `CC0-1.0`, `Public-Domain` or `CC-BY-3.0-US`, and the per-source policy of "Provenance and Licensing" holds (generated rows are
   `CC0-1.0`; MASC rows are `CC-BY-3.0-US`). A `CC-BY-3.0-US` row requires `ATTRIBUTION.md` to name MASC. No file path names an evaluation-only set (WikipediaHomographData,
   heteronym-bench, Wikipedia, Misaki and similar names), except exactly `whd-mapping.tsv`. No `.tsv` file (except `MANIFEST.tsv`; in the data files only the `id`, `source` and
   `license` cells) contains a forbidden license or source term: share-alike, non-commercial, no-derivatives (CC-BY-SA, CC-BY-NC, CC-BY-ND), Wikipedia or Misaki. Each of
   `README.md`, `prompts/README.md` and `audit/audit-summary.md` declares `License:` with allowed ids only.
5. **Internal separation**: a sentence is in only one data file, a natural document is in only one split, and a synthetic (reading, template) pair is in only one data file.
6. **Evaluation overlays**: the rules of "Evaluation Overlays" for `regression.tsv` and `generalization.tsv` (or the staged copy given with `--generalization`). In the
   overlays, every cell except the sentence is scanned for the forbidden terms of step 4.

### External leakage check (optional)

When `--eval-sets <dir>` or `TEXTEN_EVAL_SETS` is given, the command also compares every row with the evaluation sets. The folder holds neutral files that an operator prepares from local
copies of the evaluation data (they are never committed, and the folder must not be inside this one): every `*.txt` file is one set with one sentence per line (UTF-8), and every `*.tsv`
file is one set whose header row has a `sentence` (or `text`) column (the sentences) and which has no quoting. The name of a set is the file name without its extension. A row whose key
(lower-case alphanumerics) equals a key of any set is a finding, and so is a sentence of at least 8 words whose word 5-grams are at least 80 percent contained in the evaluation 5-grams.
Without a folder, the output says that the external leakage check was skipped; that is what CI does.

### Manifest

`MANIFEST.tsv` has two `#` comment lines, then one tab-separated line per file: `file sha256 bytes rows`, sorted by relative path with `/`, where `rows` is the number of lines minus the
header for `.tsv` files and the number of lines for other files. It does not list itself, `external/` or `work/`. **Any change of a listed file, including a Markdown reflow, changes
its hash: regenerate the manifest after the last edit of the folder.** The trainer only verifies the manifest; the current file was written once by a throwaway script, and the format above
is all a regeneration needs.

### Review of the data files

The large TSV files (`train.tsv`, `dev.tsv`, `holdout-natural.tsv`, `holdout-independent.tsv`, both provenance tables, `exclusions.tsv`, `gaps-v4.tsv`, `extension-candidates.tsv`,
`audit/*.tsv` and `check-output.txt`) cannot be reviewed line by line and are not listed in the review configuration. They are controlled instead by `MANIFEST.tsv` (which is reviewed:
any change of any file changes it), by the `check-data` gates, by the documented second-model audits (see Audit), and by a sampled review recorded in the pull request.

### How the files were built

The files were built by one-off builder and check scripts that are not part of the repository (they read local spike folders, the PartOfSpeech data and a local CMUdict cache). The recipe is
described in this README ("Provenance and Licensing", "Splits", "Harvest rules", "Inventory Extension"), the exact prompts are under [prompts/](prompts/README.md), and the per-row
provenance is in the provenance files. `work/` and `external/` are local caches that are ignored by git and are not part of the data.
