using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.SynthesisSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests;

/// <summary>
///     System-level tests for the SpeechOnnxChatterboxTurbo system: a host registers the package's model with a Speech
///     <see cref="SpeechModelCatalog"/> through <see cref="SpeechModelCatalogChatterboxTurboExtensions.AddChatterboxTurboModels"/>,
///     loads it through <see cref="SpeechSynthesizerFactory"/> and synthesizes through the Speech library's real pipeline over
///     a synthetic install (tiny ONNX graphs written at test time; no network, audio hardware or real model).
/// </summary>
/// <remarks>
///     The synthetic encoder emits, as the conditioning prompt, the clip's peak amplitude times 1000, and the synthetic
///     decoder echoes its speech tokens as the waveform. The first three samples of every synthesized segment are therefore
///     the prompt tokens (<c>peak * 1000</c> of the reference clip used), and the last three are the silence token, which makes
///     the voice and mood clip actually used observable at the system boundary.
/// </remarks>
[Collection(SessionHookCollection.Name)]
public sealed class SpeechOnnxChatterboxTurboTests : IDisposable
{
    /// <summary>The number of generated speech tokens every synthetic segment is bounded to.</summary>
    private const int MaxNewTokens = 4;

    /// <summary>The number of conditioning prompt tokens the synthetic encoder emits.</summary>
    private const int PromptLength = 3;

    /// <summary>The number of trailing silence tokens the engine appends before decoding.</summary>
    private const int SilenceLength = 3;

    /// <summary>The samples in one synthesized segment of the synthetic install.</summary>
    private const int SegmentSamples = PromptLength + MaxNewTokens + SilenceLength;

    /// <summary>A scratch directory unique to this test instance.</summary>
    private readonly string _root;

