<!-- markdownlint-disable table-too-wide -->

# Blind review prompt of the fourth-round synthetic rows

One sub-agent of another model family than the writers judges every fourth-round synthetic row that passed the
mechanical filters, blind: it sees the sentence, the target span and the candidate readings with their descriptions in a
seeded shuffled order, never the writer's label, and freezes its judgments by SHA-256 before it compares them with the
labels. A row whose judgment differs from the label, or is `unsure`, is dropped, never relabeled. If a reading then
falls below 90 percent of its quota, a top-up round is written and a second instance of the same model reviews only the
new rows with the same prompt (the earlier review folder is moved aside first). This prompt was committed before any row
was written, with the writer prompt ([generation-synthetic-v4.md](generation-synthetic-v4.md)); the record below is
completed after the review. The review is recorded as `audit/review-synthetic-v4.tsv`.

| Field | Value |
| --- | --- |
| Operator | GitHub Copilot CLI session orchestrated for DEMA Consulting |
| Agent type | `general-purpose` |
| Model | `gpt-6.1-sol` |
| Dispatched (UTC) | to be recorded |
| Completed (UTC) | to be recorded |
| Source | The prompt text below is given to the reviewer verbatim. Only the staging folder is replaced by `<STAGE>`. |

## Input

The orchestrator writes two files into the staging folder from the rows that passed the mechanical filters:
`review-blind.tsv` (`id file lemma sentence start end`, no reading; `file` is the writer's output file) and
`review-labels.tsv` (`id reading_id`). The staging folder also holds `readings.tsv` and `forms.tsv`.

## Review record

To be recorded after the review: the times, the rows reviewed, the agreements, disagreements and `unsure` answers per
lemma, the SHA-256 of the frozen judgments and the rows dropped, as in `RUN-LOG.md` (entry "PR B: synthetic-v4 data
round").

## Prompt text

```text
You are an independent BLIND ANNOTATOR for an English heteronym pronunciation dataset: for one target word in each
sentence, you decide which pronunciation (reading) applies. Your judgments decide which newly written training rows
are kept: a row is kept only when your reading equals the writer's label.

WORKSPACE. Work ONLY in <STAGE>. Allowed reads: <STAGE>\review-prompt.md (this prompt), <STAGE>\readings.tsv,
<STAGE>\forms.tsv, <STAGE>\review-blind.tsv and, ONLY in step 3 below, <STAGE>\review-labels.tsv. Write ONLY under
<STAGE>\review\. FORBIDDEN: any other file or folder on disk (in particular the part-*.tsv files, train.tsv, dev.tsv,
any git repository and any test or evaluation set), web search, and any model or evaluation output. The TSV files are
tab-separated with NO quoting: parse them by splitting lines on the tab character (Python: csv.reader(f,
delimiter='\t', quoting=csv.QUOTE_NONE) with a high field_size_limit); otherwise offsets appear shifted. Write every
output file as UTF-8 without byte order mark, with LF line endings and a final newline.

INPUT. <STAGE>\review-blind.tsv has the header `id	file	lemma	sentence	start	end` and no reading: the target is
sentence[start:end] (Python offsets, end exclusive). <STAGE>\review-labels.tsv has the header `id	reading_id`: the
writer's labels. Do not open review-labels.tsv before step 3.

STEP 1, CANDIDATES. With a script, list for every row of review-blind.tsv the candidate readings: the reading_ids that
forms.tsv maps the lower-cased target form to, sorted, then shuffled with Python
random.Random("20261121|" + id).shuffle(candidates). Write <STAGE>\review\candidates.tsv (`id	candidates`, the
shuffled ids joined by "|"). Judge each row with its candidates in that order, each with its description from
readings.tsv.

STEP 2, JUDGE. Judge every row yourself, in the order of review-blind.tsv, from the sentence, the target span and the
candidates' descriptions only. The pronunciation is what matters: read every candidate's description (they say, for
example, what a reading rhymes with). Choose exactly one candidate reading_id, or `unsure` when the sentence does not let
a careful reader decide. Write <STAGE>\review\judgments.tsv (`id	review	confidence	note`; confidence high, medium or
low; a short note for every medium, low or unsure judgment). Then freeze it: compute the SHA-256 of judgments.tsv and
write it as the only line of <STAGE>\review\judgments.sha256 before you open review-labels.tsv. Do not change
judgments.tsv after that.

STEP 3, COMPARE. Only now read review-labels.tsv. Write <STAGE>\review\review.tsv with the header
`id	file	original	review	agree	note` (original = the writer's label, review = your judgment, agree = yes or no; an
`unsure` judgment is no), one line per row in the order of review-blind.tsv, and <STAGE>\review\summary.md: your model
name, the times you started and finished (UTC), the SHA-256 of judgments.tsv recomputed now (it must equal the frozen
one), the numbers of rows, agreements, disagreements and unsure answers in total and per lemma, and every disagreement or
unsure row with its sentence, both readings and your reasoning. Do not recommend relabeling: a row you disagree with is
dropped, never relabeled.

Do not modify any input file. Return ONLY: rows reviewed, agreements, disagreements and unsure answers (in total and per
lemma), and the SHA-256 of judgments.tsv.
```
