using DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.ClusteringSubsystem;

/// <summary>
///     The integer bigram counts between vocabulary words, stored as sorted adjacency lists in both directions.
/// </summary>
/// <remarks>
///     Words are vocabulary ranks <c>0</c> to <see cref="WordCount"/> minus one. For a word <c>u</c>, the entries
///     <c>RightStart[u]</c> to <c>RightStart[u + 1]</c> list each word <c>v</c> that followed <c>u</c> and the count
///     <c>n(u, v)</c>, in ascending <c>v</c>; the left lists hold the same counts by the right word. The marginals are the
///     sums of these counts. Immutable after construction and thread-safe for reading.
/// </remarks>
internal sealed class BigramGraph
{
    /// <summary>
    ///     Initializes a graph from sorted, unique pair keys.
    /// </summary>
    /// <param name="words">The number of words.</param>
    /// <param name="keys">The keys <c>(u &lt;&lt; 32) | v</c> in ascending order, unique.</param>
    /// <param name="counts">The positive count of each key.</param>
    private BigramGraph(int words, long[] keys, long[] counts)
    {
        WordCount = words;
        (RightStart, RightWord, RightCount) = Adjacency(words, keys, counts, static key => (int)(key >> 32), static key => (int)(uint)key);

        // The left lists are the same pairs ordered by the right word, then the left word
        var swapped = keys.Select(static key => ((long)(uint)key << 32) | (key >>> 32)).ToArray();
        var swappedCounts = (long[])counts.Clone();
        Array.Sort(swapped, swappedCounts);
        (LeftStart, LeftWord, LeftCount) = Adjacency(words, swapped, swappedCounts, static key => (int)(key >> 32), static key => (int)(uint)key);

        LeftMarginal = new long[words];
        RightMarginal = new long[words];
        for (var u = 0; u < words; u++)
        {
            for (var e = RightStart[u]; e < RightStart[u + 1]; e++)
            {
                LeftMarginal[u] += RightCount[e];
                RightMarginal[RightWord[e]] += RightCount[e];
                Total += RightCount[e];
            }
        }
    }

    /// <summary>Gets the number of words.</summary>
    public int WordCount { get; }

    /// <summary>Gets the number of distinct pairs.</summary>
    public long PairTypes => RightWord.LongLength;

    /// <summary>Gets the total of all pair counts.</summary>
    public long Total { get; }

    /// <summary>Gets, per word, how often it is the left word of a pair.</summary>
    public long[] LeftMarginal { get; }

    /// <summary>Gets, per word, how often it is the right word of a pair.</summary>
    public long[] RightMarginal { get; }

    /// <summary>Gets the start of each word's right list; one extra entry ends the last list.</summary>
    internal int[] RightStart { get; }

    /// <summary>Gets the right words of all right lists.</summary>
    internal int[] RightWord { get; }

    /// <summary>Gets the counts of all right lists.</summary>
    internal long[] RightCount { get; }

    /// <summary>Gets the start of each word's left list; one extra entry ends the last list.</summary>
    internal int[] LeftStart { get; }

    /// <summary>Gets the left words of all left lists.</summary>
    internal int[] LeftWord { get; }

    /// <summary>Gets the counts of all left lists.</summary>
    internal long[] LeftCount { get; }

    /// <summary>
    ///     Builds a graph from sorted unique pair keys.
    /// </summary>
    /// <param name="words">The number of words; at least 0.</param>
    /// <param name="keys">The keys <c>(left &lt;&lt; 32) | right</c>, strictly ascending, both words below <paramref name="words"/>.</param>
    /// <param name="counts">The positive count of each key.</param>
    /// <returns>The graph.</returns>
    /// <exception cref="ArgumentException">The keys are not strictly ascending, name an unknown word, or a count is not positive.</exception>
    public static BigramGraph FromSortedKeys(int words, long[] keys, long[] counts)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(counts);
        ArgumentOutOfRangeException.ThrowIfNegative(words);
        if (keys.Length != counts.Length)
        {
            throw new ArgumentException("every key needs one count", nameof(counts));
        }

        for (var i = 0; i < keys.Length; i++)
        {
            if ((i > 0 && keys[i] <= keys[i - 1]) || (keys[i] >> 32) >= words || (uint)keys[i] >= (uint)words || keys[i] < 0 || counts[i] <= 0)
            {
                throw new ArgumentException("the keys must be strictly ascending pairs of known words with positive counts", nameof(keys));
            }
        }

