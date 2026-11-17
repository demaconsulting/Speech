## TrainingDataSubsystem Design

![TrainingDataSubsystem Structure](TextEnTrainingDataSubsystemView.svg)

### Overview

The SpeechTextEn TrainingDataSubsystem is the `check-data` command of the Text.En trainer
(`tools/DemaConsulting.Speech.Text.En.Trainer`), a console tool that is built and tested with the repository but is
neither packaged nor shipped, and that reads no part of the Text.En runtime. It reads the training and evaluation
data of the English heteronym classifier that lives in `data/text-en/` and fails when that data is malformed,
altered, mislabeled, restricted, undocumented or leaking evaluation material. It contains the following units:

- **TsvTable**: the strict reader of one tab-separated file
- **TextEnDataSet**: the one-time loader of every table of the data folder into typed rows, with the closed
  vocabulary of data sources
- **DataManifest**: the verifier of `MANIFEST.tsv` against the files of the folder
- **SpanValidator**: the strict whole-word check of the target span of one row
- **CheckReport**: the collector of findings, information lines and the exit code
- **SchemaChecker**: the structural and referential checks of the inventory, the mappings and the data rows
- **LicenseChecker**: the license, attribution and excluded-material checks
- **LeakageChecker**: the internal split separation checks and the optional check against external evaluation sets
- **CheckDataCommand**: the command line entry that orchestrates the other units (the `Program.cs` glue of the tool
  dispatches to it)

The data is a tool input, not a software item; it is described under *Data Model* below. The trainer's `train` and
`evaluate` commands are the sibling TrainingSubsystem (see *SpeechTextEn TrainingSubsystem Design*), which reuses this
subsystem's table reader and data folder discovery.

### Interfaces

