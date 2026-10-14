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

        // Act: feed another second of audio that leaves the utterance text unchanged
        engine.AcceptSamples(Tone(1));

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

    /// <summary>Proves a long quiet run after speech finalizes the utterance and resets the model.</summary>
    [Fact]
    public void TryDecode_QuietAfterSpeech_EndpointsAndResets()
    {
        // Arrange: an engine scripted to emit "hello", remembering the network reset count
        var (engine, encoder, network) = Create(null, 1, Blank);
        var resetsBefore = network.ResetCount;

        // Act: feed one second of speech followed by three seconds of digital silence
        engine.AcceptSamples(Tone(1));
        engine.AcceptSamples(new float[3 * Rate]);
        var results = Drain(engine);

        // Assert: a provisional and then a final result, with the model state reset
        Assert.Equal(2, results.Count);
        Assert.False(results[0].IsFinal);
        Assert.True(results[1].IsFinal);
        Assert.Equal("hello", results[1].Text);
        Assert.True(encoder.ResetCount >= 1);
        Assert.True(network.ResetCount > resetsBefore);
    }

    /// <summary>Proves a quiet run inside a single large block still splits the utterance.</summary>
    [Fact]
    public void TryDecode_QuietInsideOneBlock_SplitsUtterance()
    {
        // Arrange: an engine plus a reference engine that sees only speech and silence
        var (engine, encoder, network) = Create(null, 1, Blank);
        var (reference, referenceEncoder, _) = Create(null, 1, Blank);
        var resetsBefore = network.ResetCount;
        reference.AcceptSamples(Tone(1).Concat(new float[3 * Rate]).ToArray());
        Drain(reference);

        // Act: deliver speech, silence, and more speech as a single block
        engine.AcceptSamples(Tone(1).Concat(new float[3 * Rate]).Concat(Tone(1)).ToArray());
        var results = Drain(engine);

        // Assert: the utterance was finalized at the silence and the later speech was still encoded
        var final = Assert.Single(results, r => r.IsFinal);
        Assert.Equal("hello", final.Text);
        Assert.True(network.ResetCount > resetsBefore);
        Assert.True(encoder.EncodeCalls > referenceEncoder.EncodeCalls);
    }

    /// <summary>Proves a partial chunk is completed with silence once the quiet cap is reached, before the endpoint.</summary>
    [Fact]
    public void TryDecode_QuietAfterShortSpeech_ReportsBeforeEndpoint()
    {
        // Arrange: an engine scripted to emit "hello"
        var (engine, _, _) = Create(null, 1, Blank);

        // Act: feed 0.3 s of speech (a partial chunk) and then one second of silence
        engine.AcceptSamples(Tone(0.3));
        engine.AcceptSamples(new float[Rate]);
        var results = Drain(engine);

        // Assert: the text is reported once as provisional, before the 2500 ms endpoint
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

    /// <summary>Proves consecutive token-free chunks after text finalize without resetting model state.</summary>
    [Fact]
    public void TryDecode_EmptyChunksAfterText_FinalizesKeepingState()
    {
        // Arrange: an engine that finalizes after one token-free chunk
        var options = new NemotronEngineOptions(EndpointEmptyChunks: 1);
        var (engine, encoder, _) = Create(options, 1, Blank);

        // Act: feed four seconds of audio so text exists and later chunks produce no tokens
        engine.AcceptSamples(Tone(4));
        var results = Drain(engine);

        // Assert: a final result was reported and the encoder state was kept
        Assert.Contains(results, r => r.IsFinal && r.Text == "hello");
        Assert.Equal(0, encoder.ResetCount);
    }

    /// <summary>Proves Reset clears pending state and resets the encoder and network.</summary>
    [Fact]
    public void Reset_ClearsState()
    {
        // Arrange: an engine holding a pending provisional result
        var (engine, encoder, network) = Create(null, 1, Blank);
        engine.AcceptSamples(Tone(1));

        // Act: reset the engine
        engine.Reset();

        // Assert: nothing is pending and the encoder and network were reset
        Assert.False(engine.TryDecode(out _));
        Assert.True(encoder.ResetCount >= 1);
        Assert.True(network.ResetCount >= 2);
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
