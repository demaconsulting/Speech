### ExternalRowConversion

**Purpose**: The conversion that the dataset adapters share. It converts a span given in bytes of the UTF-8 encoding of
a sentence to a span of its characters, loads the mapping from a dataset's own labels to the repository's reading
identifiers, and turns the records an adapter read into the labeled rows the evaluator takes, skipping the rows that
cannot be used and accounting for every one of them.

**Data Model** (internal types):

- `SkipReasons`: the stable names of the reasons a row is skipped, as they appear in the console output and the record,
  in the fixed order of the record: `bad-offsets` (the span cannot be placed on the sentence as the target word),
  `duplicate-of-whd` (the row is a verbatim copy of a row of another evaluated dataset), `reading-not-candidate` (the
  mapped reading is not a candidate of the target's form), `unknown-form` (the target is not a form of the reading
  inventory) and `unmapped-label` (the label is not in the mapping)
- `ExternalRecord`: one row as an adapter reads it: a row id (never published), the dataset's own label, the sentence,
  the span in characters and the stratum
- `ExternalReadResult`: what an adapter read from one source: the usable records in file order, the rows the adapter
  skipped by reason, and the numbers of rows whose sentence has a non-ASCII character, whose byte offsets were converted
  and differ, and whose offsets were repaired
- `ConversionCounts`: the rows read, the rows used, the rows skipped under every reason (each present, possibly zero),
  the three counters above and the number of used rows whose sentence equals a training or development sentence
- `ExternalRowSet`: the set name, the evaluator rows, the counts and whether the rows of one sentence form one
  resampling cluster
- `ByteOffsets`: a static class with `TryToCharOffsets` and `HasNonAscii`
- `ReadingIdMap`: the reading identifier of each label, with the header of the WHD mapping file (the WHD word identifier
  and `reading_id`) and of the benchmark mapping file (`bench_key`, `reading_id`)
- `ExternalRowConverter`: a static class with `Convert` and `TrainDevKeys`

**Byte offsets.** `TryToCharOffsets(sentence, byteStart, byteEnd, ...)` walks the sentence by Unicode scalar value,
adding the UTF-8 length of each to a byte position and the UTF-16 length to a character position, and returns the
character position at the byte start and at the byte end. It is false for a span that is empty or reversed, that starts
or ends outside the sentence, or whose start or end falls inside a multi-byte character. `HasNonAscii` is true when any
character is above U+007F.

**The mapping.** `ReadingIdMap.Load(path, header, inventory)` reads the file as strict UTF-8, requires the exact header,
two non-empty cells in every row, no label twice and a reading identifier that is in the inventory. The mapping contains
identifiers only: the dataset's label and our reading identifier.

**Conversion.** `Convert(setName, read, map, inventory, trainDevKeys, clusterBySentence)` starts the skip counts from
the adapter's own skips and checks each record in this order: its span must lie inside the sentence (otherwise
`bad-offsets`), its label must be in the mapping (`unmapped-label`), its target text must be a form of the inventory
(`unknown-form`), and the mapped reading must be a candidate of that form (`reading-not-candidate`). A usable record
becomes a labeled row that carries the mapped reading identifier and the stratum as its source, and is counted as an
overlap when its normalized sentence key is among the training and development keys; overlapping rows are **kept**, and
only counted, because dropping them would hide the effect that the count is there to show. The rows read always equal
the rows used plus all skipped rows. `TrainDevKeys(corpus)` builds the set of normalized sentence keys of the training
and development rows; the corpus holds no holdout row.

**Error Handling**: null arguments throw `ArgumentNullException`. A mapping file that is missing, not UTF-8, with a
wrong header or row, a repeated label or an unknown reading identifier is an `InvalidDataException` that names the file
and the line. A row that cannot be used is never an error; it is a counted skip.

**Dependencies**: `TrainingCorpus` and its `LabeledRow` (from the TrainingSubsystem), the sentence keys of
`LeakageChecker` (from the TrainingDataSubsystem), and the library's `ReadingInventory` (form lookup and candidates).

**Callers**: `WikipediaHomographAdapter` and `HeteronymBenchAdapter` (the types and `ByteOffsets`),
`EvaluateExternalCommand` (`ReadingIdMap`, `Convert`, `TrainDevKeys`), `ExternalEvaluationRecord` (the counts and the
skip reasons) and the unit tests.
