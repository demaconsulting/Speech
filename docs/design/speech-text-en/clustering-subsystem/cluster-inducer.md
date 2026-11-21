### ClusterInducer

**Purpose**: Induce hierarchical word classes from integer bigram counts by deterministic windowed agglomerative
clustering that maximizes the mutual information of adjacent classes, with a result that is identical for any number of
threads and on every supported platform.

**Data Model**:

- `BigramGraph`: the number of words (vocabulary ranks), compressed adjacency lists in both directions (for a word `u`,
  each following word `v` with the count `n(u, v)` in ascending `v`, and the same pairs by the right word), the left and
  right marginals of each word and the total `N`; built by `FromSortedKeys` (strictly ascending keys `(u << 32) | v`) or
  `FromPairs` (any order, repeated pairs added). Immutable
- `InducerOptions`: the number of clusters `C` (at least 2), the threads (at least 1), an optional progress callback
  with its interval, and an optional observer (for verification only)
- `InducedClusters`: the bit-string path of every word by rank, the number of clusters before the final hierarchy, and
  every `ClusterMerge` (step, the word just added or -1, the lower and higher slot, the loss)
- the state of one run: `C + 1` slots; the slot-by-slot counts `n` and their transpose (`long`), the terms `q` and their
  transpose, the losses `L(a, b)` for `a < b`, the logarithms of the summed left and right marginals of every slot pair,
  the marginals of each slot and their logarithms, the sum `s(c)` of all terms involving each slot, the occupied slots
  in ascending order, a union-find over the placed words with the slot of each root, and the table of logarithms of
  counts below `min(2^24, N + 1)`. `ClustererVersion` 1

**Definitions.** For clusters `c` and `d`, `n(c, d)` sums the bigrams between the placed words of `c` and `d`; `nL(c)`
and `nR(c)` sum the full left and right marginals of their words. `q(c, d) = n (ln n + ln N - ln nL(c) - ln nR(d))`,
and `q = 0` when `n = 0`. `s(c) = Σd q(c, d) + Σd q(d, c) - q(c, c)`. The loss of merging `a` and `b` into `m` is
`L(a, b) = s(a) + s(b) - q(a, b) - q(b, a) - [Σd∉{a,b} (q(m, d) + q(d, m)) + q(m, m)]`, the decrease of `Σ q`.

**Key Methods**:

- **Induce(graph, options, token)**: runs the procedure below and returns the result
  1. **Start**: ranks `0` to `C - 1` fill slots `0` to `C - 1` (fewer when the vocabulary is smaller); all terms, sums
     and losses are computed fresh
  2. **Per further word** `w` (ranks ascending), with the free slot `k`:
     - *add* (one thread): `n(k, ·)` and `n(·, k)` from `w`'s adjacency lists mapped to the current slots, its
       marginals, terms and pair logarithms; then, in parallel, every sum `s` fresh; then, in parallel over rows, each
       existing pair `(i, j)` gains `q(i, k) + q(k, i) + q(j, k) + q(k, j) - q(i+j, k) - q(k, i+j)` and every pair with
       `k` is computed fresh;
     - *choose*: in parallel the minimum of each row under (loss, higher slot), then one thread reduces the rows in
       ascending order under (loss, lower slot, higher slot);
     - *update* (parallel over rows): each pair `(i, j)` without `a` or `b` replaces its terms against `a` and `b`
       (`q(i, a) + q(a, i) + q(i, b) + q(b, i) + q(j, ...)` and the hypothetical `q(i+j, a)`, `q(a, i+j)`, `q(i+j, b)`,
       `q(b, i+j)`) by its terms against the merged cluster `m` (`q(i, m) + q(m, i) + q(j, ...)` and `q(i+j, m)`,
       `q(m, i+j)`);
     - *apply* (one thread, then parallel): slot `a` takes the merged counts and marginals, slot `b` becomes free, the
       union-find joins the words, `a`'s terms and pair logarithms are recomputed, then every sum and every loss with
       `a` fresh
  3. **Final hierarchy**: the remaining clusters are merged down to one with the same choose, update and apply phases;
     each merge makes a node whose child from the lower slot gets bit 0 and the other bit 1, and each word's path is the
     root-to-leaf bits of its cluster
  Progress is reported at the interval and at the end of both stages. The observer, when given, receives the members of
  every slot and every maintained loss before each choice (used by the brute-force oracle test)

**Determinism.** Counts are `long`. Every logarithm is `DeterministicMath.Log`, either directly (a count of at least the
table size) or from the table, which is filled with that same function, so both give the same bits. Only `+`, `-`, `*`
and `/` on `double` are used, each expression in a fixed order and each sum in ascending slot order; there is no
platform math call, fused multiply-add, vector reduction or culture-sensitive operation. In every parallel phase each
cell is written by exactly one iteration with a formula that does not depend on the division of the work, and each
iteration reads only state frozen for the phase; the phases are separated by the completion of the parallel loop (a full
barrier), and with one thread the loops run sequentially. The output therefore does not depend on the thread count or
the platform. Ties are broken by the lowest lower slot, then the lowest higher slot.

**Cost.** Each word costs about `8 C²` term evaluations (the update of all pairs, the fresh losses of the new and the
merged slot) and O(degree) to map its adjacency lists; memory is `7 (C + 1)²` numbers, the adjacency lists and the
logarithm table (at most 128 MiB). A micro-benchmark with `C = 512` measured about 5 ms per word on one thread and about
1.1 ms per word with 8 or more threads on a 28-thread laptop processor, so a vocabulary of 50,000 words takes about 4
minutes on one thread and about 1 minute on many.

**Error Handling**: null arguments throw `ArgumentNullException`; fewer than two words, fewer than two clusters, fewer
than one thread or a progress interval below 1 throw `ArgumentException`, and so do invalid graph keys or counts.
Cancellation throws `OperationCanceledException` between steps.

**Dependencies**: the TrainingSubsystem's `DeterministicMath` and the .NET base class library (parallel loops).

**Callers**: `ClusterCorpusReader` (builds the `BigramGraph`), `ClusterCommand` and the unit tests.
