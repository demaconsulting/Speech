<!-- markdownlint-disable table-too-wide -->

# Synthetic sentence generation prompt, second round

Four sub-agents (`synth2-0` to `synth2-3`) received this prompt, identical except for the number n = 0, 1, 2, 3 in
`PART=<n>`, in the output file name `part-<n>.tsv` and in the id prefix `syn2-<n>-`. Each wrote sentences from scratch
for the readings of its share of the lemmas that still needed rows (the gap list of `gaps-v2.tsv`), seeing the reading
descriptions but not the holdout files, any evaluation set or the web. They wrote 3,823 rows. The shown text is the
prompt of `synth2-0`.

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `claude-sonnet-5.5` |
| Dispatched (UTC) | 2026-11-17T04:04:09.560Z (2026-11-16, local time 23:04 to 23:52) |
| Completed (UTC) | 2026-11-17T04:10:31Z (part 0), 04:11:01Z (part 1), 04:11:13Z (part 2), 04:15:11Z (part 3) |
| Source | Verbatim text of the sub-agent task prompt, recovered from the session event log. Only local absolute paths are replaced by `<STAGE>`. |

## Prompt text

```text
PART=0. Generate synthetic training sentences for an English heteronym pronunciation classifier. Write ONLY under STAGE = <STAGE> (output folder STAGE\synthetic-v2\). Do NOT touch any git repository.

Allowed reads: STAGE\readings.tsv (reading_id, lemma, pos_hint, description), STAGE\forms.tsv (surface forms -> candidate readings), STAGE\gaps-v2.tsv (need_synthetic per reading), STAGE\README.md, and STAGE\train.tsv + STAGE\dev.tsv (ONLY to avoid duplicating existing sentences for your readings). FORBIDDEN: STAGE\holdout-natural.tsv, anything under STAGE\external\ or holdout-independent\, any spike folder, any test/eval set, and web search for example sentences. Write every sentence yourself, originally.

Your share: take the sorted list of distinct lemmas in readings.tsv; you handle lemmas whose 0-based index i satisfies i % 4 == PART. For each reading of your lemmas with need_synthetic > 0, write exactly need_synthetic new sentences using a surface form from forms.tsv that maps to that reading, with exactly that reading/sense (use the description; when in doubt about which pronunciation a reading means, re-read the description and pos_hint of all readings of the lemma).

Quality rules (diversity over volume): natural English across registers (news, technical, fiction, dialogue, instructions, headlines-as-sentences, questions), 5-35 words, target at varied positions; ~30% inflected forms where forms.tsv lists them for this reading; include hard cases (distant context cues, coordination, passives, reduced relatives, imperatives, headlines, garden-path-looking but resolved); for tense/aspect readings (read.past/read.present etc.) vary auxiliaries, time adverbs, agreement; target appears exactly once; no near-duplicates (vary structure and vocabulary, not just one word); no famous quotations or copyrighted text; no offensive content (for sensitive lemmas use only the neutral technical sense).

Output STAGE\synthetic-v2\part-0.tsv with header `id	lemma	reading_id	sentence	start	end	source	license	split	batch` where id = `syn2-0-{n}`, start/end = Python character offsets of the target token (end exclusive), source = `synthetic-v2`, license = `CC0-1.0`, batch = a template/batch id you assign so that sentences written together share a batch (write in batches of ~5-10 per reading), split: assign whole batches so ~85% of each reading's rows are train and ~15% dev (no batch straddles). Write a Python validator and run it: offsets slice to a form listed in forms.tsv for that reading (case-insensitive), reading exists, no duplicate sentences (also vs train.tsv/dev.tsv after normalizing case/punctuation), counts equal need_synthetic. Append per lemma so progress is not lost. Also write STAGE\synthetic-v2\part-0-prompt.md recording these instructions verbatim (minus local paths) for provenance.

Return ONLY: rows written, readings covered, validation result.
```
