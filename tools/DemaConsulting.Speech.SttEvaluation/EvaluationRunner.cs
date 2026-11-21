using System.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.SttEvaluation;

/// <summary>
///     One recording of a scenario: the audio to stream and what a perfect recognizer would report.
/// </summary>
/// <param name="Id">The identifier of the recording in reports.</param>
/// <param name="Reference">The reference transcript.</param>
/// <param name="Samples">The mono samples at the model's sample rate.</param>
/// <param name="HasPunctuatedReference">Whether the reference is punctuated and cased, so a verbatim word error rate is meaningful.</param>
internal sealed record ScenarioInput(string Id, string Reference, float[] Samples, bool HasPunctuatedReference);

/// <summary>
///     The measurement of one recording: its transcript and alignment against the reference.
/// </summary>
/// <param name="Input">The recording that was streamed.</param>
/// <param name="Transcript">What the recognizer reported.</param>
/// <param name="Alignment">The normalized alignment of the transcript against the reference.</param>
/// <param name="VerbatimAlignment">The verbatim (cased, punctuated) alignment, or <see langword="null"/> when the reference is not punctuated.</param>
internal sealed record ItemResult(
    ScenarioInput Input,
    SttTranscript Transcript,
    WordAlignment Alignment,
    WordAlignment? VerbatimAlignment);

/// <summary>
///     The measurement of one scenario: its per-recording results and their corpus totals.
/// </summary>
/// <param name="Scenario">The scenario name (one of <see cref="EvaluationScenarios.All"/>).</param>
/// <param name="ExpectedSegments">The number of segments a correct endpointing would produce, or <see langword="null"/> when not defined for the scenario.</param>
/// <param name="Items">The per-recording results, in order.</param>
internal sealed record ScenarioResult(string Scenario, int? ExpectedSegments, IReadOnlyList<ItemResult> Items)
{
    /// <summary>Gets the corpus alignment: the sum of the per-recording alignments.</summary>
    public WordAlignment Alignment => Items.Aggregate(WordAlignment.Empty, (sum, item) => sum.Add(item.Alignment));

    /// <summary>Gets the corpus verbatim alignment, or <see langword="null"/> when no recording has a punctuated reference.</summary>
    public WordAlignment? VerbatimAlignment => Items.Any(item => item.VerbatimAlignment is not null)
        ? Items.Where(item => item.VerbatimAlignment is not null)
            .Aggregate(WordAlignment.Empty, (sum, item) => sum.Add(item.VerbatimAlignment!))
        : null;

    /// <summary>Gets the total number of committed segments.</summary>
    public int Segments => Items.Sum(item => item.Transcript.SegmentCount);

    /// <summary>Gets the total duration of the recordings, in seconds.</summary>
    public double SpeechSeconds => Items.Sum(item => item.Transcript.SpeechSeconds);

    /// <summary>Gets the total wall-clock processing time, in seconds.</summary>
    public double ProcessingSeconds => Items.Sum(item => item.Transcript.ProcessingTime.TotalSeconds);

    /// <summary>Gets the real-time factor of the scenario: processing time over fed audio time.</summary>
    public double RealTimeFactor
    {
        get
        {
            var fedSeconds = Items.Sum(item => (double)item.Transcript.FedSamples / item.Transcript.SampleRate);
            return fedSeconds == 0 ? 0.0 : ProcessingSeconds / fedSeconds;
        }
    }

    /// <summary>Gets the mean audio-clock time to the first result with text, in seconds, or <see langword="null"/> when none had text.</summary>
    public double? MeanFirstResultSeconds => Mean(Items.Select(item => item.Transcript.FirstResultSeconds));

    /// <summary>Gets the mean audio-clock lag from the end of a recording to its last committed segment, in seconds, or <see langword="null"/>.</summary>
    public double? MeanFinalLagSeconds => Mean(Items.Select(item => item.Transcript.FinalLagSeconds));

