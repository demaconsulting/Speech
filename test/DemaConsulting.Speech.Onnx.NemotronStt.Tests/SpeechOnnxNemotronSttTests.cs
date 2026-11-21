using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.SttEvaluation;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests;

/// <summary>
///     The test collection of the real-model system tests, which run one at a time and after the
///     parallel tests.
/// </summary>
/// <remarks>
///     The real model decodes several minutes of audio across the collection; running these tests
///     in parallel with each other (or with the CPU-bound unit tests) would only slow every test
///     down and make the run time unpredictable on the small CI runners. The fixture shares the
///     verified data set and the LibriSpeech-subset transcripts that two tests assert on.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SpeechOnnxNemotronSttCollection : ICollectionFixture<SpeechOnnxNemotronSttFixture>
{
    /// <summary>The name of the collection.</summary>
    public const string Name = "SpeechOnnxNemotronStt real model";
}

/// <summary>
///     Shared, read-only resources of the real-model system tests: the installed model, the
///     verified evaluation data, and the LibriSpeech-subset transcripts.
/// </summary>
/// <remarks>
///     The model is found the way an application finds it: in the default per-user
///     <see cref="SpeechModelStore"/>, where <c>tools/ModelDownloader</c> (and so the CI workflow)
///     installs it. Every test creates its own backend, so no recognition state is shared between
///     tests; only immutable data and the transcripts computed once by
///     <see cref="GetLibriSpeechResults"/> are shared. Members are thread-safe (lazy initialization).
/// </remarks>
public sealed class SpeechOnnxNemotronSttFixture
{
    /// <summary>The sample rate of the model and of every test recording, in Hz.</summary>
    internal const int SampleRate = 16000;

    /// <summary>The model under test, as the catalog registers it.</summary>
    private readonly OnnxNemotronMultilingualRecognitionModel _model = new();

    /// <summary>The default per-user model store.</summary>
    private readonly SpeechModelStore _store = new();

    /// <summary>The verified evaluation data copied beside the test assembly.</summary>
    private readonly Lazy<SttDataSet> _data = new(
        () => SttDataSet.Load(Path.Join(AppContext.BaseDirectory, "TestData", "stt-en")));

    /// <summary>The LibriSpeech-subset transcripts, computed on first use.</summary>
    private readonly Lazy<IReadOnlyList<UtteranceResult>> _libriSpeech;

    /// <summary>Initializes the lazily computed resources.</summary>
    public SpeechOnnxNemotronSttFixture()
    {
        _libriSpeech = new Lazy<IReadOnlyList<UtteranceResult>>(TranscribeLibriSpeech);
    }

    /// <summary>Gets the model under test, through the interface an application uses.</summary>
    internal IRecognitionModel Model => _model;

    /// <summary>Gets the verified evaluation data (the manifest checksums are checked on first use).</summary>
    internal SttDataSet Data => _data.Value;

    /// <summary>
    ///     Creates a fresh backend over the installed model, skipping the calling test when the
    ///     model is not installed in this environment.
    /// </summary>
    /// <returns>A loaded backend with the shipped defaults; the caller disposes it.</returns>
    internal IRecognitionBackend CreateBackend()
    {
        SkipUnlessInstalled();
        return Model.CreateBackend(_store.GetCurrentDirectory(OnnxNemotronMultilingualRecognitionModel.ModelId));
    }

    /// <summary>
    ///     Gets the transcript of every LibriSpeech-subset utterance, each streamed on its own
    ///     through one backend reset before each utterance (as the evaluation tool does), skipping
    ///     the calling test when the model is not installed.
    /// </summary>
    /// <returns>One result per utterance, in manifest order.</returns>
    internal IReadOnlyList<UtteranceResult> GetLibriSpeechResults()
    {
        SkipUnlessInstalled();
        return _libriSpeech.Value;
    }

    /// <summary>Loads one data item's audio at the model's sample rate.</summary>
    /// <param name="file">The item's file name without folder or extension (for example <c>61-70968-0014</c>).</param>
    /// <returns>The item and its samples.</returns>
    internal (SttDataItem Item, float[] Samples) Load(string file)
    {
        var item = Data.Items.Single(i => i.Id == file);
        return (item, SttDataSet.LoadSamples(item, SampleRate));
    }

