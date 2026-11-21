using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Text.En.Trainer.TrainingSubsystem;

/// <summary>
///     The weights of one training epoch after averaging, quantizing and pruning, as they would be stored in a model.
/// </summary>
/// <param name="Epoch">The number of epochs trained so far, from 1.</param>
/// <param name="Weights">The deployable integer weights by bucket; zero means no stored weight. The array is reused by the next epoch.</param>
internal readonly record struct EpochWeights(int Epoch, short[] Weights);

/// <summary>
///     Trains the heteronym model: a candidate-restricted multi-class averaged perceptron over the hashed features,
///     in integer arithmetic.
/// </summary>
/// <remarks>
///     <para>
///     <b>Learning.</b> For each training row the trainer scores the candidate readings of the row's form with the
///     runtime's own scoring (the same buckets, salts and integer sums) and, when the best candidate is not the
///     labeled one, adds one to the weights of the labeled candidate's buckets and subtracts one from those of the
///     predicted candidate's buckets, once per feature occurrence. An exact tie keeps the earlier candidate, the
///     runtime's tie rule. Rows are visited once per epoch in the configured order, and a natural row is visited as
///     many times per epoch as the settings say. The positional order is a seeded shuffle of the row positions; the
///     keyed order sorts the visits by a seeded key of the row id, so adding training rows never changes the order in
///     which the existing rows are visited relative to each other.
///     </para>
///     <para>
///     <b>Exact averaging.</b> The average of the weights after each of the <c>T</c> steps is computed exactly from
///     the current weight <c>W</c> and the time-weighted sum <c>U</c> of the updates as
///     <c>((T + 1) W - U) / T</c>, in 64-bit integers, then scaled, rounded half away from zero, clamped to 16-bit
///     range and pruned. No floating-point arithmetic is involved, so the result is identical on every platform.
///     </para>
///     <para>
///     <b>Overflow bound.</b> The arithmetic must never wrap silently, because a wrapped value would corrupt a weight
///     without any sign of it. Let <c>T</c> be the most steps training can take (the weighted rows visited per epoch
///     times <c>MaxEpochs</c>) and <c>F</c> the most feature occurrences one candidate has (conjoined plus shared).
///     A step changes one bucket at most <c>2F</c> times (the labeled and the predicted candidate), each time by one,
///     so <c>|W| &lt;= 2FT</c>. The inner term <c>(T + 1) W - U</c> equals the sum of <c>delta * (T + 1 - s)</c> over
///     the updates made at steps <c>s</c>, so <c>|(T + 1) W - U| &lt;= F T (T + 1)</c>, and every intermediate value
///     (<c>(T + 1) W</c> at most <c>2FT(T + 1)</c>, <c>U</c> at most <c>FT(T + 1)</c>) stays below
///     <c>2FT(T + 1)</c>. The constructor therefore requires <c>2 * Scale * F * T * (T + 1) + T</c> to fit in a
///     64-bit integer (this also covers the doubled numerator and the rounding term in <c>Quantize</c>) and
///     <c>2FT</c> to fit in a 32-bit integer (the live weights), computing the bound in 128-bit integers so the
///     check itself cannot overflow. Settings beyond the bound are rejected before any training; the arithmetic is
///     also <c>checked</c> so that a flaw in this derivation would throw instead of wrapping. For the committed
///     corpus and settings the bound is far inside the limits, so the committed model is unaffected.
///     </para>
///     <para>
///     Single-thread and allocation-bounded: two tables of 2^hashBits entries. One instance trains once; it is not
///     thread-safe.
///     </para>
/// </remarks>
internal sealed class AveragedPerceptronTrainer
{
    /// <summary>The largest magnitude a stored weight may have (the writer cannot store -32768).</summary>
    private const int MaxWeight = short.MaxValue;

    /// <summary>The corpus whose rows are trained on.</summary>
    private readonly TrainingCorpus _corpus;

    /// <summary>The settings.</summary>
    private readonly TrainingSettings _settings;

    /// <summary>The bucket mask.</summary>
    private readonly uint _mask;

    /// <summary>
    ///     Initializes a trainer.
    /// </summary>
    /// <param name="corpus">The corpus. Must not be null.</param>
    /// <param name="settings">The settings. Must not be null and valid.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     The settings are out of range, or, for this corpus, they could overflow the integer averaging arithmetic
    ///     (see the class remarks); the message names the settings to reduce.
    /// </exception>
    public AveragedPerceptronTrainer(TrainingCorpus corpus, TrainingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();
        RequireNoOverflow(corpus, settings);
        _corpus = corpus;
        _settings = settings;
        _mask = (uint)((1L << settings.HashBits) - 1);
    }

