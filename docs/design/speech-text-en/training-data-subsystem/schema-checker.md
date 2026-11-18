### SchemaChecker

**Purpose**: Check that the tables of the data folder are internally consistent: that the reading inventory is well
formed, that every table refers only to readings that exist, that every labeled row is a valid example, that the
provenance tables and the rows correspond, and that the published gap table equals the counts of the rows. What the
cells mean is checked here; the file format is checked earlier by `TsvTable`, and the license values by
`LicenseChecker`.

**Data Model**: `SchemaChecker` holds the loaded `TextEnDataSet` and the `CheckReport`; its findings use the check name
`schema`. `ReadingCount` is a read-only record of the training rows of natural origin, the generated training rows, the
development rows, the natural holdout rows and the independent holdout rows of one reading; the rows still needed are
derived (`NeedSynthetic` is `max(0, 40 - natural - generated training rows)`, `NeedNatural` is
`max(0, 10 - natural training rows)`) and `ToCells` gives the cells of its line of `gaps-v4.tsv`. `ReadingCounts` holds
the `ReadingCount` of every reading. The rules it applies are the following.

- **Inventory** (`readings.tsv`): the identifier is unique and matches
  `^[a-z0-9]+(-[a-z0-9]+)*\.[a-z0-9]+(-[a-z0-9]+)*(\.[a-z0-9]+(-[a-z0-9]+)*)?$` (lowercase ASCII), and its lemma part
  (the text before the first period) equals the `lemma` cell; every part-of-speech hint (split on `|`) is one of `NOUN`,
  `VERB`, `ADJ`, `ADV`, `ADP`, `AUX`, `PROPN`; the status is `active` or `deprecated`; the description is not empty and
  is unique; and no cell contains a character from the ranges U+0250 to U+02FF, U+0300 to U+036F or U+1D00 to U+1DBF
  (the inventory carries no phonetic transcription).
- **Referential integrity**: every `whd_wordid` of the mapping is unique and its reading exists; every reading has a
  mapping, except the readings that an extension decision with the decision `add` and a kind other than `new-forms`
  added; every form refers to an existing reading and every reading has at least one form; an extension decision is
  `add` or `defer`, an `add` lists readings that exist and a `defer` lists none.
- **Rows**: the identifier is unique over the four data files; the `split` cell equals the split of the file; the
  reading exists; the `lemma` equals the lemma of the reading; the source is in the vocabulary (`SourceInfo.TryGet`);
  and, when both offsets are integers (otherwise the load reported it), the span passes `SpanValidator.Validate` with
  the forms of the reading.
- **Provenance identifiers**: what a row needs is decided by its `source`, never by its identifier. A row of a
  `synthetic*` source has an entry in the synthetic provenance table and a row of a `harvest-*` source (including the
  `-llm` variants) has an entry in the harvest provenance table; the only row without a provenance table is a row of the
  `independent-holdout` source in `holdout-independent.tsv`. An entry may not be repeated and must match a row of the
  kind its table describes (an entry for a row with an unknown source is left to the unknown-source finding).
- **Source consistency** (per row, reported at the row): the `independent-holdout` source is allowed only in
  `holdout-independent.tsv` and every row of that file has that source; an `independent-holdout` identifier starts with
  `hind-`; a synthetic identifier starts with `syn`; and a harvested identifier starts with neither `syn` nor `hind-`.
  Rows with an unknown source are not judged here. A synthetic row renamed `hind-x` is a finding for its identifier and
  for its missing synthetic provenance entry, and a row moved to or from the independent holdout file is a finding for
  its source.
- **Reading counts**: the committed `gaps-v4.tsv` must equal the computed counts cell by cell: a row that is stale, a
  repeated or unknown reading, and a reading without a row are findings.

**Key Methods**:

- **SchemaChecker(data, report)**: stores the arguments; a null argument throws `ArgumentNullException`.
- **Run()**: runs the rules above in that order, records the findings with the file and line, and adds information lines
  (not findings): the counts of decisions, readings, lemmas, mapped identifiers and forms, the rows by split and kind,
  and the readings with rows in each split. It returns the `ReadingCounts`. A rule that depends on a table that did
  not load simply sees no rows, because the load already reported the cause.
- **ReadingCounts.Build(data)**: the counts from the loaded rows; **WriteTable(writer)** writes a header and one line
  per reading (the command prints it for the counts option).

**Error Handling**: every violation is a finding; the methods never throw for the content of the data.

**Dependencies**: `TextEnDataSet` (including the source vocabulary `SourceInfo`, whose `KindOf` maps a source to
harvest, synthetic or independent holdout), `SpanValidator` and `CheckReport`.

**Callers**: `CheckDataCommand` and the unit tests.
