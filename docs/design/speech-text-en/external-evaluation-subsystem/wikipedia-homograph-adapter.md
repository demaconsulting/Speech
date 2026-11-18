### WikipediaHomographAdapter

**Purpose**: Read the pinned files of Google WikipediaHomographData from the verified cache into external records. It
knows the format of that dataset and nothing of the evaluator.

**Data Model**: a static, stateless and thread-safe class with the source id `whd` and one method. The dataset has, for
each homograph, one tab-separated file in a folder for the training split and one in a folder for the evaluation split.
Every file starts with a header of the five fields `homograph`, the word identifier, `sentence`, `start` and `end`; the
fields are quoted in the style of comma-separated files (a quote inside a field is written twice) but separated by tabs;
`start` and `end` are unquoted integers that count **bytes** of the UTF-8 encoding of the sentence (the first byte is
included and the last is not). The label of a row is its word identifier, the stratum is `whd-train` or `whd-eval`, and
the row id is the stratum, the pinned path and the record number, which is never published.

**Key Methods**: **ReadSplit(cache, source, split)** takes the pinned paths of the source that start with
`data/<split>/` and end with `.tsv`, in ordinal path order, so a file that is not pinned is never read. For each file it
reads the bytes through `ExternalSourceCache.ReadAllBytes` (which verifies them) and decodes them as strict UTF-8. A
small record reader splits the text into records of fields: a field that starts with a quote is read to its closing
quote with a doubled quote standing for one quote and may hold tabs and line breaks; the closing quote must be followed
by a tab, a line break or the end; a line feed (with an optional carriage return before it) ends a record; blank lines
are skipped. The first record of a file must be exactly the five header fields; a file with no record at all (an empty
file or only blank lines) is rejected because it lacks that header. Every other record must have five fields
whose last two are non-negative integers. The byte span is converted with `ByteOffsets.TryToCharOffsets`; the row is
skipped as `bad-offsets` when the conversion fails or when the converted span, compared ignoring case, is not the row's
`homograph` column. Otherwise an `ExternalRecord` is added. The result counts the rows that have a character outside
ASCII and the rows whose converted span differs from the byte span; it reports no repaired row.

**Error Handling**: null arguments throw `ArgumentNullException`. A file that is not UTF-8, a quote that is not closed
or is followed by an unexpected character, a header that is not the five fields (or that is missing because the file is
empty), or a record without five fields and
integer offsets is an `InvalidDataException` that names the pinned path and the record number and never quotes content.
A cache failure from `ExternalSourceCache` propagates. A row whose span is not its homograph is a counted skip, not an
error.

**Dependencies**: `ExternalSourceCache` (the verified bytes and the pinned file list) and `ExternalRowConversion`
(`ByteOffsets`, `ExternalRecord`, `ExternalReadResult` and `SkipReasons`).

**Callers**: `EvaluateExternalCommand` and the unit tests.
