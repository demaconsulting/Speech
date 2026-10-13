using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

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

    /// <summary>Proves a token stuck in a loop is capped at the configured run length and then broken.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_StuckToken_IsCappedAtRunLength()
    {
        // Arrange
        var networks = new FakeNetworks([100], repeatLast: true);
        using var engine = Create(networks, maxNewTokens: 13, maxRepeats: 12);

        // Act
        var result = engine.GenerateDetailed("Hello there.", 0);
        var speech = SpeechTokens(networks, result);

        // Assert
        Assert.Equal(13, speech.Length);
        Assert.All(speech.Take(12), token => Assert.Equal(100, token));
        Assert.NotEqual(100, speech[12]);
    }

    /// <summary>Proves no run anywhere exceeds the cap because the block applies per step.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_StuckToken_NeverExceedsCapAnywhere()
    {
        // Arrange
        var networks = new FakeNetworks([100], repeatLast: true);
        using var engine = Create(networks, maxNewTokens: 100, ratio: 100, maxRepeats: 3);

        // Act
        var result = engine.GenerateDetailed("Hello there.", 0);
        var speech = SpeechTokens(networks, result);

        // Assert
        var longest = 0;
        var run = 0;
        for (var i = 0; i < speech.Length; i++)
        {
            run = i > 0 && speech[i] == speech[i - 1] ? run + 1 : 1;
            longest = Math.Max(longest, run);
        }

        Assert.Equal(100, speech.Length);
        Assert.Equal(3, longest);
    }

    /// <summary>Proves a cap of zero disables the guard.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_CapZero_AllowsUnboundedRepeats()
    {
        // Arrange
        var networks = new FakeNetworks([100], repeatLast: true);
        using var engine = Create(networks, maxNewTokens: 50, maxRepeats: 0);

        // Act
        var result = engine.GenerateDetailed("Hello there.", 0);

        // Assert
        Assert.All(SpeechTokens(networks, result), token => Assert.Equal(100, token));
        Assert.Equal(50, result.SpeechTokenCount);
    }

    /// <summary>Proves a run that reaches the repeat cap still ends normally, at the stop token, and is not flagged truncated.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_StopAfterCappedRun_EndsWithoutTruncation()
    {
        // Arrange
        var networks = new FakeNetworks([100, 100, OnnxChatterboxTurboSynthesisEngine.StopSpeechToken]);
        using var engine = Create(networks, maxRepeats: 2);

        // Act
        var result = engine.GenerateDetailed("Hello there.", 0);

        // Assert
        Assert.False(result.HitTokenLimit);
        Assert.Equal(2, result.SpeechTokenCount);
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
        // Arrange: voices with distinct sample values so the encoded clip identifies the voice that was used
        var networks = new FakeNetworks([OnnxChatterboxTurboSynthesisEngine.StopSpeechToken], repeatLast: true);
        using var engine = CreateWith(networks, ConstantVoice("first", 0.1f), ConstantVoice("second", 0.2f));

        // Act
        engine.Generate("One.", 1.0f, 0);
        engine.Generate("Two.", 1.0f, 0);
        engine.Generate("Three.", 1.0f, 1);
        engine.Generate("Four.", 1.0f, 99);
        engine.Generate("Five.", 1.0f, -1);

        // Assert: one encode per voice, in first-use order, each with its own clip
        Assert.Equal(2, networks.EncodeCount);
        Assert.Equal(0.1f, networks.EncodeInputs[0][0], 3);
        Assert.Equal(0.2f, networks.EncodeInputs[1][0], 3);
    }

    /// <summary>Proves an unknown speaker id is spoken with the first voice rather than another voice.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_UnknownSpeaker_UsesFirstVoiceClip()
    {
        // Arrange
        var networks = new FakeNetworks([OnnxChatterboxTurboSynthesisEngine.StopSpeechToken], repeatLast: true);
        using var engine = CreateWith(networks, ConstantVoice("first", 0.1f), ConstantVoice("second", 0.2f));

        // Act
        engine.Generate("Hi.", 1.0f, 7);

        // Assert
        Assert.Single(networks.EncodeInputs);
        Assert.Equal(0.1f, networks.EncodeInputs[0][0], 3);
    }

    /// <summary>Proves the speed argument is ignored, since the model exposes no speed control.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_Speed_IsIgnored()
    {
        // Arrange
        var networks = new FakeNetworks([100, OnnxChatterboxTurboSynthesisEngine.StopSpeechToken]);
        using var engine = Create(networks);

        // Act
        var slow = engine.Generate("Hello there.", 0.5f, 0);
        var fast = engine.Generate("Hello there.", 2.0f, 0);

        // Assert: the decoder echoes the tokens, so identical audio proves speed had no influence
        Assert.Equal(slow.Samples.ToArray(), fast.Samples.ToArray());
        Assert.Equal(slow.SampleRate, fast.SampleRate);
    }

    /// <summary>Proves disposing during a generation stops it at its next step, waits for it, and releases the networks once.</summary>
    [Fact]
    public async Task OnnxChatterboxTurboSynthesisEngine_Dispose_DuringGeneration_StopsGenerationAndWaitsForIt()
    {
        // Arrange: the first language-model step blocks until released
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var networks = new FakeNetworks([100], repeatLast: true)
        {
            OnStep = () =>
            {
                entered.Set();
                release.Wait();
            },
        };
        var engine = Create(networks);
        var cancellation = TestContext.Current.CancellationToken;
        var generation = Task.Run(() => engine.Generate("Hello there.", 1.0f, 0), cancellation);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(30), cancellation));

        // Act: dispose from another thread; it must wait for the in-flight generation rather than free the networks under it
        var disposal = Task.Run(engine.Dispose, cancellation);
        await Assert.ThrowsAsync<TimeoutException>(() => disposal.WaitAsync(TimeSpan.FromMilliseconds(300), cancellation));
        Assert.Equal(0, networks.DisposeCount);
        release.Set();

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(() => generation);
        await disposal;
        Assert.Equal(1, networks.DisposeCount);
        Assert.Single(networks.Steps);
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
        Assert.Throws<ArgumentNullException>(() => new OnnxChatterboxTurboSynthesisEngine(null!, Tokenizer, voices, 10, 1, 12));
        Assert.Throws<ArgumentNullException>(() => new OnnxChatterboxTurboSynthesisEngine(networks, null!, voices, 10, 1, 12));
        Assert.Throws<ArgumentException>(() => new OnnxChatterboxTurboSynthesisEngine(networks, Tokenizer, [], 10, 1, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OnnxChatterboxTurboSynthesisEngine(networks, Tokenizer, voices, 0, 1, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OnnxChatterboxTurboSynthesisEngine(networks, Tokenizer, voices, 10, 0, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OnnxChatterboxTurboSynthesisEngine(networks, Tokenizer, voices, 10, 1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OnnxChatterboxTurboSynthesisEngine(networks, Tokenizer, voices, 10, double.NaN, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => new OnnxChatterboxTurboSynthesisEngine(networks, Tokenizer, voices, 10, double.PositiveInfinity, 12));
    }

    /// <summary>Proves a very large (but finite) ratio is clamped to the absolute cap instead of overflowing.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_HugeRatio_ClampsToMaxNewTokens()
    {
        // Arrange
        var networks = new FakeNetworks([100], repeatLast: true);
        using var engine = new OnnxChatterboxTurboSynthesisEngine(networks, Tokenizer, [Voice("a")], 5, double.MaxValue, 0);

        // Act
        var result = engine.GenerateDetailed("Hello there.", 0);

        // Assert
        Assert.True(result.HitTokenLimit);
        Assert.Equal(5, networks.Steps.Count);
    }

    /// <summary>Proves null text is rejected.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_NullText_ThrowsArgumentNullException()
    {
        // Arrange
        var networks = new FakeNetworks([100]);
        using var engine = Create(networks);

        // Assert
        Assert.Throws<ArgumentNullException>(() => engine.GenerateDetailed(null!, 0));
    }

    /// <summary>Proves a marked segment encodes the voice's mood clip, never tokenizes the marker, and caches per mood.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_MoodMarker_UsesMoodClipOnceAndHidesMarker()
    {
        // Arrange
        var networks = new FakeNetworks([OnnxChatterboxTurboSynthesisEngine.StopSpeechToken], repeatLast: true);
        using var engine = CreateWith(networks, MoodVoice("v0", "sad"));
        var plainIds = Tokenizer.Encode("Hello there.");

        // Act
        engine.Generate(ChatterboxTurboCapabilityProfile.WithMoodMarker("sad", "Hello there."), 1.0f, 0);
        engine.Generate(ChatterboxTurboCapabilityProfile.WithMoodMarker("sad", "Hello there."), 1.0f, 0);
        engine.Generate("Hello there.", 1.0f, 0);

        // Assert: one mood encode (0.5 samples) and one base encode (zeros); the tokenizer input is the plain text
        Assert.Equal(2, networks.EncodeCount);
        Assert.Equal(0.5f, networks.EncodeInputs[0][0]);
        Assert.Equal(0f, networks.EncodeInputs[1][0]);
        Assert.All(networks.EmbedCalls.Where(call => call.Length > 1), call => Assert.Equal(plainIds, call));
    }

    /// <summary>Proves a mood the voice lacks, and a voice with no moods, fall back to the shared base clip.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_MoodMissing_FallsBackToBaseClip()
    {
        // Arrange
        var networks = new FakeNetworks([OnnxChatterboxTurboSynthesisEngine.StopSpeechToken], repeatLast: true);
        using var engine = CreateWith(networks, MoodVoice("v0", "sad"), Voice("plain"));

        // Act
        engine.Generate(ChatterboxTurboCapabilityProfile.WithMoodMarker("anger", "One."), 1.0f, 0);
        engine.Generate(ChatterboxTurboCapabilityProfile.WithMoodMarker("sad", "Two."), 1.0f, 1);
        engine.Generate(ChatterboxTurboCapabilityProfile.WithMoodMarker("calm", "Three."), 1.0f, 1);

        // Assert: both base encodes (one per voice), no mood clip used, and the base entry is shared per voice
        Assert.Equal(2, networks.EncodeCount);
        Assert.All(networks.EncodeInputs, input => Assert.Equal(0f, input[0]));
    }

    /// <summary>Proves a marker-only or marker-plus-whitespace text yields empty audio without network calls.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_MarkerOnlyText_ReturnsEmptyAudio()
    {
        // Arrange
        var networks = new FakeNetworks([100]);
        using var engine = Create(networks);

        // Act
        var audio = engine.Generate(ChatterboxTurboCapabilityProfile.WithMoodMarker("sad", "  "), 1.0f, 0);

        // Assert
        Assert.Empty(audio.Samples);
        Assert.Equal(0, networks.EncodeCount);
    }

    /// <summary>Proves the repeat cap and token bound apply to each marked segment independently.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_MarkedSegments_GuardsApplyPerSegment()
    {
        // Arrange
        var networks = new FakeNetworks([100], repeatLast: true);
        using var engine = CreateWith(networks, maxNewTokens: 13, maxRepeats: 12, MoodVoice("v0", "sad"));

        // Act
        var first = engine.GenerateDetailed(ChatterboxTurboCapabilityProfile.WithMoodMarker("sad", "Hello there."), 0);
        var firstSpeech = SpeechTokens(networks, first);
        var second = engine.GenerateDetailed(ChatterboxTurboCapabilityProfile.WithMoodMarker("sad", "Hello there."), 0);

        // Assert
        Assert.Equal(13, firstSpeech.Length);
        Assert.Equal(13, second.SpeechTokenCount);
        Assert.NotEqual(100, firstSpeech[12]);
    }

    /// <summary>Proves a profile-rendered plan is consumed in order and no marker character reaches the tokenizer.</summary>
    [Fact]
    public void OnnxChatterboxTurboSynthesisEngine_Generate_RenderedPlan_OrderedClipsAndNoMarkerInTokens()
    {
        // Arrange
        var networks = new FakeNetworks([OnnxChatterboxTurboSynthesisEngine.StopSpeechToken], repeatLast: true);
        using var engine = CreateWith(networks, MoodVoice("v0", "sad", "shout"));
        var profile = new ChatterboxTurboCapabilityProfile(ChatterboxTurboEndOfSentenceTagPolicy.Strip);
        var spans = new[]
        {
            TaggedTextSpan.PlainText("Plain start. "),
            TaggedTextSpan.TagSpan(NaturalLanguageAudioTag.Sad),
            TaggedTextSpan.PlainText(" So sad. "),
            TaggedTextSpan.TagSpan(NaturalLanguageAudioTag.Loud),
            TaggedTextSpan.PlainText(" Now loud."),
        };

        // Act
        var plan = profile.Render(spans, new OnnxChatterboxTurboEnglishSynthesisModel());
        foreach (var segment in plan.Segments)
        {
            engine.Generate(segment.Text, 1.0f, 0);
        }

        // Assert: base, sad and shout clips are encoded in order of first use; marker never tokenized
        Assert.Equal(3, plan.Segments.Count);
        Assert.Equal(3, networks.EncodeCount);
        Assert.Equal([0f, 0.5f, 0.25f], networks.EncodeInputs.Select(input => input[0]));
        Assert.All(
            networks.EmbedCalls.Where(call => call.Length > 1),
            call => Assert.DoesNotContain(call, id => id >= 0xE000 && id <= 0xE200));
        Assert.Equal(Tokenizer.Encode("Plain start."), networks.EmbedCalls[0]);
    }

    /// <summary>Extracts the generated speech tokens from the last decoder input.</summary>
    private static long[] SpeechTokens(FakeNetworks networks, ChatterboxTurboGenerationResult result) =>
        networks.DecodedTokens[1..(1 + result.SpeechTokenCount)];

    /// <summary>Creates an engine over fake networks.</summary>
    private static OnnxChatterboxTurboSynthesisEngine Create(
        FakeNetworks networks,
        int maxNewTokens = 1024,
        double ratio = 25.0,
        int voiceCount = 1,
        int maxRepeats = 12) =>
        new(networks, Tokenizer, [.. Enumerable.Range(0, voiceCount).Select(i => Voice("v" + i))], maxNewTokens, ratio, maxRepeats);

    /// <summary>Creates a one-second voice with mood clips whose samples are 0.5 (first mood) or 0.25 (second).</summary>
    private static ChatterboxTurboReferenceVoice MoodVoice(string id, params string[] moods)
    {
        var voice = Voice(id);
        for (var i = 0; i < moods.Length; i++)
        {
            voice = voice.WithMoodSamples(moods[i], Enumerable.Repeat(0.5f / (1 << i), 24000).ToArray(), 24000);
        }

        return voice;
    }

    /// <summary>Creates an engine over the given voices.</summary>
    private static OnnxChatterboxTurboSynthesisEngine CreateWith(
        FakeNetworks networks,
        params ChatterboxTurboReferenceVoice[] voices) =>
        new(networks, Tokenizer, voices, 1024, 25.0, 12);

    /// <summary>Creates an engine over the given voices with explicit guards.</summary>
    private static OnnxChatterboxTurboSynthesisEngine CreateWith(
        FakeNetworks networks,
        int maxNewTokens,
        int maxRepeats,
        params ChatterboxTurboReferenceVoice[] voices) =>
        new(networks, Tokenizer, voices, maxNewTokens, 25.0, maxRepeats);

    /// <summary>Creates a one-second voice whose every sample has the given value.</summary>
    private static ChatterboxTurboReferenceVoice ConstantVoice(string id, float value) =>
        ChatterboxTurboReferenceVoice.FromSamples(id, Enumerable.Repeat(value, 24000).ToArray(), 24000);

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

        /// <summary>Gets the reference audio passed to each encode call.</summary>
        public List<float[]> EncodeInputs { get; } = [];

        /// <summary>Gets or sets an action run at the start of every language-model step.</summary>
        public Action? OnStep { get; set; }

        /// <summary>Gets the number of dispose calls.</summary>
        public int DisposeCount { get; private set; }

        /// <inheritdoc/>
        public ChatterboxTurboConditioning Encode(float[] referenceAudio)
        {
            EncodeCount++;
            EncodeInputs.Add(referenceAudio);
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
                _owner.OnStep?.Invoke();
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