        return new BigramGraph(words, keys, counts);
    }

    /// <summary>
    ///     Builds a graph from pair counts in any order; repeated pairs are added.
    /// </summary>
    /// <param name="words">The number of words.</param>
    /// <param name="pairs">The pairs (left word, right word, positive count).</param>
    /// <returns>The graph.</returns>
    public static BigramGraph FromPairs(int words, IEnumerable<(int Left, int Right, long Count)> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        var totals = new SortedDictionary<long, long>();
        foreach (var (left, right, count) in pairs)
        {
            var key = ((long)left << 32) | (uint)right;
            totals[key] = totals.GetValueOrDefault(key) + count;
        }

        return FromSortedKeys(words, [.. totals.Keys], [.. totals.Values]);
    }

    /// <summary>
    ///     Builds compressed adjacency lists from sorted keys.
    /// </summary>
    /// <param name="words">The number of words.</param>
    /// <param name="keys">The sorted keys.</param>
    /// <param name="counts">The counts.</param>
    /// <param name="first">Extracts the list owner of a key.</param>
    /// <param name="second">Extracts the neighbor of a key.</param>
    /// <returns>The starts, neighbors and counts.</returns>
    private static (int[] Start, int[] Word, long[] Count) Adjacency(int words, long[] keys, long[] counts, Func<long, int> first, Func<long, int> second)
    {
        var start = new int[words + 1];
        foreach (var key in keys)
        {
            start[first(key) + 1]++;
        }

        for (var u = 0; u < words; u++)
        {
            start[u + 1] += start[u];
        }

        return (start, [.. keys.Select(second)], counts);
    }
}

/// <summary>One merge of the clustering.</summary>
/// <param name="Step">The 0-based merge number.</param>
/// <param name="Word">The vocabulary rank of the word added just before the merge, or -1 for a merge of the final hierarchy.</param>
/// <param name="SlotA">The lower slot; it keeps the merged cluster.</param>
/// <param name="SlotB">The higher slot; it becomes free.</param>
/// <param name="Loss">The loss of the merge (the smallest one).</param>
internal sealed record ClusterMerge(int Step, int Word, int SlotA, int SlotB, double Loss);

/// <summary>The result of the clustering.</summary>
/// <param name="Paths">The bit-string path of each word, by vocabulary rank.</param>
/// <param name="Clusters">The number of clusters before the final hierarchy was built (the number of distinct paths).</param>
/// <param name="Merges">Every merge in order.</param>
internal sealed record InducedClusters(IReadOnlyList<string> Paths, int Clusters, IReadOnlyList<ClusterMerge> Merges);

/// <summary>Progress of the clustering.</summary>
/// <param name="WordsAdded">The number of words placed so far.</param>
/// <param name="Words">The vocabulary size.</param>
/// <param name="FinalMerges">The number of merges of the final hierarchy done so far.</param>
/// <param name="FinalMergesTotal">The number of merges of the final hierarchy.</param>
internal sealed record InducerProgress(int WordsAdded, int Words, int FinalMerges, int FinalMergesTotal);

/// <summary>The state just before a merge is chosen; for verification only.</summary>
/// <param name="Members">The words of each occupied slot.</param>
/// <param name="Losses">The maintained loss of each pair of occupied slots (lower slot first).</param>
internal sealed record InducerSnapshot(IReadOnlyDictionary<int, IReadOnlyList<int>> Members, IReadOnlyDictionary<(int A, int B), double> Losses);

/// <summary>Options of the clustering.</summary>
/// <param name="Clusters">The number of clusters; at least 2.</param>
/// <param name="Threads">The number of worker threads; at least 1. The result does not depend on it.</param>
/// <param name="Progress">Receives progress, or null.</param>
/// <param name="ProgressEvery">The number of words between two progress reports; at least 1.</param>
/// <param name="Observer">Receives the state before every merge, or null; slow, for verification only.</param>
internal sealed record InducerOptions(int Clusters, int Threads, Action<InducerProgress>? Progress = null, int ProgressEvery = 1000, Action<InducerSnapshot>? Observer = null);

