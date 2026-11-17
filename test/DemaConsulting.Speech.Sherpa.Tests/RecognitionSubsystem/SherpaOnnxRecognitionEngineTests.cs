using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.Sherpa.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Sherpa.Tests.RecognitionSubsystem;

/// <summary>
///     Behavioral tests for <see cref="SherpaOnnxRecognitionEngine"/>'s handling of endpoints,
///     silence, and a model's post-reset warm-up interval, driven only through the public
///     <see cref="IRecognitionBackend"/> surface with real speech.
/// </summary>
/// <remarks>
///     <see cref="SherpaOnnxRecognitionEngine"/> wraps native, un-fakeable sherpa-onnx types, so
///     these tests load the real, already installed streaming models and drive them with a real
///     recording (see <see cref="SherpaOnnxRecognitionEngineAccuracyTests"/> for its provenance)
///     plus synthesized silence and quiet noise. They assert only what a caller can observe:
///     transcribed words, their completeness against the recording's known-correct text, and
///     whether any result is reported. They are skipped, not failed, when the model is not
///     installed in this environment.
/// </remarks>
public sealed class SherpaOnnxRecognitionEngineTests
{
    /// <summary>The sample rate of the recording and of every synthesized block, in Hz.</summary>
    private const int SampleRate = 16000;

    /// <summary>The length of one analysis window used to find the recording's natural pauses.</summary>
    private const int PauseWindowSamples = SampleRate / 10;

    /// <summary>The RMS level below which an analysis window counts as a pause.</summary>
    private const double PauseRmsThreshold = 0.01;

    /// <summary>The number of consecutive quiet windows that make a natural pause worth extending.</summary>
    private const int MinimumPauseWindows = 3;

    /// <summary>The seconds of digital silence appended to each natural pause so the endpoint detector fires.</summary>
    private const double ExtraPauseSeconds = 1.5;

    /// <summary>
    ///     Proves that speech which resumes immediately after the endpoints created by longer
    ///     pauses is transcribed without lost words and without duplicated words, for the model
    ///     that declares a post-reset warm-up interval.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_Transcribe_NemotronPausesBetweenPhrases_LosesAndDuplicatesNoWords()
    {
        // Arrange: the real recording with each natural pause extended so the endpoint detector
        // fires, followed immediately by more speech
        using var engine = CreateNemotronEngine();
        var samples = ExtendNaturalPauses(
            SherpaOnnxRecognitionEngineAccuracyTests.ReadMonoPcm16Wav(RecordingPath));

        // Act
        var transcript = SherpaOnnxRecognitionEngineAccuracyTests.Transcribe(engine, samples, SampleRate);
        var wordErrorRate = WordErrorRateCalculator.Compute(
            SherpaOnnxRecognitionEngineAccuracyTests.GroundTruthTranscript,
            transcript);

        // Assert: lost words (deletions) and duplicated words (insertions) both raise the error rate
        Assert.True(
            wordErrorRate <= SherpaOnnxRecognitionEngineAccuracyTests.MaximumAcceptableWordErrorRate,
            $"Word Error Rate {wordErrorRate:P1} exceeded the tolerance. Recognized transcript: \"{transcript}\"");
    }

    /// <summary>
    ///     Proves that quiet noise preceding the speech does not cause the beginning of the
    ///     speech to be lost.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_Transcribe_NemotronQuietNoiseBeforeSpeech_KeepsOpeningWords()
    {
        // Arrange: several seconds of quiet room noise, then the opening of the real recording
        using var engine = CreateNemotronEngine();
        var opening = Excerpt(SherpaOnnxRecognitionEngineAccuracyTests.ReadMonoPcm16Wav(RecordingPath), seconds: 8.0);
        var samples = QuietNoise(seconds: 4.0).Concat(opening).ToArray();

        // Act
        var transcript = SherpaOnnxRecognitionEngineAccuracyTests.Transcribe(engine, samples, SampleRate);

        // Assert: the opening words of the first line are present in order
        var words = WordErrorRateCalculator.NormalizeAndSplit(transcript);
        Assert.Contains("sunset", words.Take(3));
        Assert.Contains("evening", words.Take(5));
    }

