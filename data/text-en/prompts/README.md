<!-- markdownlint-disable table-too-wide -->

# Prompts

License: CC0-1.0

The prompts that produced the generated, LLM-labeled and audited data of this folder, as given to the sub-agents. All
runs were GitHub Copilot CLI sub-agents orchestrated for DEMA Consulting: first round on 2026-11-15 (Anthropic
`claude-sonnet-5.5`), second round, independent holdout and audit on 2026-11-16 (local time 23:04 to 23:52), third round
and holdout review on 2026-11-17 (local time 00:16 to 00:30; `claude-sonnet-5.5` for the second-round generation and
labeling, OpenAI `gpt-5.6-luna` for the independent holdout and the blind audit, OpenAI `gpt-6.1-sol` for the
adjudication and the holdout review).

| File | Model | Produced | Output of the run |
| --- | --- | --- | --- |
| [generation-synthetic.md](generation-synthetic.md) | `claude-sonnet-5.5` | the first-round synthetic sentences | `source = synthetic` rows (10,528 in the final files) |
| [labeling-harvest-llm.md](labeling-harvest-llm.md) | `claude-sonnet-5.5` | the model-chosen readings of natural sentences, first round | `source = harvest-*-llm` rows (1,410 of them; 2 more `recount` rows were checked by hand and re-marked) |
| [harvest-derivation.md](harvest-derivation.md) | `claude-sonnet-5.5` (wrote the scripts) | the rule-derived natural rows | `source = harvest-*` rows without `-llm`; a rule, not a model, chose the reading |
| [generation-synthetic-v2.md](generation-synthetic-v2.md) | `claude-sonnet-5.5` | the second-round synthetic sentences | `source = synthetic-v2` rows (3,820 in the final files) |
| [labeling-natural-v2.md](labeling-natural-v2.md) | `claude-sonnet-5.5` | the second batch of model-chosen readings (the 819 candidates) | 818 `harvest-*-llm` rows (1 skipped) |
| [generation-synthetic-v3.md](generation-synthetic-v3.md) | `claude-sonnet-5.5` | the third-round synthetic sentences (gap to 40 rows) | `source = synthetic-v3` rows (650 in the final files) |
| [holdout-independent.md](holdout-independent.md) | `gpt-5.6-luna` | the independent test set | `holdout-independent.tsv` (1,972 rows) |
| [audit-blind-relabel.md](audit-blind-relabel.md) | `gpt-5.6-luna` | the blind relabel audit of 16,397 rows | agreement figures and the disagreement list (see `audit/`) |
| [adjudication.md](adjudication.md) | `gpt-6.1-sol` | the decisions on the 85 disagreements and the description fixes | `audit/adjudication.tsv`, `audit/description-fixes.tsv` |
| [holdout-review.md](holdout-review.md) | `gpt-6.1-sol` | the blind review of the LLM-labeled holdout-natural rows | `audit/holdout-review.tsv` |
| [final-review.md](final-review.md) | `gpt-6.1-sol` | the final blind review of the 650 round-3 synthetic rows and 17 holdout rows | `audit/final-review.tsv` |
| [generalization.md](generalization.md) | `gpt-5.6-luna` | the blind generalization set of the word-cluster work (committed before the set was written, 2026-11-20; written in five rounds and reviewed blind by `claude-sonnet-5.5`, 2026-11-20 and 2026-11-21 UTC; see the record in the prompt file) | `generalization.tsv` (242 rows), `audit/generalization-review.tsv` |

The prompt files are the verbatim task prompts recovered from the session event log of the runs (they are not
reconstructions). Only local absolute paths were replaced by `<SPIKE>`, `<PARTOFSPEECH>`, `<SPEECH-REPO>` and `<STAGE>`.
The first-round prompts refer to the working files of the spike (`gaps.tsv`, `labels.tsv`, `harvest-unlabeled.tsv`) and
to its working reading labels (WikipediaHomographData word ids); the committed rows use the reading ids of
`readings.tsv`, mapped by a one-off builder script that is not part of the repository. The second-round prompts use the
reading ids of `readings.tsv` directly.

The two blind label reviews of the generalization set have no prompt file here (their task prompt is not recorded). Each
reviewer saw only the row id, the sentence, the target span and the candidate readings with their descriptions (not the
writer's label), in a seeded shuffled order, and answered a reading id or `unclear` with a confidence and a short note
(`audit/generalization-review.tsv`).