    /// <summary>
    ///     Streams samples through a backend the way a recognition session does (20 ms blocks,
    ///     the model's text hook, one flush at the end).
    /// </summary>
    /// <param name="backend">The backend to feed.</param>
    /// <param name="samples">The audio at <see cref="SampleRate"/>.</param>
    /// <param name="trailingSilenceSeconds">Digital silence fed after the audio, before the flush.</param>
    /// <returns>Every reported result with its audio-clock position.</returns>
    internal SttTranscript Transcribe(IRecognitionBackend backend, float[] samples, double trailingSilenceSeconds = 0.0) =>
        SttTranscriber.Transcribe(backend, samples, SampleRate, Model.NormalizeText, trailingSilenceSeconds);

    /// <summary>Skips the calling test when the model is not installed in the default store.</summary>
    private void SkipUnlessInstalled()
    {
        if (!_store.IsInstalled(OnnxNemotronMultilingualRecognitionModel.ModelId))
        {
            Assert.Skip(
                $"The model '{OnnxNemotronMultilingualRecognitionModel.ModelId}' is not installed; install it with " +
                $"'dotnet run --project tools/ModelDownloader -- {OnnxNemotronMultilingualRecognitionModel.ModelId}'.");
        }
    }

    /// <summary>Transcribes every LibriSpeech-subset utterance with one backend.</summary>
    /// <returns>One result per utterance, in manifest order.</returns>
    private List<UtteranceResult> TranscribeLibriSpeech()
    {
        using var backend = CreateBackend();
        var results = new List<UtteranceResult>();
        foreach (var item in Data.GetSet(SttDataSet.LibriSpeechSet))
        {
            backend.Reset();
            var transcript = Transcribe(backend, SttDataSet.LoadSamples(item, SampleRate));
            results.Add(new UtteranceResult(item, transcript, WordErrorRateCalculator.Align(item.Reference, transcript.Text)));
        }

        return results;
    }
}

/// <summary>The transcript of one recording and its alignment against the reference.</summary>
/// <param name="Item">The recording.</param>
/// <param name="Transcript">What the recognizer reported.</param>
/// <param name="Alignment">The alignment of the transcript against the reference.</param>
internal sealed record UtteranceResult(SttDataItem Item, SttTranscript Transcript, WordAlignment Alignment);

/// <summary>
///     System tests of <c>DemaConsulting.Speech.Onnx.NemotronStt</c> with the real, installed
///     Nemotron 3.5 streaming model: accuracy, segmentation, and the handling of silence, pauses and
///     session boundaries, observed only through the public backend surface.
/// </summary>
/// <remarks>
///     The recordings are the license-tracked evaluation data (<c>data/stt-en/ATTRIBUTION.md</c>),
///     verified against <c>data/stt-en/MANIFEST.tsv</c> before use. The model is int4-quantized
///     and ONNX Runtime may pick different kernels per CPU, so the tests assert thresholds,
///     counts and word presence, never exact transcripts. The accuracy thresholds are
///     <see cref="SttEvaluationGates"/>, shared with the evaluation tool's <c>--check</c>. The tests
///     are skipped (not failed) when the model is not installed; the CI workflow always installs
///     it, so in CI they run on every operating system. They are compiled for one target framework
///     only: the engine logic is covered on every framework by the fake-based unit tests, and
///     decoding minutes of audio once per framework would only multiply the CI time.
/// </remarks>
[Collection(SpeechOnnxNemotronSttCollection.Name)]
public sealed class SpeechOnnxNemotronSttTests(SpeechOnnxNemotronSttFixture fixture)
{
    /// <summary>The sample rate of every recording, in Hz.</summary>
    private const int SampleRate = SpeechOnnxNemotronSttFixture.SampleRate;

    /// <summary>The seconds of the poem at which its first phrase ("Sunset and evening star, And one clear call for me!") begins.</summary>
    private const double FirstPhraseStartSeconds = 0.9;

