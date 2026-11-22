using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using DemaConsulting.Speech.Sherpa.ModelManagementSubsystem;
using DemaConsulting.Speech.SttEvaluation;

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

    /// <summary>The seconds of the fixture at which the first phrase ("Sunset and evening star, and one clear call for me") begins.</summary>
    private const double FirstPhraseStartSeconds = 0.9;

    /// <summary>The seconds of the fixture at which the first phrase ends, at the start of a 1.5 s natural pause.</summary>
    private const double FirstPhraseEndSeconds = 6.2;

    /// <summary>The seconds of the fixture at which the second phrase ("And may there be no moaning of the bar") begins.</summary>
    private const double SecondPhraseStartSeconds = 7.7;

    /// <summary>The seconds of the fixture at which the second phrase ends, at the start of a natural pause.</summary>
    private const double SecondPhraseEndSeconds = 9.9;

    /// <summary>
    ///     Proves that speech resuming immediately after an endpoint keeps every word exactly once,
    ///     for the model that declares a post-reset warm-up interval. Two real phrases from the
    ///     recording are separated by a silent gap only slightly longer than the endpoint detector's
    ///     trailing-silence rule, so the second phrase starts right after the endpoint resets the
    ///     recognizer - the situation in which its opening words ("And may") were lost to the
    ///     encoder's warm-up blackout before the warm-up handling existed.
    /// </summary>
    /// <param name="gapSeconds">The silent gap between the two phrases, in seconds.</param>
    [Theory(Timeout = 300000)]
    [InlineData(1.2)]
    [InlineData(1.35)]
    public async Task SherpaOnnxRecognitionEngine_Transcribe_NemotronSpeechResumingRightAfterEndpoint_KeepsEveryWordExactlyOnce(
        double gapSeconds)
    {
        // Arrange: phrase one, a gap just beyond the endpoint rule, then phrase two
        using var engine = CreateNemotronEngine();
        var recording = SherpaOnnxRecognitionEngineAccuracyTests.ReadMonoPcm16Wav(RecordingPath);
        var samples = Slice(recording, FirstPhraseStartSeconds, FirstPhraseEndSeconds)
            .Concat(new float[(int)(SampleRate * gapSeconds)])
            .Concat(Slice(recording, SecondPhraseStartSeconds, SecondPhraseEndSeconds))
            .ToArray();

        // Act
        var transcript = await Task.Run(
            () => SherpaOnnxRecognitionEngineAccuracyTests.Transcribe(engine, samples, SampleRate),
            TestContext.Current.CancellationToken);

        // Assert: the words spanning the endpoint are present, in order, exactly once
        var normalized = " " + string.Join(' ', WordErrorRateCalculator.NormalizeAndSplit(transcript)) + " ";
        Assert.Contains(" for me and may there be no moaning ", normalized, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(normalized, " and may "));
        Assert.Equal(1, CountOccurrences(normalized, " call "));
        Assert.Equal(1, CountOccurrences(normalized, " moaning "));
    }

    /// <summary>
    ///     Proves that speech preceded by digital silence - including a length at which the
    ///     endpoint detector fires on that leading silence just as the speech begins - never gains
    ///     stray or duplicated words: the tail of the first phrase is reported exactly once, with
    ///     nothing added before or after it.
    /// </summary>
    /// <remarks>
    ///     Only the tail ("evening star and one clear call for me") is asserted: with a leading
    ///     silence of about 2.3 to 2.7 seconds the opening word is lost to the recognizer's own
    ///     warm-up after the endpoint (a known limitation, recorded in the verification document),
    ///     so the test pins what must hold at every length rather than that limitation.
    /// </remarks>
    /// <param name="leadingSilenceSeconds">The digital silence before the first phrase, in seconds.</param>
    [Theory(Timeout = 300000)]
    [InlineData(2.5)]
    [InlineData(4.0)]
    public async Task SherpaOnnxRecognitionEngine_Transcribe_NemotronLeadingSilenceBeforeSpeech_AddsNoStrayOrDuplicatedWords(
        double leadingSilenceSeconds)
    {
        // Arrange: leading silence, then the first phrase of the recording
        using var engine = CreateNemotronEngine();
        var recording = SherpaOnnxRecognitionEngineAccuracyTests.ReadMonoPcm16Wav(RecordingPath);
        var samples = new float[(int)(SampleRate * leadingSilenceSeconds)]
            .Concat(Slice(recording, FirstPhraseStartSeconds, FirstPhraseEndSeconds))
            .ToArray();

        // Act
        var transcript = await Task.Run(
            () => SherpaOnnxRecognitionEngineAccuracyTests.Transcribe(engine, samples, SampleRate),
            TestContext.Current.CancellationToken);

        // Assert: the phrase's tail ends the transcript, and no word was added around it
        var words = WordErrorRateCalculator.NormalizeAndSplit(transcript);
        var tail = string.Join(' ', words);
        Assert.EndsWith("evening star and one clear call for me", tail, StringComparison.Ordinal);
        Assert.InRange(words.Length, 8, 10);
        Assert.Equal(1, CountOccurrences(" " + tail + " ", " evening "));
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
    /// <param name="warmupWindowMs">An override of the model's declared warm-up window, or <see langword="null"/> for the declared one.</param>
    private static SherpaOnnxRecognitionEngine CreateNemotronEngine(int? warmupWindowMs = null)
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
            warmupWindowMs ?? recognitionModel.PostEndpointWarmupWindowMs);
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

    /// <summary>Returns the samples of <paramref name="samples"/> from <paramref name="startSeconds"/> up to <paramref name="endSeconds"/>.</summary>
    private static float[] Slice(float[] samples, double startSeconds, double endSeconds)
    {
        var start = (int)(SampleRate * startSeconds);
        return samples[start..(int)(SampleRate * endSeconds)];
    }

    /// <summary>Counts the non-overlapping occurrences of <paramref name="value"/> in <paramref name="text"/>.</summary>
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