/// <summary>
///     Induces hierarchical word classes from bigram counts by deterministic windowed agglomerative clustering that
///     maximizes the mutual information of adjacent classes (the class model of Brown et al. 1992, in its windowed form).
/// </summary>
/// <remarks>
///     <para>
///     <b>Procedure.</b> The first <c>C</c> words by rank fill slots <c>0</c> to <c>C - 1</c>. Each further word enters the
///     free slot, the pair of occupied slots whose merge loses the least mutual information is merged into the lower slot,
///     and the higher slot becomes free. After the last word the <c>C</c> clusters are merged the same way down to one;
///     each such merge makes a node whose child from the lower slot gets bit 0 and the other bit 1, and a word's path is
///     its cluster's root-to-leaf bits.
///     </para>
///     <para>
///     <b>Arithmetic.</b> For clusters <c>c</c> and <c>d</c>, <c>q(c, d) = n(c, d) (ln n(c, d) + ln N - ln nL(c) - ln
///     nR(d))</c> (zero when <c>n(c, d)</c> is zero), with <c>n</c> the bigram count between the placed words of the two
///     clusters, <c>nL</c> and <c>nR</c> the full left and right marginals of their words and <c>N</c> the total. Counts
///     are <see cref="long"/>; every logarithm comes from <see cref="DeterministicMath.Log"/>, directly or through a
///     table filled by it; only addition, subtraction, multiplication and division on <see cref="double"/> are used, each
///     sum in ascending slot order and each expression in a fixed order. There is no platform math library call, no fused
///     multiply-add, no vector or SIMD reduction and no culture-sensitive operation, so the result is the same on every
///     operating system and processor the runtime supports.
///     </para>
///     <para>
///     <b>Threads.</b> Each step runs in phases separated by the completion of a parallel loop (a full barrier): add a
///     word, update the losses, choose the merge, update the losses for the merge, apply it. Within a phase every cell is
///     written by exactly one iteration with a formula that does not depend on how iterations are split among threads,
///     and every iteration reads only state frozen for that phase; the chosen merge is the minimum under the total order
///     (loss, lower slot, higher slot), found per row and then reduced in ascending row order. The output is therefore
///     identical for any number of threads. The class is static and thread-safe; each call has its own state.
///     </para>
/// </remarks>
internal static class ClusterInducer
{
    /// <summary>The version of the clustering procedure; recorded so a procedure change invalidates the table.</summary>
    public const int ClustererVersion = 1;

    /// <summary>The largest log-table size: counts below it take their logarithm from the table.</summary>
    public const int LogTableLimit = 1 << 24;

    /// <summary>
    ///     Induces the word classes.
    /// </summary>
    /// <param name="graph">The bigram counts; at least two words.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">Checked between steps.</param>
    /// <returns>The paths of the words and the merge sequence.</returns>
    /// <exception cref="ArgumentException">The graph has fewer than two words, fewer than two clusters are requested, or the thread count or progress interval is below 1.</exception>
    /// <exception cref="OperationCanceledException">The run was canceled.</exception>
    public static InducedClusters Induce(BigramGraph graph, InducerOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(options);
        if (graph.WordCount < 2)
        {
            throw new ArgumentException("at least two words are needed", nameof(graph));
        }

        if (options.Clusters < 2 || options.Threads < 1 || options.ProgressEvery < 1)
        {
            throw new ArgumentException("at least two clusters, one thread and a progress interval of one are needed", nameof(options));
        }

        var state = new State(graph, options, cancellationToken);
        return state.Run();
    }

    /// <summary>The mutable state of one clustering run.</summary>
    private sealed class State
    {
        /// <summary>The bigram counts.</summary>
        private readonly BigramGraph _graph;

        /// <summary>The options.</summary>
        private readonly InducerOptions _options;

        /// <summary>The cancellation token.</summary>
        private readonly CancellationToken _cancellation;

        /// <summary>The parallel options.</summary>
        private readonly ParallelOptions _parallel;

        /// <summary>The number of slots (clusters plus one free slot).</summary>
        private readonly int _slots;

        /// <summary>The logarithms of small counts, from <see cref="DeterministicMath.Log"/>.</summary>
        private readonly double[] _log;

        /// <summary>The logarithm of the total count.</summary>
        private readonly double _lnTotal;

        /// <summary>The bigram counts between slots, row-major: <c>n[c * S + d] = n(c, d)</c>.</summary>
        private readonly long[] _n;

        /// <summary>The transposed counts: <c>nT[d * S + c] = n(c, d)</c>.</summary>
        private readonly long[] _nT;

        /// <summary>The terms <c>q(c, d)</c>, row-major.</summary>
        private readonly double[] _q;

        /// <summary>The transposed terms: <c>qT[d * S + c] = q(c, d)</c>.</summary>
        private readonly double[] _qT;

        /// <summary>The merge losses <c>L(a, b)</c> for <c>a &lt; b</c>, row-major.</summary>
        private readonly double[] _loss;

        /// <summary>The logarithms of the summed left marginals of two slots, at <c>[min * S + max]</c>.</summary>
        private readonly double[] _lnPairLeft;

        /// <summary>The logarithms of the summed right marginals of two slots, at <c>[min * S + max]</c>.</summary>
        private readonly double[] _lnPairRight;

        /// <summary>The left marginal of each slot.</summary>
        private readonly long[] _left;

        /// <summary>The right marginal of each slot.</summary>
        private readonly long[] _right;

