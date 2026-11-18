### HeteronymBenchAdapter

**Purpose**: Read the pinned English read set of heteronym-bench from the verified cache into external records. It knows
the format of that benchmark and nothing of the evaluator.

**Data Model**: a static, stateless and thread-safe class with the source id `bench` and the pinned path of the English
read set, the one JSON file of the benchmark's `data` folder that holds the English items. The file is one JSON document
with a language code `en` and an `items` object that maps an item id to an object with the fields `text` (the
sentence), `word` (the target word), `pos` (the target span as two integers, in characters), `answer` (the benchmark's
own reading key) and `src` (where the item came from; a value of `whd` marks an item copied from WikipediaHomographData,
other values are the benchmark's own sources). The label of an item is `word:answer`, its stratum is `bench-` followed
by its `src`, and its row id is the item id, which is never published.

**Key Methods**: **Read(cache, source)** reads the read set through `ExternalSourceCache.ReadAllBytes`, refuses a file
larger than 16 MB and parses it. It requires a document that is an object with the language `en` and an `items` object.
For each item it reads the four string fields (each non-empty) and the two-integer `pos`; a missing or mistyped field is
an error naming the item id. Then:

1. An item whose `src` is `whd` is counted as skipped under `duplicate-of-whd` before anything else is looked at: it is
   a verbatim copy of a sentence of WikipediaHomographData, evaluated there, and counting it again would weight the same
   sentences twice.
2. Otherwise the span is placed on the text. When the span lies inside the text and its characters, compared ignoring
   case, are the item's word, it is used. If not, the span is **repaired**: its two numbers are read as byte offsets
   and converted with `ByteOffsets.TryToCharOffsets`; if the converted span is the word, the item is used and counted
   as repaired. If neither reading gives the word, the item is skipped as `bad-offsets`.

The result counts the items whose text has a character outside ASCII and the repaired items. For the pinned revision the
two known defects of the file are in `whd` items, which are skipped first, so no evaluated item is repaired and the
repair rule is a defense for a later revision of the file.

**Error Handling**: null arguments throw `ArgumentNullException`. A file that is too large, is not valid JSON (the
message names the file and the exception kind, not the content), is not a document of English items, has no `items`
object, or has an item with a missing or unusable field is an `InvalidDataException` that names the item and never
quotes content. A cache failure from `ExternalSourceCache` propagates. An item that is skipped is a count, not an error.

**Dependencies**: `ExternalSourceCache` (the verified bytes) and `ExternalRowConversion` (`ByteOffsets`,
`ExternalRecord`, `ExternalReadResult` and `SkipReasons`); the framework's JSON reader.

**Callers**: `EvaluateExternalCommand` and the unit tests.
