### WordClusterTable

**Purpose**: Build the rows of the word-cluster table from the clustered vocabulary so that no runtime lookup can return
another word's cluster, and own the table's fixed text form `word-clusters.tsv`.

**Data Model**:

- `WordClusterEntry`: the normalized word, the bit-string path of its cluster and its corpus count
- the text form: the header `word TAB path TAB count`, one row per word in ordinal word order, LF line endings, UTF-8
  without byte order mark

**Key Methods**:

- **Build(words, paths, counts, out droppedCollisions, out droppedIneligible)**: leaves out every word that is not
  eligible (1 to 40 characters of the reader's eligible set), groups the remaining words by
  `FeatureHashing.Fnv1a(word)` (the runtime lookup key of a normalized word) and leaves out **every** word of a group of
  two or more, counting both kinds, and returns the rest in ordinal word order. A word that is not in the table can
  still match a table hash, with a probability of about the table size divided by 2^32
- **Write(entries)**: the exact text form above
- **Parse(bytes, maximumPaths, minimumCount)**: strict UTF-8 with LF endings and no byte order mark, the exact header,
  three cells per row, eligible words that are unique and in ordinal order, paths of `0` and `1` only, counts that are
  canonical integers of at least the minimum; then the distinct paths are prefix-free (checked between neighbors in
  ordinal order) and at most `maximumPaths`, and no two words share a lookup hash
- **IsEligibleWord(word)**: the eligibility rule shared with the reader

**Error Handling**: null arguments throw `ArgumentNullException`; lists of different lengths or a repeated word throw
`ArgumentException`; any rule broken by a parsed table throws `InvalidDataException` with the line.

**Dependencies**: the Text.En library's `FeatureHashing` (internal) and `ClusterCorpusReader` (the eligible set).

**Callers**: `ClusterCommand` and the unit and subsystem tests.
