namespace DemaConsulting.Speech.Text.En.HeteronymSubsystem;

/// <summary>
///     The outcome of classifying one heteronym occurrence.
/// </summary>
/// <param name="ReadingIndex">The table index of the chosen reading in the <see cref="ReadingInventory"/>.</param>
/// <param name="Confidence">The calibrated confidence of the choice, from 0 to 1.</param>
internal readonly record struct HeteronymPrediction(int ReadingIndex, double Confidence);

/// <summary>
///     Chooses which reading of an inventory form a word occurrence has, by scoring only that form's
///     candidate readings with a <see cref="SparsePerceptronModel"/>, and reports a temperature-calibrated
///     confidence.
/// </summary>
/// <remarks>
///     <para>
///     <b>Candidate-restricted.</b> Only the candidates of the word's form are scored, so a reading that is
///     not a candidate can never win, whatever the weights of its buckets. A form with one candidate is not
///     ambiguous: its reading is returned with confidence 1 without touching the model.
///     </para>
///     <para>
///     <b>Decision.</b> A candidate's score is the sum, over the extracted features, of the integer weights of
///     the buckets that the feature hashes select through the candidate's label salt (conjoined features) and
///     role salt (shared features). The highest integer score wins; <b>an exact tie goes to the candidate
///     that comes first in the inventory's <c>forms.tsv</c> row order</b>, because the inventory has no
///     default reading (a trained model's bias feature normally breaks real ties). The decision uses integers
///     only, so it is identical on every platform.
///     </para>
///     <para>
///     <b>Confidence.</b> With scores <c>s</c> and the top score <c>m</c>, the confidence is
///     <c>1 / sum(exp((s - m) / (scale * temperature)))</c> over the candidates, clamped to [0, 1]. Only this
///     reported number uses <see cref="Math.Exp(double)"/>; it can differ in the last bits between platforms,
///     which rounding to three decimals hides except exactly on a rounding or threshold boundary.
///     </para>
///     <para>
///     Instances are immutable and thread-safe; <see cref="Classify"/> allocates nothing (its feature buffers
///     are on the stack).
///     </para>
/// </remarks>
internal sealed class HeteronymClassifier
{
    /// <summary>The model whose weights are scored.</summary>
    private readonly SparsePerceptronModel _model;

    /// <summary>The optional observer called after each model-scored classification, for tests.</summary>
    private readonly Action<int>? _classified;

    /// <summary>The number of model-scored classifications so far.</summary>
    private int _count;

    /// <summary>
    ///     Initializes a new instance, checking that the model fits this build and the inventory.
    /// </summary>
    /// <param name="inventory">The reading inventory the candidates come from. Must not be null.</param>
    /// <param name="model">The model. Must not be null.</param>
    /// <param name="classified">
    ///     An optional observer called with the running number of classifications right after each
    ///     model-scored one; tests use it to cancel at an exact token. Production passes null.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="inventory"/> or <paramref name="model"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     The model's feature-set version is not the one this build extracts, or the model was trained for a newer
    ///     inventory revision than <paramref name="inventory"/> has.
    /// </exception>
    internal HeteronymClassifier(ReadingInventory inventory, SparsePerceptronModel model, Action<int>? classified = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(model);

        // A model binds its weights to reading ids through salts, so an older model works with a newer
        // inventory, but a model of a newer inventory may name readings this build does not know.
        if (model.FeatureSetVersion != HeteronymFeatureExtractor.FeatureSetVersion)
        {
            throw new InvalidDataException("The heteronym model was trained with a different feature set than this build extracts.");
        }

        if (model.InventoryVersion > inventory.Version)
        {
            throw new InvalidDataException("The heteronym model was trained for a newer reading inventory than this build has.");
        }

        Inventory = inventory;
        _model = model;
        _classified = classified;
    }

    /// <summary>Gets the inventory whose candidates this classifier chooses among.</summary>
    internal ReadingInventory Inventory { get; }

    /// <summary>
    ///     Classifies one occurrence of an inventory form.
    /// </summary>
    /// <param name="sentence">The tokens of the occurrence's sentence in text order, punctuation included.</param>
    /// <param name="target">The index of the word within <paramref name="sentence"/>.</param>
    /// <param name="formIndex">The inventory form of the word, from <see cref="ReadingInventory.TryFindForm"/>.</param>
    /// <returns>The chosen reading and its confidence.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="target"/> is negative or not below the length of <paramref name="sentence"/>, whatever the form's candidate count.</exception>
    /// <remarks>Thread-safe and allocation-free.</remarks>
    internal HeteronymPrediction Classify(ReadOnlySpan<ContextToken> sentence, int target, int formIndex)
    {
        // Validate the target before anything else, for every form: the single-candidate fast path below would
        // otherwise hide a caller defect that only an ambiguous form reveals. The unsigned comparison rejects a
        // negative index and an index at or beyond the length in one overflow-safe test.
        if ((uint)target >= (uint)sentence.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(target), "The target is not a token of the sentence.");
        }