    /// <summary>The seconds of the poem at which its first phrase ends, at the start of a 1.5 s natural pause.</summary>
    private const double FirstPhraseEndSeconds = 6.2;

    /// <summary>The seconds of the poem at which its second phrase ("And may there be no moaning of the bar,") begins.</summary>
    private const double SecondPhraseStartSeconds = 7.7;

    /// <summary>The seconds of the poem at which its second phrase ends, at the start of a natural pause.</summary>
    private const double SecondPhraseEndSeconds = 9.9;

    /// <summary>The reference text of the poem's first phrase.</summary>
    private const string FirstPhrase = "Sunset and evening star, And one clear call for me!";

    /// <summary>The reference text of the poem's second phrase.</summary>
    private const string SecondPhrase = "And may there be no moaning of the bar,";

    /// <summary>
    ///     The highest word error rate accepted for one short segment (a phrase or one utterance of
    ///     about 20 words). One or two misrecognized words stay below it, while a segment that
    ///     lost a large part of its words or absorbed a neighboring utterance exceeds it.
    /// </summary>
    private const double SegmentMaximumWordErrorRate = 0.2;

    /// <summary>
    ///     The highest accepted corpus word error rate on LibriSpeech-subset utterances attenuated
    ///     by <see cref="QuietAttenuationDecibels"/>: the measured value at the shipped defaults
    ///     (see <c>docs/evaluation/nemotron-stt/README.md</c>) plus a margin for CPU-dependent
    ///     numeric differences of the int4 model.
    /// </summary>
    private const double QuietSpeechMaximumWordErrorRate = 0.08;

    /// <summary>The attenuation of the quiet-speech test, in decibels (the evaluation tool's quiet scenario).</summary>
    private const double QuietAttenuationDecibels = 20.0;

    /// <summary>The maximum utterance length of the endpoint rules, in seconds.</summary>
    private const double MaximumUtteranceSeconds = 20.0;

    /// <summary>
    ///     The tolerance on the maximum utterance length, in seconds: one 0.56 s encoder chunk of
    ///     decoding latency plus the carried last word of the previous segment.
    /// </summary>
    private const double MaximumUtteranceToleranceSeconds = 1.5;

    /// <summary>The length of the analysis frame used to trim quiet recording ends, in samples (20 ms).</summary>
    private const int TrimFrameSamples = SampleRate / 50;

    /// <summary>The frame RMS below which a recording end counts as quiet when trimming.</summary>
    private const double TrimRmsThreshold = 0.01;

    /// <summary>The quiet kept at each trimmed recording end, in samples (100 ms), so no word onset or decay is cut.</summary>
    private const int TrimMarginSamples = SampleRate / 10;

    /// <summary>
    ///     LibriSpeech-subset utterances with no internal pause of 0.6 s or more (longest quiet run
    ///     between the first and last loud 20 ms frame at RMS 0.01: 0.18, 0.24, 0.24 and 0.14 s), so
    ///     only the inserted gaps can end an utterance.
    /// </summary>
    private static readonly string[] UnpausedUtterances =
        ["61-70968-0014", "1089-134686-0000", "1221-135766-0004", "1320-122612-0001"];

    /// <summary>
    ///     Proves that the corpus word error rate on the LibriSpeech test-clean subset stays within
    ///     the shared accuracy gate.
    /// </summary>
    [Fact]
    public void SpeechOnnxNemotronStt_Transcribe_LibriSpeechSubset_WordErrorRateWithinGate()
    {
        // Arrange / Act: every utterance streamed on its own, as the evaluation tool does
        var results = fixture.GetLibriSpeechResults();
        var corpus = results.Aggregate(WordAlignment.Empty, (sum, result) => sum.Add(result.Alignment));

        // Assert: corpus WER (errors over all reference words) within the gate
        Assert.True(
            corpus.WordErrorRate <= SttEvaluationGates.LibriSpeechMaximumWordErrorRate,
            $"LibriSpeech-subset WER {corpus.WordErrorRate:P2} ({corpus.Errors} errors in {corpus.ReferenceWords} words) " +
            $"exceeds {SttEvaluationGates.LibriSpeechMaximumWordErrorRate:P0}.");
    }

