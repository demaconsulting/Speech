### TextEnDataSet

**Purpose**: Load the data folder once into typed rows, so that each checker works on the same parsed tables and
none of them re-reads a file or counts columns. The unit also owns the closed vocabulary of data sources with the
license policy and the generated-or-natural flag of each source.

**Data Model**: `TextEnDataSet` holds the data folder `Root` and these tables, each a list of typed read-only records
that carry their one-based `Line`: the `Readings` (`ReadingRecord`: id, lemma, part-of-speech hint, description,
status) and `ReadingsById` (the first reading of each id), the `Mapping` (`WhdMappingRecord`), the `Forms`
(`FormRecord`: form, reading id, kind), the `Decisions` (`ExtensionDecision`: lemma, kind, decision, the readings it
lists), the `Rows` of the four data files (`DataRow`: file, line, id, lemma, reading id, sentence, start and end offsets
that are null when not integers, source, license, split) with `DataFiles` (`DataFileInfo`: file name, split name and the
value of the split column) describing the four files in order train, dev, holdout-natural and holdout-independent,
the `HarvestProvenance` and `SyntheticProvenance` (`ProvenanceRecord`: id and the key, the source document or the
template), and the `Gaps` table. `LicenseIds` holds the three allowed license identifiers (`CC0-1.0`, `Public-Domain`,
`CC-BY-3.0-US`) and the allow-list. `SourceInfo` is a read-only record of a source name, whether it is generated and its
allowed licenses; the closed vocabulary is built once: the generated sources `synthetic`, `synthetic-v2`, `synthetic-v3`
and `independent-holdout` (`CC0-1.0` only), `harvest-masc` (`CC-BY-3.0-US`), `harvest-open-train` and `harvest-silver`
(`CC0-1.0` or `Public-Domain`), and the other harvest corpora (`Public-Domain`), each harvest source also with the
suffix `-llm`. The expected header of every table is a public constant of the class.

**Key Methods**:

- **Load(root, report)**: reads every table through `TsvTable` with its expected header, converts each row to a typed
  record, parses `start` and `end` as invariant-culture integers, indexes the forms by reading, applies the format rules
  to every other `.tsv` file of the folder, and returns the loaded set. A missing required file, a header that does not
  match, a format defect or a value that is not an integer is a finding that names the file and line; for an integer it
  keeps the row (with a null offset), for a wrong header it loads no row of that file; the load continues. It never
  throws for a defect in the data.
- **FormsOf(readingId)**: the set of forms listed for a reading, empty for an unknown reading or a reading without
  forms.
- **SourceInfo.TryGet(source, out info)**: looks a source up in the vocabulary; false for any other string. The caller
  decides whether an unknown source is a finding; the schema check reports it and the license check ignores it so
  that one defect gives one finding.
- **SourceInfo.KindOf(source)**: the kind of a source, `Harvest` for a harvest source, `Synthetic` for the three
  synthetic sources and `IndependentHoldout` for `independent-holdout`, or null for an unknown source. The schema and
  leakage checks decide provenance and separation from this kind and never from a row identifier.

**Error Handling**: findings, not exceptions, for every defect of content, as above; an unreadable file propagates its
input and output exception to `CheckDataCommand`.

**Dependencies**: `TsvTable`, `DataManifest` (to enumerate the other `.tsv` files) and `CheckReport`; the .NET base
class library only.

**Callers**: `CheckDataCommand` (loads once), `SchemaChecker`, `LicenseChecker`, `LeakageChecker` and the unit tests.
