using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="OnnxChatterboxTurboSynthesisEngine"/>, run against a hand-written fake of the
///     graph seam so the generation algorithm is verified without any ONNX model.
/// </summary>
public sealed class OnnxChatterboxTurboSynthesisEngineTests
{
    /// <summary>The embedded tokenizer.</summary>
    private static readonly ByteLevelBpeTokenizer Tokenizer = OnnxChatterboxTurboEnglishSynthesisModel.LoadTokenizer();

    /// <summary>Proves the loop's prompt assembly, step arguments, stop handling and decoder input.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_ScriptedTokens_AssemblesPromptAndDecoderInput()
    {
        // Arrange
        var networks = new FakeNetworks([100, 200, OnnxChatterboxTurboSynthesisEngine.StopSpeechToken]);
        using var engine = Create(networks);
        var textIds = Tokenizer.Encode("Hello there.").Length;

        // Act
        var result = engine.GenerateDetailed("Hello there.", 0);

        // Assert
        Assert.False(result.HitTokenLimit);
        Assert.Equal(2, result.SpeechTokenCount);
        Assert.Equal(OnnxChatterboxTurboSynthesisEngine.OutputSampleRate, result.Audio.SampleRate);
        Assert.Equal(3, networks.Steps.Count);
        Assert.Equal((1 + textIds, 1 + textIds, 0), networks.Steps[0]);
        Assert.Equal((1, 2 + textIds, 1 + textIds), networks.Steps[1]);
        Assert.Equal((1, 3 + textIds, 2 + textIds), networks.Steps[2]);
        Assert.Equal([100L], networks.EmbedCalls[1]);
        Assert.Equal(
            [7L, 100L, 200L, 4299L, 4299L, 4299L],
            networks.DecodedTokens);
    }

    /// <summary>Proves generation is bounded and the result reports the limit when the model never stops.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_NeverStops_IsBoundedAndFlagged()
    {
        // Arrange
        var networks = new FakeNetworks([100], repeatLast: true);
        using var engine = Create(networks, maxNewTokens: 5);

        // Act
        var result = engine.GenerateDetailed("Hello there.", 0);

        // Assert
        Assert.True(result.HitTokenLimit);
        Assert.Equal(5, networks.Steps.Count);
        Assert.Equal(5, result.SpeechTokenCount);
    }

    /// <summary>Proves the length-proportional bound stops a runaway earlier than the hard cap.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_ProportionalBound_StopsRunawayEarly()
    {
        // Arrange
        var networks = new FakeNetworks([100], repeatLast: true);
        using var engine = Create(networks, maxNewTokens: 100000, ratio: 1.0);
        var textIds = Tokenizer.Encode("Hi.").Length;

        // Act
        var result = engine.GenerateDetailed("Hi.", 0);

        // Assert
        Assert.True(result.HitTokenLimit);
        Assert.Equal(textIds + 32, networks.Steps.Count);
    }

    /// <summary>Proves empty text yields empty audio without touching the networks.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_EmptyText_ReturnsEmptyAudioWithoutNetworkCalls()
    {
        // Arrange
        var networks = new FakeNetworks([100]);
        using var engine = Create(networks);

        // Act
        var audio = engine.Generate("   ", 1.0f, 0);

        // Assert
        Assert.Empty(audio.Samples);
        Assert.Equal(0, networks.EncodeCount);
        Assert.Empty(networks.EmbedCalls);
    }

    /// <summary>Proves each voice is encoded once, cached, and an unknown speaker falls back to the first voice.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_Voices_AreEncodedOnceAndFallBack()
    {
        // Arrange
        var networks = new FakeNetworks([OnnxChatterboxTurboSynthesisEngine.StopSpeechToken], repeatLast: true);
        using var engine = Create(networks, voiceCount: 2);

        // Act
        engine.Generate("One.", 1.0f, 0);
        engine.Generate("Two.", 1.0f, 0);
        engine.Generate("Three.", 1.0f, 1);
        engine.Generate("Four.", 1.0f, 99);
        engine.Generate("Five.", 1.0f, -1);

        // Assert
        Assert.Equal(2, networks.EncodeCount);
    }

    /// <summary>Proves disposal releases the networks once and later calls throw.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Dispose_DisposesNetworksAndRejectsGenerate()
    {
        // Arrange
        var networks = new FakeNetworks([100]);
        var engine = Create(networks);

        // Act
        engine.Dispose();
        engine.Dispose();

        // Assert
        Assert.Equal(1, networks.DisposeCount);
        Assert.Throws<ObjectDisposedException>(() => engine.Generate("Hello.", 1.0f, 0));
    }

