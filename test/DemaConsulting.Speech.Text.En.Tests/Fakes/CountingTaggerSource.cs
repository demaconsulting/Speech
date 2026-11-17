using DemaConsulting.PartOfSpeech;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.Fakes;

/// <summary>
///     Hands out <see cref="PartOfSpeechAdapter"/>s whose tagger factory still returns the real
///     shared tagger without disturbing the process-wide <c>Tagger.Default</c> that other tests
///     share, and lets a test run an action (for example a cancellation) the moment the tagger is
///     requested or make the first requests fail, so it can reach those points of a request
///     deterministically. (The class name is historical: it once counted the requests, which no
///     test asserts any more.)
/// </summary>
internal sealed class CountingTaggerSource
{
    /// <summary>The number of times the factory has been invoked (it decides which invocations fail).</summary>
    private int _count;

    /// <summary>An optional action run each time the factory is invoked.</summary>
    private readonly Action? _onCreate;

    /// <summary>How many leading invocations throw before the factory starts succeeding.</summary>
    private readonly int _failures;

    /// <summary>
    ///     Initializes a new instance.
    /// </summary>
    /// <param name="onCreate">An optional action to run when the tagger is first requested.</param>
    /// <param name="failures">
    ///     How many of the first invocations throw <see cref="InvalidOperationException"/> instead
    ///     of returning the tagger (they are still counted); zero means the factory always succeeds.
    /// </param>
    public CountingTaggerSource(Action? onCreate = null, int failures = 0)
    {
        _onCreate = onCreate;
        _failures = failures;
    }

    /// <summary>Gets a factory that runs the configured action and failures, and otherwise returns the shared tagger.</summary>
    public Func<Tagger> Factory => () =>
    {
        var invocation = Interlocked.Increment(ref _count);
        _onCreate?.Invoke();
        if (invocation <= _failures)
        {
            throw new InvalidOperationException("Simulated tagger initialization failure.");
        }

        return Tagger.Default;
    };

    /// <summary>
    ///     Creates an adapter over this source's counting factory.
    /// </summary>
    /// <param name="sentenceTagged">An optional observer called after each sentence is tagged.</param>
    /// <returns>A new adapter whose tagger requests go through the factory.</returns>
    public PartOfSpeechAdapter CreateAdapter(Action<int>? sentenceTagged = null) => new(Factory, sentenceTagged);
}
