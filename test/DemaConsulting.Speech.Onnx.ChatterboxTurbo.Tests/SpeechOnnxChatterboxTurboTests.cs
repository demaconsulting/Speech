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
        var model = Assert.IsAssignableFrom<ISynthesisModel>(entry.Model);

        // The supplied reference voice is registered together with the model
        Assert.Equal(["a"], VoiceChoice(model).Options.Select(option => option.Value));
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
        var (loaded, model) = await LoadAsync(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens });
        await using var engine = loaded;

        // Act
        var speech = await engine.SynthesizeAsync("Hello there, how are you today?", TestContext.Current.CancellationToken);

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
        var (loaded, _) = await LoadAsync(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens });
        await using var engine = loaded;

        // Act
        var speech = await engine.SynthesizeAsync("Hello there.", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(engine.IsAvailable);
        var segment = Assert.Single(speech);
        Assert.Equal(24000, segment.SampleRate);
        Assert.Equal(SegmentSamples, segment.Samples.Count);
        Assert.All(segment.Samples, sample => Assert.True(float.IsFinite(sample)));
    }

    /// <summary>
    ///     Proves the package runs on the CPU without depending on an accelerator: the loaded runtime offers the CPU
    ///     provider, no accelerator provider library ships with the package, and synthesis still succeeds. Providers the
    ///     runtime itself lists (such as CoreML on macOS) are not our concern and are not asserted against.
    /// </summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_SyntheticInstall_RunsOnCpuOnly()
    {
        // Arrange
        InstallSynthetic();
        var (loaded, _) = await LoadAsync(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens });
        await using var engine = loaded;

        // Act
        var speech = await engine.SynthesizeAsync("Hello there.", TestContext.Current.CancellationToken);
        var providers = Microsoft.ML.OnnxRuntime.OrtEnv.Instance().GetAvailableProviders();
        var acceleratorLibraries = Directory.EnumerateFiles(AppContext.BaseDirectory, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Where(name => name is not null &&
                (name.Contains("providers_cuda", StringComparison.OrdinalIgnoreCase) ||
                 name.Contains("providers_tensorrt", StringComparison.OrdinalIgnoreCase) ||
                 name.Contains("providers_openvino", StringComparison.OrdinalIgnoreCase) ||
                 name.Contains("DirectML", StringComparison.OrdinalIgnoreCase) ||
                 name.Contains("providers_coreml", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        // Assert: the CPU provider is present, the package ships no accelerator provider library, and synthesis succeeded
        // cspell:ignore tensorrt coreml
        Assert.Contains("CPUExecutionProvider", providers);
        Assert.Empty(acceleratorLibraries);
        Assert.NotEmpty(Assert.Single(speech).Samples);
    }

    /// <summary>Proves reference clips of other sample rates and channel counts, in memory or as a WAV or FLAC file, become selectable voices.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_VoiceCloning_AcceptsAnyRateAndChannelCount()
    {
        // Arrange
        InstallSynthetic();
        var wavPath = Path.Join(_root, "clips", "stereo44.wav");
        WriteWav(wavPath, 0.25f, sampleRate: 44100, channels: 2);
        var flacPath = Path.Join(_root, "clips", "mono16.flac");
        Directory.CreateDirectory(Path.GetDirectoryName(flacPath)!);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "voice-mono-16-16000.flac"), flacPath);
        var stereo48K = ChatterboxTurboReferenceVoice.FromSamples("memory", Enumerable.Repeat(0.25f, 2 * 48000).ToArray(), 48000, 2);
        var file = ChatterboxTurboReferenceVoice.FromFile("file", wavPath);
        var flac = ChatterboxTurboReferenceVoice.FromFile("flac", flacPath);
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [stereo48K, file, flac], MaxNewTokens = MaxNewTokens };

        // Act
        var memory = await SynthesizeAsync(options, "Hello there.", "memory");
        var fromFile = await SynthesizeAsync(options, "Hello there.", "file");
        var fromFlac = await SynthesizeAsync(options, "Hello there.", "flac");

        // Assert: both clips were normalized to mono 24 kHz audio of the same level before conditioning
        Assert.InRange(Prompt(memory), 200, 320);
        Assert.InRange(Prompt(fromFile), 200, 320);

        // The FLAC clip is conditioned on exactly the audio the voice decoded and normalized from that file
        Assert.Equal((int)(flac.Samples.Span.ToArray().Max() * 1000f), Prompt(fromFlac));
        Assert.True(Prompt(fromFlac) > 0);
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
        var (loaded, _) = await LoadAsync(model, Values(model, "mine"));
        await using var engine = loaded;
        var speech = await engine.SynthesizeAsync("Hello there.", TestContext.Current.CancellationToken);

        // Assert: the explicit voice stays first and the folder voice follows it
        Assert.Equal(["a", "mine"], VoiceChoice(model).Options.Select(option => option.Value));
        Assert.Equal(500, Prompt(speech));
    }

    /// <summary>Proves voices of an installed voice pack are offered with no host code and are spoken when selected.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_StoreVoices_InstalledPackVoicesOfferedAndSpoken()
    {
        // Arrange: a pack's current voices directory holds one clip
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };
        using var catalog = NewCatalog();
        using (var library = new ReferenceVoiceLibrary(catalog.Store))
        {
            var packVoices = Path.Join(library.RootPath, ReferenceVoicePack.VoiceZero10.Id, "current", "voices");
            WriteWav(Path.Join(packVoices, "pack-voice.wav"), 0.75f);

            // The pack counts as installed once its install manifest exists
            await File.WriteAllTextAsync(
                Path.Join(library.RootPath, ReferenceVoicePack.VoiceZero10.Id, "install-manifest.json"),
                "{\"TotalSizeBytes\":1,\"InstalledAtUtc\":\"2026-01-01T00:00:00+00:00\"}",
                TestContext.Current.CancellationToken);
        }

        catalog.AddChatterboxTurboModels(options);
        var model = Assert.IsAssignableFrom<ISynthesisModel>(Assert.Single(catalog.Enumerate()).Model);

        // Act
        var (loaded, _) = await LoadAsync(model, Values(model, "pack-voice"));
        await using var engine = loaded;
        var speech = await engine.SynthesizeAsync("Hello there.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["a", "pack-voice"], VoiceChoice(model).Options.Select(option => option.Value));
        Assert.Equal(750, Prompt(speech));
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
        var (loaded, _) = await LoadAsync(model, Values(model, "a"));
        await using var engine = loaded;
        var speech = await engine.SynthesizeAsync("Hello there.", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["a", "damaged"], VoiceChoice(model).Options.Select(option => option.Value));
        Assert.Equal(250, Prompt(speech));
    }

    /// <summary>Proves speaking with a damaged store clip fails only at that use, with an error naming the damaged file.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_StoreVoicesResilience_DamagedClipFailsAtFirstUseNamingFile()
    {
        // Arrange
        InstallSynthetic();
        using var catalog = NewCatalog();
        using (var library = new ReferenceVoiceLibrary(catalog.Store))
        {
            await File.WriteAllBytesAsync(Path.Join(library.EnsureUserDirectory(), "damaged.wav"), "not a wav file"u8.ToArray(), TestContext.Current.CancellationToken);
        }

        catalog.AddChatterboxTurboModels(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens });
        var model = Assert.IsAssignableFrom<ISynthesisModel>(Assert.Single(catalog.Enumerate()).Model);
        var (loaded, _) = await LoadAsync(model, Values(model, "damaged"));
        await using var engine = loaded;

        // Act
        var failure = await Record.ExceptionAsync(async () => await engine.SynthesizeAsync("Hello there.", TestContext.Current.CancellationToken));

        // Assert
        Assert.NotNull(failure);
        Assert.Contains("damaged.wav", failure.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Proves text after a mood tag is spoken with the voice's mood clip.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_MoodVoices_UseMoodClip()
    {
        // Arrange
        InstallSynthetic();
        var options = MoodOptions();

        // Act
        var plainText = await SynthesizeAsync(options, "Hello there.", "sadly");
        var moodText = await SynthesizeAsync(options, "[sad] Hello there.", "sadly");

        // Assert
        Assert.Equal(250, Prompt(plainText));
        Assert.Equal(500, Prompt(moodText));
    }

    /// <summary>Proves text after a mood tag is spoken with the voice's base clip when the voice has no clip for that mood.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_MoodVoices_FallBackToBaseClip()
    {
        // Arrange
        InstallSynthetic();
        var options = MoodOptions();

        // Act
        var fallback = await SynthesizeAsync(options, "[sad] Hello there.", "plain");

        // Assert
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

    /// <summary>Proves a sigh renders as a real silent segment of a fixed 300 ms between two spoken ones.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_SilenceTagRendering_SighIsFixedSilence()
    {
        // Arrange
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };

        // Act
        var sigh = await SynthesizeAsync(options, "Oh well. [sighs] Fine, then.");

        // Assert
        Assert.Equal(3, sigh.Count);
        Assert.NotEmpty(sigh[0].Samples);
        Assert.Empty(sigh[1].Samples);
        Assert.Equal(TimeSpan.FromMilliseconds(300), sigh[1].PreSilence + sigh[1].PostSilence);
        Assert.NotEmpty(sigh[2].Samples);
    }

    /// <summary>Proves a native laugh stays inside the sentence it interrupts, as the model's own sound.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_NativeTagRendering_LaughStaysInsideSentence()
    {
        // Arrange
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };

        // Act
        var laugh = await SynthesizeAsync(options, "That was [laughs] really funny today.");

        // Assert
        Assert.Equal(SegmentSamples, Assert.Single(laugh).Samples.Count);
    }

    /// <summary>Proves the throat-clear and gasp tags, like the laugh, stay inside the sentence they interrupt as the model's own sounds.</summary>
    [Theory]
    [InlineData("That was [clears throat] really funny today.")]
    [InlineData("That was [gasp] really funny today.")]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_NativeTagRendering_ThroatClearAndGaspStayInsideSentence(string text)
    {
        // Arrange
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };

        // Act
        var speech = await SynthesizeAsync(options, text);

        // Assert
        Assert.Equal(SegmentSamples, Assert.Single(speech).Samples.Count);
    }

    /// <summary>Proves the short and long pause tags render as real silent segments of their fixed durations.</summary>
    [Theory]
    [InlineData("Oh well. [short pause] Fine, then.", 300)]
    [InlineData("Oh well. [long pause] Fine, then.", 900)]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_SilenceTagRendering_PauseIsFixedSilence(string text, int expectedMilliseconds)
    {
        // Arrange
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };

        // Act
        var speech = await SynthesizeAsync(options, text);

        // Assert
        Assert.Equal(3, speech.Count);
        Assert.NotEmpty(speech[0].Samples);
        Assert.Empty(speech[1].Samples);
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), speech[1].PreSilence + speech[1].PostSilence);
        Assert.NotEmpty(speech[2].Samples);
    }

    /// <summary>Proves every audio tag the model cannot reproduce is omitted and the text is spoken as one ordinary sentence.</summary>
    [Theory]
    [InlineData("[whispers]")]
    [InlineData("[soft]")]
    [InlineData("[breathy]")]
    [InlineData("[sarcastic]")]
    [InlineData("[snorts]")]
    [InlineData("[emphasis]")]
    [InlineData("[slow]")]
    [InlineData("[very slow]")]
    [InlineData("[fast]")]
    [InlineData("[very fast]")]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_OmittedTagRendering_EveryUnsupportedTagIsOmitted(string tag)
    {
        // Arrange
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };

        // Act
        var omitted = await SynthesizeAsync(options, $"Hello {tag} there my friend.");

        // Assert
        Assert.Equal(SegmentSamples, Assert.Single(omitted).Samples.Count);
    }

    /// <summary>Proves the append and drop policies render a trailing tag distinctly: appending lengthens the text, dropping matches untagged text.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_TagPlacementPolicy_SuffixAndStripRenderDistinctly()
    {
        // Arrange: the generation bound scales with the text length, so the output length reveals how long the spoken text was
        InstallSynthetic();
        ChatterboxTurboModelOptions Options(ChatterboxTurboEndOfSentenceTagPolicy policy) => new()
        {
            ReferenceVoices = [Voice("a", 0.25f)],
            MaxNewTokens = 200,
            MaxAudioTokensPerTextToken = 1.0,
            EndOfSentenceTagPolicy = policy,
        };

        // Act
        var suffix = await SynthesizeAsync(Options(ChatterboxTurboEndOfSentenceTagPolicy.Suffix), "That was really funny [laughs]");
        var strip = await SynthesizeAsync(Options(ChatterboxTurboEndOfSentenceTagPolicy.Strip), "That was really funny [laughs]");
        var untagged = await SynthesizeAsync(Options(ChatterboxTurboEndOfSentenceTagPolicy.Strip), "That was really funny");

        // Assert
        Assert.True(Assert.Single(suffix).Samples.Count > Assert.Single(strip).Samples.Count);
        Assert.Equal(Assert.Single(untagged).Samples.Count, Assert.Single(strip).Samples.Count);
    }

    /// <summary>Proves an undefined end-of-sentence tag policy is rejected by the options.</summary>
    [Fact]
    public void SpeechOnnxChatterboxTurbo_SystemIntegration_TagPlacementPolicy_UndefinedValue_IsRejected()
    {
        // Arrange
        var options = new ChatterboxTurboModelOptions();

        // Act
        var exception = Record.Exception(() => options.EndOfSentenceTagPolicy = (ChatterboxTurboEndOfSentenceTagPolicy)99);

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal(ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord, options.EndOfSentenceTagPolicy);
    }

    /// <summary>Proves the configured generation bound, not the fixture's output length, sets how many speech tokens are generated.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_RunawayProtection_ConfiguredBoundSetsGeneratedTokenCount()
    {
        // Arrange: the synthetic language model never stops and the decoder echoes one sample per token
        InstallSynthetic();
        ChatterboxTurboModelOptions Options(int bound) => new() { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = bound };

        // Act
        var four = await SynthesizeAsync(Options(4), "Hello there.");
        var six = await SynthesizeAsync(Options(6), "Hello there.");

        // Assert: two more allowed tokens yield exactly two more samples
        Assert.Equal(PromptLength + 4 + SilenceLength, Assert.Single(four).Samples.Count);
        Assert.Equal(PromptLength + 6 + SilenceLength, Assert.Single(six).Samples.Count);
    }

    /// <summary>Proves a tag with no usable sound is omitted: the text is spoken as one ordinary sentence.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_OmittedTagRendering_UnsupportedTagIsOmitted()
    {
        // Arrange
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };

        // Act
        var omitted = await SynthesizeAsync(options, "Hello [whispers] there my friend.");

        // Assert
        Assert.Equal(SegmentSamples, Assert.Single(omitted).Samples.Count);
    }

    /// <summary>Proves a trailing tag is kept next to speech by default rather than left without speech after it.</summary>
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

    /// <summary>Proves a host can choose to append or drop a trailing tag, and neither choice leaves a tag without speech.</summary>
    [Theory]
    [InlineData(ChatterboxTurboEndOfSentenceTagPolicy.Suffix)]
    [InlineData(ChatterboxTurboEndOfSentenceTagPolicy.Strip)]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_TagPlacementPolicy_HostChoiceKeepsTagWithSpeech(ChatterboxTurboEndOfSentenceTagPolicy policy)
    {
        // Arrange
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions
        {
            ReferenceVoices = [Voice("a", 0.25f)],
            MaxNewTokens = MaxNewTokens,
            EndOfSentenceTagPolicy = policy,
        };

        // Act
        var trailing = await SynthesizeAsync(options, "That was really funny [laughs]");

        // Assert
        Assert.Equal(SegmentSamples, Assert.Single(trailing).Samples.Count);
    }

    /// <summary>Proves long text is split into ten sentences, each synthesized, with no sentence left out.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_LongTextSegmentation_SplitsAndSpeaksEverySentence()
    {
        // Arrange: ten sentences of 35 characters, 359 characters in total and so over the 200-character limit
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };
        var text = string.Join(' ', Enumerable.Repeat("This is a short test sentence here.", 10));

        // Act
        var speech = await SynthesizeAsync(options, text);

        // Assert: every sentence became its own spoken segment
        Assert.Equal(10, speech.Count);
        Assert.All(speech, segment => Assert.Equal(SegmentSamples, segment.Samples.Count));
    }

    /// <summary>Proves a single over-long sentence is split into several parts, none of them left unspoken.</summary>
    [Fact]
    public async Task SpeechOnnxChatterboxTurbo_SystemIntegration_LongTextSegmentation_SplitsOverLongSentence()
    {
        // Arrange: one 450-character sentence with no sentence punctuation
        InstallSynthetic();
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a", 0.25f)], MaxNewTokens = MaxNewTokens };
        var text = string.Join(' ', Enumerable.Repeat("word", 90));

        // Act
        var speech = await SynthesizeAsync(options, text);

        // Assert: at least ceil(449 / 200) parts, all spoken
        Assert.True(speech.Count >= 3);
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
        // The catalog is only needed to register and enumerate the model; the loaded engine does not depend on it afterwards
        using var catalog = NewCatalog();
        catalog.AddChatterboxTurboModels(options);
        var model = Assert.IsAssignableFrom<ISynthesisModel>(Assert.Single(catalog.Enumerate()).Model);
        return await LoadAsync(model, voice is null ? null : Values(model, voice));
    }

    /// <summary>Loads an already registered model through the Speech factory over the synthetic install.</summary>
    private async Task<(ISpeechSynthesizerEngine Engine, ISynthesisModel Model)> LoadAsync(ISynthesisModel model, Dictionary<string, object>? values)
    {
        var engine = await SpeechSynthesizerFactory.LoadAsync(model, _root, null, values, TestContext.Current.CancellationToken);
        return (engine, model);
    }

    /// <summary>Builds options with one voice that has a distinct "sad" clip and one voice that has none.</summary>
    private ChatterboxTurboModelOptions MoodOptions()
    {
        var sadPath = Path.Join(_root, "clips", "sad.wav");
        WriteWav(sadPath, 0.5f);
        var withSad = Voice("sadly", 0.25f).WithMoodFiles(new Dictionary<string, string> { ["sad"] = sadPath });
        return new ChatterboxTurboModelOptions { ReferenceVoices = [withSad, Voice("plain", 0.25f)], MaxNewTokens = MaxNewTokens };
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