    /// <summary>Returns the mean of the present values, or <see langword="null"/> when none is present.</summary>
    /// <param name="values">The values, some possibly absent.</param>
    /// <returns>The mean, or <see langword="null"/>.</returns>
    private static double? Mean(IEnumerable<double?> values)
    {
        var present = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return present.Count == 0 ? null : present.Average();
    }
}

/// <summary>
///     The measurement of one model over the requested scenarios.
/// </summary>
/// <param name="ModelId">The model id.</param>
/// <param name="LoadSeconds">The wall-clock time to create the backend, in seconds.</param>
/// <param name="Scenarios">The scenario results, in run order.</param>
internal sealed record ModelEvaluation(string ModelId, double LoadSeconds, IReadOnlyList<ScenarioResult> Scenarios);

/// <summary>
///     The names and shapes of the evaluation scenarios.
/// </summary>
/// <remarks>
///     Each scenario exercises a behavior a user notices: plain accuracy (<see cref="LibriSpeech"/>,
///     <see cref="Poem"/>), finalizing each utterance of paused speech (<see cref="Paused"/>),
///     splitting long continuous speech (<see cref="Continuous"/>), and recovering quiet speech
///     (<see cref="Quiet"/>). All are built from the verified data set, so nothing but the
///     committed recordings is ever measured.
/// </remarks>
internal static class EvaluationScenarios
{
    /// <summary>Every LibriSpeech subset utterance on its own (the backend is reset between them). Gated.</summary>
    public const string LibriSpeech = "librispeech";

    /// <summary>The whole "Crossing the Bar" recitation as one stream. Gated.</summary>
    public const string Poem = "poem";

    /// <summary>The first <see cref="PausedUtteranceCount"/> LibriSpeech utterances separated by <see cref="PauseSeconds"/> of silence, as one stream.</summary>
    public const string Paused = "paused";

    /// <summary>LibriSpeech utterances joined without gaps until at least <see cref="ContinuousMinimumSeconds"/>, as one stream.</summary>
    public const string Continuous = "continuous";

    /// <summary>Every LibriSpeech subset utterance attenuated by <see cref="QuietAttenuationDecibels"/>.</summary>
    public const string Quiet = "quiet";

    /// <summary>The number of utterances of the paused scenario.</summary>
    public const int PausedUtteranceCount = 4;

    /// <summary>The digital silence between the utterances of the paused scenario, in seconds (long enough that each utterance should finalize).</summary>
    public const double PauseSeconds = 1.5;

    /// <summary>The minimum length of the continuous scenario, in seconds (twice the 20 s maximum utterance length of the Sherpa endpoint rules).</summary>
    public const double ContinuousMinimumSeconds = 45.0;

    /// <summary>The attenuation of the quiet scenario, in decibels.</summary>
    public const double QuietAttenuationDecibels = 20.0;

    /// <summary>Gets every scenario name, in run order.</summary>
    public static IReadOnlyList<string> All { get; } = [LibriSpeech, Poem, Paused, Continuous, Quiet];

    /// <summary>Gets the scenarios <c>--check</c> gates.</summary>
    public static IReadOnlyList<string> Gated { get; } = [LibriSpeech, Poem];