    /// <summary>
    ///     Proves that no LibriSpeech-subset utterance gains words after its last spoken word, the
    ///     signature of a tail hallucination when the end of the stream is flushed.
    /// </summary>
    [Fact]
    public void SpeechOnnxNemotronStt_Transcribe_LibriSpeechSubset_AddsNoInventedWords()
    {
        // Arrange / Act
        var results = fixture.GetLibriSpeechResults();

        // Assert: no utterance has insertions after its last aligned reference word
        var invented = results.Where(r => r.Alignment.InventedWords > 0).ToList();
        Assert.True(
            invented.Count == 0,
            "Invented words after the speech: " +
            string.Join("; ", invented.Select(r => $"{r.Item.Id}: \"{r.Transcript.Text}\"")));
    }

    /// <summary>
    ///     Proves that the word error rate of the complete "Crossing the Bar" recitation, streamed
    ///     as one recording with its natural pauses, stays within the shared accuracy gate.
    /// </summary>
    [Fact]
    public void SpeechOnnxNemotronStt_Transcribe_CrossingTheBar_WordErrorRateWithinGate()
    {
        // Arrange
        using var backend = fixture.CreateBackend();
        var poem = fixture.Data.GetSet(SttDataSet.CrossingTheBarSet).Single();

        // Act
        var transcript = fixture.Transcribe(backend, SttDataSet.LoadSamples(poem, SampleRate));

        // Assert
        var alignment = WordErrorRateCalculator.Align(poem.Reference, transcript.Text);
        Assert.True(
            alignment.WordErrorRate <= SttEvaluationGates.CrossingTheBarMaximumWordErrorRate,
            $"Poem WER {alignment.WordErrorRate:P2} exceeds {SttEvaluationGates.CrossingTheBarMaximumWordErrorRate:P0}: " +
            $"\"{transcript.Text}\"");
    }

    /// <summary>
    ///     Proves that speech followed by trailing silence long enough to end the utterance is
    ///     finalized by the endpoint without invented words, and that the flush at the end of the
    ///     silence adds no words.
    /// </summary>
    /// <param name="file">The utterance; both produced a tail hallucination before silence padding replaced zero padding.</param>
    [Theory]
    [InlineData("237-126133-0007")]
    [InlineData("1221-135766-0004")]
    public void SpeechOnnxNemotronStt_Transcribe_TrailingSilenceThenFlush_AddsNoInventedWords(string file)
    {
        // Arrange: the utterance followed by 3 s of silence (beyond the 2.4 s rule)
        using var backend = fixture.CreateBackend();
        var (item, samples) = fixture.Load(file);

        // Act
        var transcript = fixture.Transcribe(backend, samples, trailingSilenceSeconds: 3.0);

        // Assert: one final, committed during the silence; nothing after the last spoken word
        var alignment = WordErrorRateCalculator.Align(item.Reference, transcript.Text);
        Assert.Equal(0, alignment.InventedWords);
        var final = Assert.Single(transcript.Finals);
        Assert.True(
            final.SamplePosition < transcript.FedSamples,
            "The utterance was not finalized until the flush at the end of the silence.");
    }

