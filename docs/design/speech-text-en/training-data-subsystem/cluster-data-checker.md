### ClusterDataChecker

**Purpose**: Gate the committed word-cluster files offline on every `check-data` run. The table `word-clusters.tsv` is
induced by the manual `cluster` command from the books pinned in `cluster-sources.tsv`, minus the sentences listed in
`cluster-removals.tsv`, and `clustering.json` records the settings and the hashes of those inputs. A full re-induction
needs the corpus cache and runs for minutes to hours, so CI cannot repeat it; this unit instead verifies the schema of
the three tables, the document-level leakage rule of the corpus, and that the record still describes the committed
files and the tokenizer of the build. Any later edit of a pin, a removal or the table, or an upgrade of the tokenizer
package (which moves the sentence boundaries the removals refer to), fails the data check until the table is induced
again.

**Data Model**: `ClusterDataChecker` holds the loaded `TextEnDataSet`, the `CheckReport` and the tokenizer version of
the build (null when unknown). Its findings use the check name `clusters`, except the leakage finding, which uses
`leakage`. The unit owns the shared vocabulary of the word-cluster files, which the ClusteringSubsystem references so
the writer and the check cannot drift:

- the file names (`SourcesFile`, `RemovalsFile`, `TableFile`, `RecordFile`) and the record schema version;
- the headers of the three tables, the groups, statuses and allowed mirror hosts of the source list, the audit rules
  and evaluation files of the removals, the latest publication year (1929) and the longest table word (40);
- every JSON field name of `clustering.json` (`SchemaField`, `SourcesShaField`, `TableShaField`, `TokenizerVersionField`
  and the others), used by `ClusteringRecord` to write and parse the record.

**Key Methods**:

- **ClusterDataChecker(data, report, tokenizerVersion)**: stores the arguments; a null data set or report throws
  `ArgumentNullException`.
- **Tables**: the names of the three tables, which `TextEnDataSet` leaves out of its generic format pass because this
  unit loads them with their exact headers.
- **Run()**: when the data folder holds none of the four files, adds the information line that the check is skipped
  and returns; when it holds some, each missing file is a finding and the check stops. Otherwise, in order:
  - **Source list**: loaded with `TsvTable` and its exact header. For every row: the identifier is `gutenberg-N` (a
    canonical positive integer) and the rows ascend by `N`; the group and status are known; title, author, license
    basis and basis reference are not blank; the year is `-` or a year of 1929 or earlier; the URL is HTTPS on an
    allowed mirror, on the default port, without query or fragment, and names the plain text of e-book `N`
    (`/N/pgN.txt`, `/N/N-0.txt` or `/N/N.txt`). A `used` row needs a lowercase 64-digit `body_sha256`, a positive
    `body_bytes` and a license basis containing `Public domain in the USA`; any other row has `-` in both pin cells.
    **Leakage**: the source documents of the `dev.tsv` and `holdout-natural.tsv` rows are recomputed from
    `harvest-provenance.tsv`, and a `used` row whose identifier is such a document is a `leakage` finding. An
    information line gives the number of books, used and not used.
  - **Removals**: loaded with their exact header. Every row names a `used` book; paragraph and sentence are canonical
    non-negative integers; the rule is `exact`, `near` or `contains`; the evaluation file is one of the five
    evaluation files; the identifier is not empty; `text_sha256` is a lowercase SHA-256; the rows are unique and
    sorted by source identifier (ordinal), paragraph and sentence.
  - **Record**: parsed with `System.Text.Json`. An unparsable document, a missing or mistyped field, or a schema other
    than 1 is a finding, and the comparisons below are skipped.
  - **Table**: loaded with its exact header (the generic data-file rules of `TsvTable` apply: UTF-8 without BOM, LF,
    NFC, constant columns). Every word has 1 to 40 characters from the eligible set (U+0021 to U+007E, U+00A1 to U+00FF,
    U+2010 to U+2027), the words are unique and in ordinal order, every path is a bit string, every count is a canonical
    positive integer; then the distinct paths are prefix-free (in ordinal order a prefix directly precedes a path it
    prefixes), no count is below the record's `minCount`, and there are no more distinct paths than the record's
    `clusters`.
  - **Comparison**: the SHA-256 of the committed source list, removals and table equal `sourcesSha256`,
    `removalsSha256` and `tableSha256`; `usedBooks` and `excludedBooks` equal the used and other rows of the source
    list; `words` and `paths` equal the table's rows and distinct paths; `tokenizerVersion` equals the version of the
    build. With no version given, an information line says the version was not compared. A summary information line
    gives the words, paths and removals.

The record's `evaluationListingSha256` (the hash of the five evaluation files the audit read) is deliberately not
compared: it changes with every development-data edit, and gating it would force a new induction for each. The manual
`cluster --check` verifies it, together with the snapshot hash and the byte-identical re-induction. The unit does not
compute lookup hashes either (the data check uses only the base class library); the uniqueness of the table's lookup
keys is verified by a trainer test on the committed table.

**Error Handling**: every violation is a finding; the methods never throw for the content of the data. A source row
with an invalid identifier or status is left out of the book counts and the removal check, so it may also cause a
record or removal finding.

**Dependencies**: `TextEnDataSet` (with `DataRow` and `ProvenanceRecord`), `TsvTable` and `CheckReport`; SHA-256 and
JSON of the base class library.

**Callers**: `CheckDataCommand`; `ClusteringRecord`, `ClusterSourceList`, `WordClusterTable` and `CorpusLeakageAudit`
reference its constants; the unit tests.
