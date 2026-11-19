using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.Piper.Tests.SynthesisSubsystem;

/// <summary>
///     An empty annotation list that holds the caller inside the engine until the test releases it, so a test can
///     make a synthesis run overlap a disposal deterministically, with no sleeps: the engine reads its annotations
///     while it holds its lease on the session, which makes this list a public-API-only gate inside a run.
/// </summary>
internal sealed class BlockingAnnotationList : IReadOnlyList<SegmentAnnotation>
{
    /// <summary>Set the first time the engine reads the list, i.e. once a run is inside the engine.</summary>
    private readonly ManualResetEventSlim _entered = new(false);

    /// <summary>Set by the test to let every blocked run continue.</summary>
    private readonly ManualResetEventSlim _released = new(false);

    /// <summary>Gets a handle that is set once a run is blocked inside the engine.</summary>
    public WaitHandle Entered => _entered.WaitHandle;

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            Gate();
            return 0;
        }
    }

    /// <inheritdoc/>
    public SegmentAnnotation this[int index]
    {
        get
        {
            Gate();
            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    /// <summary>Lets every run that is, or later gets, blocked on this list continue.</summary>
    public void Release() => _released.Set();

    /// <inheritdoc/>
    public IEnumerator<SegmentAnnotation> GetEnumerator()
    {
        Gate();
        return Enumerable.Empty<SegmentAnnotation>().GetEnumerator();
    }

    /// <inheritdoc/>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Signals that a run is inside the engine, then waits until the test releases it.</summary>
    private void Gate()
    {
        _entered.Set();
        if (!_released.Wait(TimeSpan.FromSeconds(60)))
        {
            throw new TimeoutException("The test never released the blocked run.");
        }
    }
}
