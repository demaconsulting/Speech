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
///     runtime's tie rule. Rows are visited in a seeded shuffle, once per epoch, and a natural row is visited as
///     many times per epoch as the settings say.
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
        var used = new List<PreparedRow>();
        foreach (var row in _corpus.Train)
        {
            if (row.IsAligned && row.IsAmbiguous)
            {
                used.Add(row);
            }
        }

        var order = new List<int>();
        for (var i = 0; i < used.Count; i++)
        {
            var repeats = used[i].Row.IsNatural ? _settings.NaturalWeight : 1;
            for (var r = 0; r < repeats; r++)
            {
                order.Add(i);
            }
        }

        var table = 1 << _settings.HashBits;
        var weights = new int[table];
        var sums = new long[table];
        var deployable = new short[table];
        var schedule = order.ToArray();
        var random = new DeterministicRandom(_settings.Seed);
        Span<int> scores = stackalloc int[ReadingInventory.MaxCandidates];
        var inventory = _corpus.Inventory;
        long step = 0;

        for (var epoch = 1; epoch <= _settings.MaxEpochs; epoch++)
        {
            random.Shuffle(schedule.AsSpan());
            foreach (var index in schedule)
            {
                step++;
                var row = used[index];
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
}

/// <summary>The deployable integer weights as a weight source.</summary>
/// <param name="Weights">The weights by bucket.</param>
internal readonly record struct ShortWeights(short[] Weights) : HeteronymClassifier.IWeightSource
{
    /// <inheritdoc/>
    public int GetWeight(uint bucket) => Weights[bucket];
}