The subsystem exposes one command line interface and no public API: every type is internal. The command is `check-data`
with the options `--data <folder>`, `--eval-sets <folder>`, `--leakage-report <file>`, `--max-findings <n>` and
`--counts`, and the environment variable `TEXTEN_EVAL_SETS` as the default of `--eval-sets`. It returns the exit code 0
when every check passes, 1 when any check reports a finding and 2 for a usage or input error. Findings and progress are
written to the standard output as lines of the form `check-data: <check>: <file>[:<line>]: <message>`; usage and input
errors go to the standard error. The subsystem consumes only the .NET base class library: none of its units uses the
Speech or Text.En libraries (the tool's project references Text.En for the sibling TrainingSubsystem only), and it has
no OTS dependency. The classes live in the `DemaConsulting.Speech.Text.En.Trainer.TrainingDataSubsystem` namespace.

### Design

`CheckDataCommand` is the composition point. It parses the options, finds the data folder, creates one `CheckReport`
and runs the checks in a fixed order, each of which adds findings to the report and none of which stops the others:

1. `DataManifest` verifies that the files of the folder are exactly the files of `MANIFEST.tsv`, with equal hashes,
   byte counts and row counts.
2. `TextEnDataSet` loads every table through `TsvTable`, which enforces the file format; the load never throws for a
   defective file, it records a finding and continues with what it could read.
3. `SchemaChecker` checks the inventory, the form table, the mapping, the extension decisions, every data row (using
   `SpanValidator` for the target span), the provenance identifiers and the gap table.
4. `LicenseChecker` checks the license of every row against the allow-list and its source, the restricted terms, the
   file names of the folder, the attribution and the `License:` lines of the documents.
5. `LeakageChecker` checks the separation of the splits, and only when an evaluation folder is supplied compares the
   rows with the sentences of the external evaluation sets.
6. `CheckReport` prints the summary and gives the exit code.

**Unit collaboration.** The checkers are independent of each other: each takes the loaded `TextEnDataSet` (and the
`CheckReport`) and nothing else (`LicenseChecker` also takes the data folder, because it inspects the file names and the
documents), so each is tested with a small data set built in code and no checker knows another. `TsvTable` is the
only unit that parses a data file, `DataManifest` the only one that hashes them, `SpanValidator` the
only one that knows what a whole-word span is, and `CheckReport` the only one that decides the exit code. The
dependency direction is `CheckDataCommand` to the units it runs, the checkers to `TextEnDataSet`, `SpanValidator` and
`CheckReport` (`LicenseChecker` also to `DataManifest` for the list of files), and `TextEnDataSet` and `DataManifest`
to `TsvTable` and `CheckReport` (`TextEnDataSet` also to `DataManifest`); there are no cycles.

**Data Model: the dataset as a tool input.** The dataset in `data/text-en/` is not a software item. It is reviewed
through its own review sets (see the review configuration) and pinned by `MANIFEST.tsv`. Every table is UTF-8 text
without a byte order mark, with line feed line ends, a header row, tab-separated columns, text in normalization form C
and **no quoting**: sentences contain quotation marks and some begin with one, so a file must be split on tabs
and never read with a quoting parser. `#` comment lines occur only in `MANIFEST.tsv`.

The files and their columns are:

- `readings.tsv` (`reading_id lemma pos_hint description status`): the inventory of readings, owned by this
  repository
- `forms.tsv` (`form reading_id kind`): the surface forms each reading can take
- `whd-mapping.tsv` (`whd_wordid reading_id`): identifier-to-identifier mapping for external evaluation, no text
- `extension-decisions.tsv` (`rank lemma forms kind class decision reason cmudict_evidence readings readings_added`):
  the decision per candidate extension, `add` or `defer`
- `train.tsv`, `dev.tsv`, `holdout-natural.tsv` and `holdout-independent.tsv`
  (`id lemma reading_id sentence start end source license split`): the labeled rows; `split` is `train`, `dev`,
  `holdout-natural` and `test` respectively
- `harvest-provenance.tsv` (`id source_id corpus sent_id label_method`): one record per row of natural origin
- `synthetic-provenance.tsv` (`id spike_id batch template`): one record per generated row
- `gaps-v4.tsv` (`reading_id train_natural train_synthetic dev holdout_natural holdout_independent need_synthetic
  need_natural`): the counts per reading and what is still missing
- `leakage-report.tsv` (`eval_set eval_sentences split rows exact near_duplicate share_5gram`): the result of the
  last external check
- `MANIFEST.tsv` (`file sha256 bytes rows`): the pinned hash, size and row count of every other file
- the other tables, `README.md`, `ATTRIBUTION.md`, `audit/`, `prompts/` and `check-output.txt`: documentation and
  records, covered by the manifest

`start` and `end` are UTF-16 code unit offsets, end exclusive, into the sentence; `sentence[start..end]` is the surface
form of the labeled word. `source` is one of a closed vocabulary: the generated sources `synthetic`, `synthetic-v2`,
`synthetic-v3` and `independent-holdout`, and `harvest-<corpus>` with or without the suffix `-llm` for the natural
corpora `masc`, `open-train`, `imperative`, `compound`, `compound-2`, `tech-deverbal`, `tech-terms`, `tech-steps`,
`tech-clauses`, `headings`, `headings-2` and `silver`.

**License policy.** The license of a row is one of `CC0-1.0`, `Public-Domain` and `CC-BY-3.0-US`, and by source:
generated rows (`synthetic*`, `independent-holdout`) are `CC0-1.0`; `harvest-masc` and `harvest-masc-llm` are
`CC-BY-3.0-US`; `harvest-open-train*` and `harvest-silver*` are `CC0-1.0` or `Public-Domain`; every other `harvest-*`
source is `Public-Domain`. The content generated or labeled for the repository by DEMA Consulting, including the text
written and the readings chosen by language models, is dedicated to the public domain under CC0 1.0, as the data
README states. The check does not recompute the license of a harvested row from its original corpus tables (those are
not available to the repository); it checks the license against the source policy above, which was derived from those
tables when the data was built, and requires a provenance record for every harvested row.

**Split rules.** Natural rows are separated by their source document, so that all rows of a document are in one split.
Synthetic rows are separated by their template, so that one template is in one split. The sentence itself may not occur
in two files. A synthetic generation batch is not a separation unit: a batch may straddle splits and is not checked.

**Manifest.** `MANIFEST.tsv` lists every file of the folder except itself, the local cache folders `external/` and
`work/`, which are never committed, and nothing else, sorted by ordinal comparison of the forward-slash relative
path, with the SHA-256 in lowercase hexadecimal, the byte count and the row count (the lines minus the header for a
`.tsv` file, all lines for other text). It is regenerated whenever any data file changes; this release has a
verifier only, so regeneration is a one-off task of the data maintainer.

**External evaluation sets.** The evaluation-only corpora (WikipediaHomographData, heteronym-bench and similar) are
never committed (owner decision). An operator who holds local copies converts them to a folder in a neutral format and
passes it with `--eval-sets` or `TEXTEN_EVAL_SETS`: every `*.txt` file is one set with one sentence per line, and
every `*.tsv` file is one set whose first row is a header and whose sentences are in the column `sentence`, or else
`text` (no quoting). The name of a set is the file name without the extension. A folder that is given but missing or
without a set is a usage error, so a missing input never looks like a pass; a folder inside the data folder is a
finding. Adapters that convert the real corpora belong to the later evaluation increment. The continuous
integration pipeline never has the sets, so it runs only the internal separation checks.

**What the gate does and does not prove.** `check-data` proves that the committed files are the reviewed files, that
every row is well formed and consistent with the inventory, that no row carries a license outside the allow-list or
a license its source may not carry, and that the committed splits are separated. It does not prove that a sentence
is correctly labeled (that was judged by blind audits by second models, documented in the data), that a harvested
sentence's license is the one the original corpus states, or that the data has no overlap with an evaluation set
the operator does not have.

**Review coverage of the data.** The documents of the data folder and the small inventory tables are covered by
review sets (see `.reviewmark.yaml`). The large tables (the four data files, the provenance and gap tables, the
exclusions and candidates, the audit tables, the check output) are deliberately not review files: tens of thousands
of rows cannot be reviewed line by line, and a review of them would be noise on every regeneration. They are controlled
by the reviewed manifest (any change to any data file changes the manifest and so forces a new review of the data
provenance set), by the checks of this subsystem, and by the documented blind audits.

**Concurrency and cancellation.** None: the command is single threaded, runs to completion and keeps no state between
runs. The checks read each file once and keep only the loaded tables.
