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
        var (engine, encoder, _) = Create(null, 1, Blank);

        engine.AcceptSamples(Tone(1));
        var results = Drain(engine);

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
        var (engine, _, _) = Create(null, 1, Blank);
        engine.AcceptSamples(Tone(1));
        Drain(engine);

        engine.AcceptSamples(Tone(1));

        Assert.Empty(Drain(engine));
    }

    /// <summary>Proves no result is produced before a whole chunk is available.</summary>
    [Fact]
    public void TryDecode_LessThanChunk_ReportsNothing()
    {
        var (engine, encoder, _) = Create(null, 1);

        engine.AcceptSamples(Tone(0.2));

        Assert.False(engine.TryDecode(out _));
        Assert.Equal(0, encoder.EncodeCalls);
    }

    /// <summary>Proves empty input is ignored.</summary>
    [Fact]
    public void AcceptSamples_Empty_IsIgnored()
    {
        var (engine, encoder, _) = Create();

        engine.AcceptSamples(ReadOnlySpan<float>.Empty);

        Assert.False(engine.TryDecode(out _));
        Assert.Equal(0, encoder.EncodeCalls);
    }

    /// <summary>Proves flush decodes the partial chunk plus the tail chunk and returns the final text.</summary>
    [Fact]
    public void TryFlush_PartialAudio_ReturnsFinalText()
    {
        var (engine, encoder, _) = Create(null, 1, Blank, 2, Blank);

        engine.AcceptSamples(Tone(0.5));
        var flushed = engine.TryFlush(out var result);

        Assert.True(flushed);
        Assert.True(result!.IsFinal);
        Assert.Equal("hello world", result.Text);
        Assert.True(encoder.EncodeCalls >= 2);
    }

    /// <summary>Proves flush with no audio produces no result and no encoder work.</summary>
    [Fact]
    public void TryFlush_NoAudio_ReturnsFalse()
    {
        var (engine, encoder, _) = Create();

        Assert.False(engine.TryFlush(out var result));
        Assert.Null(result);
        Assert.Equal(0, encoder.EncodeCalls);
    }

    /// <summary>Proves flush with audio but no tokens returns no result.</summary>
    [Fact]
    public void TryFlush_AudioWithoutTokens_ReturnsFalse()
    {
        var (engine, _, _) = Create();

        engine.AcceptSamples(Tone(0.5));

        Assert.False(engine.TryFlush(out _));
    }

    /// <summary>Proves the final tail chunk count is configurable.</summary>
    [Fact]
    public void TryFlush_NoTailChunks_DecodesOnlyAudio()
    {
        var (withTail, encoderWith, _) = Create(null, 1);
        var (withoutTail, encoderWithout, _) = Create(new NemotronEngineOptions(FlushTailChunks: 0), 1);

        withTail.AcceptSamples(Tone(0.5));
        withTail.TryFlush(out _);
        withoutTail.AcceptSamples(Tone(0.5));
        withoutTail.TryFlush(out _);

        Assert.Equal(encoderWithout.EncodeCalls + 1, encoderWith.EncodeCalls);
    }

    /// <summary>Proves a long quiet run after speech finalizes the utterance and resets the model.</summary>
    [Fact]
    public void TryDecode_QuietAfterSpeech_EndpointsAndResets()
    {
        var (engine, encoder, network) = Create(null, 1, Blank);
        var resetsBefore = network.ResetCount;

        engine.AcceptSamples(Tone(1));
        engine.AcceptSamples(new float[3 * Rate]);
        var results = Drain(engine);

        Assert.Equal(2, results.Count);
        Assert.False(results[0].IsFinal);
        Assert.True(results[1].IsFinal);
        Assert.Equal("hello", results[1].Text);
        Assert.True(encoder.ResetCount >= 1);
        Assert.True(network.ResetCount > resetsBefore);
    }

    /// <summary>Proves a partial chunk is completed with silence once the quiet cap is reached, before the endpoint.</summary>
    [Fact]
    public void TryDecode_QuietAfterShortSpeech_ReportsBeforeEndpoint()
    {
        var (engine, _, _) = Create(null, 1, Blank);

        engine.AcceptSamples(Tone(0.3));
        engine.AcceptSamples(new float[Rate]);
        var results = Drain(engine);

        var result = Assert.Single(results);
        Assert.Equal("hello", result.Text);
        Assert.False(result.IsFinal);
    }

    /// <summary>Proves leading silence alone never endpoints (no speech yet).</summary>
    [Fact]
    public void TryDecode_OnlySilence_NeverEndpoints()
    {
        var (engine, encoder, _) = Create(null, 1);

        engine.AcceptSamples(new float[5 * Rate]);

        Assert.Empty(Drain(engine));
        Assert.Equal(0, encoder.ResetCount);
    }

    /// <summary>Proves consecutive token-free chunks after text finalize without resetting model state.</summary>
    [Fact]
    public void TryDecode_EmptyChunksAfterText_FinalizesKeepingState()
    {
        var options = new NemotronEngineOptions(EndpointEmptyChunks: 1);
        var (engine, encoder, _) = Create(options, 1, Blank);

        engine.AcceptSamples(Tone(4));
        var results = Drain(engine);

        Assert.Contains(results, r => r.IsFinal && r.Text == "hello");
        Assert.Equal(0, encoder.ResetCount);
    }

    /// <summary>Proves Reset clears pending state and resets the encoder and network.</summary>
    [Fact]
    public void Reset_ClearsState()
    {
        var (engine, encoder, network) = Create(null, 1, Blank);
        engine.AcceptSamples(Tone(1));

        engine.Reset();

        Assert.False(engine.TryDecode(out _));
        Assert.True(encoder.ResetCount >= 1);
        Assert.True(network.ResetCount >= 2);
    }

    /// <summary>Proves Dispose disposes the encoder and network, is idempotent, and later calls throw.</summary>
    [Fact]
    public void Dispose_DisposesCollaboratorsAndBlocksUse()
    {
        var (engine, encoder, network) = Create();

        engine.Dispose();
        engine.Dispose();

        Assert.True(encoder.Disposed);
        Assert.True(network.Disposed);
        Assert.Throws<ObjectDisposedException>(() => engine.AcceptSamples(new float[10]));
        Assert.Throws<ObjectDisposedException>(() => engine.TryDecode(out _));
        Assert.Throws<ObjectDisposedException>(() => engine.TryFlush(out _));
        Assert.Throws<ObjectDisposedException>(engine.Reset);
    }

    /// <summary>Proves null collaborators are rejected and the sample rate is 16 kHz.</summary>
    [Fact]
    public void Constructor_NullArguments_Throw()
    {
        var vocabulary = new NemotronVocabulary(Words);
        var decoder = new NemotronRnntGreedyDecoder(new FakeRnntNetwork(Blank));

        Assert.Throws<ArgumentNullException>(() => new OnnxNemotronRecognitionEngine(null!, decoder, vocabulary));
        Assert.Throws<ArgumentNullException>(() => new OnnxNemotronRecognitionEngine(new FakeNemotronEncoder(), null!, vocabulary));
        Assert.Throws<ArgumentNullException>(() => new OnnxNemotronRecognitionEngine(new FakeNemotronEncoder(), decoder, null!));
        Assert.Equal(16000, OnnxNemotronRecognitionEngine.SampleRate);
    }
}
