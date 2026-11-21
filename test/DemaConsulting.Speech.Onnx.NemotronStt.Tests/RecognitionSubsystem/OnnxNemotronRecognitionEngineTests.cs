using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.RecognitionSubsystem;

/// <summary>
///     Unit tests for <see cref="OnnxNemotronRecognitionEngine"/> using a fake encoder and a
///     scripted prediction network, so no model files are needed.
/// </summary>
public sealed class OnnxNemotronRecognitionEngineTests
{
    /// <summary>Samples per second.</summary>
    private const int Rate = 16000;

    /// <summary>The blank id of the fake network (also the last vocabulary index).</summary>
    private const int Blank = 3;

    /// <summary>A tiny vocabulary: unk, "hello", "world", blank.</summary>
    private static readonly string[] Words = ["<unk>", "\u2581hello", "\u2581world", "<blank>"];

    /// <summary>Builds a speech-level tone.</summary>
    private static float[] Tone(double seconds)
    {
        var samples = new float[(int)(seconds * Rate)];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (float)(0.3 * Math.Sin(2 * Math.PI * 300 * i / Rate));
        }

        return samples;
    }

    /// <summary>Creates an engine around fakes.</summary>
    private static (OnnxNemotronRecognitionEngine Engine, FakeNemotronEncoder Encoder, FakeRnntNetwork Network) Create(
        NemotronEngineOptions? options = null,
        params int[] script)
    {
        var encoder = new FakeNemotronEncoder();
        var network = new FakeRnntNetwork(Blank, script);
        var engine = new OnnxNemotronRecognitionEngine(
            encoder,
            new NemotronRnntGreedyDecoder(network),
            new NemotronVocabulary(Words),
            options);
        return (engine, encoder, network);
    }

    /// <summary>Drains every result currently available.</summary>
    private static List<SpeechRecognitionResult> Drain(OnnxNemotronRecognitionEngine engine)
    {
        var results = new List<SpeechRecognitionResult>();
        while (engine.TryDecode(out var result))
        {
            results.Add(result!);
        }

        return results;
    }

    /// <summary>Builds digital silence.</summary>
    /// <param name="seconds">The duration.</param>
    /// <returns>The zero samples.</returns>
    private static float[] Silence(double seconds) => new float[(int)(seconds * Rate)];

    /// <summary>Joins audio pieces into one block.</summary>
    /// <param name="pieces">The pieces in order.</param>
    /// <returns>The concatenated samples.</returns>
    private static float[] Join(params float[][] pieces) => pieces.SelectMany(p => p).ToArray();

    /// <summary>
    ///     Creates an engine around loudness-reacting fakes: a 7-frame encoder (like the real model)
    ///     and a network that says "hello", then "world", on each quiet-to-loud transition.
    /// </summary>
    /// <param name="options">The engine options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The engine and its fakes.</returns>
    private static (OnnxNemotronRecognitionEngine Engine, LoudnessFakeEncoder Encoder, LoudnessFakeNetwork Network) CreateLoudness(
        NemotronEngineOptions? options = null)
    {
        var encoder = new LoudnessFakeEncoder();
        var network = new LoudnessFakeNetwork(Blank, 1, 2);
        var engine = new OnnxNemotronRecognitionEngine(
            encoder,
            new NemotronRnntGreedyDecoder(network),
            new NemotronVocabulary(Words),
            options);
        return (engine, encoder, network);
    }

    /// <summary>
    ///     Creates an engine whose scripted network says the two-piece word "hel" + "lo" on every
    ///     encoder frame, for the maximum-length rule.
    /// </summary>
    /// <param name="maxUtteranceMs">The maximum utterance length rule.</param>
    /// <returns>The engine and its network.</returns>
    private static (OnnxNemotronRecognitionEngine Engine, FakeRnntNetwork Network) CreateContinuousWords(int maxUtteranceMs)
    {
        var script = Enumerable.Repeat(new[] { 1, 2, Blank }, 400).SelectMany(t => t).ToArray();
        var network = new FakeRnntNetwork(Blank, script);
        var engine = new OnnxNemotronRecognitionEngine(
            new FakeNemotronEncoder(),
            new NemotronRnntGreedyDecoder(network),
            new NemotronVocabulary(["<unk>", "\u2581hel", "lo", "<blank>"]),
            new NemotronEngineOptions(Endpoint: new NemotronEndpointRules(MaxUtteranceMs: maxUtteranceMs)));
        return (engine, network);
    }

    /// <summary>Proves a decoded chunk yields a provisional result with the detokenized text.</summary>
    [Fact]
    public void TryDecode_SpeechChunk_ReportsProvisionalText()
    {
        // Arrange: an engine whose scripted network emits "hello" then blank
        var (engine, encoder, _) = Create(null, 1, Blank);

        // Act: feed one second of speech-level audio and drain the results
        engine.AcceptSamples(Tone(1));
        var results = Drain(engine);

        // Assert: exactly one provisional result carries the detokenized text and a full encoder input
        var result = Assert.Single(results);
        Assert.Equal("hello", result.Text);
        Assert.False(result.IsFinal);
        Assert.Equal(65, encoder.LastFrameCount);
        Assert.Equal(65 * 128, encoder.LastFeatureLength);
    }

    /// <summary>Proves unchanged text is not re-reported.</summary>
    [Fact]
    public void TryDecode_NoNewTokens_ReportsNothing()
    {
        // Arrange: an engine that has already reported "hello" and then only produces blanks
        var (engine, _, _) = Create(null, 1, Blank);
        engine.AcceptSamples(Tone(1));
        Drain(engine);

        // Act: feed another half second of audio that leaves the utterance text unchanged (short
        // enough that the trailing blank frames stay below the 1.2 s endpoint rule)
        engine.AcceptSamples(Tone(0.5));

        // Assert: nothing new is reported
        Assert.Empty(Drain(engine));
    }

    /// <summary>Proves no result is produced before a whole chunk is available.</summary>
    [Fact]
    public void TryDecode_LessThanChunk_ReportsNothing()
    {
        // Arrange: a fresh engine
        var (engine, encoder, _) = Create(null, 1);

        // Act: feed 0.2 seconds, less than one encoder chunk
        engine.AcceptSamples(Tone(0.2));

        // Assert: no result is available and the encoder never ran
        Assert.False(engine.TryDecode(out _));
        Assert.Equal(0, encoder.EncodeCalls);
    }

    /// <summary>Proves empty input is ignored.</summary>
    [Fact]
    public void AcceptSamples_Empty_IsIgnored()
    {
        // Arrange: a fresh engine
        var (engine, encoder, _) = Create();

        // Act: accept an empty block
        engine.AcceptSamples(ReadOnlySpan<float>.Empty);

        // Assert: nothing is decoded and no result is pending
        Assert.False(engine.TryDecode(out _));
        Assert.Equal(0, encoder.EncodeCalls);
    }

    /// <summary>Proves flush decodes the partial chunk plus the tail chunk and returns the final text.</summary>
    [Fact]
    public void TryFlush_PartialAudio_ReturnsFinalText()
    {
        // Arrange: an engine scripted to emit "hello" and then "world"
        var (engine, encoder, _) = Create(null, 1, Blank, 2, Blank);

        // Act: feed half a second (a partial chunk) and flush
        engine.AcceptSamples(Tone(0.5));
        var flushed = engine.TryFlush(out var result);

        // Assert: a final result holds both words and the tail chunk was encoded too
        Assert.True(flushed);
        Assert.True(result!.IsFinal);
        Assert.Equal("hello world", result.Text);
        Assert.True(encoder.EncodeCalls >= 2);
    }

    /// <summary>Proves flush with no audio produces no result and no encoder work.</summary>
    [Fact]
    public void TryFlush_NoAudio_ReturnsFalse()
    {
        // Arrange: a fresh engine that has accepted no audio
        var (engine, encoder, _) = Create();

        // Act: flush immediately
        var flushed = engine.TryFlush(out var result);

        // Assert: no result and no encoder work
        Assert.False(flushed);
        Assert.Null(result);
        Assert.Equal(0, encoder.EncodeCalls);
    }

    /// <summary>Proves flush with audio but no tokens returns no result.</summary>
    [Fact]
    public void TryFlush_AudioWithoutTokens_ReturnsFalse()
    {
        // Arrange: an engine whose network emits only blanks
        var (engine, _, _) = Create();

        // Act: feed half a second of audio and flush
        engine.AcceptSamples(Tone(0.5));
        var flushed = engine.TryFlush(out _);

        // Assert: no text was produced, so no result is returned
        Assert.False(flushed);
    }

    /// <summary>Proves the final tail chunk count is configurable.</summary>
    [Fact]
    public void TryFlush_NoTailChunks_DecodesOnlyAudio()
    {
        // Arrange: one engine with the default tail chunk and one with none
        var (withTail, encoderWith, _) = Create(null, 1);
        var (withoutTail, encoderWithout, _) = Create(new NemotronEngineOptions(FlushTailChunks: 0), 1);

        // Act: feed identical audio to both and flush each
        withTail.AcceptSamples(Tone(0.5));
        withTail.TryFlush(out _);
        withoutTail.AcceptSamples(Tone(0.5));
        withoutTail.TryFlush(out _);

        // Assert: the default engine encodes exactly one more chunk
        Assert.Equal(encoderWithout.EncodeCalls + 1, encoderWith.EncodeCalls);
    }

    /// <summary>Proves a partial chunk is completed with silence once the quiet cap is reached, before the endpoint.</summary>
    [Fact]
    public void TryDecode_QuietAfterShortSpeech_ReportsBeforeEndpoint()
    {
        // Arrange: an engine scripted to emit "hello"
        var (engine, _, _) = Create(null, 1, Blank);

        // Act: feed 0.3 s of speech (a partial chunk) and then 0.8 s of silence
        engine.AcceptSamples(Tone(0.3));
        engine.AcceptSamples(new float[(int)(0.8 * Rate)]);
        var results = Drain(engine);

        // Assert: the text is reported once as provisional, before the 1.2 s endpoint
        var result = Assert.Single(results);
        Assert.Equal("hello", result.Text);
        Assert.False(result.IsFinal);
    }

    /// <summary>Proves the padding threshold follows the configured limiter cap.</summary>
    [Fact]
    public void TryDecode_CustomLimiterCap_PadsAtThatCap()
    {
        // Arrange: an engine whose limiter cap is 200 ms (10 frames)
        var options = new NemotronEngineOptions(Limiter: new SilenceRunLimiterOptions(MaxQuietFrames: 10));
        var (engine, _, _) = Create(options, 1, Blank);

        // Act: feed 0.3 s of speech and then 0.3 s of silence, past the custom cap
        engine.AcceptSamples(Tone(0.3));
        engine.AcceptSamples(new float[(int)(0.3 * Rate)]);
        var results = Drain(engine);

        // Assert: the partial chunk was completed and its text reported
        var result = Assert.Single(results);
        Assert.Equal("hello", result.Text);
    }

    /// <summary>Proves leading silence alone never endpoints (no speech yet).</summary>
    [Fact]
    public void TryDecode_OnlySilence_NeverEndpoints()
    {
        // Arrange: a fresh engine
        var (engine, encoder, _) = Create(null, 1);

        // Act: feed five seconds of digital silence only
        engine.AcceptSamples(new float[5 * Rate]);

        // Assert: no result is reported and the model state is never reset
        Assert.Empty(Drain(engine));
        Assert.Equal(0, encoder.ResetCount);
    }

    /// <summary>Proves silence after speech finalizes the utterance by the decode-clock rule without resetting the model.</summary>
    [Fact]
    public void OnnxNemotronRecognitionEngine_TryDecode_SilenceAfterSpeech_FinalizesWithoutModelReset()
    {
        // Arrange: an engine scripted to emit "hello", remembering the network reset count
        var (engine, encoder, network) = Create(null, 1, Blank);
        var resetsBefore = network.ResetCount;

        // Act: feed one second of speech followed by three seconds of digital silence
        engine.AcceptSamples(Tone(1));
        engine.AcceptSamples(Silence(3));
        var results = Drain(engine);

        // Assert: a provisional and then a final result, with the encoder and decoder state kept
        Assert.Equal(2, results.Count);
        Assert.False(results[0].IsFinal);
        Assert.True(results[1].IsFinal);
        Assert.Equal("hello", results[1].Text);
        Assert.Equal(0, encoder.ResetCount);
        Assert.Equal(resetsBefore, network.ResetCount);
    }

    /// <summary>Proves paused speech delivered as one block yields one final per utterance.</summary>
    [Fact]
    public void OnnxNemotronRecognitionEngine_TryDecode_PausedSpeechInOneBlock_ReportsOneFinalPerUtterance()
    {
        // Arrange: an engine around loudness-reacting fakes
        var (engine, encoder, _) = CreateLoudness();

        // Act: two half-second utterances separated and followed by 1.5 s pauses, as one block
        engine.AcceptSamples(Join(Tone(0.5), Silence(1.5), Tone(0.5), Silence(1.5)));
        var finals = Drain(engine).Where(r => r.IsFinal).Select(r => r.Text).ToList();

        // Assert: each utterance has its own final and the model was never reset
        Assert.Equal(["hello", "world"], finals);
        Assert.Equal(0, encoder.ResetCount);
    }

    /// <summary>Proves a pause shorter than the after-speech rule keeps one utterance.</summary>
    [Fact]
    public void OnnxNemotronRecognitionEngine_TryDecode_ShortPauseBelowRule_KeepsOneUtterance()
    {
        // Arrange: an engine around loudness-reacting fakes
        var (engine, _, _) = CreateLoudness();

        // Act: two short utterances separated by a 0.6 s pause, then flush
        engine.AcceptSamples(Join(Tone(0.3), Silence(0.6), Tone(0.3)));
        var finalsBeforeFlush = Drain(engine).Count(r => r.IsFinal);
        engine.TryFlush(out var result);

        // Assert: nothing was finalized at the pause; the flush returns both words as one final
        Assert.Equal(0, finalsBeforeFlush);
        Assert.Equal("hello world", result!.Text);
    }

    /// <summary>Proves steady audio that yields no new tokens finalizes after the trailing blank frames reach the rule.</summary>
    [Fact]
    public void OnnxNemotronRecognitionEngine_TryDecode_SteadyAudioWithoutNewTokens_FinalizesAfterTrailingBlanks()
    {
        // Arrange: an engine scripted to emit "hello" once and then only blanks
        var (engine, encoder, _) = Create(null, 1, Blank);

        // Act: feed four seconds of a steady tone that never reads as quiet
        engine.AcceptSamples(Tone(4));
        var results = Drain(engine);

        // Assert: the text was finalized (blank frames are trailing silence) and the encoder state kept
        Assert.Contains(results, r => r.IsFinal && r.Text == "hello");
        Assert.Equal(0, encoder.ResetCount);
    }

    /// <summary>Proves custom endpoint rules change when the utterance is finalized.</summary>
    [Fact]
    public void OnnxNemotronRecognitionEngine_TryDecode_CustomEndpointRules_FinalizesAtConfiguredSilence()
    {
        // Arrange: one engine with a 400 ms after-speech rule and one with the defaults
        var custom = Create(new NemotronEngineOptions(Endpoint: new NemotronEndpointRules(TrailingSilenceAfterSpeechMs: 400)), 1, Blank).Engine;
        var standard = Create(null, 1, Blank).Engine;

        // Act: feed both 0.3 s of speech and 0.6 s of silence
        custom.AcceptSamples(Join(Tone(0.3), Silence(0.6)));
        standard.AcceptSamples(Join(Tone(0.3), Silence(0.6)));
        var customResults = Drain(custom);
        var standardResults = Drain(standard);

        // Assert: only the engine with the short rule has finalized
        Assert.Contains(customResults, r => r.IsFinal && r.Text == "hello");
        Assert.DoesNotContain(standardResults, r => r.IsFinal);
    }

    /// <summary>Proves speech after an endpoint is decoded with the kept model state and no token is lost or repeated.</summary>
    [Fact]
    public void OnnxNemotronRecognitionEngine_TryDecode_SpeechAfterEndpoint_KeepsEveryToken()
    {
        // Arrange: an engine around loudness-reacting fakes
        var (engine, encoder, network) = CreateLoudness();
        var resetsBefore = network.ResetCount;

        // Act: an utterance and a 2 s pause, drained; then another utterance and pause
        engine.AcceptSamples(Join(Tone(0.5), Silence(2)));
        var first = Drain(engine);
        engine.AcceptSamples(Join(Tone(0.5), Silence(2)));
        var second = Drain(engine);
        var flushed = engine.TryFlush(out _);

        // Assert: each utterance's word appears exactly once in its own final, with no model reset
        Assert.Equal("hello", Assert.Single(first, r => r.IsFinal).Text);
        Assert.Equal("world", Assert.Single(second, r => r.IsFinal).Text);
        Assert.False(flushed);
        Assert.Equal(2, network.Emitted);
        Assert.Equal(0, encoder.ResetCount);
        Assert.Equal(resetsBefore, network.ResetCount);
    }

    /// <summary>Proves continuous speech beyond the maximum utterance length is split into several finals without losing words.</summary>
    [Fact]
    public void OnnxNemotronRecognitionEngine_TryDecode_ContinuousTokensBeyondMaximum_SplitsUtterance()
    {
        // Arrange: a 3 s maximum and a network that says "hello" on every 280 ms frame
        var (engine, network) = CreateContinuousWords(3000);

        // Act: ten seconds of continuous speech, then flush the rest
        engine.AcceptSamples(Tone(10));
        var finals = Drain(engine).Where(r => r.IsFinal).Select(r => r.Text).ToList();
        engine.TryFlush(out var rest);
        finals.Add(rest!.Text);

        // Assert: several finals, none much longer than 3 s of words, and every word kept exactly once
        var words = finals.SelectMany(f => f.Split(' ')).ToList();
        Assert.True(finals.Count >= 3, $"Expected at least 3 finals, got {finals.Count}.");
        Assert.All(finals, f => Assert.True(f.Split(' ').Length <= 12, $"Final too long: {f}"));
        Assert.Equal(network.Advanced.Count / 2, words.Count);
    }

    /// <summary>Proves the maximum-length split never cuts a word made of several tokens.</summary>
    [Fact]
    public void OnnxNemotronRecognitionEngine_TryDecode_MaximumLengthSplit_KeepsWordsWhole()
    {
        // Arrange: a 1 s maximum and a network that says the two-piece word "hel" + "lo" on every frame
        var (engine, _) = CreateContinuousWords(1000);

        // Act: five seconds of continuous speech
        engine.AcceptSamples(Tone(5));
        var finals = Drain(engine).Where(r => r.IsFinal).Select(r => r.Text).ToList();

        // Assert: there are several finals and every word in them is the whole word
        Assert.True(finals.Count >= 2, $"Expected at least 2 finals, got {finals.Count}.");
        Assert.All(finals.SelectMany(f => f.Split(' ')), word => Assert.Equal("hello", word));
    }

    /// <summary>Proves silence alone never produces text, through decoding and flushing.</summary>
    [Fact]
    public void OnnxNemotronRecognitionEngine_TryFlush_SilenceOnly_ReportsNoText()
    {
        // Arrange: an engine around loudness-reacting fakes
        var (engine, _, network) = CreateLoudness();

        // Act: five seconds of digital silence, then flush
        engine.AcceptSamples(Silence(5));
        var decoded = Drain(engine);
        var flushed = engine.TryFlush(out var result);

        // Assert: no result at all and no word was ever predicted
        Assert.Empty(decoded);
        Assert.False(flushed);
        Assert.Null(result);
        Assert.Equal(0, network.Emitted);
    }

    /// <summary>Proves the flush padding is silence: a pause followed by the flush adds no invented word.</summary>
    [Fact]
    public void OnnxNemotronRecognitionEngine_TryFlush_SpeechThenSilence_AddsNoWordsFromPadding()
    {
        // Arrange: an engine around loudness-reacting fakes (all-zero log-mel padding would read
        // as a new loud sound after the pause and produce a second word)
        var (engine, _, network) = CreateLoudness();

        // Act: half a second of speech, a 0.3 s pause (below the limiter cap), then flush
        engine.AcceptSamples(Join(Tone(0.5), Silence(0.3)));
        var flushed = engine.TryFlush(out var result);

        // Assert: the final holds only the spoken word
        Assert.True(flushed);
        Assert.Equal("hello", result!.Text);
        Assert.Equal(1, network.Emitted);
    }

    /// <summary>Proves the partial chunk and the tail chunk are completed with silence-level features, never zero rows.</summary>
    [Fact]
    public void OnnxNemotronRecognitionEngine_TryFlush_PartialChunk_PadsWithSilenceFeatures()
    {
        // Arrange: an engine around loudness-reacting fakes
        var (engine, encoder, _) = CreateLoudness();

        // Act: a fifth of a second of speech (a partial chunk), then flush
        engine.AcceptSamples(Tone(0.2));
        engine.TryFlush(out _);

        // Assert: two chunks were encoded and every row after the speech is at silence level
        Assert.Equal(2, encoder.EncodeCalls);
        Assert.Equal(2 * NemotronEngineOptions.ChunkFrames, encoder.RowMaxima.Count);
        Assert.All(encoder.RowMaxima.Skip(25), max => Assert.True(max < -10f, $"Padding row max {max} is not silence."));
    }

    /// <summary>Proves invalid engine options are rejected.</summary>
    [Fact]
    public void OnnxNemotronRecognitionEngine_Constructor_InvalidOptions_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: valid collaborators
        var vocabulary = new NemotronVocabulary(Words);

        // Act / Assert: a negative tail chunk count and a negative endpoint rule are rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => new OnnxNemotronRecognitionEngine(
            new FakeNemotronEncoder(),
            new NemotronRnntGreedyDecoder(new FakeRnntNetwork(Blank)),
            vocabulary,
            new NemotronEngineOptions(FlushTailChunks: -1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OnnxNemotronRecognitionEngine(
            new FakeNemotronEncoder(),
            new NemotronRnntGreedyDecoder(new FakeRnntNetwork(Blank)),
            vocabulary,
            new NemotronEngineOptions(Endpoint: new NemotronEndpointRules(TrailingSilenceAfterSpeechMs: -1))));
    }

    /// <summary>Proves Reset returns the engine to the start-of-stream state: a new stream gives the same results as a fresh engine.</summary>
    [Fact]
    public void Reset_AfterAudio_NextStreamMatchesFreshEngine()
    {
        // Arrange: an engine holding state from earlier audio (a decoded chunk with a pending
        // provisional result plus half a second of not-yet-encoded audio), and a fresh reference
        var (engine, encoder, network) = Create(null, 1, Blank);
        engine.AcceptSamples(Tone(1.5));
        Drain(engine);
        network.Enqueue(1, Blank);
        var encodeCallsBeforeReset = encoder.EncodeCalls;
        var (fresh, freshEncoder, _) = Create(null, 1, Blank);

        // Act: reset the engine, then give both engines the same new audio
        engine.Reset();
        Assert.False(engine.TryDecode(out _));
        engine.AcceptSamples(Tone(1));
        fresh.AcceptSamples(Tone(1));
        var afterReset = Drain(engine);
        var expected = Drain(fresh);

        // Assert: the reset engine reports the same results and does the same amount of work as the
        // fresh engine, so nothing of the earlier stream leaks into the new one
        Assert.Equal(
            expected.Select(r => (r.Text, r.IsFinal)),
            afterReset.Select(r => (r.Text, r.IsFinal)));
        Assert.Equal(freshEncoder.EncodeCalls, encoder.EncodeCalls - encodeCallsBeforeReset);
        Assert.Equal(freshEncoder.LastFrameCount, encoder.LastFrameCount);
        Assert.Equal(freshEncoder.LastFeatureLength, encoder.LastFeatureLength);
    }

    /// <summary>Proves Dispose disposes the encoder and network, is idempotent, and later calls throw.</summary>
    [Fact]
    public void Dispose_DisposesCollaboratorsAndBlocksUse()
    {
        // Arrange: an engine around fakes
        var (engine, encoder, network) = Create();

        // Act: dispose twice
        engine.Dispose();
        engine.Dispose();

        // Assert: collaborators are disposed and every operation throws
        Assert.True(encoder.Disposed);
        Assert.True(network.Disposed);
        Assert.Throws<ObjectDisposedException>(() => engine.AcceptSamples(new float[10]));
        Assert.Throws<ObjectDisposedException>(() => engine.TryDecode(out _));
        Assert.Throws<ObjectDisposedException>(() => engine.TryFlush(out _));
        Assert.Throws<ObjectDisposedException>(engine.Reset);
    }

    /// <summary>Proves the engine declares the 16 kHz sample rate the model consumes.</summary>
    [Fact]
    public void SampleRate_Is16kHz()
    {
        // Arrange: no setup is needed for the static rate
        // Act: read the declared rate
        var rate = OnnxNemotronRecognitionEngine.SampleRate;

        // Assert: it is 16 kHz
        Assert.Equal(16000, rate);
    }

    /// <summary>Proves null collaborators are rejected.</summary>
    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        // Arrange: valid collaborators to combine with one null at a time
        var vocabulary = new NemotronVocabulary(Words);
        var decoder = new NemotronRnntGreedyDecoder(new FakeRnntNetwork(Blank));

        // Act and Assert: each null argument is rejected
        Assert.Throws<ArgumentNullException>(() => new OnnxNemotronRecognitionEngine(null!, decoder, vocabulary));
        Assert.Throws<ArgumentNullException>(() => new OnnxNemotronRecognitionEngine(new FakeNemotronEncoder(), null!, vocabulary));
        Assert.Throws<ArgumentNullException>(() => new OnnxNemotronRecognitionEngine(new FakeNemotronEncoder(), decoder, null!));
    }
}
