# Audit Summary

License: CC0-1.0

A blind relabeling audit of the model-written and model-labeled rows of dataset v2, followed by a final adjudication of
the disagreements. DEMA Consulting dedicates the audit records to the public domain under CC0 1.0. The audit changed
labels and descriptions only; it did not add data.

## Method

1. **Audit set**: 16,397 rows: 10,532 synthetic rows of the first generation, 3,823 synthetic rows of the second
   generation and 2,042 LLM-labeled natural rows (the 188 first-round LLM-labeled natural rows of the holdout-natural
   split were not audited; the 97 second-round rows of that split were).
2. **Blind relabel**: four sub-agents (`audit-0` to `audit-3`, GitHub Copilot CLI, OpenAI `gpt-5.6-luna`, a different
   model family from the authors `claude-sonnet-5.5`) received only the sentence, the target word and the candidate
   readings with their descriptions, never the original label, and chose one candidate or `unsure`. Instruction: every
   judgment is the agent's own reading, no keyword rules. Prompt:
   [../prompts/audit-blind-relabel.md](../prompts/audit-blind-relabel.md).
3. **Adjudication**: one sub-agent (`adjudicator`, `gpt-6.1-sol`) saw the 85 disagreements with both labels and decided
   `keep`, `relabel:<reading>` or `drop` by the pronunciation the target should get, and proposed better descriptions
   for readings whose description had caused systematic errors. Prompt:
   [../prompts/adjudication.md](../prompts/adjudication.md).
4. **Consistency re-check** (by the data steward, with the fixed descriptions): all rows of `recount`, `bless/blessed`,
   `close`, `use` (`used`), `lead`, `primer`, `read`, `desert`, `rodeo` and `jesus` were reviewed for consistency with
   the fixed descriptions (`consistency-fixes.tsv`).

## Results

- Agreement of the blind audit with the original labels: **16,312 of 16,397 (99.48 percent)**; 85 disagreements (6 of
  them `unsure`).
  - synthetic: 10,473 of 10,532 (99.44 percent)
  - synthetic-v2: 3,820 of 3,823 (99.92 percent)
  - natural LLM-labeled: 2,019 of 2,042 (98.87 percent)
- Adjudication of the 85 disagreements: 39 `keep` (original label correct), 38 `relabel`, 8 `drop`.
- Applied: 38 relabels and 8 drops of the adjudicator, plus 8 relabels and 7 drops from the consistency re-check
  (`consistency-fixes.tsv`: 6 `close` rows with the noun "end" moved to `close.verb`, 2 `blessed` rows moved to
  `bless.verb`, 7 bare-name `jesus` rows dropped, 3 of them in the independent holdout).
- 13 reading descriptions were improved (`description-fixes.tsv`). The adjudicator's proposals contain IPA; the
  inventory has none (decision D5), so `readings.tsv` has the same content in ordinary spelling hints. No fix needed a
  new reading, so no id changed. Three `pos_hint` values were widened to match (`close.verb`, `read.present`,
  `bless.verb`).

## Systematic issues found

- **recount**: the first generator inverted the two readings: all 38 counting examples ("recount the ballots") were
  written as `recount.narrate`. Relabeled to `recount.again`; the descriptions now state the prefix vowel.
- **blessed**: "blessed with/to/by" and "blessed enough" are normally one syllable (`bless.verb`) even when the word is
  used as an adjective; 16 examples confirmed the original `bless.verb` label.
- **close**: the noun "close" meaning the end ("close of business", "at the close of the day") has the final z sound of
  `close.verb`, not that of `close.adj-noun`.
- **read**: "a good read" (noun) is pronounced like `read.present`; some sentences cannot be decided (a sign that "read"
  something can be present or past) and were dropped.
- **lead**: electrical and ECG leads belong to `lead.guide` (the guide pronunciation), not to the metal.
- **desert**: the proper name Mount Desert Island (Maine) is pronounced like "dessert", so it matches `desert.verb`
  although it is a noun; "Desert View" and "Desert Inn" stay `desert.noun`. Two broken catalog sentences that contain it
  were dropped.
- **rodeo**: Rodeo, California and Rodeo Drive share the second-syllable-stress pronunciation (`rodeo.drive`); the
  cowboy event is `rodeo.event`.
- **jesus**: a bare first name gives no cue for English versus Spanish pronunciation; such rows cannot be labeled and
  were dropped. Rows with a Spanish cue (a surname, a place, a mariachi band) stay `jesus.spanish`.
- **used**: "used to" for a former habit or for accustomed ("be used to") is `use.habitual`; the participle "used to" of
  purpose ("a tool used to cut") is `use.verb`.
