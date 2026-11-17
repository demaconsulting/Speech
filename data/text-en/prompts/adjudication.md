<!-- markdownlint-disable table-too-wide -->

# Adjudication prompt

One sub-agent (`adjudicator`) decided the 85 disagreements between the original labels and the blind audit and proposed
description fixes. See [../audit/audit-summary.md](../audit/audit-summary.md).

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `gpt-6.1-sol` |
| Dispatched (UTC) | 2026-11-17T04:48:50.313Z (2026-11-16, local time 23:04 to 23:52) |
| Completed (UTC) | 2026-11-17T04:51:52Z |
| Source | Verbatim text of the sub-agent task prompt, recovered from the session event log. Only local absolute paths are replaced by `<STAGE>`. |

## Prompt text

```text
You are the final adjudicator for an English heteronym pronunciation dataset. Work ONLY under AUDIT = <STAGE>\audit. Read AUDIT\disagreements.tsv (85 rows) and <STAGE>\readings.tsv (reading descriptions). Do not touch any git repository.

Each row has a target word in a sentence, the candidate readings (reading_id: description), an original label (from the data author) and an audit label (from an independent blind annotator), which disagree. The dataset trains a text-to-speech classifier, so what matters is the PRONUNCIATION the target should get in that sentence (e.g. recount "narrate" /rɪˈkaʊnt/ vs "count again" /riːˈkaʊnt/; blessed adjective /ˈblɛsɪd/ vs verb/participle /blɛst/ - note "blessed with/to" is normally /blɛst/; read past /rɛd/ vs present /riːd/; Mount Desert Island is locally pronounced like "dessert" - decide which reading's pronunciation matches and note it).

For each row decide: `keep` (original label correct), `relabel:<reading_id>` (a candidate reading is correct and differs from the original), or `drop` (genuinely ambiguous, proper-name quirk with no fitting reading, or broken sentence). Also check whether a reading DESCRIPTION in readings.tsv is itself misleading and caused systematic errors (e.g. recount) - if so, propose an improved description.

Output AUDIT\adjudication.tsv with header `id	decision	pronunciation_note` (one row per input id), and AUDIT\description-fixes.tsv (`reading_id	current_description	proposed_description	reason`) if any. Return ONLY: counts of keep/relabel/drop, the systematic issues found, and proposed description fixes.
```