    /// <summary>
    ///     Rejects settings that could make the integer averaging arithmetic overflow on this corpus.
    /// </summary>
    /// <remarks>
    ///     Silent wrap-around would corrupt weights with no visible sign, so the worst case is bounded up front, from the
    ///     corpus and the settings, using the derivation in the class remarks. The bound is computed in 128-bit
    ///     integers, which cannot overflow for any int-sized corpus and the validated settings.
    /// </remarks>
    /// <param name="corpus">The corpus whose rows will be trained on.</param>
    /// <param name="settings">The validated settings.</param>
    /// <exception cref="InvalidDataException">The worst-case value does not fit the 64-bit or 32-bit arithmetic.</exception>
    private static void RequireNoOverflow(TrainingCorpus corpus, TrainingSettings settings)
    {
        // The weighted number of rows visited per epoch and the most feature occurrences of one candidate.
        Int128 visits = 0;
        Int128 features = 0;
        foreach (var row in corpus.Train)
        {
            if (!row.IsAligned || !row.IsAmbiguous)
            {
                continue;
            }

            visits += row.Row.IsNatural ? settings.NaturalWeight : 1;
            features = Int128.Max(features, row.Conjoined.Length + row.Shared.Length);
        }

        // T, the 64-bit bound on the scaled numerator and its rounding, and the 32-bit bound on a live weight.
        Int128 steps = visits * settings.MaxEpochs;
        Int128 numeratorBound = (2 * (Int128)settings.Scale * features * steps * (steps + 1)) + steps;
        Int128 weightBound = 2 * features * steps;
        if (numeratorBound > long.MaxValue || weightBound > int.MaxValue)
        {
            throw new InvalidDataException(
                $"the settings could overflow the integer averaging arithmetic on this corpus (scale {settings.Scale}, "
                + $"naturalWeight {settings.NaturalWeight}, maxEpochs {settings.MaxEpochs}, up to {steps} training steps "
                + $"of up to {features} features); reduce scale, naturalWeight or maxEpochs");
        }
    }

    /// <summary>
    ///     Scores the candidates of a prepared row with integer weights and returns the best candidate position.
    /// </summary>
    /// <typeparam name="TWeights">The weight source.</typeparam>
    /// <param name="inventory">The inventory that supplies the salts.</param>
    /// <param name="row">The aligned, ambiguous row.</param>
    /// <param name="weights">The weights.</param>
    /// <param name="mask">The bucket mask of the weights.</param>
    /// <param name="scores">Receives the integer score of every candidate; at least as long as the candidate list.</param>
    /// <returns>The position of the best candidate in the row's candidate list; a tie keeps the earlier one.</returns>
    public static int Decide<TWeights>(ReadingInventory inventory, PreparedRow row, TWeights weights, uint mask, Span<int> scores)
        where TWeights : struct, HeteronymClassifier.IWeightSource
    {
        var best = 0;
        for (var c = 0; c < row.Candidates.Count; c++)
        {
            var reading = row.Candidates[c];
            scores[c] = HeteronymClassifier.ScoreCandidate(weights, row.Conjoined, row.Shared, inventory.LabelSalt(reading), inventory.RoleSalt(reading), mask);
            if (scores[c] > scores[best])
            {
                best = c;
            }
        }

        return best;
    }

    /// <summary>
    ///     Trains for the configured number of epochs and reports the deployable weights after each one.
    /// </summary>
    /// <param name="onEpoch">Called after each epoch with its deployable weights; the array must not be kept past the call.</param>
    public void Train(Action<EpochWeights> onEpoch)
    {
        ArgumentNullException.ThrowIfNull(onEpoch);

        // The rows that carry signal, and the visiting order with natural rows repeated.
        var used = UsedRows(_corpus);
        var schedule = new VisitSchedule(used, _settings);
        var table = 1 << _settings.HashBits;
        var weights = new int[table];
        var sums = new long[table];
        var deployable = new short[table];
        Span<int> scores = stackalloc int[ReadingInventory.MaxCandidates];
        var inventory = _corpus.Inventory;
        long step = 0;

        for (var epoch = 1; epoch <= _settings.MaxEpochs; epoch++)
        {
            schedule.NextEpoch();
            for (var position = 0; position < schedule.Count; position++)
            {
                step++;
                var row = used[schedule.RowAt(position)];
                var predicted = Decide(inventory, row, new IntWeights(weights), _mask, scores);
                var gold = IndexOf(row.Candidates, row.LabelIndex);
                if (predicted == gold)
                {
                    continue;
                }

                Update(weights, sums, row, inventory, row.Candidates[gold], +1, step);
                Update(weights, sums, row, inventory, row.Candidates[predicted], -1, step);
            }

            Quantize(weights, sums, step, deployable);
            onEpoch(new EpochWeights(epoch, deployable));
        }
    }

