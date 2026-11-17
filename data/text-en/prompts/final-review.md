<!-- markdownlint-disable table-too-wide -->

# Final blind review prompt

One sub-agent (`audit-final`) judged the 650 round-3 synthetic rows and the 17 remaining LLM-labeled holdout-natural
rows blind. Results: [../audit/audit-summary.md](../audit/audit-summary.md).

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `gpt-6.1-sol` |
| Dispatched (UTC) | 2026-11-17T05:28:00.470Z (local time 2026-11-17 00:28) |
| Completed (UTC) | 2026-11-17T05:30:28Z |
| Source | Verbatim text of the sub-agent task prompt, recovered from the session event log. Only local absolute paths are replaced by `<STAGE>`. |

## Prompt text

```text
You are an independent blind annotator for an English heteronym pronunciation dataset. Work ONLY under STAGE = <STAGE>; write outputs to STAGE\audit\final-review\. Do not touch any git repository. IMPORTANT: the TSV files are tab-separated with NO quoting - parse them with quoting disabled (Python: csv.reader(f, delimiter='\t', quoting=csv.QUOTE_NONE) with a high field_size_limit, or split lines on '\t'); otherwise offsets will appear shifted.

Rows to review:
(a) every row in STAGE\train.tsv whose `source` is `synthetic-v3` (~650 rows);
(b) every row in STAGE\holdout-natural.tsv whose `source` ends with `-llm` and whose id is NOT in STAGE\audit\holdout-review.tsv (~17 rows).
Use STAGE\readings.tsv and STAGE\forms.tsv for candidate readings and their descriptions (focus on PRONUNCIATION).

Blindness: first write a helper that extracts those rows WITHOUT the reading_id column into a working file; judge each row yourself from the sentence (choose a candidate reading_id or `unsure`); only afterwards compare with the existing labels.

Outputs in STAGE\audit\final-review\: `review.tsv` (`id	file	original	review	agree	note`) and `summary.md` (counts, agreement rates for (a) and (b), each disagreement with reasoning and a recommended decision keep | relabel:<id> | drop). Do not modify the data files.

Return ONLY: rows reviewed per group, agreement rates, disagreements with recommended decisions.
```
