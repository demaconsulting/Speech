using System.Text;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Tests.RecognitionSubsystem;

/// <summary>
///     Real-speech transcription accuracy tests for <see cref="SherpaOnnxRecognitionEngine"/>,
///     verifying that the real, installed recognition models transcribe genuine human speech
///     correctly - not just buffer/bookkeeping behavior exercised with silence and a synthesized
///     tone (see <see cref="SherpaOnnxRecognitionEngineTests"/>'s remarks).
/// </summary>
/// <remarks>
///     Before this class existed, no automated test anywhere in this project asserted on actual
///     transcribed text content against a known-correct ground truth: the existing engine-level
///     tests deliberately only prove buffer/replay/grace bookkeeping using silence and a
///     synthesized sine-wave tone, and every other recognition test replaces the real engine with
///     a fake. That left a genuine coverage gap - nothing proved the real, shipped models
///     actually transcribe real speech with acceptable accuracy. This class closes that gap using
///     a real, CC0-licensed spoken-word recitation of Tennyson's public-domain poem "Crossing the
///     Bar" (<c>TestData/crossing-the-bar-16k-mono.wav</c>; full attribution and license in
///     <c>TestData/NOTICE.md</c>), scored against the poem's own published text using Word Error
///     Rate (<see cref="WordErrorRateCalculator"/>).
///     <para>
///     Following the exact pattern <see cref="SherpaOnnxRecognitionEngineTests"/> already
///     established for native-runtime-dependent code: each test loads a real, already-installed
///     model and is skipped (not failed) when that model is not installed in this environment,
///     since downloading a multi-hundred-megabyte production model is outside the scope of a unit
///     test run.
///     </para>
///     <para>
///     A tolerant WER threshold (20%), not an exact string match, is used because a real STT
///     model's raw output legitimately differs from the ground truth in ways that do not reflect
///     a transcription failure - most notably Tennyson's archaic spelling "crost" (for
///     "crossed"), which a modern language model's vocabulary may render as either spelling or
///     something else phonetically similar - while still requiring a low error rate consistent
///     with a clear, single-speaker, studio-quality recording.
///     </para>
/// </remarks>
public sealed class SherpaOnnxRecognitionEngineAccuracyTests
{
    /// <summary>
    ///     The maximum Word Error Rate accepted for a clear, single-speaker, studio-quality
    ///     spoken-word recording. Chosen as a defensible tolerance: high enough to absorb a
    ///     handful of archaic-word misrecognitions (for example "crost"/"bourne") without masking
    ///     a genuine transcription regression, but low enough that a model producing a
    ///     substantially wrong transcript still fails this test.
    /// </summary>
    private const double MaximumAcceptableWordErrorRate = 0.20;

    /// <summary>
    ///     The ground-truth transcript of <c>TestData/crossing-the-bar-16k-mono.wav</c>: the full,
    ///     public-domain text of Alfred, Lord Tennyson's 1889 poem "Crossing the Bar", exactly as
    ///     recited in the recording. See <c>TestData/NOTICE.md</c> for the recording's license and
    ///     attribution.
    /// </summary>
    private const string GroundTruthTranscript = """
        Sunset and evening star,
        And one clear call for me!
        And may there be no moaning of the bar,
        When I put out to sea,

        But such a tide as moving seems asleep,
        Too full for sound and foam,
        When that which drew from out the boundless deep
        Turns again home.

        Twilight and evening bell,
        And after that the dark!
        And may there be no sadness of farewell,
        When I embark;

        For though from out our bourne of Time and Place
        The flood may bear me far,
        I hope to see my Pilot face to face
        When I have crost the bar.
        """;

    /// <summary>
    ///     The number of trailing silence samples fed once every real audio sample has been fed,
    ///     giving the real endpoint detector enough trailing silence to flush the final in-flight
    ///     utterance as a finalized result before the test reads back the accumulated transcript.
    /// </summary>
    private const double TrailingSilenceSeconds = 2.0;

    /// <summary>
    ///     Resolves the real installed model directory for the given model, using the same
    ///     default store root <see cref="SpeechModelStore"/> uses,
    ///     without depending on that class's non-test-only construction path - mirrors
    ///     <see cref="SherpaOnnxRecognitionEngineTests"/>'s identically named helper.
    /// </summary>
    private static string InstalledModelDirectory(string modelId) => Path.Join(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DemaConsulting.Speech",
        "Models",
        modelId,
        "current");

    /// <summary>Whether the given model is installed in this environment.</summary>
    private static bool IsModelInstalled(string modelId)
    {
        var directory = InstalledModelDirectory(modelId);
        return Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any();
    }

