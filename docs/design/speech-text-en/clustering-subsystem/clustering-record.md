### ClusteringRecord

**Purpose**: Hold and write `clustering.json`, the record that ties the word-cluster table to its settings and inputs,
so the table's origin can be verified offline and its induction repeated exactly.

**Data Model**:

- `ClusteringSettings`: clusters, vocabulary limit, minimum count and the versions of the clusterer, the reader and the
  audit
- `ClusteringInputs`: the SHA-256 of `cluster-sources.tsv`, the numbers of used and not used books, the SHA-256 of
  `cluster-removals.tsv`, the SHA-256 of the evaluation listing, the tokenizer version (the informational version of the
  part-of-speech package without build metadata), the snapshot's SHA-256, size and file count, and the snapshot URL
  (empty while it is not published)
- `ClusteringCorpus`: paragraphs, sentences, tokens, types, bigram types, removed sentences and tokens, matched
  evaluation sentences and affected books
- `ClusteringResult`: the table's SHA-256, its words and distinct paths, the words dropped for a shared hash or an
  ineligible character, and the token coverage in ten-thousandths
- `ClusteringRecord`: the schema (1) and the four parts. Every field is an integer or a string; nothing depends on the
  machine, the time or the thread count. The JSON field names that a consistency check needs are public constants

**Key Methods**:

- **Write()**: the JSON document with the fields in a fixed order, two-space indentation, LF line endings and a final
  LF, so the same induction always writes the same bytes
- **Parse(bytes)**: reads every field with its type and refuses another schema
- **ToText()**: the written text, for comparisons

**Error Handling**: invalid JSON, another schema, a missing field or a field of the wrong type throw
`InvalidDataException` naming the file.

**Dependencies**: the .NET base class library only (the JSON reader and writer).

**Callers**: `ClusterCommand` and the unit and subsystem tests.