    /// <summary>
    ///     Proves that speech separated by pauses of about 1.5 to 2 seconds yields one final per
    ///     utterance, each committed during the pause that follows it and holding only that
    ///     utterance's words.
    /// </summary>
    [Fact]
    public void SpeechOnnxNemotronStt_Transcribe_PausedUtterances_FinalizesEachUtterance()
    {
        // Arrange: four utterances without internal pauses, separated by 1.5-2 s of silence
        using var backend = fixture.CreateBackend();
        var gapsSeconds = new[] { 1.5, 2.0, 1.75, 2.0 };
        var utterances = UnpausedUtterances.Select(fixture.Load).ToList();
        var samples = new List<float>();
        var ends = new List<long>();
        for (var i = 0; i < utterances.Count; i++)
        {
            samples.AddRange(utterances[i].Samples);
            ends.Add(samples.Count);
            samples.AddRange(new float[(int)(SampleRate * gapsSeconds[i])]);
        }

        // Act
        var transcript = fixture.Transcribe(backend, [.. samples]);

        // Assert: one final per utterance, committed in the following pause, with that utterance's words
        Assert.Equal(utterances.Count, transcript.SegmentCount);
        for (var i = 0; i < utterances.Count; i++)
        {
            var final = transcript.Finals[i];
            var nextStart = ends[i] + (long)(SampleRate * gapsSeconds[i]);
            Assert.InRange(final.SamplePosition, ends[i], nextStart);
            var wer = WordErrorRateCalculator.Compute(utterances[i].Item.Reference, final.Text);
            Assert.True(
                wer <= SegmentMaximumWordErrorRate,
                $"Final {i} \"{final.Text}\" does not match utterance {utterances[i].Item.Id} (WER {wer:P1}).");
        }
    }

    /// <summary>
    ///     Proves that speech resuming after an endpoint keeps every word exactly once: the opening
    ///     words of the second phrase are not lost and the first phrase is not repeated.
    /// </summary>
    /// <param name="gapSeconds">The silent gap between the two phrases, in seconds.</param>
    [Theory]
    [InlineData(1.5)]
    [InlineData(2.5)]
    public void SpeechOnnxNemotronStt_Transcribe_SpeechResumingAfterEndpoint_KeepsEveryWordExactlyOnce(double gapSeconds)
    {
        // Arrange: phrase one, a gap beyond the endpoint rule, then phrase two
        using var backend = fixture.CreateBackend();
        var poem = PoemSamples();
        var samples = Slice(poem, FirstPhraseStartSeconds, FirstPhraseEndSeconds)
            .Concat(new float[(int)(SampleRate * gapSeconds)])
            .Concat(Slice(poem, SecondPhraseStartSeconds, SecondPhraseEndSeconds))
            .ToArray();

        // Act
        var transcript = fixture.Transcribe(backend, samples);

        // Assert: two finals; the second opens with the second phrase; no word repeated
        Assert.Equal(2, transcript.SegmentCount);
        var second = WordErrorRateCalculator.NormalizeAndSplit(transcript.Finals[1].Text);
        Assert.Contains("may", second.Take(3));
        var words = " " + string.Join(' ', WordErrorRateCalculator.NormalizeAndSplit(transcript.Text)) + " ";
        Assert.Equal(1, CountOccurrences(words, " call "));
        Assert.Equal(1, CountOccurrences(words, " may "));
        Assert.Equal(1, CountOccurrences(words, " moaning "));
        var wer = WordErrorRateCalculator.Compute(FirstPhrase + " " + SecondPhrase, transcript.Text);
        Assert.True(wer <= SegmentMaximumWordErrorRate, $"WER {wer:P1}: \"{transcript.Text}\"");
    }

    /// <summary>
    ///     Proves that continuous speech longer than the 20 s maximum utterance length is split
    ///     into several finals, each within the maximum, without losing words at the splits.
    /// </summary>
    [Fact]
    public void SpeechOnnxNemotronStt_Transcribe_ContinuousSpeechBeyondMaximum_FinalizesAtMostEveryTwentySeconds()
    {
        // Arrange: unpaused utterances with their quiet ends trimmed, joined into one >30 s stream
        using var backend = fixture.CreateBackend();
        var utterances = UnpausedUtterances.Select(fixture.Load).ToList();
        var samples = utterances.SelectMany(u => TrimQuietEnds(u.Samples)).ToArray();
        var reference = string.Join(' ', utterances.Select(u => u.Item.Reference));
        Assert.True(samples.Length > 1.5 * MaximumUtteranceSeconds * SampleRate);

        // Act
        var transcript = fixture.Transcribe(backend, samples);

        // Assert: split while streaming, every segment (the flushed last one included) within the
        // maximum, and the words kept
        Assert.True(transcript.SegmentCount >= 2, $"Expected at least 2 finals, got {transcript.SegmentCount}.");
        var previous = 0L;
        foreach (var final in transcript.Finals)
        {
            Assert.InRange(
                (double)(final.SamplePosition - previous) / SampleRate,
                0.0,
                MaximumUtteranceSeconds + MaximumUtteranceToleranceSeconds);
            previous = final.SamplePosition;
        }

        var wer = WordErrorRateCalculator.Compute(reference, transcript.Text);
        Assert.True(
            wer <= SttEvaluationGates.LibriSpeechMaximumWordErrorRate,
            $"WER {wer:P2} exceeds {SttEvaluationGates.LibriSpeechMaximumWordErrorRate:P0}: \"{transcript.Text}\"");
    }