    /// <summary>
    ///     Reads a 16-bit PCM mono WAV file's samples as normalized <c>[-1.0, 1.0]</c> floats,
    ///     scanning the RIFF chunk structure for the <c>fmt </c> and <c>data</c> chunks rather
    ///     than assuming a fixed header size, so the reader tolerates any well-formed canonical
    ///     WAV file regardless of which tool produced it.
    /// </summary>
    /// <param name="path">The absolute path of the WAV file to read. Must exist.</param>
    /// <returns>The file's audio samples, normalized to <c>[-1.0, 1.0]</c>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the file is not a RIFF/WAVE container, is not 16-bit PCM, or is not
    ///     single-channel - this test's fixture is committed as exactly that format (see
    ///     <c>TestData/NOTICE.md</c>), so any mismatch indicates the fixture itself was corrupted
    ///     or replaced with an incompatible file, not a normal runtime condition to recover from.
    /// </exception>
    private static float[] ReadMonoPcm16Wav(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        // The 12-byte RIFF/WAVE container header precedes every chunk
        if (new string(reader.ReadChars(4)) != "RIFF")
        {
            throw new InvalidDataException($"'{path}' is not a RIFF file.");
        }

        reader.ReadInt32(); // Overall RIFF chunk size - not needed, chunks below are self-describing
        if (new string(reader.ReadChars(4)) != "WAVE")
        {
            throw new InvalidDataException($"'{path}' is not a WAVE file.");
        }

        // Scan chunks in order until both "fmt " and "data" have been found; a canonical WAV
        // file always presents them in that order, but scanning (rather than assuming fixed
        // offsets) tolerates any extra metadata chunks a tool may have inserted between them.
        short channelCount = 0;
        short bitsPerSample = 0;
        byte[]? dataBytes = null;
        while (stream.Position < stream.Length && (channelCount == 0 || dataBytes is null))
        {
            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadInt32();

            if (chunkId == "fmt ")
            {
                reader.ReadInt16(); // Audio format tag - assumed uncompressed PCM (1), verified below
                channelCount = reader.ReadInt16();
                reader.ReadInt32(); // Sample rate - the model declares its own expected rate
                reader.ReadInt32(); // Byte rate - derivable from the fields already read
                reader.ReadInt16(); // Block align - derivable from the fields already read
                bitsPerSample = reader.ReadInt16();

                // Skip any additional format bytes beyond the canonical 16-byte PCM fmt chunk
                var consumed = 16;
                if (chunkSize > consumed)
                {
                    reader.ReadBytes(chunkSize - consumed);
                }
            }
            else if (chunkId == "data")
            {
                dataBytes = reader.ReadBytes(chunkSize);
            }
            else
            {
                // An unrecognized chunk (metadata, padding, etc.) - skip its declared size
                reader.ReadBytes(chunkSize);
            }
        }

        if (dataBytes is null)
        {
            throw new InvalidDataException($"'{path}' has no 'data' chunk.");
        }

        if (bitsPerSample != 16 || channelCount != 1)
        {
            throw new InvalidDataException(
                $"'{path}' must be 16-bit mono PCM, but was {bitsPerSample}-bit with {channelCount} channel(s).");
        }

        // Convert interleaved 16-bit signed PCM bytes into normalized [-1.0, 1.0] floats
        var sampleCount = dataBytes.Length / 2;
        var samples = new float[sampleCount];
        for (var i = 0; i < sampleCount; i++)
        {
            var pcmValue = (short)(dataBytes[2 * i] | (dataBytes[2 * i + 1] << 8));
            samples[i] = pcmValue / (float)short.MaxValue;
        }

        return samples;
    }