    /// <summary>Initializes a fresh scratch directory.</summary>
    public SpeechOnnxChatterboxTurboTests()
    {
        _root = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    /// <summary>Deletes the scratch directory.</summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup only.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>Proves a host registers the model with its catalog through one call that returns the same catalog.</summary>
    [Fact]
    public void SpeechOnnxChatterboxTurbo_SystemIntegration_AddChatterboxTurboModels_RegistersModelAndReturnsCatalog()
    {
        // Arrange
        using var catalog = NewCatalog();

        // Act
        var result = catalog.AddChatterboxTurboModels(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)] });

        // Assert
        Assert.Same(catalog, result);
        var entry = Assert.Single(catalog.Enumerate());
        Assert.Equal("chatterbox-turbo-onnx-en", entry.Id);
        Assert.IsAssignableFrom<ISynthesisModel>(entry.Model);
    }

    /// <summary>Proves the registered model is a downloadable synthesis model with selectable precision variants and voices.</summary>
    [Fact]
    public void SpeechOnnxChatterboxTurbo_SystemIntegration_RegisteredModel_IsDownloadableWithVariantsAndVoices()
    {
        // Arrange
        using var catalog = NewCatalog();
        catalog.AddChatterboxTurboModels(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f), Voice("b", 0.5f)] });
        var model = Assert.IsAssignableFrom<ISynthesisModel>(Assert.Single(catalog.Enumerate()).Model);

        // Act
        var other = new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions
        {
            ReferenceVoices = [Voice("a", 0.25f)],
            LanguageModelVariant = ChatterboxTurboLanguageModelVariant.Fp32,
        });

        // Assert
        Assert.Equal(SpeechModelRole.Synthesis, model.Role);
        Assert.NotEmpty(model.DownloadDescriptor.Files);
        Assert.All(model.DownloadDescriptor.Files, file => Assert.False(string.IsNullOrWhiteSpace(file.Sha256Checksum)));
        Assert.NotEqual(model.Id, other.Id);
        Assert.Equal(["a", "b"], VoiceChoice(model).Options.Select(option => option.Value));
    }

    /// <summary>Proves text is tokenized with no tokenizer in the install: only the four graphs are present.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_EmbeddedTokenizer_SynthesizesWithoutTokenizerDownload()
    {
        // Arrange
        InstallSynthetic();
        var (engine, model) = await LoadAsync(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens });

        // Act
        var speech = await engine.SynthesizeAsync("Hello there, how are you today?", TestContext.Current.CancellationToken);
        await engine.DisposeAsync();

        // Assert
        Assert.DoesNotContain(model.DownloadDescriptor.Files, file => file.RelativeInstallPath.Contains("tokenizer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories), path => path.Contains("tokenizer", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(Assert.Single(speech).Samples);
    }

    /// <summary>Proves the Speech pipeline synthesizes 24 kHz audio from text through the ONNX-Runtime-backed engine.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_SyntheticInstall_SynthesizesThroughSpeechPipeline()
    {
        // Arrange
        InstallSynthetic();
        var (engine, _) = await LoadAsync(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens });

        // Act
        var speech = await engine.SynthesizeAsync("Hello there.", TestContext.Current.CancellationToken);
        await engine.DisposeAsync();

        // Assert
        Assert.True(engine.IsAvailable);
        var segment = Assert.Single(speech);
        Assert.Equal(24000, segment.SampleRate);
        Assert.Equal(SegmentSamples, segment.Samples.Count);
    }

    /// <summary>Proves reference clips of other sample rates and channel counts, in memory or as a WAV file, become selectable voices.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_VoiceCloning_AcceptsAnyRateAndChannelCount()
    {
        // Arrange
        InstallSynthetic();
        var wavPath = Path.Join(_root, "clips", "stereo44.wav");
        WriteWav(wavPath, 0.25f, sampleRate: 44100, channels: 2);
        var stereo48K = ChatterboxTurboReferenceVoice.FromSamples("memory", Enumerable.Repeat(0.25f, 2 * 48000).ToArray(), 48000, 2);
        var file = ChatterboxTurboReferenceVoice.FromFile("file", wavPath);
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [stereo48K, file], MaxNewTokens = MaxNewTokens };

        // Act
        var memory = await SynthesizeAsync(options, "Hello there.", "memory");
        var fromFile = await SynthesizeAsync(options, "Hello there.", "file");

        // Assert: both clips were normalized to mono 24 kHz audio of the same level before conditioning
        Assert.InRange(Prompt(memory), 200, 320);
        Assert.InRange(Prompt(fromFile), 200, 320);
    }

    /// <summary>Proves the engine speaks in the voice cloned from the selected reference clip.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_VoiceConditioning_UsesSelectedVoiceClip()
    {
        // Arrange
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f), Voice("b", 0.5f)], MaxNewTokens = MaxNewTokens };

        // Act
        var defaultVoice = await SynthesizeAsync(options, "Hello there.");
        var first = await SynthesizeAsync(options, "Hello there.", "a");
        var second = await SynthesizeAsync(options, "Hello there.", "b");

        // Assert
        Assert.Equal(250, Prompt(defaultVoice));
        Assert.Equal(250, Prompt(first));
        Assert.Equal(500, Prompt(second));
    }

    /// <summary>Proves voices from the user's voice folder are offered with no host code and are spoken when selected.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_StoreVoices_OfferedByDefaultAndSpoken()
    {
        // Arrange
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };
        using var catalog = NewCatalog();
        using (var library = new ReferenceVoiceLibrary(catalog.Store))
        {
            WriteWav(Path.Join(library.EnsureUserDirectory(), "mine.wav"), 0.5f);
        }

        catalog.AddChatterboxTurboModels(options);
        var model = Assert.IsAssignableFrom<ISynthesisModel>(Assert.Single(catalog.Enumerate()).Model);

        // Act
        var engine = await SpeechSynthesizerFactory.LoadAsync(model, _root, null, Values(model, "mine"), TestContext.Current.CancellationToken);
        var speech = await engine.SynthesizeAsync("Hello there.", TestContext.Current.CancellationToken);
        await engine.DisposeAsync();

        // Assert: the explicit voice stays first and the folder voice follows it
        Assert.Equal(["a", "mine"], VoiceChoice(model).Options.Select(option => option.Value));
        Assert.Equal(500, Prompt(speech));
    }

    /// <summary>Proves a damaged store clip neither prevents the model from loading nor the other voices from being used.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_StoreVoicesResilience_DamagedClipDoesNotBlockOtherVoices()
    {
        // Arrange
        InstallSynthetic();
        using var catalog = NewCatalog();
        using (var library = new ReferenceVoiceLibrary(catalog.Store))
        {
            await File.WriteAllBytesAsync(Path.Join(library.EnsureUserDirectory(), "damaged.wav"), "not a wav file"u8.ToArray(), TestContext.Current.CancellationToken);
        }

        // Registration reads no clip
        catalog.AddChatterboxTurboModels(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens });
        var model = Assert.IsAssignableFrom<ISynthesisModel>(Assert.Single(catalog.Enumerate()).Model);

        // Act: the model still loads and speaks the explicit voice
        var engine = await SpeechSynthesizerFactory.LoadAsync(model, _root, null, Values(model, "a"), TestContext.Current.CancellationToken);
        var speech = await engine.SynthesizeAsync("Hello there.", TestContext.Current.CancellationToken);
        await engine.DisposeAsync();

        // Assert
        Assert.Equal(["a", "damaged"], VoiceChoice(model).Options.Select(option => option.Value));
        Assert.Equal(250, Prompt(speech));
    }

    /// <summary>Proves text after a mood tag is spoken with the voice's mood clip, and the base clip when the voice has none.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_MoodVoices_UseMoodClipWithBaseFallback()
    {
        // Arrange
        InstallSynthetic();
        var sadPath = Path.Join(_root, "clips", "sad.wav");
        WriteWav(sadPath, 0.5f);
        var withSad = Voice("sadly", 0.25f).WithMoodFiles(new Dictionary<string, string> { ["sad"] = sadPath });
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [withSad, Voice("plain", 0.25f)], MaxNewTokens = MaxNewTokens };

        // Act
        var plainText = await SynthesizeAsync(options, "Hello there.", "sadly");
        var moodText = await SynthesizeAsync(options, "[sad] Hello there.", "sadly");
        var fallback = await SynthesizeAsync(options, "[sad] Hello there.", "plain");

        // Assert
        Assert.Equal(250, Prompt(plainText));
        Assert.Equal(500, Prompt(moodText));
        Assert.Equal(250, Prompt(fallback));
    }

    /// <summary>Proves no mood marker, supplied by the caller or produced while preparing the text, reaches the speech.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_MoodMarkerHidden_NeverSpokenOrSynthesized()
    {
        // Arrange
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };

        // Act
        var markerOnly = await SynthesizeAsync(options, "\uE100\uE101");
        var withMarker = await SynthesizeAsync(options, "\uE100Hello there.");
        var plain = await SynthesizeAsync(options, "Hello there.");
        var moodTag = await SynthesizeAsync(options, "[sad] Hello there.");

        // Assert: a caller-supplied marker is stripped and a mood tag adds nothing spoken of its own
        Assert.All(markerOnly, segment => Assert.Empty(segment.Samples));
        Assert.Equal(plain.Select(segment => segment.Samples), withMarker.Select(segment => segment.Samples));
        Assert.Equal(SegmentSamples, Assert.Single(moodTag).Samples.Count);
    }

    /// <summary>Proves tags render as real silence, as inline native sounds, or by omission, and never fail.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_NativeTagRendering_SilenceInlineOrOmitted()
    {
        // Arrange
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };

        // Act
        var sigh = await SynthesizeAsync(options, "Oh well. [sighs] Fine, then.");
        var laugh = await SynthesizeAsync(options, "That was [laughs] really funny today.");
        var omitted = await SynthesizeAsync(options, "Hello [whispers] there my friend.");

        // Assert: a sigh is a real silent segment between two spoken ones
        Assert.Equal(3, sigh.Count);
        Assert.NotEmpty(sigh[0].Samples);
        Assert.Empty(sigh[1].Samples);
        Assert.True(sigh[1].PreSilence + sigh[1].PostSilence > TimeSpan.Zero);
        Assert.NotEmpty(sigh[2].Samples);

        // A native laugh stays inside the sentence it interrupts
        Assert.Equal(SegmentSamples, Assert.Single(laugh).Samples.Count);

        // A tag with no usable sound is omitted: the text is spoken as one ordinary sentence
        Assert.Equal(SegmentSamples, Assert.Single(omitted).Samples.Count);
    }

    /// <summary>Proves a trailing tag is kept next to speech rather than left without speech after it.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_TagPlacement_TrailingTagIsKeptWithSpeech()
    {
        // Arrange
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };

        // Act
        var trailing = await SynthesizeAsync(options, "That was really funny [laughs]");

        // Assert: the tag does not become a segment of its own with no speech after it
        Assert.Equal(SegmentSamples, Assert.Single(trailing).Samples.Count);
    }

    /// <summary>Proves long text is split into several sentences, each synthesized, with no sentence left out.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_LongTextSegmentation_SplitsAndSpeaksEverySentence()
    {
        // Arrange: ten sentences of about 35 characters, well over the 200-character limit in total
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };
        var text = string.Join(' ', Enumerable.Repeat("This is a short test sentence here.", 10));

        // Act
        var speech = await SynthesizeAsync(options, text);

        // Assert
        Assert.True(speech.Count > 1);
        Assert.All(speech, segment => Assert.Equal(SegmentSamples, segment.Samples.Count));
    }

    /// <summary>Proves a generation that never emits end-of-speech is truncated after a bounded amount of work, without an exception.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_RunawayProtection_BoundsGenerationWithoutException()
    {
        // Arrange: the synthetic language model never emits the end-of-speech token
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };

        // Act
        var speech = await SynthesizeAsync(options, "Hello there, this sentence goes on and on without ever ending.");

        // Assert
        Assert.All(speech, segment => Assert.Equal(SegmentSamples, segment.Samples.Count));
    }

    /// <summary>Creates a catalog over a scratch store.</summary>
    private SpeechModelCatalog NewCatalog() =>
        new(new SpeechModelStoreOptions { RootPathOverride = Path.Join(_root, "store") });

    /// <summary>
    ///     Writes the synthetic graphs, replacing the encoder with one whose conditioning prompt is the reference clip's
    ///     peak amplitude times 1000.
    /// </summary>
    private void InstallSynthetic()
    {
        var (encoder, _, _, _) = ChatterboxTurboGraphFixture.Write(Path.Join(_root, "onnx"));
        File.WriteAllBytes(encoder, ClipEchoEncoder());
    }

    /// <summary>Registers the model, loads it through the Speech factory over the synthetic install and returns both.</summary>
    private async Task<(ISpeechSynthesizerEngine Engine, ISynthesisModel Model)> LoadAsync(ChatterboxTurboModelOptions options, string? voice = null)
    {
        using var catalog = NewCatalog();
        catalog.AddChatterboxTurboModels(options);
        var model = Assert.IsAssignableFrom<ISynthesisModel>(Assert.Single(catalog.Enumerate()).Model);
        var engine = await SpeechSynthesizerFactory.LoadAsync(model, _root, null, voice is null ? null : Values(model, voice), TestContext.Current.CancellationToken);
        return (engine, model);
    }

    /// <summary>Loads the model for the options, synthesizes the text with an optional voice and disposes the engine.</summary>
    private async Task<IReadOnlyList<SynthesizedSpeech>> SynthesizeAsync(ChatterboxTurboModelOptions options, string text, string? voice = null)
    {
        var (engine, _) = await LoadAsync(options, voice);
        try
        {
            return await engine.SynthesizeAsync(text, TestContext.Current.CancellationToken);
        }
        finally
        {
            await engine.DisposeAsync();
        }
    }

    /// <summary>Gets the model's voice choice parameter.</summary>
    private static ChoiceParameter VoiceChoice(ISynthesisModel model) =>
        Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));

    /// <summary>Builds a parameter bag selecting a voice.</summary>
    private static Dictionary<string, object> Values(ISynthesisModel model, string voice) =>
        new() { [VoiceChoice(model).Id] = voice };

    /// <summary>Gets the first conditioning prompt token (<c>peak * 1000</c> of the clip used) of the first spoken segment.</summary>
    private static int Prompt(IReadOnlyList<SynthesizedSpeech> speech) =>
        (int)speech.First(segment => segment.Samples.Count > 0).Samples[0];

    /// <summary>Creates a one-second mono 24 kHz voice whose every sample is <paramref name="level"/>.</summary>
    private static ChatterboxTurboReferenceVoice Voice(string id, float level) =>
        ChatterboxTurboReferenceVoice.FromSamples(id, Enumerable.Repeat(level, 24000).ToArray(), 24000);

    /// <summary>Writes a one-second constant-level 16-bit PCM WAV file.</summary>
    private static void WriteWav(string path, float level, int sampleRate = 24000, int channels = 1)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var frames = sampleRate;
        var value = (short)(level * 32768f);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + (frames * channels * 2));
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2);
        writer.Write((short)(channels * 2));
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(frames * channels * 2);
        for (var i = 0; i < frames * channels; i++)
        {
            writer.Write(value);
        }
    }

    /// <summary>Builds a speech encoder whose prompt tokens are the audio's peak amplitude times 1000.</summary>
    private static byte[] ClipEchoEncoder() =>
        new OnnxModelWriter("encoder")
            .Input("audio_values", OnnxModelWriter.Float, "1", "n")
            .FloatInitializer("c_features", [1, 2, ChatterboxTurboGraphFixture.EmbeddingSize], Enumerable.Repeat(0.5f, 2 * ChatterboxTurboGraphFixture.EmbeddingSize).ToArray())
            .FloatInitializer("c_embeddings", [1, 4], [1f, 2f, 3f, 4f])
            .FloatInitializer("c_speaker", [1, 3, 2], [1f, 2f, 3f, 4f, 5f, 6f])
            .FloatInitializer("scale", [1, PromptLength], [1000f, 1000f, 1000f])
            .Node("ReduceMax", ["audio_values"], ["peak"])
            .Node("Mul", ["peak", "scale"], ["scaled"])
            .Node("Cast", ["scaled"], ["audio_tokens"], OnnxModelWriter.IntAttribute("to", OnnxModelWriter.Int64))
            .Node("Identity", ["c_features"], ["audio_features"])
            .Node("Identity", ["c_embeddings"], ["speaker_embeddings"])
            .Node("Identity", ["c_speaker"], ["speaker_features"])
            .Output("audio_features", OnnxModelWriter.Float, "1", "2", "1024")
            .Output("audio_tokens", OnnxModelWriter.Int64, "1", "3")
            .Output("speaker_embeddings", OnnxModelWriter.Float, "1", "4")
            .Output("speaker_features", OnnxModelWriter.Float, "1", "3", "2")
            .Build();
}
