<!-- markdownlint-disable table-too-wide -->

# Synthetic sentence generation prompt

Four sub-agents (`synth-0` to `synth-3`) received this prompt, identical except for the number n = 0, 1, 2, 3 in
`SYNTH_PART=<n>`, in the output file name `part-<n>.tsv` and in the id prefix `syn<n>-`. Each wrote sentences from
scratch for the (homograph, reading) pairs of its share, with no access to any test set, web or external corpus (the
prompt forbids reading them). The agents finished between 21:06 and 21:12 UTC on 2026-11-15. The shown text is the
prompt of `synth-0`; the other three differ from it only in those three places. Reading ids in the prompt (for example
`read_past`) are the working labels of the spike; they were later mapped to the self-owned ids of `readings.tsv`.

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `claude-sonnet-5.5` |
| Dispatched (UTC) | 2026-11-15T20:54:42.919Z |
| Source | Verbatim text of the sub-agent task prompt, recovered from the session event log. Only local absolute paths are replaced by `<SPIKE>`, `<PARTOFSPEECH>` and `<SPEECH-REPO>`. |

## Prompt text

```text
SYNTH_PART=0. You generate synthetic training sentences for a heteronym pronunciation classifier spike. Work ONLY in SPIKE = <SPIKE>. Do not modify any git repo.

STRICT LEAKAGE RULE: you may read ONLY `SPIKE\data\gaps.tsv`, `SPIKE\data\labels.tsv`, and `SPIKE\data\README.md`. Do NOT open whd-*.tsv, bench.tsv, pos-gold.tsv, user36.tsv, branch-gold.tsv, harvest*.tsv, anything in SPIKE\external, or search the web for example sentences. Write every sentence yourself, originally.

Your share: take the sorted list of distinct homographs in gaps.tsv; you handle those whose 0-based index i satisfies i % 4 == SYNTH_PART. For each (homograph, label) in your share with needed > 0, write `needed` new sentences (cap 40) that use the homograph with exactly that pronunciation/sense (check labels.tsv IPA and type to be sure which reading the label means; e.g. `read_past` /rɛd/, `live_adj` /laɪv/).

Quality rules (diversity matters more than volume):
- Natural English, varied registers (news, technical, fiction, dialogue, instructions, headlines-as-sentences, questions), lengths 5-35 words, target at varied positions (start/middle/end).
- Use inflected forms where they keep the same pronunciation split (e.g. "records", "recorded" for the verb reading) in roughly 30% of sentences, base form otherwise; only use forms recognizable as the same homograph.
- Avoid making every example trivially cued (not always "to X"/"the X"); include hard cases: the reading cued only by distant context, coordination ("we record and present..."), passive voice, reduced relatives, headlines, imperatives, ambiguous-looking but resolved contexts. For past vs present (read), include auxiliaries, time adverbs, tense of other verbs, and subject agreement.
- No duplicate or near-duplicate sentences; never copy famous quotations or copyrighted text.
- The target token must appear exactly once in the sentence (if the homograph also appears elsewhere, rewrite).

Output: `SPIKE\data\synthetic\part-0.tsv` (create folder), header `id	homograph	label	sentence	start	end	source	split`; id = `syn0-{n}`; start/end = Python character offsets of the target token (end exclusive); source = `synthetic`; split: deterministic ~85% train / 15% dev (e.g. every 7th row per label -> dev). Write a small Python validator and run it: offsets slice to a token matching the homograph (case-insensitive, or a valid inflection), label exists for the homograph in labels.tsv, no duplicate sentences. Work in batches (e.g. one homograph at a time, appending) so progress is not lost.

Return ONLY: rows written per label count summary (totals), homographs covered, validation result.
```
