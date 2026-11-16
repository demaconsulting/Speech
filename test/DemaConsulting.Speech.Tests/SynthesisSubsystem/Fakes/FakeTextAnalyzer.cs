using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem.Fakes;

/// <summary>
///     Scripted <see cref="ITextAnalyzer"/> test double: records every text and token it is
///     given and returns whatever the supplied function computes (or <see cref="TextAnalysisResult.Empty"/>
///     when none was supplied), so a test can prove how often the analyzer ran and what it saw.
/// </summary>
internal sealed class FakeTextAnalyzer : ITextAnalyzer
{
    /// <summary>The scripted analysis function.</summary>
    private readonly Func<string, CancellationToken, TextAnalysisResult?> _analyze;

    /// <summary>Guards the recorded texts.</summary>
    private readonly object _lock = new();

    /// <summary>Every text received, in order.</summary>
    private readonly List<string> _texts = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="FakeTextAnalyzer"/> class.
    /// </summary>
    /// <param name="analyze">
    ///     The function computing the result for a text, or <see langword="null"/> for
    ///     <see cref="TextAnalysisResult.Empty"/>. It may return <see langword="null"/> or throw
    ///     to script those behaviors.
    /// </param>
    public FakeTextAnalyzer(Func<string, CancellationToken, TextAnalysisResult?>? analyze = null)
    {
        _analyze = analyze ?? ((_, _) => TextAnalysisResult.Empty);
    }

    /// <summary>Gets the number of <see cref="Analyze"/> calls received.</summary>
    public int CallCount
    {
        get
        {
            lock (_lock)
            {
                return _texts.Count;
            }
        }
    }

    /// <summary>Gets every text received, in arrival order.</summary>
    public IReadOnlyList<string> Texts
    {
        get
        {
            lock (_lock)
            {
                return [.. _texts];
            }
        }
    }

    /// <summary>
    ///     Creates an analyzer that annotates every occurrence of a word with the kind
    ///     <c>"reading"</c> and the word itself as the value.
    /// </summary>
    /// <param name="word">The word to annotate.</param>
    /// <param name="sentenceBreaks">Optional authoritative sentence breaks to return.</param>
    /// <returns>The scripted analyzer.</returns>
    public static FakeTextAnalyzer AnnotatingWord(string word, IReadOnlyList<int>? sentenceBreaks = null) =>
        new((text, _) =>
        {
            List<SegmentAnnotation> annotations = [];
            var index = text.IndexOf(word, StringComparison.Ordinal);
            while (index >= 0)
            {
                annotations.Add(new SegmentAnnotation(index, word.Length, "reading", word));
                index = text.IndexOf(word, index + word.Length, StringComparison.Ordinal);
            }

            return new TextAnalysisResult(annotations, sentenceBreaks);
        });

    /// <inheritdoc/>
    public TextAnalysisResult Analyze(string text, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            _texts.Add(text);
        }

        // The scripted function may legitimately return null to prove the session tolerates it.
        return _analyze(text, cancellationToken)!;
    }
}