    /// <summary>Proves invalid constructor arguments are rejected.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Constructor_InvalidArguments_Throw()
    {
        // Arrange
        var networks = new FakeNetworks([100]);
        var voices = new[] { Voice("a") };

        // Assert
        Assert.Throws<ArgumentNullException>(() => new OnnxChatterboxTurboSynthesisEngine(null!, Tokenizer, voices, 10, 1));
        Assert.Throws<ArgumentNullException>(() => new OnnxChatterboxTurboSynthesisEngine(networks, null!, voices, 10, 1));
        Assert.Throws<ArgumentException>(() => new OnnxChatterboxTurboSynthesisEngine(networks, Tokenizer, [], 10, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OnnxChatterboxTurboSynthesisEngine(networks, Tokenizer, voices, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OnnxChatterboxTurboSynthesisEngine(networks, Tokenizer, voices, 10, 0));
    }

    /// <summary>Creates an engine over fake networks.</summary>
    private static OnnxChatterboxTurboSynthesisEngine Create(
        FakeNetworks networks,
        int maxNewTokens = 1024,
        double ratio = 25.0,
        int voiceCount = 1) =>
        new(networks, Tokenizer, [.. Enumerable.Range(0, voiceCount).Select(i => Voice("v" + i))], maxNewTokens, ratio);

    /// <summary>Creates a one-second voice.</summary>
    private static ChatterboxTurboReferenceVoice Voice(string id) =>
        ChatterboxTurboReferenceVoice.FromSamples(id, new float[24000], 24000);

    /// <summary>Hand-written fake of the graph seam recording every call.</summary>
    private sealed class FakeNetworks : IChatterboxTurboNetworks
    {
        /// <summary>The tokens the language model emits in order.</summary>
        private readonly long[] _script;

        /// <summary>True to repeat the last scripted token forever.</summary>
        private readonly bool _repeatLast;

        /// <summary>Initializes a new instance of the <see cref="FakeNetworks"/> class.</summary>
        public FakeNetworks(long[] script, bool repeatLast = false)
        {
            _script = script;
            _repeatLast = repeatLast;
        }

        /// <summary>Gets the recorded step arguments (sequence length, attention length, first position).</summary>
        public List<(int Sequence, int Attention, int Position)> Steps { get; } = [];

        /// <summary>Gets the recorded embed calls.</summary>
        public List<long[]> EmbedCalls { get; } = [];

        /// <summary>Gets the last decoder input.</summary>
        public long[] DecodedTokens { get; private set; } = [];

        /// <summary>Gets the number of encode calls.</summary>
        public int EncodeCount { get; private set; }

        /// <summary>Gets the number of dispose calls.</summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc/>
        public ChatterboxTurboConditioning Encode(float[] referenceAudio)
        {
            EncodeCount++;
            return new ChatterboxTurboConditioning(new float[OnnxChatterboxTurboSynthesisEngine.EmbeddingSize], 1, [7], [1f], [1f, 2f], 1);
        }

        /// <inheritdoc/>
        public float[] Embed(long[] tokenIds)
        {
            EmbedCalls.Add(tokenIds);
            return new float[tokenIds.Length * OnnxChatterboxTurboSynthesisEngine.EmbeddingSize];
        }

        /// <inheritdoc/>
        public IChatterboxTurboLanguageModelState BeginGeneration() => new State(this);

        /// <inheritdoc/>
        public float[] Decode(long[] speechTokens, ChatterboxTurboConditioning conditioning)
        {
            DecodedTokens = speechTokens;
            return new float[speechTokens.Length * 10];
        }

        /// <inheritdoc/>
        public void Dispose() => DisposeCount++;

        /// <summary>Scripted language-model state.</summary>
        private sealed class State : IChatterboxTurboLanguageModelState
        {
            /// <summary>The owner.</summary>
            private readonly FakeNetworks _owner;

            /// <summary>The next scripted position.</summary>
            private int _index;

            /// <summary>Initializes a new instance of the <see cref="State"/> class.</summary>
            public State(FakeNetworks owner) => _owner = owner;

            /// <inheritdoc/>
            public float[] Step(float[] inputEmbeddings, int sequenceLength, int attentionLength, int firstPosition)
            {
                _owner.Steps.Add((sequenceLength, attentionLength, firstPosition));
                var token = _owner._script[Math.Min(_index, _owner._script.Length - 1)];
                if (_index >= _owner._script.Length && !_owner._repeatLast)
                {
                    token = OnnxChatterboxTurboSynthesisEngine.StopSpeechToken;
                }

                _index++;
                var logits = new float[OnnxChatterboxTurboSynthesisEngine.StopSpeechToken + 1];
                logits[token] = 10f;
                return logits;
            }

            /// <inheritdoc/>
            public void Dispose()
            {
            }
        }
    }
}
