<!-- markdownlint-disable table-too-wide -->

# Labeling prompt for the second batch of LLM-labeled natural rows

One sub-agent (`labeler-v2`) labeled the 819 natural candidates whose reading the part of speech cannot decide (the
forms of the extension readings and `recount`). It labeled 818 and skipped 1 (garbled text).

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `claude-sonnet-5.5` |
| Dispatched (UTC) | 2026-11-17T04:04:09.562Z (2026-11-16, local time 23:04 to 23:52) |
| Completed (UTC) | 2026-11-17T04:09:52Z |
| Source | Verbatim text of the sub-agent task prompt, recovered from the session event log. Only local absolute paths are replaced by `<STAGE>`. |

## Prompt text

```text
Label natural English sentences for a heteronym pronunciation classifier. Write ONLY under STAGE = <STAGE> (output folder STAGE\labeled-v2\). Do NOT touch any git repository.

Allowed reads: STAGE\unlabeled-candidates.tsv (inspect its columns first), STAGE\readings.tsv (reading_id, lemma, pos_hint, description), STAGE\forms.tsv, STAGE\README.md. FORBIDDEN: holdout files, STAGE\external\, spike folders, any test/eval set, web.

For each of the ~819 candidate rows decide the correct reading_id for the target token from the candidate readings of its surface form (forms.tsv), using the sentence context and reading descriptions. Assign a reading ONLY when confident; otherwise `skip` with a short reason (ambiguous, not the homograph, tokenization error, garbled, proper name, tagger error making it a different word, etc.). Be careful with: `used` (used to = habitual /juːst/ vs used = employed /juːzd/; "used to" + verb vs "used to" + noun phrase as in "used to cut"), `does` (verb vs plural of doe), `number` (count vs comparative of numb), `putting` (put vs putt), and other extension lemmas. Work in batches of ~100, appending output so progress is not lost.

Outputs: STAGE\labeled-v2\labeled.tsv with header `id	lemma	reading_id	sentence	start	end	source	license	split` (keep original id, sentence, offsets, document split and license from the candidate row; source = original source + `-llm`); STAGE\labeled-v2\skipped.tsv (id, reason); STAGE\labeled-v2\prompt.md recording these instructions verbatim (minus local paths) for provenance. Validate offsets/readings with a small Python script.

Return ONLY: counts labeled per reading (top 15), skipped by reason, validation result.
```