        var candidates = Inventory.GetCandidates(formIndex);
        if (candidates.Length == 1)
        {
            return new HeteronymPrediction(candidates[0], 1.0);
        }

        // Extract the hashed features once; they are shared by every candidate and only the salts differ.
        Span<uint> conjoined = stackalloc uint[HeteronymFeatureExtractor.MaxConjoined];
        Span<uint> shared = stackalloc uint[HeteronymFeatureExtractor.MaxShared];
        HeteronymFeatureExtractor.Extract(sentence, target, conjoined, shared, out var conjoinedCount, out var sharedCount);

        // Score every candidate with integers; a strictly greater score wins, so a tie keeps the earlier candidate.
        var mask = _model.Mask;
        var weights = new ModelWeights(_model);
        Span<int> scores = stackalloc int[candidates.Length];
        var best = 0;
        for (var c = 0; c < candidates.Length; c++)
        {
            var score = ScoreCandidate(weights, conjoined[..conjoinedCount], shared[..sharedCount], Inventory.LabelSalt(candidates[c]), Inventory.RoleSalt(candidates[c]), mask);
            scores[c] = score;
            if (score > scores[best])
            {
                best = c;
            }
        }

        // Calibrated confidence: the softmax probability of the top candidate at the model's temperature.
        var divisor = (double)_model.Scale * _model.Temperature;
        var sum = 0.0;
        for (var c = 0; c < scores.Length; c++)
        {
            sum += Math.Exp((scores[c] - scores[best]) / divisor);
        }

        var confidence = Math.Clamp(1.0 / sum, 0.0, 1.0);
        var prediction = new HeteronymPrediction(candidates[best], confidence);
        if (_classified is not null)
        {
            _classified(Interlocked.Increment(ref _count));
        }

        return prediction;
    }

    /// <summary>
    ///     Computes the integer score of one candidate reading: the sum of the weights of the buckets its
    ///     features select.
    /// </summary>
    /// <typeparam name="TWeights">The weight source; a struct, so the call is specialized and allocation-free.</typeparam>
    /// <param name="weights">The weights of the buckets.</param>
    /// <param name="conjoined">The conjoined feature hashes, scored through <paramref name="labelSalt"/>.</param>
    /// <param name="shared">The shared feature hashes, scored through <paramref name="roleSalt"/>.</param>
    /// <param name="labelSalt">The candidate's label salt.</param>
    /// <param name="roleSalt">The candidate's role salt.</param>
    /// <param name="mask">The bucket mask of the weights.</param>
    /// <returns>The score; a duplicated feature hash counts once per occurrence.</returns>
    /// <remarks>
    ///     The single definition of a candidate's score. The classifier scores a loaded model with it and the offline
    ///     trainer scores its training weights with it, so training and run time cannot disagree.
    /// </remarks>
    internal static int ScoreCandidate<TWeights>(
        TWeights weights,
        ReadOnlySpan<uint> conjoined,
        ReadOnlySpan<uint> shared,
        uint labelSalt,
        uint roleSalt,
        uint mask)
        where TWeights : struct, IWeightSource
    {
        var score = 0;
        foreach (var feature in conjoined)
        {
            score += weights.GetWeight(FeatureHashing.Bucket(feature, labelSalt, mask));
        }

        foreach (var feature in shared)
        {
            score += weights.GetWeight(FeatureHashing.Bucket(feature, roleSalt, mask));
        }

        return score;
    }

    /// <summary>
    ///     A source of integer bucket weights that <see cref="ScoreCandidate{TWeights}"/> can score.
    /// </summary>
    internal interface IWeightSource
    {
        /// <summary>
        ///     Gets the weight of a bucket.
        /// </summary>
        /// <param name="bucket">The bucket, already masked.</param>
        /// <returns>The integer weight; 0 for a bucket without one.</returns>
        int GetWeight(uint bucket);
    }

    /// <summary>
    ///     The weights of a loaded <see cref="SparsePerceptronModel"/> as a weight source.
    /// </summary>
    /// <param name="Model">The model.</param>
    private readonly record struct ModelWeights(SparsePerceptronModel Model) : IWeightSource
    {
        /// <inheritdoc/>
        public int GetWeight(uint bucket) => Model.GetWeight(bucket);
    }
}