        /// <summary>The logarithm of each slot's left marginal.</summary>
        private readonly double[] _lnLeft;

        /// <summary>The logarithm of each slot's right marginal.</summary>
        private readonly double[] _lnRight;

        /// <summary>The sum of all terms involving each slot.</summary>
        private readonly double[] _sum;

        /// <summary>Whether each slot holds a cluster.</summary>
        private readonly bool[] _occupied;

        /// <summary>The union-find parent of each placed word.</summary>
        private readonly int[] _parent;

        /// <summary>The slot of each union-find root word.</summary>
        private readonly int[] _slotOfRoot;

        /// <summary>The root word of each occupied slot.</summary>
        private readonly int[] _rootOfSlot;

        /// <summary>The per-row best pair of the merge search: the higher slot.</summary>
        private readonly int[] _rowBestSlot;

        /// <summary>The per-row best pair of the merge search: the loss.</summary>
        private readonly double[] _rowBestLoss;

        /// <summary>Scratch vectors of the merge and add updates, indexed by slot.</summary>
        private readonly long[] _v1, _v2, _v3, _v4, _v5, _v6;

        /// <summary>Scratch vectors of the merge and add updates, indexed by slot.</summary>
        private readonly double[] _t1, _t2;

        /// <summary>The merges so far.</summary>
        private readonly List<ClusterMerge> _merges = [];

        /// <summary>The occupied slots in ascending order.</summary>
        private int[] _active = [];

        /// <summary>The number of words placed so far.</summary>
        private int _placedCount;

        /// <summary>
        ///     Initializes the state.
        /// </summary>
        /// <param name="graph">The bigram counts.</param>
        /// <param name="options">The options.</param>
        /// <param name="cancellation">The cancellation token.</param>
        public State(BigramGraph graph, InducerOptions options, CancellationToken cancellation)
        {
            _graph = graph;
            _options = options;
            _cancellation = cancellation;
            _parallel = new ParallelOptions { MaxDegreeOfParallelism = options.Threads, CancellationToken = cancellation };
            _slots = Math.Min(options.Clusters, graph.WordCount) + 1;
            var cells = _slots * _slots;
            _n = new long[cells];
            _nT = new long[cells];
            _q = new double[cells];
            _qT = new double[cells];
            _loss = new double[cells];
            _lnPairLeft = new double[cells];
            _lnPairRight = new double[cells];
            _left = new long[_slots];
            _right = new long[_slots];
            _lnLeft = new double[_slots];
            _lnRight = new double[_slots];
            _sum = new double[_slots];
            _occupied = new bool[_slots];
            _rootOfSlot = new int[_slots];
            _rowBestSlot = new int[_slots];
            _rowBestLoss = new double[_slots];
            (_v1, _v2, _v3, _v4, _v5, _v6) = (new long[_slots], new long[_slots], new long[_slots], new long[_slots], new long[_slots], new long[_slots]);
            (_t1, _t2) = (new double[_slots], new double[_slots]);
            _parent = new int[graph.WordCount];
            _slotOfRoot = new int[graph.WordCount];

            // Every count is at most the total, so the table needs no entry beyond it; each entry is independent
            _log = new double[(int)Math.Min(LogTableLimit, graph.Total + 1)];
            For(1, _log.Length, i => _log[i] = DeterministicMath.Log(i));
            _lnTotal = graph.Total == 0 ? 0.0 : Ln(graph.Total);
        }

        /// <summary>
        ///     Runs the clustering.
        /// </summary>
        /// <returns>The result.</returns>
        public InducedClusters Run()
        {
            var words = _graph.WordCount;
            var initial = _slots - 1;

            // The first C words fill slots 0 to C - 1; all their pair losses are computed fresh
            for (var w = 0; w < initial; w++)
            {
                Place(w, w);
            }

            Refresh();
            For(0, _active.Length, index =>
            {
                var a = _active[index];
                for (var other = index + 1; other < _active.Length; other++)
                {
                    _loss[(a * _slots) + _active[other]] = Fresh(a, _active[other]);
                }
            });
            Report(initial, 0, initial - 1);

            // Each further word enters the free slot, then the cheapest merge restores C clusters
            var free = initial;
            for (var w = initial; w < words; w++)
            {
                _cancellation.ThrowIfCancellationRequested();
                Add(w, free);
                free = Merge(w);
                if ((w + 1) % _options.ProgressEvery == 0)
                {
                    Report(w + 1, 0, initial - 1);
                }
            }

            Report(words, 0, initial - 1);

            // The final hierarchy: merge the C clusters down to one, recording a node per merge
            var finalSlotOfWord = new int[words];
            for (var w = 0; w < words; w++)
            {
                finalSlotOfWord[w] = _slotOfRoot[Find(w)];
            }

            var leafOf = new Dictionary<int, int>();
            var node = new int[_slots];
            var children = new List<(int Zero, int One)>();
            foreach (var slot in _active)
            {
                node[slot] = children.Count;
                leafOf[slot] = children.Count;
                children.Add((-1, -1));
            }

            var clusters = _active.Length;
            var step = 0;
            while (_active.Length > 1)
            {
                _cancellation.ThrowIfCancellationRequested();
                var (a, b) = ChooseAndMerge(-1);
                children.Add((node[a], node[b]));
                node[a] = children.Count - 1;
                step++;
                if (step % _options.ProgressEvery == 0)
                {
                    Report(words, step, clusters - 1);
                }
            }

            Report(words, clusters - 1, clusters - 1);
            return new InducedClusters(Paths(finalSlotOfWord, leafOf, children, node[_active[0]]), clusters, _merges);
        }