    /// <summary>
    ///     Proves that silence-only input, including digital silence and quiet noise long enough
    ///     to fire endpoints, never produces a recognition result carrying text.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_Transcribe_NemotronSilenceOnly_ReportsNoText()
    {
        // Arrange
        using var engine = CreateNemotronEngine();
        var samples = new float[(int)(SampleRate * 3.0)]
            .Concat(QuietNoise(seconds: 3.0))
            .Concat(new float[(int)(SampleRate * 3.0)])
            .ToArray();

        // Act: feed every block and collect every result, provisional or final
        var texts = FeedAndCollectAllResults(engine, samples);

        // Assert
        Assert.DoesNotContain(texts, text => text.Length > 0);
    }

    /// <summary>
    ///     Proves that once speech ending at a natural pause has been fully transcribed and its
    ///     final endpoint has fired, asking the engine for further results with no new audio reports nothing, so no
    ///     already-reported words are repeated.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_TryDecodeAndTryFlush_NemotronNoNewAudioAfterEndpoint_ReportNothing()
    {
        // Arrange: transcribe the opening phrases, ending at a natural pause, plus trailing silence
        using var engine = CreateNemotronEngine();
        var samples = Excerpt(SherpaOnnxRecognitionEngineAccuracyTests.ReadMonoPcm16Wav(RecordingPath), seconds: 8.0);
        var transcript = SherpaOnnxRecognitionEngineAccuracyTests.Transcribe(engine, samples, SampleRate);
        Assert.NotEmpty(transcript);

        // Act: ask for more results with no further audio
        var decoded = engine.TryDecode(out var decodedResult);
        var flushed = engine.TryFlush(out var flushedResult);

        // Assert: nothing further is reported
        Assert.False(decoded && decodedResult is { Text.Length: > 0 }, $"Unexpected decoded text: {decodedResult?.Text}");
        Assert.False(flushed && flushedResult is { Text.Length: > 0 }, $"Unexpected flushed text: {flushedResult?.Text}");
    }

    /// <summary>
    ///     Proves that, for a model that declares no post-reset warm-up interval, the engine
    ///     reports the same sequence of final results on repeated runs over the same real speech,
    ///     so endpoint sequencing is unaffected by the warm-up handling being available.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_Transcribe_ZipformerNoWarmupInterval_ReportsStableFinalResults()
    {
        // Arrange: two engines over the model that declares no warm-up interval
        var model = new SherpaOnnxZipformerEnRecognitionModel();
        Assert.Equal(0, ((IRecognitionModel)model).PostEndpointWarmupWindowMs);
        using var first = CreateZipformerEngine();
        using var second = CreateZipformerEngine();
        var samples = Excerpt(SherpaOnnxRecognitionEngineAccuracyTests.ReadMonoPcm16Wav(RecordingPath), seconds: 15.0);

        // Act
        var firstFinals = FeedAndCollectFinalResults(first, samples);
        var secondFinals = FeedAndCollectFinalResults(second, samples);

        // Assert: identical, non-empty final-result sequences
        Assert.NotEmpty(firstFinals);
        Assert.Equal(firstFinals, secondFinals);
    }

    /// <summary>Gets the path of the real recording copied next to the test assembly.</summary>
    private static string RecordingPath =>
        Path.Join(AppContext.BaseDirectory, "TestData", "crossing-the-bar-16k-mono.wav");

    /// <summary>Builds a real Nemotron engine over the installed model, skipping the test if it is not installed.</summary>
    private static SherpaOnnxRecognitionEngine CreateNemotronEngine()
    {
        var model = new SherpaOnnxNemotronStreamingEnRecognitionModel();
        if (!SherpaOnnxRecognitionEngineAccuracyTests.IsModelInstalled(model.Id))
        {
            Assert.Skip("The real streaming Nemotron model is not installed in this environment.");
        }

        var recognitionModel = (IRecognitionModel)model;
        Assert.True(recognitionModel.PostEndpointWarmupWindowMs > 0);
        return new SherpaOnnxRecognitionEngine(
            SherpaOnnxNemotronStreamingEnRecognitionModel.BuildEngineConfig(
                SherpaOnnxRecognitionEngineAccuracyTests.InstalledModelDirectory(model.Id)),
            recognitionModel.AudioFormat.SampleRate,
            recognitionModel.PostEndpointWarmupWindowMs);
    }