    /// <summary>
    ///     Proves that silence-only input - digital silence and quiet noise, long enough to reach
    ///     every endpoint rule - never produces a result with text, provisional or final.
    /// </summary>
    [Fact]
    public void SpeechOnnxNemotronStt_Transcribe_SilenceOnly_ReportsNoText()
    {
        // Arrange
        using var backend = fixture.CreateBackend();
        var samples = new float[SampleRate * 3]
            .Concat(QuietNoise(seconds: 3.0))
            .Concat(new float[SampleRate * 3])
            .ToArray();

        // Act
        var transcript = fixture.Transcribe(backend, samples);

        // Assert: no result of any kind carries text
        Assert.DoesNotContain(transcript.Events, e => !string.IsNullOrWhiteSpace(e.Text));
    }

    /// <summary>
    ///     Proves that speech preceded by digital silence - including a length at which the long
    ///     trailing-silence rule fires just before the speech begins - keeps its opening words and
    ///     gains no stray words.
    /// </summary>
    /// <param name="leadingSilenceSeconds">The digital silence before the phrase, in seconds.</param>
    [Theory]
    [InlineData(2.5)]
    [InlineData(4.0)]
    public void SpeechOnnxNemotronStt_Transcribe_LeadingSilenceBeforeSpeech_KeepsPhraseWithoutStrayWords(
        double leadingSilenceSeconds)
    {
        // Arrange: leading silence, then the poem's first phrase
        using var backend = fixture.CreateBackend();
        var samples = new float[(int)(SampleRate * leadingSilenceSeconds)]
            .Concat(Slice(PoemSamples(), FirstPhraseStartSeconds, FirstPhraseEndSeconds))
            .ToArray();

        // Act
        var transcript = fixture.Transcribe(backend, samples);

        // Assert: the opening word is kept, and nothing is added around the phrase
        var words = WordErrorRateCalculator.NormalizeAndSplit(transcript.Text);
        Assert.Contains("sunset", words.Take(2));
        var alignment = WordErrorRateCalculator.Align(FirstPhrase, transcript.Text);
        Assert.Equal(0, alignment.Insertions);
        Assert.True(alignment.WordErrorRate <= SegmentMaximumWordErrorRate, $"\"{transcript.Text}\"");
    }

    /// <summary>
    ///     Proves that quiet noise preceding the speech does not cause its opening words to be lost.
    /// </summary>
    [Fact]
    public void SpeechOnnxNemotronStt_Transcribe_QuietNoiseBeforeSpeech_KeepsOpeningWords()
    {
        // Arrange: 4 s of quiet room noise, then the opening of the poem up to a natural pause
        using var backend = fixture.CreateBackend();
        var samples = QuietNoise(seconds: 4.0).Concat(Slice(PoemSamples(), 0.0, SecondPhraseEndSeconds)).ToArray();

        // Act
        var transcript = fixture.Transcribe(backend, samples);

        // Assert: the opening words of the first line are present at the start
        var words = WordErrorRateCalculator.NormalizeAndSplit(transcript.Text);
        Assert.Contains("sunset", words.Take(3));
        Assert.Contains("evening", words.Take(5));
    }