        /// <summary>
        ///     Computes the logarithm of a count.
        /// </summary>
        /// <param name="value">The count; positive.</param>
        /// <returns>The natural logarithm, from the table or <see cref="DeterministicMath.Log"/>.</returns>
        private double Ln(long value) => value < _log.Length ? _log[value] : DeterministicMath.Log(value);

        /// <summary>
        ///     Computes one term <c>n (ln n + ln N - ln left - ln right)</c>.
        /// </summary>
        /// <param name="count">The count.</param>
        /// <param name="lnLeft">The logarithm of the left marginal.</param>
        /// <param name="lnRight">The logarithm of the right marginal.</param>
        /// <returns>The term; zero for a zero count.</returns>
        private double Term(long count, double lnLeft, double lnRight) =>
            count == 0 ? 0.0 : count * (((Ln(count) + _lnTotal) - lnLeft) - lnRight);

        /// <summary>
        ///     Places a word alone into a free slot (counts, marginals and union-find only).
        /// </summary>
        /// <param name="word">The word.</param>
        /// <param name="slot">The free slot.</param>
        private void Place(int word, int slot)
        {
            _placedCount = word + 1;
            _parent[word] = word;
            _slotOfRoot[word] = slot;
            _rootOfSlot[slot] = word;
            _left[slot] = _graph.LeftMarginal[word];
            _right[slot] = _graph.RightMarginal[word];
            _lnLeft[slot] = _left[slot] == 0 ? double.NegativeInfinity : Ln(_left[slot]);
            _lnRight[slot] = _right[slot] == 0 ? double.NegativeInfinity : Ln(_right[slot]);

            // Counts against every placed word (lower ranks); a word's pair with itself goes to n(slot, slot)
            for (var e = _graph.RightStart[word]; e < _graph.RightStart[word + 1]; e++)
            {
                var other = _graph.RightWord[e];
                if (other <= word)
                {
                    var target = other == word ? slot : _slotOfRoot[Find(other)];
                    _n[(slot * _slots) + target] += _graph.RightCount[e];
                    _nT[(target * _slots) + slot] += _graph.RightCount[e];
                }
            }

            for (var e = _graph.LeftStart[word]; e < _graph.LeftStart[word + 1]; e++)
            {
                var other = _graph.LeftWord[e];
                if (other < word)
                {
                    var source = _slotOfRoot[Find(other)];
                    _n[(source * _slots) + slot] += _graph.LeftCount[e];
                    _nT[(slot * _slots) + source] += _graph.LeftCount[e];
                }
            }

            _occupied[slot] = true;
            _active = [.. Enumerable.Range(0, _slots).Where(s => _occupied[s])];
            Terms(slot);
        }

        /// <summary>
        ///     Recomputes the terms and pair logarithms of one slot against every occupied slot.
        /// </summary>
        /// <param name="slot">The slot.</param>
        private void Terms(int slot)
        {
            foreach (var d in _active)
            {
                var forward = Term(_n[(slot * _slots) + d], _lnLeft[slot], _lnRight[d]);
                var backward = Term(_n[(d * _slots) + slot], _lnLeft[d], _lnRight[slot]);
                _q[(slot * _slots) + d] = forward;
                _qT[(d * _slots) + slot] = forward;
                _q[(d * _slots) + slot] = backward;
                _qT[(slot * _slots) + d] = backward;
                if (d != slot)
                {
                    var cell = (Math.Min(slot, d) * _slots) + Math.Max(slot, d);
                    var left = _left[slot] + _left[d];
                    var right = _right[slot] + _right[d];
                    _lnPairLeft[cell] = left == 0 ? double.NegativeInfinity : Ln(left);
                    _lnPairRight[cell] = right == 0 ? double.NegativeInfinity : Ln(right);
                }
            }
        }