    /// <summary>
    ///     Gets the order in which training visits the rows in each epoch, as row ids and repeat numbers.
    /// </summary>
    /// <remarks>
    ///     Training needs no such list; it exists so that the visiting order, which determines the learned weights,
    ///     can be inspected and its properties (repeatability, and for the keyed order the stability of the existing
    ///     rows' relative order when rows are added) verified directly. It replays exactly the schedule
    ///     <see cref="Train"/> uses, without training. The settings are validated; the overflow bound is not checked
    ///     because nothing is computed with weights.
    /// </remarks>
    /// <param name="corpus">The corpus. Must not be null.</param>
    /// <param name="settings">The settings; the seed, the natural-row weight, the epoch count and the order are used. Must not be null and valid.</param>
    /// <returns>One list per epoch, in visiting order, of the row id and the repeat number (0 for the first visit of the row in the epoch).</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="InvalidDataException">The settings are out of range.</exception>
    internal static IReadOnlyList<IReadOnlyList<(string Id, int Repeat)>> VisitOrder(TrainingCorpus corpus, TrainingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(corpus);
        ArgumentNullException.ThrowIfNull(settings);
        settings.Validate();

        var used = UsedRows(corpus);
        var schedule = new VisitSchedule(used, settings);
        var epochs = new List<IReadOnlyList<(string Id, int Repeat)>>(settings.MaxEpochs);
        for (var epoch = 1; epoch <= settings.MaxEpochs; epoch++)
        {
            schedule.NextEpoch();
            var visits = new List<(string Id, int Repeat)>(schedule.Count);
            for (var position = 0; position < schedule.Count; position++)
            {
                visits.Add((used[schedule.RowAt(position)].Row.Id, schedule.RepeatAt(position)));
            }

            epochs.Add(visits);
        }

        return epochs;
    }

    /// <summary>
    ///     Computes the sort key of one visit in the keyed visiting order.
    /// </summary>
    /// <remarks>
    ///     The key is a chain of SplitMix64 output mixes (the first output of <see cref="DeterministicRandom"/> for a
    ///     given state): the seed, then the epoch, the row-id hash and the repeat number, each combined by exclusive or
    ///     and mixed again. It depends on nothing but these four values, so a row keeps its key, and therefore its
    ///     place relative to every other existing row, when rows are added to or removed from the corpus; and a
    ///     different seed or epoch gives an unrelated order.
    /// </remarks>
    /// <param name="seed">The training seed.</param>
    /// <param name="epoch">The epoch, from 1.</param>
    /// <param name="idHash">The 64-bit FNV-1a hash of the row id (see <see cref="RowIdHash"/>).</param>
    /// <param name="repeat">The repeat number of the visit within the epoch, from 0.</param>
    /// <returns>The key; visits are taken in increasing key order.</returns>
    internal static ulong VisitKey(ulong seed, int epoch, ulong idHash, int repeat)
    {
        var key = Mix(seed);
        key = Mix(key ^ (ulong)epoch);
        key = Mix(key ^ idHash);
        return Mix(key ^ (ulong)repeat);
    }

    /// <summary>
    ///     Computes the 64-bit FNV-1a hash of a row id over its UTF-8 bytes.
    /// </summary>
    /// <remarks>
    ///     The row id, not the row's position, identifies a row in the keyed order, so the hash must be a fixed,
    ///     platform-independent function of the id text; FNV-1a (offset basis 14695981039346656037, prime
    ///     1099511628211) is such a function.
    /// </remarks>
    /// <param name="id">The row id. Must not be null.</param>
    /// <returns>The hash.</returns>
    internal static ulong RowIdHash(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        const ulong OffsetBasis = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;
        var hash = OffsetBasis;
        foreach (var b in System.Text.Encoding.UTF8.GetBytes(id))
        {
            hash = unchecked((hash ^ b) * Prime);
        }

        return hash;
    }

    /// <summary>
    ///     Mixes a 64-bit value: the first SplitMix64 output of a generator whose state is the value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The mixed value.</returns>
    private static ulong Mix(ulong value) => new DeterministicRandom(value).NextUInt64();