    /// <summary>
    ///     Proves that quiet speech (20 dB below the recordings) is still recognized, within a
    ///     word error rate bound measured at the shipped defaults.
    /// </summary>
    [Fact]
    public void SpeechOnnxNemotronStt_Transcribe_QuietSpeech_RecoversWords()
    {
        // Arrange: every other subset utterance (both speaker genders), attenuated by 20 dB
        using var backend = fixture.CreateBackend();
        var gain = (float)Math.Pow(10.0, -QuietAttenuationDecibels / 20.0);
        var items = fixture.Data.GetSet(SttDataSet.LibriSpeechSet).Where((_, index) => index % 2 == 0).ToList();

        // Act: each utterance on its own, as the LibriSpeech scenario does
        var corpus = WordAlignment.Empty;
        foreach (var item in items)
        {
            backend.Reset();
            var samples = SttDataSet.LoadSamples(item, SampleRate).Select(s => s * gain).ToArray();
            var transcript = fixture.Transcribe(backend, samples);
            corpus = corpus.Add(WordErrorRateCalculator.Align(item.Reference, transcript.Text));
        }

        // Assert
        Assert.True(
            corpus.WordErrorRate <= QuietSpeechMaximumWordErrorRate,
            $"Quiet-speech WER {corpus.WordErrorRate:P2} exceeds {QuietSpeechMaximumWordErrorRate:P0}.");
    }

    /// <summary>
    ///     Proves that once speech ending at a natural pause has been finalized by its endpoint,
    ///     asking for further results with no new audio reports nothing, so no words are repeated.
    /// </summary>
    [Fact]
    public void SpeechOnnxNemotronStt_TryDecodeAndTryFlush_NoNewAudioAfterEndpoint_ReportNothing()
    {
        // Arrange: the poem's opening up to a natural pause plus silence beyond the endpoint rules
        using var backend = fixture.CreateBackend();
        var samples = Slice(PoemSamples(), 0.0, SecondPhraseEndSeconds).Concat(new float[SampleRate * 3]).ToArray();
        var texts = FeedWithoutFlush(backend, samples);
        Assert.Contains(texts, text => text.Length > 0);

        // Act: ask for more results with no further audio
        var decoded = backend.TryDecode(out var decodedResult);
        var flushed = backend.TryFlush(out var flushedResult);

        // Assert: nothing further is reported
        Assert.False(decoded && decodedResult is { Text.Length: > 0 }, $"Unexpected decoded text: {decodedResult?.Text}");
        Assert.False(flushed && flushedResult is { Text.Length: > 0 }, $"Unexpected flushed text: {flushedResult?.Text}");
    }

    /// <summary>
    ///     Proves that a flush recovers the words of an utterance abandoned with no trailing silence
    ///     (a push-to-talk release) as a final result.
    /// </summary>
    [Fact]
    public void SpeechOnnxNemotronStt_TryFlush_AbandonedUtterance_RecoversTrailingWords()
    {
        // Arrange: the poem's first 4.5 s ("Sunset and evening star, ..."), with no trailing silence
        using var backend = fixture.CreateBackend();
        FeedWithoutFlush(backend, Slice(PoemSamples(), 0.0, 4.5));

        // Act
        var flushed = backend.TryFlush(out var result);

        // Assert: a final result holding the first line's words
        Assert.True(flushed);
        Assert.NotNull(result);
        Assert.True(result.IsFinal);
        var words = WordErrorRateCalculator.NormalizeAndSplit(result.Text);
        Assert.All(new[] { "sunset", "evening", "star" }, word => Assert.Contains(word, words));
    }

    /// <summary>
    ///     Proves that resetting the backend discards an abandoned utterance, so none of its words
    ///     surface in the next session, which is fed silence only.
    /// </summary>
    [Fact]
    public void SpeechOnnxNemotronStt_Reset_AbandonedUtterance_DoesNotBleedIntoNextSession()
    {
        // Arrange: the poem's first 3 s, left mid-utterance with no trailing silence
        using var backend = fixture.CreateBackend();
        FeedWithoutFlush(backend, Slice(PoemSamples(), 0.0, 3.0));

        // Act: end the session, then run a new one over silence only
        backend.Reset();
        var transcript = fixture.Transcribe(backend, new float[SampleRate * 3 / 2]);

        // Assert: the new session reports no text at all
        Assert.DoesNotContain(transcript.Events, e => !string.IsNullOrWhiteSpace(e.Text));
    }

