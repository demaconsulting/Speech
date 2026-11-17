<!-- markdownlint-disable table-too-wide -->

# Independent holdout writing prompt

One sub-agent (`holdout-writer`) of a different model family than the authors of the training data wrote 5 sentences for
every reading of the inventory without access to any training, development or labeled data. The result is the test-only
file `holdout-independent.tsv`.

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `gpt-5.6-luna` |
| Dispatched (UTC) | 2026-11-17T04:04:09.563Z (2026-11-16, local time 23:04 to 23:52) |
| Completed (UTC) | 2026-11-17T04:10:43Z |
| Source | Verbatim text of the sub-agent task prompt, recovered from the session event log. Only local absolute paths are replaced by `<STAGE>`. |

## Prompt text

```text
You are writing an INDEPENDENT test set for an English heteronym pronunciation classifier. Independence is the whole point: the training data was written by a different AI model, and you must NOT look at it. Write ONLY under STAGE\holdout-independent\ where STAGE = <STAGE>. Do NOT touch any git repository.

Allowed reads: STAGE\readings.tsv (reading_id, lemma, pos_hint, description) and STAGE\forms.tsv (surface forms and which readings they can have). FORBIDDEN: every other file under STAGE (train.tsv, dev.tsv, synthetic*, labeled*, holdout-natural.tsv, unlabeled-candidates.tsv, README, etc.), any other folder on disk, and web search for example sentences. Write every sentence yourself, originally.

Task: for EVERY reading in readings.tsv (~395 readings, ~207 lemmas), write 5 sentences in which a surface form of the lemma (from forms.tsv for that reading) is used with exactly that reading/sense. Aim for realistic, varied, sometimes difficult English: mix registers (news, technical, conversational, literary, instructions, headlines, questions), lengths 4-35 words, positions; include cases where the cue is far from the word, where both readings of the lemma appear plausible until later context, coordination, passives, imperatives, and natural (not textbook) phrasing. For tense readings (e.g. read.past vs read.present) include cases resolved by auxiliaries, time adverbs, or surrounding verb tense. Target token appears exactly once per sentence. No famous quotations or copyrighted text; for sensitive lemmas use only neutral senses.

Output STAGE\holdout-independent\holdout.tsv with header `id	lemma	reading_id	sentence	start	end	source	license	split` with id `hind-{n}`, start/end Python character offsets (end exclusive) of the target token, source `independent-holdout`, license `CC0-1.0`, split `test`. Write and run a Python validator: offsets slice to a form listed in forms.tsv for that reading (case-insensitive), reading exists, exactly 5 rows per reading, no duplicate sentences. Write in batches by lemma, appending, so progress is not lost. Also write STAGE\holdout-independent\prompt.md recording these instructions verbatim (minus local paths) and your model name, for provenance.

Return ONLY: rows written, readings covered, validation result.
```
