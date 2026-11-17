<!-- markdownlint-disable table-too-wide -->

# Blind review of the LLM-labeled holdout rows

One sub-agent (`holdout-audit`) judged the LLM-labeled natural rows of `holdout-natural.tsv` blind. Results:
[../audit/audit-summary.md](../audit/audit-summary.md).

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `gpt-6.1-sol` |
| Dispatched (UTC) | 2026-11-17T05:16:27.024Z (local time 2026-11-17 00:16) |
| Completed (UTC) | 2026-11-17T05:20:16Z |
| Source | Verbatim text of the sub-agent task prompt, recovered from the session event log. Only local absolute paths are replaced by `<STAGE>`. |

## Prompt text

```text
You are an independent annotator verifying a natural-text TEST set for an English heteronym pronunciation classifier. Work ONLY under STAGE = <STAGE> (write outputs to STAGE\audit\holdout-review\). Do not touch any git repository.

Read STAGE\holdout-natural.tsv, STAGE\readings.tsv, STAGE\forms.tsv. Select ONLY the rows whose `source` ends with `-llm` (about 188 rows; these labels were assigned by another model and never audited). For a blind review, first write a helper that extracts those rows WITHOUT the reading_id column into a working file, judge each row yourself from the sentence and the candidate readings for the target form (forms.tsv + readings descriptions - focus on PRONUNCIATION), and only afterwards compare with the existing labels. Output `unsure` when the sentence genuinely does not determine the pronunciation.

Outputs in STAGE\audit\holdout-review\: `review.tsv` (`id	original	review	agree	note`), and `summary.md` (counts, agreement rate, list of disagreements with your reasoning and a recommended decision keep | relabel:<id> | drop for each). Do NOT modify holdout-natural.tsv yourself.

Return ONLY: rows reviewed, agreement rate, number of disagreements and recommended decisions summary.
```