    /// <summary>
    ///     Feeds every sample of <paramref name="samples"/> through <paramref name="engine"/> in
    ///     fixed-size streaming chunks (mirroring the block sizes
    ///     <see cref="SherpaOnnxRecognitionEngineTests"/> already feeds elsewhere), draining every
    ///     decode result after each chunk, and concatenates every finalized result's text (in
    ///     production order) into one full transcript.
    /// </summary>
    /// <param name="engine">The real engine to feed. Must not be <see langword="null"/>.</param>
    /// <param name="samples">The full utterance's normalized mono samples, at the engine's declared rate.</param>
    /// <param name="sampleRate">The sample rate <paramref name="samples"/> is at, in Hz.</param>
    /// <returns>The concatenated text of every finalized result the engine produced, in order.</returns>
    /// <remarks>
    ///     Only finalized (<see cref="SpeechRecognitionResult.IsFinal"/>) results are
    ///     accumulated: provisional results are still-forming hypotheses the engine may revise or
    ///     supersede, and would otherwise duplicate or corrupt words already captured by a later
    ///     final result for the same segment. Trailing silence (<see cref="TrailingSilenceSeconds"/>)
    ///     is fed once the real audio is exhausted so the last in-flight utterance's endpoint
    ///     fires and its final result is not silently lost.
    /// </remarks>
    private static string Transcribe(IRecognitionEngine engine, float[] samples, int sampleRate)
    {
        var chunkSize = sampleRate / 10; // 0.1-second chunks, matching this project's existing test convention
        var transcript = new StringBuilder();

        for (var offset = 0; offset < samples.Length; offset += chunkSize)
        {
            var length = Math.Min(chunkSize, samples.Length - offset);
            engine.AcceptSamples(samples.AsSpan(offset, length));
            DrainFinalResults(engine, transcript);
        }

        // Flush the final in-flight utterance with trailing silence, matching how a real capture
        // session's last utterance is only finalized once the speaker stops talking.
        var silence = new float[(int)(sampleRate * TrailingSilenceSeconds)];
        for (var offset = 0; offset < silence.Length; offset += chunkSize)
        {
            var length = Math.Min(chunkSize, silence.Length - offset);
            engine.AcceptSamples(silence.AsSpan(offset, length));
            DrainFinalResults(engine, transcript);
        }

        return transcript.ToString();
    }

    /// <summary>
    ///     Repeatedly calls <see cref="IRecognitionEngine.TryDecode"/> until it reports nothing
    ///     new, appending every finalized result's text to <paramref name="transcript"/>.
    /// </summary>
    private static void DrainFinalResults(IRecognitionEngine engine, StringBuilder transcript)
    {
        while (engine.TryDecode(out var result))
        {
            if (result is { IsFinal: true, Text.Length: > 0 })
            {
                if (transcript.Length > 0)
                {
                    transcript.Append(' ');
                }

                transcript.Append(result.Text);
            }
        }
    }

    /// <summary>
    ///     Proves that the real, installed streaming Zipformer model transcribes a genuine,
    ///     clear, studio-quality human speech recording with a Word Error Rate below the accepted
    ///     tolerance, closing the real-speech accuracy coverage gap described in this class's
    ///     remarks.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_Transcribe_RealCrossingTheBarRecording_WordErrorRateBelowTolerance()
    {
        // Arrange: skip if the real Zipformer model is not installed in this environment
        var model = new SherpaOnnxZipformerEnRecognitionModel();
        if (!IsModelInstalled(model.Id))
        {
            Assert.Skip("The real streaming Zipformer model is not installed in this environment.");
        }

        var modelDirectory = InstalledModelDirectory(model.Id);
        var config = ((IRecognitionModel)model).CreateEngineConfig(modelDirectory);
        using var engine = new SherpaOnnxRecognitionEngine(
            config,
            ((IRecognitionModel)model).AudioFormat.SampleRate,
            ((IRecognitionModel)model).PostEndpointWarmupWindowMs);

        var wavPath = Path.Join(AppContext.BaseDirectory, "TestData", "crossing-the-bar-16k-mono.wav");
        var samples = ReadMonoPcm16Wav(wavPath);

        // Act: stream the real recording through the real engine and score the result
        var transcript = Transcribe(engine, samples, ((IRecognitionModel)model).AudioFormat.SampleRate);
        var wordErrorRate = WordErrorRateCalculator.Compute(GroundTruthTranscript, transcript);

        // Assert: the real model transcribed clear studio-quality speech within tolerance
        Assert.True(
            wordErrorRate <= MaximumAcceptableWordErrorRate,
            $"Word Error Rate {wordErrorRate:P1} exceeded the {MaximumAcceptableWordErrorRate:P0} tolerance. " +
            $"Recognized transcript: \"{transcript}\"");
    }

