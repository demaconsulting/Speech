<!-- markdownlint-disable table-too-wide -->

# Blind relabel audit prompt

Four sub-agents (`audit-0` to `audit-3`) received this prompt, identical except for the number n = 0, 1, 2, 3 in
`AUDIT PART=<n>`, the input and result file names and the approximate row count; each judged one quarter of the 16,397
audited rows blind (the original label was not in the input). See
[../audit/audit-summary.md](../audit/audit-summary.md). The shown text is the prompt of `audit-0`.

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `gpt-5.6-luna` |
| Dispatched (UTC) | 2026-11-17T04:16:56.641Z (2026-11-16, local time 23:04 to 23:52) |
| Completed (UTC) | 2026-11-17T04:31:13Z to 04:47:29Z |
| Source | Verbatim text of the sub-agent task prompt, recovered from the session event log. Only local absolute paths are replaced by `<STAGE>`. |

## Prompt text

```text
AUDIT PART=0. You are an independent annotator for English heteronyms (words spelled the same but pronounced differently depending on meaning/part of speech, e.g. "record" noun vs verb, "read" past vs present, "bass" fish vs music, "used" employed vs habitual "used to"). Work ONLY under AUDIT = <STAGE>\audit. Read ONLY AUDIT\input-0.tsv. Do NOT open key.tsv or any other file in or outside this folder, and do not touch any git repository - your judgments must be blind.

input-0.tsv columns: id, lemma, target (the exact word occurrence to judge), sentence, candidates (`reading_id: description || ...`). For each row, read the sentence and decide which candidate reading the target has in this sentence. Output one of the candidate reading_ids, or `unsure` if the sentence genuinely does not determine it (or is ungrammatical/nonsensical/the target is misused). Judge carefully and independently; do not guess a "most likely" reading when context is truly ambiguous - use `unsure`.

Process ALL ~4,300 rows, in batches of ~150 rows, appending to output so progress is not lost (if you restart, resume after the last written id). You may write small helper scripts to read/batch the file, but every judgment must be your own reading of the sentence - NO heuristics, keyword rules or automatic labeling.

Output AUDIT\result-0.tsv with header `id	audit_reading	note` (note: empty, or a few words for unsure/odd cases). Verify at the end that every input id appears exactly once in the output.

Return ONLY: rows judged, count of unsure, and confirmation that all ids are covered.
```
