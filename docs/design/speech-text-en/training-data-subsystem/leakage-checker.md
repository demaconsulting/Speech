### LeakageChecker

**Purpose**: Keep the splits honest. The unit checks that the committed training, development and holdout data share no
sentence, document or template, and, when an operator supplies local copies of the evaluation sets, that no row
repeats or nearly repeats an evaluation sentence.

**Data Model**: `LeakageChecker` holds the loaded `TextEnDataSet` and the `CheckReport`; its findings use the check name
`leakage`. `SentenceKeys` is an internal static helper with three pure functions: `Key(s)` lowercases the text with
`ToLowerInvariant()` and keeps only the ASCII letters and digits; `Words(s)` is the runs of ASCII letters and digits of
the lowercased text; and `Ngrams(s, 5)` is the set of word five-grams (joined by a space). `EvaluationSet` is a
read-only record of a set name and its sentences, and `ExternalLeakageRow` a read-only record of one line of the leakage
report (set, number of sentences, split, rows, exact matches, near duplicates and rows sharing a five-gram). The
thresholds are fixed: a near duplicate is a row with at least eight words of which at least 80 percent of the word
five-grams occur in the union of all five-grams of all evaluation sets.

**Key Methods**:

- **CheckInternal()**: always runs. A sentence key that occurs in two different data files is a finding that names the
  other file and row; the same key more than once inside one file is allowed (one sentence can carry several targets). A
  natural document (the `source_id` of the harvest provenance table) that occurs in rows of two splits is a finding; a
  document twice in one split is allowed. A synthetic template (the `template` of the synthetic provenance table) of one
  reading that occurs in two files is a finding; the same template text for another reading is allowed. Whether a row
  is natural or synthetic is decided by its source (`SourceInfo.KindOf`), never by its identifier, so renaming a row
  cannot take it out of the check. The generation
  batch of the second round is not a separation unit and is not checked.
- **LoadEvaluationSets(directory)** (static): loads every `*.txt` file as one set with one sentence per line, and every
  `*.tsv` file as one set whose sentences are in the column `sentence` or else `text` of the header row (no quoting).
  The set name is the file name without its extension, and the sets are sorted by name. Lines and cells that are empty
  or only white space are skipped; every other sentence keeps its original text. A folder that does not exist
  throws `DirectoryNotFoundException`, and a folder without a set, a tab-separated set without such a column or a file
  that yields no sentence (an empty file, a file of blank lines, a header-only table) throws `InvalidDataException`,
  naming the file; `CheckDataCommand` turns all of them into a usage error (exit code 2), so a missing or hollow input
  never means a pass.
- **CheckExternal(sets)**: runs only when sets were supplied; sets that hold no sentence at all throw
  `InvalidDataException` (the defensive counterpart of the per-file rule, also reachable by a caller that builds the
  sets itself). A row whose key equals a key of an evaluation sentence is an exact finding and a row that meets the
  near-duplicate rule is a near-duplicate finding, each naming the sets. For every set and split it counts the rows with
  an exact match, with a near duplicate and with any shared five-gram, adds those counts as information lines, and
  returns them as report rows (sets sorted by name, splits in file order).
- **WriteReport(path, rows)** (static): writes the rows in the format of `leakage-report.tsv` (header
  `eval_set eval_sentences split rows exact near_duplicate share_5gram`, UTF-8 without byte order mark, LF).
- **IsInside(directory, root)** (static): whether a folder is the data folder or lies below it, by full path.

**Error Handling**: violations are findings; a missing or empty evaluation folder, or an evaluation file without a
sentence, is an exception that
`CheckDataCommand` reports as a usage error, because silently passing without evaluating anything would be wrong. An
unreadable evaluation file propagates its input and output exception to `CheckDataCommand`.

**Dependencies**: `TextEnDataSet` (including `SourceInfo`) and `CheckReport`; the .NET base class library only. The
adapters that convert the real evaluation corpora to the neutral format are not part of this unit.

**Callers**: `CheckDataCommand` and the unit tests.