        /// <summary>
        ///     Recomputes the sum of all terms involving each occupied slot, in ascending slot order.
        /// </summary>
        private void Refresh() => For(0, _active.Length, index =>
        {
            var c = _active[index];
            var row = c * _slots;
            var total = 0.0;
            foreach (var d in _active)
            {
                total += _q[row + d];
                total += _qT[row + d];
            }

            _sum[c] = total - _q[row + c];
        });

        /// <summary>
        ///     Computes the loss of merging two occupied slots from scratch.
        /// </summary>
        /// <param name="a">The lower slot.</param>
        /// <param name="b">The higher slot.</param>
        /// <returns>The loss <c>L(a, b)</c>.</returns>
        private double Fresh(int a, int b)
        {
            var rowA = a * _slots;
            var rowB = b * _slots;
            var lnLeftM = _lnPairLeft[rowA + b];
            var lnRightM = _lnPairRight[rowA + b];
            var after = 0.0;
            foreach (var d in _active)
            {
                if (d == a || d == b)
                {
                    continue;
                }

                after += Term(_n[rowA + d] + _n[rowB + d], lnLeftM, _lnRight[d]);
                after += Term(_nT[rowA + d] + _nT[rowB + d], _lnLeft[d], lnRightM);
            }

            var self = _n[rowA + a] + _n[rowA + b] + _n[rowB + a] + _n[rowB + b];
            after += Term(self, lnLeftM, lnRightM);
            return (((_sum[a] + _sum[b]) - _q[rowA + b]) - _q[rowB + a]) - after;
        }

        /// <summary>
        ///     Adds a word to the free slot and updates every loss for it.
        /// </summary>
        /// <param name="word">The word.</param>
        /// <param name="k">The free slot.</param>
        private void Add(int word, int k)
        {
            // Phase A1 (one thread): counts, marginals and terms of the new slot
            Place(word, k);
            var rowK = k * _slots;
            foreach (var j in _active)
            {
                _v1[j] = _n[(j * _slots) + k];
                _v2[j] = _n[rowK + j];
                _t1[j] = _q[(j * _slots) + k] + _q[rowK + j];
            }

            // Phase A2 (parallel): the sums of every slot now include the new slot
            Refresh();

            // Phase A3 (parallel): each existing pair gains the new slot's terms; pairs with the new slot are fresh
            var lnLeftK = _lnLeft[k];
            var lnRightK = _lnRight[k];
            For(0, _active.Length, index =>
            {
                var i = _active[index];
                if (i == k)
                {
                    return;
                }

                var row = i * _slots;
                for (var other = index + 1; other < _active.Length; other++)
                {
                    var j = _active[other];
                    if (j == k)
                    {
                        continue;
                    }

                    var toK = Term(_v1[i] + _v1[j], _lnPairLeft[row + j], lnRightK);
                    var fromK = Term(_v2[i] + _v2[j], lnLeftK, _lnPairRight[row + j]);
                    _loss[row + j] += ((_t1[i] + _t1[j]) - toK) - fromK;
                }
            });
            For(0, _active.Length, index =>
            {
                var j = _active[index];
                if (j != k)
                {
                    _loss[(Math.Min(j, k) * _slots) + Math.Max(j, k)] = Fresh(Math.Min(j, k), Math.Max(j, k));
                }
            });
        }

        /// <summary>
        ///     Chooses and applies the cheapest merge after a word was added.
        /// </summary>
        /// <param name="word">The word just added.</param>
        /// <returns>The slot that became free.</returns>
        private int Merge(int word) => ChooseAndMerge(word).B;

        /// <summary>
        ///     Chooses the cheapest merge and applies it.
        /// </summary>
        /// <param name="word">The word just added, or -1 in the final hierarchy.</param>
        /// <returns>The merged slots.</returns>
        private (int A, int B) ChooseAndMerge(int word)
        {
            Observe();

            // Phase B (parallel, then one thread): the per-row minimum, reduced in ascending row order
            For(0, _active.Length, index =>
            {
                var i = _active[index];
                var row = i * _slots;
                var bestSlot = -1;
                var bestLoss = 0.0;
                for (var other = index + 1; other < _active.Length; other++)
                {
                    var j = _active[other];
                    var loss = _loss[row + j];
                    if (bestSlot < 0 || loss < bestLoss)
                    {
                        bestSlot = j;
                        bestLoss = loss;
                    }
                }

                _rowBestSlot[i] = bestSlot;
                _rowBestLoss[i] = bestLoss;
            });

            var a = -1;
            var b = -1;
            var best = 0.0;
            foreach (var i in _active)
            {
                if (_rowBestSlot[i] >= 0 && (a < 0 || _rowBestLoss[i] < best))
                {
                    (a, b, best) = (i, _rowBestSlot[i], _rowBestLoss[i]);
                }
            }

            _merges.Add(new ClusterMerge(_merges.Count, word, a, b, best));
            UpdateForMerge(a, b);
            ApplyMerge(a, b);
            return (a, b);
        }

