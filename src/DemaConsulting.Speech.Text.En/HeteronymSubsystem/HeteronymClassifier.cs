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
///     <b>Adjective cue.</b> Grammar that marks the word as an adjective is added to the model's evidence as a fixed
///     prior: when the tagger tags the word <c>ADJ</c> and a degree adverb stands right before it (<i>very minute</i>,
///     <i>so close</i>, <i>quite content</i>) or it is coordinated with another adjective (<i>minute and clear</i>,
///     <i>grey and minute</i>), every candidate whose part-of-speech hint includes <c>ADJ</c> gets a bonus worth odds of
///     <see cref="AdjectiveCueLogOdds"/> (about 9 to 1) on the model's own calibrated scale. The model's sparse features
///     see these constructions too rarely to weigh them (the flagged <i>very minute and clear</i> got the time reading at
///     0.59), while a degree adverb before a word that the tagger reads as an adjective leaves almost no room for a noun
///     or a verb. It is a prior, not a rule: a strong contrary verdict of the model still wins. The bonus is an integer, so
///     the decision stays integer-only; the training never sees it, so the model and its training record are unchanged.
///     </para>
///     <para>
///     <b>Noun-form cue.</b> A reading can be a noun of its lemma and still never a noun of a given form: <c>wind.coil</c>
///     is a noun as <i>a wind</i> (a bend), but <i>wound</i> is only its past tense. When the tagger tags such a form
///     <c>NOUN</c> and exactly one candidate can be a noun of it (<see cref="ReadingInventory.CanBeNoun"/>), that
///     candidate gets a prior of <see cref="NounFormCueLogOdds"/> (the flagged <i>a knife wound</i> got the coil reading
///     at 0.55). The prior is small on purpose: the tagger also tags a verb <c>NOUN</c> now and then (<i>the tape wound
///     onto the spool</i>), and a strong prior or a hard constraint lost that development row; a cue for every noun or
///     verb tag over all heteronyms lost 1 to 2 points of development accuracy and 11 rows of the external set, so the
///     cue is limited to the forms that rule a reading out by themselves.
///     </para>
///     <para>
///     <b>Past-time cue.</b> A sentence with <i>yesterday</i>, <i>ago</i>, <i>previously</i> or <i>last</i> before a word of time
///     gives a past-tense candidate a prior of <see cref="PastTimeCueLogOdds"/>, unless the word follows an auxiliary or a
///     particle (<i>did not read</i>, <i>to read</i>), which take the base form whatever the time.
///     </para>
///     <para>
///     <b>Context cues.</b> A short table of cue words per reading marks a use the training data seldom shows: a word of
///     the body or of a material within <see cref="ContextCueWindow"/> words of <i>tear</i> (<i>a deep muscle tear</i>, <i>a
///     tear in the fabric</i>, <i>wear and tear</i>) gives the rip reading a prior of <see cref="ContextCueLogOdds"/>. The
///     training data has one noun use of that reading; adding rows was tried, but retraining the single-epoch model
///     re-rolled other readings, so the evidence is a prior instead.
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
    /// <summary>The prior log-odds the adjective cue adds to an adjective reading (ln 9: odds of about 9 to 1).</summary>
    internal const double AdjectiveCueLogOdds = 2.2;

    /// <summary>The prior log-odds the noun-form cue adds to the one reading that can be a noun (1: odds of about 2.7 to 1).</summary>
    internal const double NounFormCueLogOdds = 1.0;

    /// <summary>The tag code of <c>ADJ</c>.</summary>
    private static readonly byte AdjectiveTag = HeteronymContext.UposCode("ADJ");

    /// <summary>The tag code of <c>NOUN</c>.</summary>
    private static readonly byte NounTag = HeteronymContext.UposCode("NOUN");

    /// <summary>The tag code of <c>AUX</c>.</summary>
    private static readonly byte AuxiliaryTag = HeteronymContext.UposCode("AUX");

    /// <summary>The tag code of <c>PART</c>.</summary>
    private static readonly byte ParticleTag = HeteronymContext.UposCode("PART");

    /// <summary>The prior log-odds the past-time cue adds to a past-tense reading (ln 9: odds of about 9 to 1).</summary>
    internal const double PastTimeCueLogOdds = 2.2;

    /// <summary>The hashes of the words that place a clause in the past on their own (<i>yesterday</i>, <i>ago</i>).</summary>
    private static readonly HashSet<uint> PastTimeWords = [.. new[] { "yesterday", "ago", "previously" }.Select(word => FeatureHashing.Fnv1a(word))];

    /// <summary>The prior log-odds a context cue adds to its reading (ln 9: odds of about 9 to 1).</summary>
    internal const double ContextCueLogOdds = 2.2;

    /// <summary>The number of words on each side of a word that a context cue is looked for in.</summary>
    internal const int ContextCueWindow = 3;

    /// <summary>
    ///     The words that, near a word, mark one of its readings where the training data has too few examples of that use
    ///     for the model to have learned it: a rip of the body or of a material (<i>a deep muscle tear</i>, <i>a tear in the
    ///     fabric</i>, <i>wear and tear</i>), whose noun use the training data has once, against the verb uses of the rip
    ///     reading and the noun uses of the drop from the eye.
    /// </summary>
    private static readonly (string ReadingId, HashSet<uint> Words)[] ContextCues =
    [
        ("tear.rip", [.. new[]
        {
            "muscle", "muscles", "ligament", "ligaments", "tendon", "tendons", "hamstring", "meniscus", "cartilage", "rotator",
            "cuff", "acl", "labrum", "groin", "fabric", "cloth", "canvas", "sail", "seam", "sleeve", "lining", "denim", "wear",
        }.Select(word => FeatureHashing.Fnv1a(word))]),
    ];

    /// <summary>The hash of <i>last</i>, which places a clause in the past before a word of time (<i>last night</i>).</summary>
    private static readonly uint LastHash = FeatureHashing.Fnv1a("last");

    /// <summary>The hashes of the words of time after <i>last</i> (<i>last night</i>, <i>last week</i>).</summary>
    private static readonly HashSet<uint> TimeNouns =
    [
        .. new[] { "night", "evening", "morning", "week", "weekend", "month", "year", "summer", "winter", "spring", "autumn", "fall", "time" }
            .Select(word => FeatureHashing.Fnv1a(word)),
    ];


    /// <summary>The tag code of <c>ADV</c>.</summary>
    private static readonly byte AdverbTag = HeteronymContext.UposCode("ADV");

    /// <summary>The tag code of <c>CCONJ</c>.</summary>
    private static readonly byte ConjunctionTag = HeteronymContext.UposCode("CCONJ");

    /// <summary>The hashes of the degree adverbs that grade an adjective right after them (<i>very</i>, <i>so</i>, <i>too</i> ...).</summary>
    private static readonly HashSet<uint> DegreeAdverbs =
    [
        .. new[]
        {
            "very", "so", "too", "quite", "extremely", "rather", "most", "more", "less", "really", "incredibly", "exceedingly",
            "remarkably", "fairly", "somewhat", "highly", "utterly", "terribly", "awfully", "perfectly", "completely", "entirely",
            "absolutely", "particularly", "especially", "unusually", "surprisingly", "equally", "pretty",
        }.Select(word => FeatureHashing.Fnv1a(word)),
    ];

    /// <summary>The model whose weights are scored.</summary>
    private readonly SparsePerceptronModel _model;

    /// <summary>The integer score the adjective cue adds to an adjective reading: <see cref="AdjectiveCueLogOdds"/> on the model's scale.</summary>
    private readonly int _adjectiveCueBonus;

    /// <summary>The integer score the noun-form cue adds: <see cref="NounFormCueLogOdds"/> on the model's scale.</summary>
    private readonly int _nounFormCueBonus;

    /// <summary>The integer score the past-time cue adds: <see cref="PastTimeCueLogOdds"/> on the model's scale.</summary>
    private readonly int _pastTimeCueBonus;

    /// <summary>The integer score a context cue adds: <see cref="ContextCueLogOdds"/> on the model's scale.</summary>
    private readonly int _contextCueBonus;

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
        _adjectiveCueBonus = (int)Math.Round((double)model.Scale * model.Temperature * AdjectiveCueLogOdds, MidpointRounding.AwayFromZero);
        _nounFormCueBonus = (int)Math.Round((double)model.Scale * model.Temperature * NounFormCueLogOdds, MidpointRounding.AwayFromZero);
        _pastTimeCueBonus = (int)Math.Round((double)model.Scale * model.Temperature * PastTimeCueLogOdds, MidpointRounding.AwayFromZero);
        _contextCueBonus = (int)Math.Round((double)model.Scale * model.Temperature * ContextCueLogOdds, MidpointRounding.AwayFromZero);
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
        // The adjective cue adds its fixed prior to the adjective readings.
        var mask = _model.Mask;
        var weights = new ModelWeights(_model);
        var cue = HasAdjectiveCue(sentence, target);
        var noun = OnlyNounOfTheForm(sentence[target].Upos, formIndex, candidates);
        var past = HasPastTimeCue(sentence, target);
        Span<int> scores = stackalloc int[candidates.Length];
        var best = 0;
        for (var c = 0; c < candidates.Length; c++)
        {
            var score = ScoreCandidate(weights, conjoined[..conjoinedCount], shared[..sharedCount], Inventory.LabelSalt(candidates[c]), Inventory.RoleSalt(candidates[c]), mask);
            if (cue && Inventory.IsAdjective(candidates[c]))
            {
                score += _adjectiveCueBonus;
            }

            if (c == noun)
            {
                score += _nounFormCueBonus;
            }

            if (past && Inventory.IsPastTense(candidates[c]))
            {
                score += _pastTimeCueBonus;
            }

            if (HasContextCue(sentence, target, Inventory.ReadingIdAt(candidates[c])))
            {
                score += _contextCueBonus;
            }

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
    ///     Tells whether the grammar around a word marks it as an adjective.
    /// </summary>
    /// <param name="sentence">The tokens of the sentence.</param>
    /// <param name="target">The index of the word.</param>
    /// <returns>
    ///     <see langword="true"/> when the tagger tags the word <c>ADJ</c> and either a degree adverb tagged <c>ADV</c>
    ///     stands right before it, or a coordinating conjunction joins it to a word tagged <c>ADJ</c> (directly, or after
    ///     one degree adverb on the right: <i>minute and very clear</i>).
    /// </returns>
    /// <remarks>Both signals must agree, the tag and the construction, so a degree word in another role (<i>the very
    /// minute he came</i>, where <i>very</i> is an adjective and <i>minute</i> a noun) does not trigger the cue.</remarks>
    internal static bool HasAdjectiveCue(ReadOnlySpan<ContextToken> sentence, int target)
    {
        if (sentence[target].Upos != AdjectiveTag)
        {
            return false;
        }

        // A degree adverb right before the word
        if (IsTagged(sentence, target - 1, AdverbTag) && DegreeAdverbs.Contains(sentence[target - 1].LowerHash))
        {
            return true;
        }

        // Coordinated with an adjective on the left (grey and minute) or on the right (minute and clear, minute and very clear)
        if (IsTagged(sentence, target - 1, ConjunctionTag) && IsTagged(sentence, target - 2, AdjectiveTag))
        {
            return true;
        }

        if (!IsTagged(sentence, target + 1, ConjunctionTag))
        {
            return false;
        }

        var next = target + 2;
        if (IsTagged(sentence, next, AdverbTag) && DegreeAdverbs.Contains(sentence[next].LowerHash))
        {
            next++;
        }

        return IsTagged(sentence, next, AdjectiveTag);
    }

    /// <summary>
    ///     Finds the one candidate that can be a noun of this form, when the word is tagged as a noun and the form itself
    ///     rules out a reading that is a noun for its lemma.
    /// </summary>
    /// <param name="tag">The tag code of the word.</param>
    /// <param name="formIndex">The form of the word.</param>
    /// <param name="candidates">The candidate readings.</param>
    /// <returns>
    ///     The index in <paramref name="candidates"/> of the only reading that can be a noun of the form (see
    ///     <see cref="ReadingInventory.CanBeNoun"/>) when the word is tagged <c>NOUN</c> and another candidate's reading is a
    ///     noun only in other forms of its lemma (<c>wound</c>: <c>wind.coil</c> is a noun as <i>a wind</i>, a bend, but
    ///     <i>wound</i> is its past tense); -1 otherwise.
    /// </returns>
    private int OnlyNounOfTheForm(byte tag, int formIndex, ReadOnlySpan<int> candidates)
    {
        if (tag != NounTag)
        {
            return -1;
        }

        var found = -1;
        var ruledOut = false;
        for (var c = 0; c < candidates.Length; c++)
        {
            if (!Inventory.CanBeNoun(formIndex, candidates[c]))
            {
                ruledOut |= Inventory.IsNounOfItsLemma(candidates[c]);
                continue;
            }

            if (found >= 0)
            {
                return -1;
            }

            found = c;
        }

        return ruledOut ? found : -1;
    }
    /// <summary>
    ///     Tells whether a cue word of a reading stands near the word.
    /// </summary>
    /// <param name="sentence">The tokens of the sentence.</param>
    /// <param name="target">The index of the word.</param>
    /// <param name="readingId">The candidate reading.</param>
    /// <returns><see langword="true"/> when the reading has cue words and one is within <see cref="ContextCueWindow"/> words of the target.</returns>
    internal static bool HasContextCue(ReadOnlySpan<ContextToken> sentence, int target, string readingId)
    {
        foreach (var (cueReading, words) in ContextCues)
        {
            if (!string.Equals(cueReading, readingId, StringComparison.Ordinal))
            {
                continue;
            }

            for (var i = Math.Max(0, target - ContextCueWindow); i <= Math.Min(sentence.Length - 1, target + ContextCueWindow); i++)
            {
                if (i != target && words.Contains(sentence[i].LowerHash))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Tells whether the sentence places a word that may be a past tense in the past.
    /// </summary>
    /// <param name="sentence">The tokens of the sentence.</param>
    /// <param name="target">The index of the word.</param>
    /// <returns>
    ///     <see langword="true"/> when the sentence has <i>yesterday</i>, <i>ago</i>, <i>previously</i> or <i>last</i> before a
    ///     word of time (<i>last night</i>, <i>last week</i>), and the word does not follow an auxiliary or a particle
    ///     (<i>did not read</i>, <i>wanted to read</i>, <i>will read</i>, which take the base form whatever the time).
    /// </returns>
    internal static bool HasPastTimeCue(ReadOnlySpan<ContextToken> sentence, int target)
    {
        // The base form after an auxiliary or a particle, one adverb allowed between (did not read, to quickly read)
        var before = target - 1;
        if (IsTagged(sentence, before, AdverbTag) || (IsTagged(sentence, before, ParticleTag) && IsTagged(sentence, before - 1, AuxiliaryTag)))
        {
            before--;
        }

        if (IsTagged(sentence, before, AuxiliaryTag) || IsTagged(sentence, before, ParticleTag))
        {
            return false;
        }

        for (var i = 0; i < sentence.Length; i++)
        {
            var hash = sentence[i].LowerHash;
            if (PastTimeWords.Contains(hash) || (hash == LastHash && i + 1 < sentence.Length && TimeNouns.Contains(sentence[i + 1].LowerHash)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tells whether the token at an index exists and has a tag.
    /// </summary>
    /// <param name="sentence">The tokens of the sentence.</param>
    /// <param name="index">The index, which may be outside the sentence.</param>
    /// <param name="tag">The tag code.</param>
    /// <returns><see langword="true"/> when the index is inside the sentence and the token there has the tag.</returns>
    private static bool IsTagged(ReadOnlySpan<ContextToken> sentence, int index, byte tag) =>
        (uint)index < (uint)sentence.Length && sentence[index].Upos == tag;

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