    /// <summary>Loads the "Crossing the Bar" recitation.</summary>
    /// <returns>The poem's samples.</returns>
    private float[] PoemSamples() =>
        SttDataSet.LoadSamples(fixture.Data.GetSet(SttDataSet.CrossingTheBarSet).Single(), SampleRate);

    /// <summary>Returns the samples from <paramref name="startSeconds"/> up to <paramref name="endSeconds"/>.</summary>
    /// <param name="samples">The recording.</param>
    /// <param name="startSeconds">The start, in seconds.</param>
    /// <param name="endSeconds">The end, in seconds.</param>
    /// <returns>The slice.</returns>
    private static float[] Slice(float[] samples, double startSeconds, double endSeconds) =>
        samples[(int)(SampleRate * startSeconds)..(int)(SampleRate * endSeconds)];

    /// <summary>
    ///     Removes the quiet start and end of a recording, keeping <see cref="TrimMarginSamples"/>
    ///     of quiet on each side, so recordings joined back to back form continuous speech.
    /// </summary>
    /// <param name="samples">The recording.</param>
    /// <returns>The trimmed recording.</returns>
    private static float[] TrimQuietEnds(float[] samples)
    {
        var frames = samples.Length / TrimFrameSamples;
        var loud = Enumerable.Range(0, frames)
            .Where(f => Rms(samples.AsSpan(f * TrimFrameSamples, TrimFrameSamples)) >= TrimRmsThreshold)
            .ToList();
        var start = Math.Max(0, loud[0] * TrimFrameSamples - TrimMarginSamples);
        var end = Math.Min(samples.Length, (loud[^1] + 1) * TrimFrameSamples + TrimMarginSamples);
        return samples[start..end];
    }

    /// <summary>Computes the root mean square of a frame.</summary>
    /// <param name="frame">The samples.</param>
    /// <returns>The RMS level.</returns>
    private static double Rms(ReadOnlySpan<float> frame)
    {
        var sumOfSquares = 0.0;
        foreach (var sample in frame)
        {
            sumOfSquares += sample * sample;
        }

        return Math.Sqrt(sumOfSquares / frame.Length);
    }

    /// <summary>Generates deterministic quiet room noise whose energy is above digital silence.</summary>
    /// <param name="seconds">The duration, in seconds.</param>
    /// <returns>Uniform noise in [-0.03, 0.03].</returns>
    private static float[] QuietNoise(double seconds)
    {
        var samples = new float[(int)(SampleRate * seconds)];
        var random = new Random(1234);
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(random.NextDouble() * 0.06 - 0.03);
        }

        return samples;
    }

    /// <summary>
    ///     Feeds samples in 20 ms blocks, draining every result after each block, without the
    ///     end-of-stream flush (an utterance left open, as a session that is still listening).
    /// </summary>
    /// <param name="backend">The backend to feed.</param>
    /// <param name="samples">The audio.</param>
    /// <returns>The text of every result, in order.</returns>
    private static List<string> FeedWithoutFlush(IRecognitionBackend backend, float[] samples)
    {
        var block = SampleRate * SttTranscriber.BlockMilliseconds / 1000;
        var texts = new List<string>();
        for (var offset = 0; offset < samples.Length; offset += block)
        {
            backend.AcceptSamples(samples.AsSpan(offset, Math.Min(block, samples.Length - offset)));
            while (backend.TryDecode(out var result) && result is not null)
            {
                texts.Add(result.Text);
            }
        }

        return texts;
    }

    /// <summary>Counts the non-overlapping occurrences of <paramref name="value"/> in <paramref name="text"/>.</summary>
    /// <param name="text">The text searched.</param>
    /// <param name="value">The value counted; its leading and trailing spaces may be shared by neighbors.</param>
    /// <returns>The number of occurrences.</returns>
    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal);
            index >= 0;
            index = text.IndexOf(value, index + value.Length - 1, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
