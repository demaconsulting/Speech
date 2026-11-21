### TextEnDataSet

**Purpose**: Load the data folder once into typed rows, so that each checker works on the same parsed tables and
none of them re-reads a file or counts columns. The unit also owns the closed vocabulary of data sources with the
license policy and the generated-or-natural flag of each source.

**Data Model**: `TextEnDataSet` holds the data folder `Root` and these tables, each a list of typed read-only records
that carry their one-based `Line`: the `Readings` (`ReadingRecord`) and `ReadingsById`, the `Mapping`
(`WhdMappingRecord`), the `Forms` (`FormRecord`), the `Decisions` (`ExtensionDecision`), the `Rows` of the four data
files (`DataRow`: file, line, id, lemma, reading id, sentence, start and end offsets that are null when not integers,
source, license, split) with `DataFiles` (`DataFileInfo`: file name, split name and the value of the split column)
describing the four files in order train, dev, holdout-natural and holdout-independent, the `HarvestProvenance` and
`SyntheticProvenance` (`ProvenanceRecord`: id and the source document or the template), the `Gaps` table, and the two
evaluation overlays: the `Regression` rows of `regression.tsv` (`RegressionRecord`: file, line, id, lemma, reading id,
sentence, nullable start and end, origin, reference, seen-in-training mark and license) and the `Generalization` rows
of `generalization.tsv` (`GeneralizationRecord`: file, line, id, lemma, reading id, sentence, nullable start and end,
kind, family, cue forms, cue-unsolvable mark, source and license). The overlays are not part of `DataFiles`, so the
split rules, the per-reading counts and the gap table do not see them; `RegressionFile` and `GeneralizationFile` name
them.
`LicenseIds` holds the three allowed license identifiers (`CC0-1.0`, `Public-Domain`, `CC-BY-3.0-US`) and the
allow-list. `SourceInfo` is a read-only record of a source name, whether it is generated and its allowed licenses; the
closed vocabulary is built once: the generated sources `synthetic`, `synthetic-v2`, `synthetic-v3` and
`independent-holdout` (`CC0-1.0` only), `harvest-masc` (`CC-BY-3.0-US`), `harvest-open-train` and `harvest-silver`
(`CC0-1.0` or `Public-Domain`), and the other harvest corpora (`Public-Domain`), each harvest source also with the
suffix `-llm`. The expected header of every table is a public constant of the class.

**Key Methods**:

- **Load(root, report)**: reads every table through `TsvTable` with its expected header, converts each row to a typed
  record, parses `start` and `end` as invariant-culture integers, indexes the forms by reading, applies the format rules
  to every other `.tsv` file of the folder except the two corpus tables (`CorpusDataChecker.Tables`, which that
  check loads with their exact headers, so a defect is reported once), and returns the loaded set. A missing required
  file, a header that does not match, a format defect or a value that is not an integer is a finding that names the file
  and line; for an integer it keeps the row (with a null offset), for a wrong header it loads no row of that file; the
  load continues and never throws for a defect in the data.
- **LoadRegression(path, name, report)** and **LoadGeneralization(path, name, report)**: read one overlay file with its
  exact header through `TsvTable`, carry `name` as the file of every record (the committed file name, or
  `staged:<file>` for a staged generalization set) and report a non-integer offset as a `schema` finding that keeps the
  row with a null offset. A header-only file has no rows and no finding; a missing file is a finding. `Load` calls both
  for the committed files, and `CheckDataCommand` calls `LoadGeneralization` for a staged file.
- **FormsOf(readingId)**: the set of forms listed for a reading, empty for an unknown reading.
- **SourceInfo.TryGet(source, out info)**: looks a source up in the vocabulary; false for any other string. The caller
  decides whether an unknown source is a finding; the schema check reports it and the license check ignores it so
  that one defect gives one finding.
- **SourceInfo.KindOf(source)**: `Harvest` for a harvest source, `Synthetic` for the three synthetic sources,
  `IndependentHoldout` for `independent-holdout`, or null for an unknown source. The schema and leakage checks decide
  provenance and separation from this kind and never from a row identifier.

**Error Handling**: findings, not exceptions, for every defect of content, as above; an unreadable file propagates its
input and output exception to `CheckDataCommand`.

**Dependencies**: `TsvTable`, `DataManifest` (to enumerate the other `.tsv` files) and `CheckReport`; the .NET base
class library only.

**Callers**: `CheckDataCommand` (loads once), `SchemaChecker`, `LicenseChecker`, `LeakageChecker`,
`EvaluationOverlayChecker`, `TrainingCorpus` and
`HeteronymEvaluator` of the TrainingSubsystem (the header of the labeled files and of the harvest provenance table) and
the unit tests.
