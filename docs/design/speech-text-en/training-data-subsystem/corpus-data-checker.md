### CorpusDataChecker

**Purpose**: Gate the committed corpus files offline on every `check-data` run. The books of the public-domain corpus
are pinned in `cluster-sources.tsv`, and the corpus sentences that repeat or quote an evaluation sentence are listed in
`cluster-removals.tsv` by the manual `corpus --audit`. Fetching and auditing need the network or the corpus cache, so
CI cannot repeat them; this unit instead verifies the schema of the two tables, the pin and the public-domain basis of
every used book, and the document-level leakage rule of the corpus.

**Data Model**: `CorpusDataChecker` holds the loaded `TextEnDataSet` and the `CheckReport`. Its findings use the check
name `corpus`, except the leakage finding, which uses `leakage`. The unit owns the shared vocabulary of the corpus
files, which the CorpusSubsystem references so the writer and the check cannot drift: the file names
(`SourcesFile`, `RemovalsFile`), the headers of the two tables, the groups, statuses and allowed mirror hosts of the
source list, the audit rules and evaluation files of the removals, and the latest publication year (1929).

**Key Methods**:

- **CorpusDataChecker(data, report)**: stores the arguments; a null data set or report throws
  `ArgumentNullException`.
- **Tables**: the names of the two tables, which `TextEnDataSet` leaves out of its generic format pass because this
  unit loads them with their exact headers.
- **Run()**: when the data folder holds neither file, adds the information line that the check is skipped and returns;
  when it holds one, the missing file is a finding and the check stops. Otherwise, in order:
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

Whether the removals are still what the audit computes for the current evaluation files and tokenizer needs the corpus
itself, so this unit cannot tell; the manual `corpus --audit` compares them byte for byte, and it is re-run before the
corpus is next used.

**Error Handling**: every violation is a finding; the methods never throw for the content of the data. A source row
with an invalid identifier or status is left out of the book counts and the removal check, so it may also cause a
removal finding.

**Dependencies**: `TextEnDataSet` (with `DataRow` and `ProvenanceRecord`), `TsvTable` and `CheckReport`.

**Callers**: `CheckDataCommand`; `CorpusSourceList` and `CorpusLeakageAudit` reference its constants; the unit tests.