    /// <summary>
    ///     Builds the recordings of one scenario from the data set at the model's sample rate.
    /// </summary>
    /// <param name="scenario">The scenario name. Must be one of <see cref="All"/>.</param>
    /// <param name="data">The verified data set.</param>
    /// <param name="sampleRate">The model's sample rate, in Hz.</param>
    /// <returns>The recordings, and the segment count a correct endpointing produces (or <see langword="null"/>).</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="scenario"/> is unknown.</exception>
    /// <exception cref="InvalidDataException">Thrown when the data set lacks the recordings the scenario needs.</exception>
    public static (IReadOnlyList<ScenarioInput> Inputs, int? ExpectedSegments) Build(
        string scenario,
        SttDataSet data,
        int sampleRate)
    {
        var libriSpeech = data.GetSet(SttDataSet.LibriSpeechSet);
        switch (scenario)
        {
            case LibriSpeech:
                return (libriSpeech.Select(item => Load(item, sampleRate, 1.0f)).ToList(), libriSpeech.Count);

            case Poem:
                var poem = data.GetSet(SttDataSet.CrossingTheBarSet);
                return (poem.Select(item => Load(item, sampleRate, 1.0f)).ToList(), null);

            case Paused:
                var paused = RequireAtLeast(libriSpeech, PausedUtteranceCount).Take(PausedUtteranceCount).ToList();
                return ([Join(Paused, paused, sampleRate, PauseSeconds)], PausedUtteranceCount);

            case Continuous:
                var continuous = new List<SttDataItem>();
                var seconds = 0.0;
                foreach (var item in libriSpeech.TakeWhile(_ => seconds < ContinuousMinimumSeconds))
                {
                    continuous.Add(item);
                    seconds += item.Seconds;
                }

                if (seconds < ContinuousMinimumSeconds)
                {
                    throw new InvalidDataException(
                        $"The LibriSpeech set has only {seconds:F1} s; the continuous scenario needs {ContinuousMinimumSeconds} s.");
                }

                return ([Join(Continuous, continuous, sampleRate, 0.0)], null);

            case Quiet:
                var gain = (float)Math.Pow(10.0, -QuietAttenuationDecibels / 20.0);
                return (libriSpeech.Select(item => Load(item, sampleRate, gain)).ToList(), libriSpeech.Count);

            default:
                throw new ArgumentException(
                    $"Unknown scenario '{scenario}'. Known scenarios: {string.Join(", ", All)}.", nameof(scenario));
        }
    }

    /// <summary>Loads one item as a recording, scaled by a gain.</summary>
    /// <param name="item">The item.</param>
    /// <param name="sampleRate">The model's sample rate.</param>
    /// <param name="gain">The linear gain (1 = unchanged, which multiplies exactly).</param>
    /// <returns>The recording.</returns>
    private static ScenarioInput Load(SttDataItem item, int sampleRate, float gain)
    {
        var samples = SttDataSet.LoadSamples(item, sampleRate);
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] *= gain;
        }

        return new ScenarioInput(item.Id, item.Reference, samples, item.HasPunctuatedReference);
    }

    /// <summary>Joins several items into one recording separated by digital silence.</summary>
    /// <param name="id">The identifier of the joined recording.</param>
    /// <param name="items">The items, in order.</param>
    /// <param name="sampleRate">The model's sample rate.</param>
    /// <param name="gapSeconds">The silence between consecutive items, in seconds.</param>
    /// <returns>The joined recording, whose reference is the references joined with spaces.</returns>
    private static ScenarioInput Join(string id, IReadOnlyList<SttDataItem> items, int sampleRate, double gapSeconds)
    {
        var gap = new float[(int)Math.Round(gapSeconds * sampleRate)];
        var samples = new List<float>();
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                samples.AddRange(gap);
            }

            samples.AddRange(SttDataSet.LoadSamples(items[i], sampleRate));
        }

        return new ScenarioInput(
            id,
            string.Join(' ', items.Select(item => item.Reference)),
            [.. samples],
            items.All(item => item.HasPunctuatedReference));
    }

    /// <summary>Checks that a set has enough items for a scenario.</summary>
    /// <param name="items">The set's items.</param>
    /// <param name="count">The number needed.</param>
    /// <returns><paramref name="items"/>.</returns>
    /// <exception cref="InvalidDataException">Thrown when there are fewer items.</exception>
    private static IReadOnlyList<SttDataItem> RequireAtLeast(IReadOnlyList<SttDataItem> items, int count) =>
        items.Count >= count
            ? items
            : throw new InvalidDataException($"The scenario needs {count} LibriSpeech utterances but the set has {items.Count}.");
}

