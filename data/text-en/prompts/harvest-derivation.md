<!-- markdownlint-disable table-too-wide -->

# Harvest derivation prompt (UPOS-derived natural rows)

The natural rows of `harvest-*` sources were derived by rule from the PartOfSpeech UPOS tags, not labeled by a language
model per row. This prompt is the instruction under which the spike scripts (UPOS to reading rules, dedupe,
evaluation-overlap exclusion) were written. The same prompt also covered building evaluation sets that are NOT part of
this data folder.

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `claude-sonnet-5.5` |
| Dispatched (UTC) | 2026-11-15T20:46:36.841Z |
| Source | Verbatim text of the sub-agent task prompt, recovered from the session event log. Only local absolute paths are replaced by `<SPIKE>`, `<PARTOFSPEECH>` and `<SPEECH-REPO>`. |

## Prompt text

```text
You are building datasets for a throwaway research SPIKE comparing two English heteronym pronunciation classifiers. Work ONLY inside the spike folder:
SPIKE = <SPIKE>
Do NOT modify any git repository. Python and git are available. Prior scouting output is in SPIKE\scout\ (scout-report.md, counts.tsv) - read it first.

## Shared data format (another agent is building the classifier against this - follow exactly)
UTF-8 TSV with header, one target token per row:
`id	homograph	label	sentence	start	end	source	split`
- `homograph`: lowercase lemma key (WHD homograph spelling, e.g. `read`, `live`, `project`).
- `label`: a WHD-style wordid (e.g. `read_past`, `read_present`, `live_adj`, `live_vrb`). Use WHD's wordids.tsv as the label inventory; if a homograph is not in WHD do not include it.
- `sentence`: the raw sentence; `start`/`end`: character offsets (Python string indices, end exclusive) of the target token in `sentence` (inflected forms allowed, e.g. "lives", "projected" - but only if the inflected form has the same pronunciation split; otherwise skip).
- `source`: dataset tag; `split`: `train`, `dev`, or `test`.
Also write `SPIKE\data\labels.tsv`: `homograph	label	ipa	type` copied/derived from WHD wordids.tsv (plus `default` column = 1 for the most frequent label in WHD train for that homograph).

## Datasets to build under SPIKE\data\
1. **WHD** (eval + local-ceiling only, NEVER for shipping): `git clone --depth 1 https://github.com/google-research-datasets/WikipediaHomographData` into SPIKE\external\. Convert its train split to `whd-train.tsv` (split=train, source=whd) and eval split to `whd-eval.tsv` (split=test). Check offsets are character offsets (the README may say byte offsets - convert correctly for non-ASCII).
2. **Harvested (shippable)**: from <PARTOFSPEECH>\data\ TRAINING corpora only (MASC, open train, silver, imperative, compound, technical, etc. - exclude every dev/test/eval/held-out set, per scout report). For each token whose lowercase form (or inflection) matches a WHD homograph, derive the label from the corpus UPOS tag when the homograph's WHD wordids are POS-determined (map wordid suffixes like _nou/_vrb/_adj/_adv to UPOS; inspect wordids.tsv for the actual suffix conventions and IPA to decide). Mark rows where UPOS does not map to exactly one label as `unlabeled` in a separate file `harvest-unlabeled.tsv` (e.g. read tense, lexical splits like bass/bow/tear/lead noun) - do NOT guess. Dedupe sentences. Write `harvest.tsv`; deterministically assign ~85% train / 15% dev by hashed sentence (split=train/dev, source=harvest-{corpus}).
3. **Other test sets** (test only):
   - `gold-v2` heteronym targets from PartOfSpeech data\eval\gold-v2 (scout found 256 targets with sense/tense labels) mapped to WHD labels where possible -> `pos-gold.tsv`.
   - heteronym-bench English readset: clone https://github.com/adventists-ai/heteronym-bench to SPIKE\external\, map its readings to WHD labels via IPA/ARPA where the word is a WHD homograph -> `bench.tsv`.
   - The project owner's 36 adversarial sentences (a local `heteronym-gold.tsv`, outside the repository), rows tagged user36-*; map to WHD labels where possible -> `user36.tsv`. Also map the remaining rows to `branch-gold.tsv` (test).
4. **Gap report** `SPIKE\data\gaps.tsv`: for each WHD homograph and label: count in harvest train, WHD train count, and `needed` = max(0, 40 - harvest_train_count) - this drives synthetic sentence generation later.
5. `SPIKE\data\README.md`: provenance, license per file (WHD = eval/ceiling only, CC BY-SA text; Llama HD not used; harvest licenses from PartOfSpeech READMEs; bench Apache-2.0), counts per file, mapping decisions and anything dropped.

Validate every row: offsets slice to the target token (case-insensitive match to homograph or a known inflection), label exists in labels.tsv for that homograph. Report counts.

Return ONLY a concise summary (<= 15 bullets): file counts, homographs covered by harvest train with >= 40 rows, total `needed` synthetic rows, and any problems.
```
