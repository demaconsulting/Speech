<!-- markdownlint-disable table-too-wide -->

# Blind ambiguity screen of the regression overlay

One sub-agent judges every scored golden of the regression overlay blind: every `regression.tsv` row except the
`data-row` rows (those are training or development rows, already labeled and audited), the 10 `forced` rows of
`readings-golden.tsv` that join the overlay, and every fork candidate of the cleanup entry in the trainer's `RUN-LOG.md`
("Cleanup after PR B"). The reviewer sees the sentence, the target span and the candidate readings with their
descriptions in a seeded shuffled order, never the label, and freezes its judgments by SHA-256 before it compares them
with the labels. The rules that act on the judgments are fixed in that entry, item 4: agree keeps the row; an `unsure`
answer, or a disagreement that makes the sentence two-way, forks a golden into one unambiguous sentence per reading (and
both forks are reviewed again with this prompt, in a new round); a `listening` row is never edited and goes to the
owner; a disagreement where the label is plainly right is kept with the reasoning noted; a golden without a clean fork
is dropped. The review is recorded as `audit/regression-review.tsv`.

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | the harness's default sub-agent model; it named itself "Claude (Anthropic)" without a version |
| Dispatched (UTC) | 2026-11-21 19:50 |
| Completed (UTC) | 2026-11-21 19:54 |
| Source | The prompt text below is given to the reviewer verbatim. Only the staging folder is replaced by `<STAGE>`. |

## Input

The orchestrator writes two files into the staging folder: `review-blind.tsv` (`id lemma sentence start end`, no reading
and no origin) and `review-labels.tsv` (`id reading_id`). The fork candidates carry neutral ids (`fork-1` and so on) in
a shuffled order, so the id does not reveal the reading. The staging folder also holds `readings.tsv` and `forms.tsv`.

## Review record

One round, 513 rows (498 overlay rows, 10 forced rows and 5 fork candidates): 510 agreements, 1 disagreement
(`fork-2`, the owner's present example, judged past) and 2 `unsure` answers (`reg-0050`, two-way; `reg-0529`, no
candidate because `confine` is not a listed form). The SHA-256 of the frozen judgments is
`0e9822fdfb2f1e5865c651ebab7e6e14eb1acb24240eb5eae060e7eb7e08f432`. The decision on every row is in
`audit/regression-review.tsv`, and the entry "Cleanup after PR B: the blind screen and the forks" of the trainer's
`RUN-LOG.md` records the run. The chosen forks were judged in this round, so no second round was needed.

## Prompt text

```text
You are an independent BLIND ANNOTATOR for an English heteronym pronunciation test suite: for one target word in each
sentence, you decide which pronunciation (reading) applies. The sentences are test sentences (goldens); your judgments
find the sentences that do not decide one reading, so they can be rewritten.

WORKSPACE. Work ONLY in <STAGE>. Allowed reads: <STAGE>\review-prompt.md (this prompt), <STAGE>\readings.tsv,
<STAGE>\forms.tsv, <STAGE>\review-blind.tsv and, ONLY in step 3 below, <STAGE>\review-labels.tsv. Write ONLY under
<STAGE>\review\. FORBIDDEN: any other file or folder on disk (in particular any git repository, any test, golden,
training or evaluation file), web search, and any model or evaluation output. The TSV files are tab-separated with NO
quoting: parse them by splitting lines on the tab character (Python: csv.reader(f, delimiter='\t',
quoting=csv.QUOTE_NONE) with a high field_size_limit); otherwise offsets appear shifted. Write every output file as
UTF-8 without byte order mark, with LF line endings and a final newline.

INPUT. <STAGE>\review-blind.tsv has the header `id	lemma	sentence	start	end` and no reading: the target is
sentence[start:end] (Python offsets, end exclusive). <STAGE>\review-labels.tsv has the header `id	reading_id`: the
labels. Do not open review-labels.tsv before step 3.

STEP 1, CANDIDATES. With a script, list for every row of review-blind.tsv the candidate readings: the reading_ids that
forms.tsv maps the lower-cased target form to, sorted, then shuffled with Python
random.Random("20261121|" + id).shuffle(candidates). Write <STAGE>\review\candidates.tsv (`id	candidates`, the
shuffled ids joined by "|"). Judge each row with its candidates in that order, each with its description from
readings.tsv.

STEP 2, JUDGE. Judge every row yourself, in the order of review-blind.tsv, from the sentence, the target span and the
candidates' descriptions only. The pronunciation is what matters: read every candidate's description (they say, for
example, what a reading rhymes with). Choose exactly one candidate reading_id, or `unsure` when the sentence does not let
a careful reader decide (for example, a habitual "I read every night" that is natural in both the present and the
past). Do not guess: a sentence that a careful native speaker could read aloud either way is `unsure`. Write
<STAGE>\review\judgments.tsv (`id	review	confidence	note`; confidence high, medium or low; a short note for every
medium, low or unsure judgment saying which readings the sentence allows and why). Then freeze it: compute the SHA-256
of judgments.tsv and write it as the only line of <STAGE>\review\judgments.sha256 before you open review-labels.tsv.
Do not change judgments.tsv after that.

STEP 3, COMPARE. Only now read review-labels.tsv. Write <STAGE>\review\review.tsv with the header
`id	original	review	agree	confidence	note` (original = the label, review = your judgment, agree = yes or no; an
`unsure` judgment is no), one line per row in the order of review-blind.tsv, and <STAGE>\review\summary.md: your model
name, the times you started and finished (UTC), the SHA-256 of judgments.tsv recomputed now (it must equal the frozen
one), the numbers of rows, agreements, disagreements and unsure answers, and every disagreement or unsure row with its
sentence, both readings and your reasoning, including whether the sentence admits both readings (two-way) or the label
is simply wrong. Do not change your judgments after seeing the labels.

Do not modify any input file. Return ONLY: rows reviewed, agreements, disagreements and unsure answers, the ids of every
disagreement and unsure row, your model name, and the SHA-256 of judgments.tsv.
```