    /// <summary>
    ///     Proves the same real-speech accuracy property for the real, installed streaming
    ///     Nemotron model, giving this coverage gap a second real model's worth of evidence since
    ///     both real models are shipped as production recognition options (see
    ///     <see cref="SherpaOnnxRecognitionEngineTests"/>'s remarks on why the Nemotron model
    ///     already receives dedicated real-model engine-level test coverage for its own defect
    ///     history).
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_Transcribe_RealCrossingTheBarRecording_NemotronWordErrorRateBelowTolerance()
    {
        // Arrange: skip if the real Nemotron model is not installed in this environment
        var model = new SherpaOnnxNemotronStreamingEnRecognitionModel();
        if (!IsModelInstalled(model.Id))
        {
            Assert.Skip("The real streaming Nemotron model is not installed in this environment.");
        }

        var modelDirectory = InstalledModelDirectory(model.Id);
        var config = ((IRecognitionModel)model).CreateEngineConfig(modelDirectory);
        using var engine = new SherpaOnnxRecognitionEngine(
            config,
            ((IRecognitionModel)model).AudioFormat.SampleRate,
            ((IRecognitionModel)model).PostEndpointWarmupWindowMs);

        var wavPath = Path.Join(AppContext.BaseDirectory, "TestData", "crossing-the-bar-16k-mono.wav");
        var samples = ReadMonoPcm16Wav(wavPath);

        // Act: stream the real recording through the real engine and score the result
        var transcript = Transcribe(engine, samples, ((IRecognitionModel)model).AudioFormat.SampleRate);
        var wordErrorRate = WordErrorRateCalculator.Compute(GroundTruthTranscript, transcript);

        // Assert: the real model transcribed clear studio-quality speech within tolerance
        Assert.True(
            wordErrorRate <= MaximumAcceptableWordErrorRate,
            $"Word Error Rate {wordErrorRate:P1} exceeded the {MaximumAcceptableWordErrorRate:P0} tolerance. " +
            $"Recognized transcript: \"{transcript}\"");
    }

    /// <summary>
    ///     The words of the first line spoken in <c>TestData/crossing-the-bar-16k-mono.wav</c>,
    ///     used only as a case-insensitive leak detector for
    ///     <see cref="SherpaOnnxRecognitionEngine_Reset_AbandonedUtteranceWithNoTrailingSilence_DoesNotBleedIntoNextSession"/> -
    ///     not as a transcription-accuracy ground truth (see
    ///     <see cref="GroundTruthTranscript"/> and <see cref="WordErrorRateCalculator"/> for that).
    /// </summary>
    private static readonly string[] FirstLineWords =
        "Sunset and evening star".Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>
    ///     Feeds every sample of <paramref name="samples"/> through <paramref name="engine"/> in
    ///     fixed-size streaming chunks, draining (and discarding) every decode result after each
    ///     chunk exactly like <c>SherpaOnnxSpeechRecognizer.ProcessFrame</c> does, but - unlike
    ///     <see cref="Transcribe"/> - feeding no trailing silence afterward, so the call returns
    ///     with the last spoken utterance still in-flight (not yet finalized by an endpoint) and
    ///     its audio only partially decoded, mirroring a push-to-talk release with no pause before
    ///     it.
    /// </summary>
    /// <param name="engine">The real engine to feed. Must not be <see langword="null"/>.</param>
    /// <param name="samples">The utterance's normalized mono samples, at the engine's declared rate.</param>
    /// <param name="sampleRate">The sample rate <paramref name="samples"/> is at, in Hz.</param>
    private static void FeedWithoutTrailingSilence(IRecognitionEngine engine, float[] samples, int sampleRate)
    {
        var chunkSize = sampleRate / 10; // 0.1-second chunks, matching this project's existing test convention

        for (var offset = 0; offset < samples.Length; offset += chunkSize)
        {
            var length = Math.Min(chunkSize, samples.Length - offset);
            engine.AcceptSamples(samples.AsSpan(offset, length));

            // Drain and discard: this step exists only to leave buffered, not-yet-decoded audio
            // behind for Reset() to discard, not to collect a transcript.
            while (engine.TryDecode(out _))
            {
                // Intentionally empty: discarding every result here is the point - see remarks.
            }
        }
    }

    /// <summary>
    ///     Feeds <paramref name="seconds"/> of digital silence through <paramref name="engine"/>
    ///     in fixed-size chunks, draining every decode result (both provisional and finalized)
    ///     after each chunk and appending any non-empty text to <paramref name="leakedText"/>.
    /// </summary>
    /// <remarks>
    ///     Both provisional and finalized text are collected here - unlike
    ///     <see cref="DrainFinalResults"/>, which only accumulates finalized text for a normal
    ///     transcript - because a leaked tail of an abandoned utterance could plausibly surface as
    ///     either a provisional hypothesis or (if silence happens to also trip the endpoint rule) a
    ///     finalized result; either would prove the bug this test guards against.
    /// </remarks>
    private static void FeedSilenceCollectingAnyText(
        IRecognitionEngine engine,
        int sampleRate,
        double seconds,
        StringBuilder leakedText)
    {
        var chunkSize = sampleRate / 10;
        var silence = new float[(int)(sampleRate * seconds)];

        for (var offset = 0; offset < silence.Length; offset += chunkSize)
        {
            var length = Math.Min(chunkSize, silence.Length - offset);
            engine.AcceptSamples(silence.AsSpan(offset, length));

            while (engine.TryDecode(out var result))
            {
                if (result is { Text.Length: > 0 })
                {
                    leakedText.Append(' ').Append(result.Text);
                }
            }
        }
    }

