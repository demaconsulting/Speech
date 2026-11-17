### TrainingCorpus

**Purpose**: Read the training and development data and turn every labeled row into what the model sees: the hashed
features of the target word, prepared with the analyzer's own tokenizer, tagger, token description and feature
extractor. The unit is the only place of the trainer that opens a data file for training, and it opens exactly four.

**Data Model**:

- `LabeledRow` (an internal sealed record): `Id`, `ReadingId`, `Sentence`, `Start` and `End` (UTF-16 offsets of the
  target, end exclusive) and `Source`. `IsNatural` is true when the source starts with `harvest-` (natural text, as
  opposed to generated or independently written text)
- `PreparedRow` (an internal sealed class, immutable): the labeled row, the inventory index of its form
  (`FormIndex`), the inventory index of the labeled reading (`LabelIndex`), the candidate reading indexes of the form
  in table order (`Candidates`), the tagger's part of speech of the target (`Upos`), the conjoined and the shared
  feature hashes, and, only when requested, the classifier context of the sentence and the target's index in it.
  A row is **aligned** when its target span is exactly one token of the runtime tokenization; only an aligned row has
  features and a tag (`IsAligned`). A row is **ambiguous** when its form has more than one candidate (`IsAmbiguous`);
  a one-candidate form carries no signal
- `TrainingCorpus` (an internal sealed class, immutable after loading): the `ReadingInventory`, the part-of-speech
  hints of every reading (the `|`-separated `pos_hint` column of `readings.tsv`, split, by reading index), the prepared
  training rows and development rows in file order, the **data hash** and the **features hash**, and the version of the
  part-of-speech package (`TaggerVersion`)
- `RowPreparer` (an internal static class): the reader of one labeled data file and the preparer of one row

*Isolation (a structural property).* `Load` opens exactly four files of the data folder: `train.tsv`, `dev.tsv`,
`readings.tsv` and `forms.tsv`, each by a name held in this unit. It never lists the folder and never names a holdout
file, so the holdout sets cannot influence a model by construction, not by convention. The evaluator opens a holdout
file only through its own, explicit set names (see *HeteronymEvaluator Design*).

*The data hash* is the SHA-256 of the canonical listing `name TAB sha256 LF` of the four input files in ordinal name
order (`dev.tsv`, `forms.tsv`, `readings.tsv`, `train.tsv`), with each file hashed over its raw bytes. A change of any
training or selection input changes it and an edit of any other file of the folder does not. It is stored in every
model header and in the training record.

*The features hash* is the SHA-256 of the ordered feature stream of the prepared rows (training rows, then development
rows): for each row its id, its label, a flag for alignment and, for an aligned row, the counts and the values of the
conjoined and the shared hashes in little-endian bytes. A drift of the tagger or the extractor therefore shows even
when the data files are unchanged; the training record keeps it for diagnosing exactly that.

**Key Methods**:

- **RowPreparer.ReadRows(path, name)**: reads a labeled file strictly through `TsvTable` (the header must be the data
  set's row header) and parses the offsets. A table finding, a non-numeric offset, an empty span or a span outside the
  sentence is an `InvalidDataException` that names the file (and the line for a span)
- **RowPreparer.Prepare(row, inventory, adapter, keepContext)**: the runtime parity path. It looks the target up as a
  form of the inventory (the label must be a candidate of that form whatever the tokenization says), neutralizes the
  tag placeholders the way the analyzer does (`EnglishTextAnalyzer.NeutralizePlaceholders`), splits and tags the
  sentence with `PartOfSpeechAdapter.Analyze`, finds the token that is exactly the target span, describes the sentence
  with `PartOfSpeechAdapter.DescribeSentence`, and extracts the hashes with `HeteronymFeatureExtractor.Extract`. Every
  step is the runtime's own unit (internals visible to the tool), so the features cannot differ from what the analyzer
  computes. When no token has exactly the target's span the row is returned not aligned, with no features. A target
  that is not a form, or a label that is not a candidate of it, is an `InvalidDataException`
- **TrainingCorpus.Load(dataRoot)** and **Load(dataRoot, adapter)**: read the four files (a missing file or invalid
  UTF-8 is an `InvalidDataException`), compute the data hash, parse the inventory from the two tables with the current
  inventory version, read the part-of-speech hints, prepare every row of `train.tsv` and then of `dev.tsv` while
  feeding the features hash, and return the corpus. The overload with an adapter lets tests supply one
- **TrainingCorpus.TaggerVersion**: the informational version of the part-of-speech assembly without build metadata,
  read once

**Error Handling**: every defect of the input is an `InvalidDataException` with a message that names the file, the
line or the row id; null arguments throw `ArgumentNullException`. The commands turn the
exception into the usage error exit code. No exception is swallowed.

**Dependencies**: from the TrainingDataSubsystem, `TsvTable`, `CheckReport` (only as the collector of the table's
findings) and the row header of `TextEnDataSet`; from the HeteronymSubsystem, `ReadingInventory`,
`HeteronymFeatureExtractor`, `HeteronymContext` (through `ContextToken`) and `FeatureHashing`; from the
AnalysisSubsystem, `PartOfSpeechAdapter` and `EnglishTextAnalyzer.NeutralizePlaceholders`; the OTS item
`DemaConsulting.PartOfSpeech` for its version only; and the framework (hashing, UTF-8).

**Callers**: `ModelSelection`, `AveragedPerceptronTrainer`, `HeteronymEvaluator`, `TrainCommand`, `EvaluateCommand` and
the unit tests.