        /// <summary>
        ///     Updates the loss of every pair not involving the merged slots (phase C).
        /// </summary>
        /// <param name="a">The lower merged slot.</param>
        /// <param name="b">The higher merged slot.</param>
        private void UpdateForMerge(int a, int b)
        {
            // One thread: the per-slot counts and terms against a, b and the merged cluster m
            var rowA = a * _slots;
            var rowB = b * _slots;
            var lnLeftM = _lnPairLeft[rowA + b];
            var lnRightM = _lnPairRight[rowA + b];
            foreach (var i in _active)
            {
                _v1[i] = _nT[rowA + i];
                _v2[i] = _n[rowA + i];
                _v3[i] = _nT[rowB + i];
                _v4[i] = _n[rowB + i];
                _v5[i] = _v1[i] + _v3[i];
                _v6[i] = _v2[i] + _v4[i];
                _t1[i] = Term(_v5[i], _lnLeft[i], lnRightM) + Term(_v6[i], lnLeftM, _lnRight[i]);
                _t2[i] = ((_qT[rowA + i] + _q[rowA + i]) + _qT[rowB + i]) + _q[rowB + i];
            }

            // Parallel over rows: replace the terms against a and b by the terms against m
            var lnLeftA = _lnLeft[a];
            var lnRightA = _lnRight[a];
            var lnLeftB = _lnLeft[b];
            var lnRightB = _lnRight[b];
            For(0, _active.Length, index =>
            {
                var i = _active[index];
                if (i == a || i == b)
                {
                    return;
                }

                var row = i * _slots;
                for (var other = index + 1; other < _active.Length; other++)
                {
                    var j = _active[other];
                    if (j == a || j == b)
                    {
                        continue;
                    }

                    var pairLeft = _lnPairLeft[row + j];
                    var pairRight = _lnPairRight[row + j];
                    var toM = Term(_v5[i] + _v5[j], pairLeft, lnRightM);
                    var fromM = Term(_v6[i] + _v6[j], lnLeftM, pairRight);
                    var toA = Term(_v1[i] + _v1[j], pairLeft, lnRightA);
                    var fromA = Term(_v2[i] + _v2[j], lnLeftA, pairRight);
                    var toB = Term(_v3[i] + _v3[j], pairLeft, lnRightB);
                    var fromB = Term(_v4[i] + _v4[j], lnLeftB, pairRight);
                    var gained = ((_t1[i] + _t1[j]) - toM) - fromM;
                    var lost = ((((_t2[i] + _t2[j]) - toA) - fromA) - toB) - fromB;
                    _loss[row + j] += gained - lost;
                }
            });
        }

        /// <summary>
        ///     Applies a merge: slot a takes the merged cluster, slot b becomes free, and the losses of a are fresh (phase D).
        /// </summary>
        /// <param name="a">The lower merged slot.</param>
        /// <param name="b">The higher merged slot.</param>
        private void ApplyMerge(int a, int b)
        {
            // One thread: merge the counts into a and clear b
            var rowA = a * _slots;
            var rowB = b * _slots;
            var self = _n[rowA + a] + _n[rowA + b] + _n[rowB + a] + _n[rowB + b];
            for (var index = 0; index < _active.Length; index++)
            {
                var d = _active[index];
                if (d != a && d != b)
                {
                    _n[rowA + d] += _n[rowB + d];
                    _nT[(d * _slots) + a] = _n[rowA + d];
                    _nT[rowA + d] += _nT[rowB + d];
                    _n[(d * _slots) + a] = _nT[rowA + d];
                }
            }

            _n[rowA + a] = self;
            _nT[rowA + a] = self;
            for (var d = 0; d < _slots; d++)
            {
                _n[rowB + d] = 0;
                _n[(d * _slots) + b] = 0;
                _nT[rowB + d] = 0;
                _nT[(d * _slots) + b] = 0;
            }

            _left[a] += _left[b];
            _right[a] += _right[b];
            _lnLeft[a] = _left[a] == 0 ? double.NegativeInfinity : Ln(_left[a]);
            _lnRight[a] = _right[a] == 0 ? double.NegativeInfinity : Ln(_right[a]);
            _occupied[b] = false;
            _active = [.. _active.Where(slot => slot != b)];
            Union(a, b);
            Terms(a);

            // Parallel: the sums of every slot, then the fresh losses of every pair with a
            Refresh();
            For(0, _active.Length, index =>
            {
                var j = _active[index];
                if (j != a)
                {
                    _loss[(Math.Min(j, a) * _slots) + Math.Max(j, a)] = Fresh(Math.Min(j, a), Math.Max(j, a));
                }
            });
        }

