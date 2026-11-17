using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.Fakes;

/// <summary>
///     One labeled training sentence for <see cref="HeteronymFixtureModel"/>: which occurrence of a word
///     in a sentence has which reading.
/// </summary>
/// <param name="Sentence">The sentence (one tokenizer sentence).</param>
/// <param name="Word">The heteronym word, compared ignoring case with the sentence's tokens.</param>
/// <param name="Occurrence">The zero-based occurrence of <paramref name="Word"/> that is labeled.</param>
/// <param name="ReadingId">The gold reading id.</param>
internal sealed record FixtureExample(string Sentence, string Word, int Occurrence, string ReadingId);

/// <summary>
///     Builds hand-made heteronym models for tests by running the real pipeline (tagger adapter, context
///     construction, feature extraction) on labeled example sentences and giving every feature of the
///     gold reading a positive weight, then writing the result through the real model writer. No binary
///     fixture is committed: the bytes are produced on demand, so they can never go stale against the
///     feature set.
/// </summary>
internal static class HeteronymFixtureModel
{
    /// <summary>The fixed-point scale of the fixture models.</summary>
    internal const float Scale = 64f;

    /// <summary>The weight given to each feature of a gold reading (one unit of real score).</summary>
    private const int FeatureWeight = 64;

    /// <summary>
    ///     Gets the default examples: the noun and verb readings of <c>record</c>, one sentence each way.
    /// </summary>
    internal static IReadOnlyList<FixtureExample> RecordExamples { get; } =
    [
        new("I want to record a record.", "record", 0, "record.verb"),
        new("I want to record a record.", "record", 1, "record.noun"),
        new("They will record the meeting.", "record", 0, "record.verb"),
        new("This record is old.", "record", 0, "record.noun"),
    ];

    /// <summary>
    ///     Builds the bytes of a model trained on examples.
    /// </summary>
    /// <param name="examples">The labeled examples.</param>
    /// <param name="hashBits">The number of hash bits.</param>
    /// <param name="temperature">The calibration temperature.</param>
    /// <param name="inventoryVersion">The inventory revision to record.</param>
    /// <param name="compress">Whether to store the payload as Deflate.</param>
    /// <returns>The model file bytes.</returns>
    internal static byte[] Build(IEnumerable<FixtureExample> examples, int hashBits = 18, float temperature = 20f, int inventoryVersion = ReadingInventory.CurrentVersion, bool compress = true)
    {
        var inventory = ReadingInventory.Default;
        var mask = (uint)((1L << hashBits) - 1);
        var weights = new Dictionary<uint, int>();
        Span<uint> conjoined = stackalloc uint[HeteronymFeatureExtractor.MaxConjoined];
        Span<uint> shared = stackalloc uint[HeteronymFeatureExtractor.MaxShared];
        foreach (var example in examples)
        {
            // Describe the sentence exactly as the analyzer does and find the labeled occurrence.
            var sentences = PartOfSpeechAdapter.Shared.Analyze(example.Sentence, tag: true, TestContext.Current.CancellationToken);
            var seen = 0;
            foreach (var sentence in sentences)
            {
                var context = sentence
                    .Select(token => HeteronymContext.Create(example.Sentence, token.Start, token.Length, token.Tag, token.Confidence))
                    .ToArray();
                for (var i = 0; i < sentence.Count; i++)
                {
                    var text = example.Sentence.Substring(sentence[i].Start, sentence[i].Length);
                    if (!string.Equals(text, example.Word, StringComparison.OrdinalIgnoreCase) || seen++ != example.Occurrence)
                    {
                        continue;
                    }

                    // Reward every feature of the gold reading, through its label and role salts.
                    Assert.True(inventory.TryGetReadingIndex(example.ReadingId, out var readingIndex));
                    HeteronymFeatureExtractor.Extract(context, i, conjoined, shared, out var conjoinedCount, out var sharedCount);
                    for (var c = 0; c < conjoinedCount; c++)
                    {
                        Add(weights, FeatureHashing.Mix(conjoined[c], inventory.LabelSalt(readingIndex)) & mask);
                    }

                    for (var s = 0; s < sharedCount; s++)
                    {
                        Add(weights, FeatureHashing.Mix(shared[s], inventory.RoleSalt(readingIndex)) & mask);
                    }
                }
            }

            Assert.True(seen > example.Occurrence, "The labeled occurrence was not found in the sentence.");
        }

        return SparsePerceptronModelWriter.Compile(
            hashBits,
            inventoryVersion,
            temperature,
            Scale,
            weights.Select(pair => (pair.Key, (short)Math.Clamp(pair.Value, short.MinValue + 1, short.MaxValue))),
            compress);
    }

    /// <summary>
    ///     Creates a provider whose model is built from examples and whose opens are counted.
    /// </summary>
    /// <param name="source">The counting source that serves the bytes.</param>
    /// <param name="classificationObserver">An optional observer called with the running classification count.</param>
    /// <returns>A provider over the source.</returns>
    internal static HeteronymModelProvider CreateProvider(CountingModelSource source, Action<int>? classificationObserver = null) =>
        new(source.Open, loadObserver: null, classificationObserver: classificationObserver);

    /// <summary>
    ///     Describes the first sentence of a text exactly as the analyzer does: the real tokenizer, the real tagger and the real context construction.
    /// </summary>
    /// <param name="sentence">The text of one sentence.</param>
    /// <returns>The context tokens and the token texts, in text order.</returns>
    internal static (ContextToken[] Context, string[] Words) Describe(string sentence)
    {
        var tokens = PartOfSpeechAdapter.Shared.Analyze(sentence, tag: true, TestContext.Current.CancellationToken)[0];
        var context = tokens
            .Select(token => HeteronymContext.Create(sentence, token.Start, token.Length, token.Tag, token.Confidence))
            .ToArray();
        return (context, tokens.Select(token => sentence.Substring(token.Start, token.Length)).ToArray());
    }

    /// <summary>
    ///     Adds one feature weight to the accumulator.
    /// </summary>
    /// <param name="weights">The accumulator.</param>
    /// <param name="bucket">The bucket.</param>
    private static void Add(Dictionary<uint, int> weights, uint bucket)
    {
        weights.TryGetValue(bucket, out var current);
        weights[bucket] = current + FeatureWeight;
    }
}