- **primer**: an ignition or priming device (an engine primer) is `primer.paint`; the textbook is `primer.textbook`.

## Holdout review

The LLM-labeled natural rows of `holdout-natural.tsv` that were in the reviewed file (268 rows) were reviewed blind by
one `gpt-6.1-sol` sub-agent on 2026-11-17 (local time 00:16 to 00:20): it judged each target from the sentence and the
candidate readings without the original label, saved its judgments, and only then compared them. Prompt:
[../prompts/holdout-review.md](../prompts/holdout-review.md).

- Agreement: **266 of 268 (99.25 percent)**; 2 disagreements, both `unsure` (`harvest-u00640` and `harvest-u00641`:
  "what you read" and "if you read" do not fix the tense). Both rows were dropped.
- The reviewer also reported 7 rows whose recorded span did not slice to a target word (`harvest-u00209`,
  `harvest-u00412`, `harvest-u00414`, `harvest-u01141`, `cand-00232`, `cand-00548`, `cand-00569`) and recommended
  dropping them. **Root cause: none in the data.** The reviewer's reader parsed the TSV with quoting enabled; each of
  these sentences starts with a double quotation mark, which a quoting reader treats as the start of a quoted field and
  removes, shifting every offset of that row by 1 to 3 characters. Read with quoting disabled, all 7 spans are whole
  target words (`read`, `read`, `job`, `sake`, `used`, `used`, `used`). The same misreading affects 293 rows of the four
  data files (sentences with a double quotation mark that a quoting reader consumes); the reviewer only saw the
  LLM-labeled holdout rows. A strict whole-word span validator (scalar-aware, bounded by non-letter/non-digit
  characters) over every row of every file finds 0 defects; it is now part of `check-data`. The 7 rows were therefore
  **kept**; the reviewer's pronunciation judgment agrees with the label in all 7. Consumers must read the files with
  quoting disabled (`csv.QUOTE_NONE`).
- 17 LLM-labeled rows of the holdout (first-round `harvest-u*` rows) were not in the reviewed file and remain
  unreviewed.
- Decisions per row: `holdout-review.tsv` (`id original review agree decision note`).

## Final review and coverage

A last blind review by `gpt-6.1-sol` (2026-11-17, local time 00:28 to 00:30) judged the 650 round-3 synthetic rows
(`synthetic-v3`, all in `train.tsv`) and the 17 LLM-labeled `holdout-natural.tsv` rows that the holdout review had not
seen, without the original labels, freezing its judgments (SHA-256 of the blinded artifacts recorded) before comparing:
**667 of 667 agree (100 percent)**; no disagreement, no `unsure`; all kept. Decisions per row: `final-review.tsv`
(`id file original review agree note`). Agreement is exact candidate-reading agreement with the existing labels, not an
external estimate of pronunciation accuracy.

**Coverage, stated precisely.** Every row of `train.tsv`, `dev.tsv` and `holdout-natural.tsv` that a language model
wrote (`source` is `synthetic`, `synthetic-v2` or `synthetic-v3`) or whose reading a language model chose (`source` ends
in `-llm`) has been judged blind by a second model of another family than its author (the relabel audit: 16,397 rows;
the holdout review: 268; the final review: 667), and the build checks this row by row (the build script, which is not
part of the repository, stopped if one was missing). Not covered: (1) the 7,048 rule-labeled natural rows
(`label_method = upos`: the reading follows the PartOfSpeech tag; no model chose it); (2) `holdout-independent.tsv`,
which is itself an independent model-written test set (another family than the training authors) and was not reviewed by
a third model. The review is a second model's judgment, not human review; rows were reviewed once, before the later
corrections (adjudication relabels, consistency fixes), which are recorded in `consistency-fixes.tsv`.

## Files

- `adjudication.tsv`: `id decision pronunciation_note` for the 85 disagreements (the note contains IPA written by the
  adjudicator).
- `description-fixes.tsv`: the 13 proposed descriptions with reasons (contain IPA; the applied descriptions are in
  `readings.tsv`).
- `consistency-fixes.tsv`: the rows changed by the consistency re-check and the template-rule moves.
- `holdout-review.tsv`: the blind review of the LLM-labeled holdout rows with the decision per row.
- `final-review.tsv`: the final blind review of the round-3 synthetic rows and the remaining LLM-labeled holdout rows.
- The per-row audit results (16,397 rows), the audit inputs and the disagreement list are not part of the data; the
  agreement figures above were computed from them.

All runs: 2026-11-16, local time 23:04 to 23:52 (2026-11-17 04:04 to 04:52 UTC), GitHub Copilot CLI sub-agents
orchestrated for DEMA Consulting.
