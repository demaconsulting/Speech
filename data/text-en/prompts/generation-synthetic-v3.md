<!-- markdownlint-disable table-too-wide -->

# Synthetic sentence generation prompt, third round

Two sub-agents (`synth3-0`, `synth3-1`) received this prompt, identical except for the part number, EVEN versus ODD
(which half of the readings) and the file and id names. They filled the remaining gap to 40 training rows per reading
(664 rows written; 650 kept after dropping 14 whose template was already in the dev split). The shown text is the prompt
of `synth3-0`.

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `claude-sonnet-5.5` |
| Dispatched (UTC) | 2026-11-17T05:16:27.023Z (local time 2026-11-17 00:16) |
| Completed (UTC) | 2026-11-17T05:19:38Z (part 1) and 05:20:18Z (part 0) |
| Source | Verbatim text of the sub-agent task prompt, recovered from the session event log. Only local absolute paths are replaced by `<STAGE>`. |

## Prompt text

```text
PART=0 of 2. Generate synthetic training sentences for an English heteronym pronunciation classifier. Write ONLY under STAGE = <STAGE> (output folder STAGE\synthetic-v3\). Do NOT touch any git repository.

Allowed reads: STAGE\readings.tsv (reading_id, lemma, pos_hint, description - descriptions include pronunciation hints like "rhymes with ..."), STAGE\forms.tsv, STAGE\gaps-v3.tsv (need_synthetic per reading), STAGE\audit\audit-summary.md (known systematic errors to avoid, e.g. recount "count again" vs "narrate"), and STAGE\train.tsv + STAGE\dev.tsv (ONLY to avoid duplicating sentences). FORBIDDEN: holdout-natural.tsv, holdout-independent.tsv, holdout-independent\, external\, work\, any spike folder, any test/eval set, web search. Write every sentence yourself, originally.

Your share: readings with need_synthetic > 0 whose lemma's 0-based index in the sorted list of distinct lemmas (of those readings) is EVEN. For each, write exactly need_synthetic sentences using a surface form from forms.tsv mapped to that reading, with exactly that reading/pronunciation (re-read the descriptions of ALL readings of the lemma first; the pronunciation is what matters).

Quality rules: natural English across registers (news, technical, fiction, dialogue, instructions, headlines-as-sentences, questions), 5-35 words, varied positions; inflected forms where listed for the reading; include hard cases (distant cues, coordination, passives, imperatives, headlines); target appears exactly once; no near-duplicates; no quotations/copyrighted text; neutral senses for sensitive lemmas.

Output STAGE\synthetic-v3\part-0.tsv with header `id	lemma	reading_id	sentence	start	end	source	license	split	batch` (id `syn3-0-{n}`, Python char offsets end-exclusive, source `synthetic-v3`, license `CC0-1.0`, batches of ~5-10 per reading; ALL rows split `train` - these top up training only). Validate with a Python script (offsets slice to a listed form for the reading, reading exists, no duplicates incl. vs train/dev normalized, counts equal need_synthetic). Write STAGE\synthetic-v3\part-0-prompt.md with these instructions verbatim (minus local paths).

Return ONLY: rows written, readings covered, validation result.
```