    /// <summary>Builds a real Zipformer engine over the installed model, skipping the test if it is not installed.</summary>
    private static SherpaOnnxRecognitionEngine CreateZipformerEngine()
    {
        var model = new SherpaOnnxZipformerEnRecognitionModel();
        if (!SherpaOnnxRecognitionEngineAccuracyTests.IsModelInstalled(model.Id))
        {
            Assert.Skip("The real streaming Zipformer model is not installed in this environment.");
        }

        var recognitionModel = (IRecognitionModel)model;
        return new SherpaOnnxRecognitionEngine(
            SherpaOnnxZipformerEnRecognitionModel.BuildEngineConfig(
                SherpaOnnxRecognitionEngineAccuracyTests.InstalledModelDirectory(model.Id)),
            recognitionModel.AudioFormat.SampleRate,
            recognitionModel.PostEndpointWarmupWindowMs);
    }

    /// <summary>
    ///     Returns the opening of <paramref name="samples"/> up to the first natural pause that
    ///     begins at least <paramref name="seconds"/> in, so the excerpt never ends mid-word.
    /// </summary>
    private static float[] Excerpt(float[] samples, double seconds)
    {
        var minimumOffset = (int)(SampleRate * seconds);
        var quietWindows = 0;
        for (var offset = 0; offset < samples.Length; offset += PauseWindowSamples)
        {
            var length = Math.Min(PauseWindowSamples, samples.Length - offset);
            var sumOfSquares = 0.0;
            for (var i = 0; i < length; i++)
            {
                sumOfSquares += samples[offset + i] * samples[offset + i];
            }

            quietWindows = Math.Sqrt(sumOfSquares / length) < PauseRmsThreshold ? quietWindows + 1 : 0;
            if (offset >= minimumOffset && quietWindows >= MinimumPauseWindows)
            {
                return samples.Take(offset).ToArray();
            }
        }

        return samples;
    }

    /// <summary>Generates deterministic quiet room noise whose energy is above digital silence.</summary>
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
    ///     Returns <paramref name="samples"/> with <see cref="ExtraPauseSeconds"/> of digital
    ///     silence appended to every natural pause, so each pause ends in an endpoint while the
    ///     speech that follows it is unchanged.
    /// </summary>
    private static float[] ExtendNaturalPauses(float[] samples)
    {
        var output = new List<float>(samples.Length + (SampleRate * 30));
        var extra = new float[(int)(SampleRate * ExtraPauseSeconds)];
        var quietWindows = 0;

        for (var offset = 0; offset < samples.Length; offset += PauseWindowSamples)
        {
            var length = Math.Min(PauseWindowSamples, samples.Length - offset);
            var sumOfSquares = 0.0;
            for (var i = 0; i < length; i++)
            {
                sumOfSquares += samples[offset + i] * samples[offset + i];
            }

            var isQuiet = Math.Sqrt(sumOfSquares / length) < PauseRmsThreshold;
            if (!isQuiet && quietWindows >= MinimumPauseWindows)
            {
                output.AddRange(extra);
            }

            quietWindows = isQuiet ? quietWindows + 1 : 0;
            for (var i = 0; i < length; i++)
            {
                output.Add(samples[offset + i]);
            }
        }

        return [.. output];
    }

    /// <summary>
    ///     Feeds <paramref name="samples"/> in 0.1-second blocks, draining every result after
    ///     each block, and returns the text of every provisional and final result in order.
    /// </summary>
    private static List<string> FeedAndCollectAllResults(IRecognitionBackend engine, float[] samples)
    {
        var texts = new List<string>();
        for (var offset = 0; offset < samples.Length; offset += PauseWindowSamples)
        {
            var length = Math.Min(PauseWindowSamples, samples.Length - offset);
            engine.AcceptSamples(samples.AsSpan(offset, length));
            while (engine.TryDecode(out var result))
            {
                texts.Add(result!.Text);
            }
        }

        return texts;
    }

    /// <summary>
    ///     Feeds <paramref name="samples"/> followed by trailing silence and returns the text of
    ///     every non-empty final result in order.
    /// </summary>
    private static List<string> FeedAndCollectFinalResults(IRecognitionBackend engine, float[] samples)
    {
        var padded = samples.Concat(new float[SampleRate * 2]).ToArray();
        var finals = new List<string>();
        for (var offset = 0; offset < padded.Length; offset += PauseWindowSamples)
        {
            var length = Math.Min(PauseWindowSamples, padded.Length - offset);
            engine.AcceptSamples(padded.AsSpan(offset, length));
            while (engine.TryDecode(out var result))
            {
                if (result is { IsFinal: true, Text.Length: > 0 })
                {
                    finals.Add(result.Text);
                }
            }
        }

        return finals;
    }
}