/// <summary>
///     Runs the evaluation scenarios against one installed recognition model.
/// </summary>
/// <remarks>
///     One backend is created per model and reused for every recording (reset before each one),
///     as a host reuses a loaded recognizer across sessions. Progress is written to the supplied
///     writer so a long run is observable. Not thread-safe; run one model at a time (models share
///     the CPU, and concurrent runs would distort the timings).
/// </remarks>
internal static class EvaluationRunner
{
    /// <summary>
    ///     Evaluates one model over the requested scenarios.
    /// </summary>
    /// <param name="model">The model to evaluate. Must not be <see langword="null"/>.</param>
    /// <param name="installedModelDirectory">The model's installed <c>current</c> folder.</param>
    /// <param name="parameterValues">The model parameter values, or <see langword="null"/> for the defaults.</param>
    /// <param name="data">The verified data set.</param>
    /// <param name="scenarios">The scenario names to run, in order.</param>
    /// <param name="trailingSilenceSeconds">Digital silence fed after each recording, before the flush, in seconds.</param>
    /// <param name="progress">The writer progress lines go to.</param>
    /// <returns>The model's measurements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when a reference argument is <see langword="null"/>.</exception>
    /// <remarks>Exceptions from the model (load failure) or the backend propagate unchanged.</remarks>
    public static ModelEvaluation Evaluate(
        IRecognitionModel model,
        string installedModelDirectory,
        IReadOnlyDictionary<string, object>? parameterValues,
        SttDataSet data,
        IReadOnlyList<string> scenarios,
        double trailingSilenceSeconds,
        TextWriter progress)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(installedModelDirectory);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(scenarios);
        ArgumentNullException.ThrowIfNull(progress);

        // Load the backend once; the load time is reported separately from the processing time
        var stopwatch = Stopwatch.StartNew();
        using var backend = model.CreateBackend(installedModelDirectory, parameterValues);
        var loadSeconds = stopwatch.Elapsed.TotalSeconds;
        progress.WriteLine($"{model.Id}: loaded in {loadSeconds:F1} s");

        var sampleRate = model.AudioFormat.SampleRate;
        var results = new List<ScenarioResult>();
        foreach (var scenario in scenarios)
        {
            var (inputs, expectedSegments) = EvaluationScenarios.Build(scenario, data, sampleRate);
            var items = inputs
                .Select(input => Measure(model, backend, input, sampleRate, trailingSilenceSeconds))
                .ToList();
            var result = new ScenarioResult(scenario, expectedSegments, items);
            results.Add(result);
            progress.WriteLine(
                $"{model.Id}: {scenario,-10} WER {EvaluationReport.Percent(result.Alignment.WordErrorRate)}, " +
                $"segments {result.Segments}, invented {result.Alignment.InventedWords}, RTF {result.RealTimeFactor:F3}");
        }

        return new ModelEvaluation(model.Id, loadSeconds, results);
    }

    /// <summary>Streams one recording through the backend and aligns its transcript.</summary>
    /// <param name="model">The model (for its text hook).</param>
    /// <param name="backend">The loaded backend, reset before use.</param>
    /// <param name="input">The recording.</param>
    /// <param name="sampleRate">The model's sample rate.</param>
    /// <param name="trailingSilenceSeconds">Digital silence fed after the recording.</param>
    /// <returns>The measurement.</returns>
    private static ItemResult Measure(
        IRecognitionModel model,
        IRecognitionBackend backend,
        ScenarioInput input,
        int sampleRate,
        double trailingSilenceSeconds)
    {
        // Every recording starts from a fresh utterance state, as each new session does
        backend.Reset();
        var transcript = SttTranscriber.Transcribe(
            backend,
            input.Samples,
            sampleRate,
            model.NormalizeText,
            trailingSilenceSeconds);

        var alignment = WordErrorRateCalculator.Align(input.Reference, transcript.Text);
        var verbatim = input.HasPunctuatedReference
            ? WordErrorRateCalculator.Align(
                WordErrorRateCalculator.SplitVerbatim(input.Reference),
                WordErrorRateCalculator.SplitVerbatim(transcript.Text))
            : null;
        return new ItemResult(input, transcript, alignment, verbatim);
    }
}