    /// <summary>
    ///     Selects the rows that carry signal: aligned rows whose form has more than one candidate reading.
    /// </summary>
    /// <param name="corpus">The corpus.</param>
    /// <returns>The rows, in training-file order.</returns>
    private static List<PreparedRow> UsedRows(TrainingCorpus corpus)
    {
        var used = new List<PreparedRow>();
        foreach (var row in corpus.Train)
        {
            if (row.IsAligned && row.IsAmbiguous)
            {
                used.Add(row);
            }
        }

        return used;
    }

    /// <summary>
    ///     Finds a reading in a candidate list.
    /// </summary>
    /// <param name="candidates">The candidates.</param>
    /// <param name="reading">The reading index.</param>
    /// <returns>The position, which exists because the row was validated.</returns>
    private static int IndexOf(IReadOnlyList<int> candidates, int reading)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == reading)
            {
                return i;
            }
        }

        throw new InvalidOperationException("The label is not a candidate.");
    }

    /// <summary>
    ///     Adds a delta to every bucket one candidate's features select, once per feature occurrence.
    /// </summary>
    /// <param name="weights">The current weights.</param>
    /// <param name="sums">The time-weighted sums of the updates.</param>
    /// <param name="row">The row.</param>
    /// <param name="inventory">The inventory that supplies the salts.</param>
    /// <param name="reading">The candidate reading.</param>
    /// <param name="delta">The change, +1 or -1.</param>
    /// <param name="step">The current step number.</param>
    private void Update(int[] weights, long[] sums, PreparedRow row, ReadingInventory inventory, int reading, int delta, long step)
    {
        var label = inventory.LabelSalt(reading);
        foreach (var feature in row.Conjoined)
        {
            var bucket = FeatureHashing.Bucket(feature, label, _mask);
            weights[bucket] = checked(weights[bucket] + delta);
            sums[bucket] = checked(sums[bucket] + (delta * step));
        }

        var role = inventory.RoleSalt(reading);
        foreach (var feature in row.Shared)
        {
            var bucket = FeatureHashing.Bucket(feature, role, _mask);
            weights[bucket] = checked(weights[bucket] + delta);
            sums[bucket] = checked(sums[bucket] + (delta * step));
        }
    }

    /// <summary>
    ///     Averages, scales, rounds, clamps and prunes the weights.
    /// </summary>
    /// <param name="weights">The current weights.</param>
    /// <param name="sums">The time-weighted sums.</param>
    /// <param name="steps">The number of steps so far.</param>
    /// <param name="result">Receives the deployable weights.</param>
    private void Quantize(int[] weights, long[] sums, long steps, short[] result)
    {
        var scale = (long)_settings.Scale;
        for (var bucket = 0; bucket < weights.Length; bucket++)
        {
            if (weights[bucket] == 0 && sums[bucket] == 0)
            {
                result[bucket] = 0;
                continue;
            }

            // The exact average of the post-step weights, scaled: scale * ((T + 1) W - U) / T, rounded half away from
            // zero. Checked: the constructor's bound makes overflow impossible, and this turns any flaw in that
            // derivation into an exception rather than a silently corrupted weight.
            var numerator = checked(scale * (((steps + 1) * weights[bucket]) - sums[bucket]));
            var magnitude = checked((2 * Math.Abs(numerator)) + steps) / (2 * steps);
            var rounded = (int)Math.Min(magnitude, MaxWeight);
            if (rounded <= _settings.PruneThreshold)
            {
                rounded = 0;
            }

            result[bucket] = (short)(numerator < 0 ? -rounded : rounded);
        }
    }

    /// <summary>The live integer weights as a weight source.</summary>
    /// <param name="Weights">The weights by bucket.</param>
    private readonly record struct IntWeights(int[] Weights) : HeteronymClassifier.IWeightSource
    {
        /// <inheritdoc/>
        public int GetWeight(uint bucket) => Weights[bucket];
    }

    /// <summary>
    ///     The visits of one training run (each used row as many times per epoch as its weight says) and their order
    ///     in the current epoch.
    /// </summary>
    /// <remarks>
    ///     The visits are listed once, in training-file order with the repeats of a row together. The positional order
    ///     shuffles the list of visit positions in place at the start of every epoch with one generator seeded once,
    ///     exactly the schedule of the first trained models. The keyed order sorts the visits by
    ///     <see cref="VisitKey"/> afresh each epoch, ties going to the ordinal row id, then the repeat number, then
    ///     the file position (the last only for duplicate ids, which the data checks forbid), so the order is total
    ///     and the sort deterministic. Not thread-safe; one instance belongs to one run.
    /// </remarks>
    private sealed class VisitSchedule
    {
        /// <summary>The row index (into the used rows) of each visit.</summary>
        private readonly int[] _rowOf;

        /// <summary>The repeat number of each visit.</summary>
        private readonly int[] _repeatOf;

        /// <summary>The visit indexes in the current epoch's order.</summary>
        private readonly int[] _schedule;

        /// <summary>The row ids, by row index, for the keyed tie-break.</summary>
        private readonly string[] _ids;

        /// <summary>The row-id hashes, by row index, for the keyed order.</summary>
        private readonly ulong[] _idHashes;

        /// <summary>The keys of the visits in the current epoch (keyed order only).</summary>
        private readonly ulong[] _keys;

        /// <summary>The settings.</summary>
        private readonly TrainingSettings _settings;

        /// <summary>The generator of the positional shuffle.</summary>
        private readonly DeterministicRandom _random;

        /// <summary>The current epoch, from 1 after the first <see cref="NextEpoch"/>.</summary>
        private int _epoch;

        /// <summary>
        ///     Initializes the visits of the used rows.
        /// </summary>
        /// <param name="used">The used rows, in training-file order.</param>
        /// <param name="settings">The validated settings.</param>
        public VisitSchedule(IReadOnlyList<PreparedRow> used, TrainingSettings settings)
        {
            _settings = settings;
            _random = new DeterministicRandom(settings.Seed);
            _ids = new string[used.Count];
            _idHashes = new ulong[used.Count];
            var rows = new List<int>();
            var repeats = new List<int>();
            for (var i = 0; i < used.Count; i++)
            {
                _ids[i] = used[i].Row.Id;
                _idHashes[i] = RowIdHash(_ids[i]);
                var count = used[i].Row.IsNatural ? settings.NaturalWeight : 1;
                for (var r = 0; r < count; r++)
                {
                    rows.Add(i);
                    repeats.Add(r);
                }
            }

            _rowOf = [.. rows];
            _repeatOf = [.. repeats];
            _schedule = new int[_rowOf.Length];
            for (var v = 0; v < _schedule.Length; v++)
            {
                _schedule[v] = v;
            }

            _keys = new ulong[_rowOf.Length];
        }

        /// <summary>Gets the number of visits per epoch.</summary>
        public int Count => _schedule.Length;

        /// <summary>
        ///     Gets the row index of the visit at a position of the current epoch.
        /// </summary>
        /// <param name="position">The position, from 0.</param>
        /// <returns>The index into the used rows.</returns>
        public int RowAt(int position) => _rowOf[_schedule[position]];

        /// <summary>
        ///     Gets the repeat number of the visit at a position of the current epoch.
        /// </summary>
        /// <param name="position">The position, from 0.</param>
        /// <returns>The repeat number, from 0.</returns>
        public int RepeatAt(int position) => _repeatOf[_schedule[position]];

        /// <summary>
        ///     Advances to the next epoch and orders its visits.
        /// </summary>
        public void NextEpoch()
        {
            _epoch++;
            if (_settings.RowOrder == RowVisitingOrder.Positional)
            {
                // The shuffle continues from the previous epoch's order with the same generator.
                _random.Shuffle(_schedule.AsSpan());
                return;
            }

            // Keyed: every visit's key depends only on the seed, the epoch, its row id and its repeat number.
            for (var v = 0; v < _keys.Length; v++)
            {
                _keys[v] = VisitKey(_settings.Seed, _epoch, _idHashes[_rowOf[v]], _repeatOf[v]);
                _schedule[v] = v;
            }

            Array.Sort(_schedule, Compare);
        }

        /// <summary>
        ///     Compares two visits in the keyed order: key, then ordinal row id, then repeat number, then file position.
        /// </summary>
        /// <param name="a">The first visit index.</param>
        /// <param name="b">The second visit index.</param>
        /// <returns>Negative, zero or positive as <paramref name="a"/> comes first, is the same visit, or comes later.</returns>
        private int Compare(int a, int b)
        {
            var byKey = _keys[a].CompareTo(_keys[b]);
            if (byKey != 0)
            {
                return byKey;
            }

            var byId = string.CompareOrdinal(_ids[_rowOf[a]], _ids[_rowOf[b]]);
            if (byId != 0)
            {
                return byId;
            }

            var byRepeat = _repeatOf[a].CompareTo(_repeatOf[b]);
            return byRepeat != 0 ? byRepeat : a.CompareTo(b);
        }
    }
}

/// <summary>The deployable integer weights as a weight source.</summary>
/// <param name="Weights">The weights by bucket.</param>
internal readonly record struct ShortWeights(short[] Weights) : HeteronymClassifier.IWeightSource
{
    /// <inheritdoc/>
    public int GetWeight(uint bucket) => Weights[bucket];
}