    /// <summary>
    ///     Regression test for the bug where <see cref="SherpaOnnxRecognitionEngine.Reset"/> did
    ///     not discard audio already accepted via <c>AcceptWaveform</c> but not yet decoded, so an
    ///     abandoned utterance's tail (for example a push-to-talk release with no trailing
    ///     silence) decoded into the next session instead of being discarded, even when only
    ///     silence was fed after <see cref="IRecognitionEngine.Reset"/>. The fix makes
    ///     session-end <c>Reset()</c> dispose the stream and create a replacement rather than
    ///     resetting the existing stream's hypothesis in place, discarding any buffered,
    ///     not-yet-decoded audio along with it. A fake-engine test cannot exercise this: the bug
    ///     lives in the real native stream's own buffered-audio lifetime, which only a real engine
    ///     backed by a real, installed model can exhibit.
    /// </summary>
    [Fact]
    public void SherpaOnnxRecognitionEngine_Reset_AbandonedUtteranceWithNoTrailingSilence_DoesNotBleedIntoNextSession()
    {
        // Arrange: skip if the real Zipformer model is not installed in this environment
        var model = new SherpaOnnxZipformerEnRecognitionModel();
        if (!IsModelInstalled(model.Id))
        {
            Assert.Skip("The real streaming Zipformer model is not installed in this environment.");
        }

        var modelDirectory = InstalledModelDirectory(model.Id);
        var config = ((IRecognitionModel)model).CreateEngineConfig(modelDirectory);

        // postEndpointWarmupWindowMs: 0 keeps the warm-up-replay paths inert and irrelevant to
        // this test, which is exercised at the ordinary session-end Reset() call site only.
        using var engine = new SherpaOnnxRecognitionEngine(
            config,
            ((IRecognitionModel)model).AudioFormat.SampleRate,
            postEndpointWarmupWindowMs: 0);

        var sampleRate = ((IRecognitionModel)model).AudioFormat.SampleRate;
        var wavPath = Path.Join(AppContext.BaseDirectory, "TestData", "crossing-the-bar-16k-mono.wav");
        var fullRecording = ReadMonoPcm16Wav(wavPath);

        // Only the recording's first line ("Sunset and evening star,") is fed - short enough
        // that the real endpoint detector (Rule1MinTrailingSilence 2.4s / Rule3MinUtteranceLength
        // 20s, see SherpaOnnxZipformerEnRecognitionModel.CreateEngineConfig) has no opportunity to
        // fire naturally during or after it. That is essential: an endpoint firing naturally
        // during feeding would already reset the stream via TryDecode's own (correct, unchanged)
        // endpoint-triggered reset path before this test's explicit engine.Reset() call ever
        // runs, leaving nothing buffered for that call to fail to discard and silently defeating
        // this regression test.
        var abandonedUtteranceSeconds = 3.0;
        var abandonedUtteranceLength = Math.Min(fullRecording.Length, (int)(sampleRate * abandonedUtteranceSeconds));
        var samples = fullRecording[..abandonedUtteranceLength];

        // Act: feed the abandoned utterance with no trailing silence, so it is left mid-decode -
        // buffered audio has been accepted but not yet fully decoded, exactly the condition the
        // bug report describes for a push-to-talk release. Then reset the session, as
        // SherpaOnnxSpeechRecognizer.StopCore does on every Stop(), and feed only silence,
        // collecting any text the engine reports.
        FeedWithoutTrailingSilence(engine, samples, sampleRate);
        engine.Reset();

        var leakedText = new StringBuilder();
        FeedSilenceCollectingAnyText(engine, sampleRate, seconds: 1.5, leakedText);
        var leaked = leakedText.ToString();

        // Assert: no text at all - and specifically none of the abandoned utterance's own first
        // line - was reported from silence-only audio fed after Reset()
        Assert.True(
            leaked.Trim().Length == 0,
            $"Expected no text after Reset() followed by silence only, but got: \"{leaked.Trim()}\"");
        Assert.All(
            FirstLineWords,
            word => Assert.DoesNotContain(word, leaked, StringComparison.OrdinalIgnoreCase));
    }
}
