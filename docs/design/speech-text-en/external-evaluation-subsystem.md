## ExternalEvaluationSubsystem Design

![ExternalEvaluationSubsystem Structure](TextEnExternalEvaluationSubsystemView.svg)

### Overview

The SpeechTextEn ExternalEvaluationSubsystem is the `evaluate-external` command of the Text.En trainer
(`tools/DemaConsulting.Speech.Text.En.Trainer`), the same console tool that hosts the `check-data`, `train` and
`evaluate` commands. The tool is built and tested with the repository but is neither packaged nor shipped. The
subsystem measures the embedded heteronym model on **external, evaluation-only datasets** that the repository does not
own: Google WikipediaHomographData (sentences from Wikipedia with a labeled reading for each homograph, with a training
and an evaluation split) and heteronym-bench (a benchmark of English items). It fetches each dataset at evaluation time
from a pinned revision into a local cache that is never committed, verifies every file against its pin, maps the
datasets' own labels to the repository's reading identifiers, evaluates the model end to end, and reports and records
only numbers. It contains the following units:

- **ExternalSourceCache**: the pins, the fetch of a pinned archive into the cache and the verified reading of cached
  files
- **ExternalRowConversion**: the byte-to-character offset conversion, the label-to-reading mapping and the conversion of
  adapter records to evaluator rows with the account of every skipped row
- **WikipediaHomographAdapter**: the reader of the pinned WikipediaHomographData files
- **HeteronymBenchAdapter**: the reader of the pinned English read set of heteronym-bench
- **ExternalEvaluationRecord**: the seven deterministic tab-separated files of the external evaluation record
- **EvaluateExternalCommand**: the command line entry of `evaluate-external`

The datasets are third-party data and the committed pins and mapping are the only things the repository holds about them
(see *Training Data, Training and External Evaluation* in *SpeechTextEn System Design*). The data of the repository is a
tool input, not a software item.

### Interfaces

The subsystem exposes one command line interface and no public API: every type is internal. `evaluate-external
[options]` evaluates and optionally writes or verifies the external evaluation record; the options and exit codes are
described with *EvaluateExternalCommand Design*. The exit codes are 0 (done, or the record is current), 1 (the record is
stale) and 2 (a usage, I/O, pin, hash or data error); results are lines that start with `evaluate-external:` on the
standard output, and usage and input errors go to the standard error.

The subsystem consumes the .NET base class library (the network is reached only by the default archive download, and
only on request), the TrainingDataSubsystem (data folder discovery, sentence keys and the path test), the
TrainingSubsystem (the corpus loader, `HeteronymEvaluator` for caller-supplied rows, `ModelSelection` for the seed
study, `TrainingRecord`, `EvaluationRecord` and `EvaluateCommand` for the model file resolution) and the internals of
the Text.En library through `InternalsVisibleTo`. The classes live in the
`DemaConsulting.Speech.Text.En.Trainer.ExternalEvaluationSubsystem` namespace. The committed inputs are
`tools/DemaConsulting.Speech.Text.En.Trainer/external-sources.json` and `external-files.tsv` (the pins),
`data/text-en/whd-mapping.tsv` and `data/text-en/bench-mapping.tsv` (the identifier mappings); the committed output is
the record in `docs/evaluation/text-en/external/`. The cache is `data/text-en/external/`, which version control ignores.

### Design

**Evaluation flow.** `EvaluateExternalCommand` resolves the data folder, the cache, the pins, the model and the record
folder, loads the pins, and has `ExternalSourceCache` make each needed source present and verified (fetching only when
asked). Per requested set, an adapter reads the rows from the verified cache and `ExternalRowConverter` maps and filters
them. Only after every pin, hash, mapping and row has been settled does it print anything: it evaluates each set through
`HeteronymEvaluator.EvaluateRows`, prints the result, runs the seed study when asked (or when it writes or checks the
record), and has `ExternalEvaluationRecord` write or compare the record.

**Fail closed (decision).** Everything that can make a figure meaningless is settled before the first figure is printed:
the pins are consistent, every needed source is cached and every pinned file has its size and hash, both mapping files
are valid, and every row has been read and converted. A failure at any of these steps is exit code 2 with a message that
names the source and file and never quotes content. Reading a cached file verifies it again against its pin. Rows that
merely cannot be used (an unmapped label, a word that is not in the inventory, a span that is not the word) are skipped,
counted by reason and reported, and the rest is evaluated.

**Fetch only on request (decision).** The command never reaches the network without `--fetch`. With it, a source whose
cache is missing or fails verification is downloaded from the pinned archive address over HTTPS, extracted into a
sibling partial folder, verified in full and only then published, so a failed download leaves the cache as it was. Only
the paths that the pins name are written, each limited to its pinned size.

**Cache location (decision).** The cache must be the git-ignored `external` folder of the data folder, which is the
default, or a folder outside the repository; any other folder inside the repository is refused before anything is
written, so a dataset cannot be committed by accident.

**External rows never reach training or selection (decision).** The seed study retrains the model with each evaluation
seed through `ModelSelection` over the `TrainingCorpus`, which opens only the training, development and inventory
files; the external rows are evaluated and are never part of a corpus. No setting is chosen from an external figure: the
figures are informational and never gate a build.

**Output without dataset text (decision).** The console output and the record files hold counts, set and stratum names,
the repository's own reading identifiers and the pins. A sentence, a target word, an item identifier or a label of a
dataset is never printed or written.

**Datasets of two kinds, two sets of rows.** WikipediaHomographData gives the sets `whd-eval` (the headline set) and
`whd-train`. Its rows have no document identifier, so rows of one sentence form one resampling cluster; that cannot see
the dependence between sentences of one article, so its intervals can be too narrow. heteronym-bench gives the set
`bench`; its items taken from WikipediaHomographData are skipped as duplicates and not pooled, and each remaining item
is its own cluster. Its answers are its own reading keys, which `bench-mapping.tsv` relates to reading identifiers. The
figures are on the granularity of the repository's readings: several identifiers of a dataset can map to one reading.

**Unit collaboration.** `ExternalSourceCache` is the only unit that opens a cache file, and only a pinned path.
`ExternalRowConversion` is the only unit that maps a label to a reading and decides whether a row is usable. The
adapters know a dataset's format and nothing of the evaluator. `ExternalEvaluationRecord` knows the layout of the record
and nothing of the datasets. `EvaluateExternalCommand` depends on every other unit, the adapters on
`ExternalSourceCache` and `ExternalRowConversion`, and there are no cycles.

**Determinism.** The pinned files are fixed, the adapters read them in ordinal path order, the bootstrap is the
evaluator's integer one with the recorded seed, and the record is integers and basis points with LF line ends and no
time stamp, so `--check` can compare it byte for byte.

**Concurrency and cancellation.** None: the command is single threaded and runs to completion. The default download
has a timeout of five minutes and nothing is kept between runs except the cache and the record.