        /// <summary>
        ///     Joins the words of slot b to slot a.
        /// </summary>
        /// <param name="a">The slot that keeps the cluster.</param>
        /// <param name="b">The slot that becomes free.</param>
        private void Union(int a, int b)
        {
            var rootA = _rootOfSlot[a];
            var rootB = _rootOfSlot[b];
            _parent[rootB] = rootA;
            _slotOfRoot[rootA] = a;
        }

        /// <summary>
        ///     Finds the root word of a placed word's cluster, halving the path.
        /// </summary>
        /// <param name="word">The word.</param>
        /// <returns>The root word.</returns>
        private int Find(int word)
        {
            while (_parent[word] != word)
            {
                _parent[word] = _parent[_parent[word]];
                word = _parent[word];
            }

            return word;
        }

        /// <summary>
        ///     Hands the current state to the observer, if any.
        /// </summary>
        private void Observe()
        {
            if (_options.Observer is null)
            {
                return;
            }

            var members = _active.ToDictionary(static slot => slot, static _ => (IReadOnlyList<int>)new List<int>());
            for (var w = 0; w < _graph.WordCount; w++)
            {
                if (IsPlaced(w))
                {
                    ((List<int>)members[_slotOfRoot[Find(w)]]).Add(w);
                }
            }

            var losses = new Dictionary<(int, int), double>();
            for (var x = 0; x < _active.Length; x++)
            {
                for (var y = x + 1; y < _active.Length; y++)
                {
                    losses[(_active[x], _active[y])] = _loss[(_active[x] * _slots) + _active[y]];
                }
            }

            _options.Observer(new InducerSnapshot(members, losses));
        }

        /// <summary>
        ///     Determines whether a word has been placed.
        /// </summary>
        /// <param name="word">The word.</param>
        /// <returns><see langword="true"/> when the word is in a cluster (words are placed in rank order).</returns>
        private bool IsPlaced(int word) => word < _placedCount;

        /// <summary>
        ///     Reports progress, if requested.
        /// </summary>
        /// <param name="words">The words placed.</param>
        /// <param name="finalMerges">The final merges done.</param>
        /// <param name="finalTotal">The final merges in total.</param>
        private void Report(int words, int finalMerges, int finalTotal) =>
            _options.Progress?.Invoke(new InducerProgress(words, _graph.WordCount, finalMerges, finalTotal));

        /// <summary>
        ///     Builds the bit-string path of every word.
        /// </summary>
        /// <param name="finalSlotOfWord">The slot of each word at the start of the final hierarchy.</param>
        /// <param name="leafOf">The leaf node of each slot at the start of the final hierarchy.</param>
        /// <param name="children">The children of each node (leaves have none).</param>
        /// <param name="root">The root node.</param>
        /// <returns>The paths by vocabulary rank.</returns>
        private static string[] Paths(int[] finalSlotOfWord, Dictionary<int, int> leafOf, List<(int Zero, int One)> children, int root)
        {
            // Depth-first from the root: the lower slot's child gets 0, the other 1
            var pathOfNode = new string[children.Count];
            var stack = new Stack<(int Node, string Path)>();
            stack.Push((root, string.Empty));
            while (stack.Count > 0)
            {
                var (node, path) = stack.Pop();
                pathOfNode[node] = path;
                if (children[node].Zero >= 0)
                {
                    stack.Push((children[node].One, path + "1"));
                    stack.Push((children[node].Zero, path + "0"));
                }
            }

            var paths = new string[finalSlotOfWord.Length];
            for (var w = 0; w < paths.Length; w++)
            {
                paths[w] = pathOfNode[leafOf[finalSlotOfWord[w]]];
            }

            return paths;
        }

        /// <summary>
        ///     Runs a loop over a range, in parallel when more than one thread is allowed.
        /// </summary>
        /// <param name="from">The first index.</param>
        /// <param name="to">The index after the last.</param>
        /// <param name="body">The body; it must write only cells no other iteration writes or reads.</param>
        private void For(int from, int to, Action<int> body)
        {
            if (_options.Threads == 1)
            {
                for (var i = from; i < to; i++)
                {
                    body(i);
                }

                return;
            }

            Parallel.For(from, to, _parallel, body);
        }
    }
}
