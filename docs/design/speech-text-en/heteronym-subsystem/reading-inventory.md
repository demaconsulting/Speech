### ReadingInventory

**Purpose**: Hold the inventory of heteronym readings as an immutable, validated lookup: which readings exist
(`record.noun`, `record.verb`, `read.past`, `read.present`) and which written forms can carry which of them. The unit
also owns the grammar of a reading id, which the inventory loader, the reading override validation and the typed
annotation reader all share, so what is accepted when a reading is declared and when an annotation is read cannot
drift apart. It decides nothing about which reading a word has in a sentence: that is the classifier's question.

**Data Model**:

*Reading id grammar* (`ReadingIdSyntax`, internal and static): an id has two or three segments separated by a single
dot; each segment is one or more of the characters `a` to `z`, `0` to `9` and `-` and neither starts nor ends with
`-`; the whole id is at most 64 UTF-16 code units. Nothing else is allowed (no upper case, no space, no other
punctuation, no non-ASCII character). The **role** of an id is the text after its first dot (`noun` for
`record.noun`, `adj-noun` for `abstract.adj-noun`, `b.c` for `a.b.c`). `IsValid` checks the grammar without looking
anything up, so it never says whether the id exists.

*The inventory* (`ReadingInventory`, public and sealed): built from two tables. The readings table
(`readings.tsv`, columns `reading_id`, `lemma`, `pos_hint`, `description` and `status`) lists the readings in file
order; the status is `active` or `deprecated`, and a lemma is never empty. The forms table (`forms.tsv`, columns
`form`, `reading_id` and `kind`) lists one row per form and candidate reading, in file order; a form is 1 to 32
lower-case ASCII letters. The unit keeps:

- the reading ids in table order, an index by id, an active flag per reading, and the ordinal-sorted read-only list of
  ids that `ReadingIds` returns;
- for each reading its **label salt** and its **role salt**, computed once when the inventory is built with the hash
  functions of the feature extractor (see *HeteronymFeatureExtractor Design*), so the classification path never hashes
  an id;
- the distinct forms in order of first appearance, and for each form its candidate reading indexes **in the order of
  the rows of `forms.tsv`**, which is also the tie-break order of the classifier (the inventory has no default reading);
- an open-addressing table of form indexes, with a size that is a power of two and at least twice the number of forms,
  keyed by the folded FNV-1a hash of the form, plus the length of the shortest and of the longest form.

The inventory is a revision: `Version` is the integer revision of the tables, a constant of the code (`CurrentVersion`)
that a test checks against the embedded tables, so a change of the data cannot pass without a deliberate decision about
the revision. **Version policy:** a model records the revision it was trained for and is accepted only by an inventory
of the same or a higher revision, because a model binds its weights to reading ids through salts and never to table
positions; the classifier enforces it (see *HeteronymClassifier Design*).

The tables are the repository's `data/text-en/readings.tsv` and `forms.tsv`, embedded by link (the data folder is the
single source of truth and the files are not copied) under the fixed manifest names
`DemaConsulting.Speech.Text.En.Resources.readings.tsv` and `DemaConsulting.Speech.Text.En.Resources.forms.tsv`. The
descriptions are not retained.

*Parser limits*: at most 5,000 readings, 5,000 rows of the forms table, 16 candidates for one form, 1,024 characters
per line and 32 characters per form. A table is LF-terminated, has no byte-order mark and no carriage return, starts
with exactly its header line (the column names above, separated by tabs) and has exactly five (readings) or three
(forms) tab-separated columns in every row. A reading id is unique and satisfies the grammar, the status is one of the
two words, every row of the forms table names a reading of the readings table, a kind is not empty and a pair of form
and reading occurs once.

**Key Methods**:

- **Default** (static property): the embedded inventory, created once under `Lazy<T>` with `ExecutionAndPublication`
  from the two manifest resources through a strict UTF-8 decoder that does not skip a byte-order mark. Thread-safe;
  every caller shares the instance
- **Version**, **ReadingIds**: the revision and the ordinal-sorted read-only collection of all reading ids
- **Contains(readingId)**: true when the id is declared (active or not); exact, ordinal comparison, so the case matters.
  A null argument throws `ArgumentNullException`
- **CandidatesFor(surfaceForm)**: the reading ids a written form can carry, in the order of the rows of `forms.tsv`,
  or an empty list for anything that is not exactly a form: an unknown word, the empty text, a hyphenated compound
  (`wind-up`), a text with a space or a period, a word with a non-ASCII letter, and the Kelvin sign. The form is
  compared with **ASCII case folding only** (`A` to `Z` become `a` to `z`; nothing else folds), never with a
  culture-aware comparison, so the result is the same under every culture (the Turkish dotted capital I is not an
  `i`). The returned list is immutable and shared. A null argument throws `ArgumentNullException`
- **TryFindForm(token, out formIndex)** (internal): the allocation-free whole-token lookup the analyzer uses for every
  token. A token shorter than the shortest or longer than the longest form is rejected at once; a token with any
  character that is not an ASCII letter is rejected as soon as it is met; otherwise the folded hash is probed in the
  table and the folded characters are compared with the stored form. Only a whole tokenizer token can match, and a
  hyphenated compound never does because `-` is not a letter
- **GetCandidates(formIndex)**, **ReadingIdAt**, **FormAt**, **IsActive**, **TryGetReadingIndex**, **LabelSalt**,
  **RoleSalt**, **FormCount** and **ReadingCount** (internal): the index-based accessors of the classifier, the
  override validation and the tests; the candidates are returned as a span over the shared array, in table order
- **Parse(readings, forms, version)** (internal): the factory behind `Default` that parses two `TextReader`s with the
  limits above; tests use it to feed hand-built tables. It reads each line through a bounded buffer, so an endless
  line is stopped at the limit
- **ReadingIdSyntax.IsValid(id)** and **ReadingIdSyntax.RoleOf(id)** (internal): the grammar and the role

**Error Handling**: a malformed table throws `InvalidDataException` whose message names the table, the one-based line
and a fixed reason (`readings.tsv line 3: duplicate reading id.`) and never the offending data, so a defect of the data
cannot leak content through an exception. The embedded tables are checked by the same parser, so a defective or missing
embedded table is a defective build: the failure is deterministic and is neither retried nor turned into a degraded
inventory. The public lookups throw only `ArgumentNullException` for a null argument and never fail for any other
text.

**Dependencies**: the framework and the hash functions of the feature extractor (`FeatureHashing`) for the salts and
the folded hash. No other unit and no OTS item.

**Callers**: `HeteronymClassifier` and `HeteronymModelProvider` (candidates, salts, version, default inventory),
`EnglishTextAnalyzer` (the allocation-free lookup, through the provider), `PronunciationOverrides` (reading override
validation: existence, activity and candidacy of a reading id), `TextEnAnnotations` (the id grammar), hosts (the public
lookups) and the tests.
