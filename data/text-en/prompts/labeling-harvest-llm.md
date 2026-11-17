<!-- markdownlint-disable table-too-wide -->

# Labeling prompt for the LLM-labeled natural rows

One sub-agent (`harvest-labeler`) labeled the natural sentences whose reading could not be derived from the
part-of-speech tag (mostly `read`, `wind`, `job`, `reading`). The sentences come from the PartOfSpeech training corpora;
the model only chose the reading. It finished at 20:58 UTC on 2026-11-15. Rows it could not decide were skipped, not
guessed.

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `claude-sonnet-5.5` |
| Dispatched (UTC) | 2026-11-15T20:54:42.922Z |
| Source | Verbatim text of the sub-agent task prompt, recovered from the session event log. Only local absolute paths are replaced by `<SPIKE>`, `<PARTOFSPEECH>` and `<SPEECH-REPO>`. |

## Prompt text

```text
You label natural sentences for a heteronym pronunciation classifier spike. Work ONLY in SPIKE = <SPIKE>. Do not modify any git repo.

LEAKAGE RULE: read ONLY `SPIKE\data\harvest-unlabeled.tsv`, `SPIKE\data\labels.tsv`, `SPIKE\data\README.md`. Do NOT open any test file (whd-*.tsv, bench.tsv, pos-gold.tsv, user36.tsv, branch-gold.tsv) or SPIKE\external.

Task: harvest-unlabeled.tsv holds ~1,437 natural sentences (mostly `read`, `wind`, `job`, `reading`, plus PROPN/NOUN rows) whose target token's label could not be derived from the POS tag. Inspect its columns first. For each row, read the sentence and decide the correct WHD label (wordid) for the target token from that homograph's candidates in labels.tsv (use IPA/type to know what each label means, e.g. read_past /rɛd/ vs read_present /riːd/; wind_nou air vs wind_vrb turn; job proper noun Job vs job). Assign a label ONLY when you are confident from the sentence; otherwise mark `skip` with a short reason (genuinely ambiguous, tokenization error, not actually the homograph, proper-name misuse, etc.). Also skip rows previously flagged as disputed/masked if the sentence is broken.

Work in batches of ~100 rows, appending to output so progress is not lost. Be careful and consistent: for read, past vs present is determined by auxiliaries (has/had/was/been read -> past participle /rɛd/; will/can/to/do read -> /riːd/), tense of the surrounding clause, time adverbs, subject agreement ("he reads" is not this form), and imperatives (present).

Outputs:
- `SPIKE\data\harvest-llm.tsv`: same shared format as other data files `id	homograph	label	sentence	start	end	source	split` (keep the original id, sentence, offsets; source = original source + `-llm`; split: deterministic ~85% train / 15% dev by hashing the sentence, consistent with harvest.tsv's scheme if documented in README).
- `SPIKE\data\harvest-llm-skipped.tsv`: id + reason.
Validate offsets and labels with a small Python script.

Return ONLY: counts labeled per homograph/label (top 15), total skipped by reason, validation result.
```
