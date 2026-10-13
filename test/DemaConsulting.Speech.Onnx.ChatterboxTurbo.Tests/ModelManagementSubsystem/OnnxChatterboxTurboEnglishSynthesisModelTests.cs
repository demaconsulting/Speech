using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.SynthesisSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using Microsoft.ML.OnnxRuntime;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="OnnxChatterboxTurboEnglishSynthesisModel"/> and
///     <see cref="ChatterboxTurboModelOptions"/>, including end-to-end synthesis over synthetic ONNX graphs.
/// </summary>
[Collection(SessionHookCollection.Name)]
public sealed class OnnxChatterboxTurboEnglishSynthesisModelTests : IDisposable
{
    /// <summary>A scratch directory unique to this test instance.</summary>
    private readonly string _root;

    /// <summary>Initializes a fresh scratch directory.</summary>
    public OnnxChatterboxTurboEnglishSynthesisModelTests()
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

    /// <summary>Proves the model's identity metadata.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Metadata_DescribesNativeTagSynthesisModel()
    {
        // Arrange
        var model = new OnnxChatterboxTurboEnglishSynthesisModel();
        ISynthesisModel synthesis = model;

        // Assert
        Assert.Equal("chatterbox-turbo-onnx-en", model.Id);
        Assert.Contains("English", model.DisplayName, StringComparison.Ordinal);
        Assert.Equal(SpeechModelRole.Synthesis, model.Role);
        Assert.Equal("MIT", model.LicenseName);
        Assert.Equal(SpeechModelAudioTagSupport.Native, model.AudioTagSupport);
        Assert.Equal(24000, synthesis.PreferredAudioFormat.SampleRate);
        Assert.Equal(1, synthesis.PreferredAudioFormat.ChannelCount);
        Assert.NotNull(synthesis.CapabilityProfile);
    }

    /// <summary>Proves the default download descriptor lists the eight default-variant files with verified checksums.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_DownloadDescriptor_DefaultVariant_ListsEightFiles()
    {
        // Act
        var files = new OnnxChatterboxTurboEnglishSynthesisModel().DownloadDescriptor.Files;

        // Assert
        Assert.Equal(8, files.Count);
        Assert.Equal(
            [
                "onnx/language_model_fp16.onnx", "onnx/language_model_fp16.onnx_data",
                "onnx/embed_tokens_q4.onnx", "onnx/embed_tokens_q4.onnx_data",
                "onnx/speech_encoder_q4.onnx", "onnx/speech_encoder_q4.onnx_data",
                "onnx/conditional_decoder_q4.onnx", "onnx/conditional_decoder_q4.onnx_data",
            ],
            files.Select(file => file.RelativeInstallPath));
        Assert.All(files, file => Assert.StartsWith("https://huggingface.co/ResembleAI/chatterbox-turbo-ONNX/", file.Uri.AbsoluteUri));
    }

    /// <summary>Proves every variant combination resolves to a distinct id and a complete checksum-backed file set.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Variants_HaveDistinctIdsAndCompleteFileSets()
    {
        // Act
        var models = (from lm in Enum.GetValues<ChatterboxTurboLanguageModelVariant>()
                      from aux in Enum.GetValues<ChatterboxTurboAuxiliaryModelVariant>()
                      select new OnnxChatterboxTurboEnglishSynthesisModel(
                          new ChatterboxTurboModelOptions { LanguageModelVariant = lm, AuxiliaryModelVariant = aux })).ToList();

        // Assert
        Assert.Equal(6, models.Select(model => model.Id).Distinct().Count());
        Assert.All(models, model => Assert.Equal(8, model.DownloadDescriptor.Files.Count));
        Assert.Equal(
            "chatterbox-turbo-onnx-en-lm-fp32-aux-fp16",
            new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions
            {
                LanguageModelVariant = ChatterboxTurboLanguageModelVariant.Fp32,
                AuxiliaryModelVariant = ChatterboxTurboAuxiliaryModelVariant.Fp16,
            }).Id);
    }

    /// <summary>Proves no voice parameter is exposed without voices, and a voice choice is exposed with them.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Parameters_ReflectSuppliedVoices()
    {
        // Arrange
        var none = new OnnxChatterboxTurboEnglishSynthesisModel();
        var two = new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a"), Voice("b")] });

        // Assert
        Assert.Empty(none.Parameters);
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(two.Parameters));
        Assert.Equal(OnnxChatterboxTurboEnglishSynthesisModel.VoiceParameterId, choice.Id);
        Assert.Equal(["a", "b"], choice.Options.Select(option => option.Value));
        Assert.Equal("a", choice.Default);
    }

    /// <summary>Proves speaker ids resolve by voice id, defaulting to the first voice.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_ResolveSpeakerId_MapsVoiceIdToIndex()
    {
        // Arrange
        ISynthesisModel model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a"), Voice("b")] });

        // Assert
        Assert.Equal(1, model.ResolveSpeakerId(new Dictionary<string, object> { ["voice"] = "b" }));
        Assert.Equal(0, model.ResolveSpeakerId(new Dictionary<string, object> { ["voice"] = "zzz" }));
        Assert.Equal(0, model.ResolveSpeakerId(new Dictionary<string, object>()));
        Assert.Equal(0, model.ResolveSpeakerId(null));
    }

    /// <summary>Proves voices are loaded from a directory of WAV and FLAC files in ordinal order after explicit voices.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_VoiceDirectory_LoadsWavAndFlacFiles()
    {
        // Arrange
        var directory = Path.Combine(_root, "voices");
        Directory.CreateDirectory(directory);
        WriteWav(Path.Combine(directory, "zed.wav"));
        WriteWav(Path.Combine(directory, "amy.wav"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "voice-mono-16-16000.flac"), Path.Combine(directory, "mid.FLAC"));
        File.WriteAllText(Path.Combine(directory, "ignored.txt"), "x");

        // Act
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("first")], ReferenceVoiceDirectory = directory });

        // Assert
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        Assert.Equal(["first", "amy", "mid", "zed"], choice.Options.Select(option => option.Value));
        Assert.Throws<DirectoryNotFoundException>(() => new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoiceDirectory = Path.Combine(_root, "nope") }));
    }

    /// <summary>Proves a damaged file in the voice directory never fails construction (it fails at first use of that voice).</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DamagedVoiceFile_DoesNotFailConstruction()
    {
        // Arrange
        var directory = Path.Combine(_root, "damaged-voices");
        Directory.CreateDirectory(directory);
        WriteWav(Path.Combine(directory, "good.wav"));
        var bad = Path.Combine(directory, "bad.wav");
        File.WriteAllBytes(bad, [1, 2, 3, 4]);

        // Act
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoiceDirectory = directory, IncludeStoreVoices = false });

        // Assert
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        Assert.Equal(["bad", "good"], choice.Options.Select(option => option.Value));
    }

    /// <summary>Proves option defaults and validation.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_Properties_HaveSafeDefaultsAndValidate()
    {
        // Arrange
        var options = new ChatterboxTurboModelOptions();

        // Assert
        Assert.Equal(ChatterboxTurboLanguageModelVariant.Fp16, options.LanguageModelVariant);
        Assert.Equal(ChatterboxTurboAuxiliaryModelVariant.Q4, options.AuxiliaryModelVariant);
        Assert.Equal(1024, options.MaxNewTokens);
        Assert.Equal(25.0, options.MaxAudioTokensPerTextToken);
        Assert.Equal(12, options.MaxConsecutiveSpeechTokenRepeats);
        Assert.InRange(options.IntraOpThreads, 1, 6);
        Assert.Equal(ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord, options.EndOfSentenceTagPolicy);
        Assert.Empty(options.ReferenceVoices);
        Assert.True(options.IncludeStoreVoices);
        Assert.Null(options.ReferenceVoiceDirectory);
        Assert.Null(options.ReferenceVoiceLibrary);
        Assert.Null(options.Diagnostics);
        Assert.Equal(150, options.MoodJoinSilenceMilliseconds);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MoodJoinSilenceMilliseconds = -1);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MoodJoinSilenceMilliseconds = 1001);
        options.MoodJoinSilenceMilliseconds = 0;
        Assert.Equal(0, options.MoodJoinSilenceMilliseconds);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.IntraOpThreads = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxNewTokens = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxAudioTokensPerTextToken = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxConsecutiveSpeechTokenRepeats = -1);
        options.MaxConsecutiveSpeechTokenRepeats = 0;
        Assert.Equal(0, options.MaxConsecutiveSpeechTokenRepeats);
    }

    /// <summary>Proves the configured mood join silence reaches the capability profile.</summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(150, 150)]
    [InlineData(400, 400)]
    public void OnnxChatterboxTurboEnglishSynthesisModel_CapabilityProfile_UsesMoodJoinSilenceOption(int configured, int expected)
    {
        // Arrange
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a")], MoodJoinSilenceMilliseconds = configured });
        var spans = AudioTagParser.Parse("Fine. [sad] Not really.");

        // Act
        var plan = ((ISynthesisModel)model).CapabilityProfile.Render(spans, model);

        // Assert
        Assert.Equal(2, plan.Segments.Count);
        Assert.Equal(expected, plan.Segments[0].PostSilenceMs);
    }

    /// <summary>Proves creating a backend without voices fails with a clear message.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_NoVoices_Throws()
    {
        // Arrange
        var model = new OnnxChatterboxTurboEnglishSynthesisModel();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => model.CreateBackend(_root));
    }

    /// <summary>Proves creating a backend from an empty model directory path is rejected.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_EmptyDirectory_ThrowsArgumentException()
    {
        // Arrange
        var model = new OnnxChatterboxTurboEnglishSynthesisModel();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => model.CreateBackend(string.Empty));
    }

    /// <summary>Proves a missing graph file is reported before any session is created.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_MissingFile_ThrowsFileNotFound()
    {
        // Arrange
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a")] });
        var created = new List<InferenceSession>();
        OnnxChatterboxTurboEnglishSynthesisModel.OnSessionCreated = created.Add;
        try
        {
            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => model.CreateBackend(_root));
            Assert.Empty(created);
        }
        finally
        {
            OnnxChatterboxTurboEnglishSynthesisModel.OnSessionCreated = null;
        }
    }

    /// <summary>Proves a full synthetic installation yields a backend that synthesizes end to end.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_SyntheticInstall_SynthesizesAudio()
    {
        // Arrange
        InstallSynthetic(halfCache: true, "language_model_fp16");
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions
        {
            ReferenceVoices = [Voice("a")],
            MaxNewTokens = 4,
            IntraOpThreads = 1,
        });

        // Act
        using var backend = model.CreateBackend(_root);
        var audio = backend.Generate("Hello there.", 1.0f, 0);

        // Assert: the synthetic decoder echoes its tokens (prompt + generated + three silence tokens)
        Assert.Equal(24000, backend.SampleRate);
        Assert.Equal(24000, audio.SampleRate);
        Assert.Equal(ChatterboxTurboGraphFixture.PromptTokens.Length + 4 + 3, audio.Samples.Length);
    }

    /// <summary>Proves a non-default language-model variant installs and loads from its own graph names.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_Fp32Variant_UsesFp32Graph()
    {
        // Arrange
        InstallSynthetic(halfCache: false, "language_model");
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions
        {
            ReferenceVoices = [Voice("a")],
            LanguageModelVariant = ChatterboxTurboLanguageModelVariant.Fp32,
            MaxNewTokens = 2,
        });

        // Act
        using var backend = model.CreateBackend(_root);

        // Assert
        Assert.NotEmpty(backend.Generate("Hello.", 1.0f, 0).Samples);
    }

    /// <summary>Proves sessions are disposed when backend creation fails after they were created.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_InvalidGraph_DisposesSessionsAndThrows()
    {
        // Arrange: language model without cache inputs
        InstallSynthetic(halfCache: true, "language_model_fp16");
        File.WriteAllBytes(
            Path.Combine(_root, "onnx", "language_model_fp16.onnx"),
            ChatterboxTurboGraphFixture.LanguageModel(halfCache: true, withPastInputs: false));
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a")] });
        var created = new List<InferenceSession>();
        OnnxChatterboxTurboEnglishSynthesisModel.OnSessionCreated = created.Add;
        try
        {
            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => model.CreateBackend(_root));
            Assert.NotEmpty(created);
            Assert.All(created, session => Assert.True(IsDisposed(session)));
        }
        finally
        {
            OnnxChatterboxTurboEnglishSynthesisModel.OnSessionCreated = null;
        }
    }

    /// <summary>Proves a null reference-voice list is rejected when assigned, with a clear argument exception.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_ReferenceVoices_Null_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new ChatterboxTurboModelOptions();

        // Act and Assert
        Assert.Throws<ArgumentNullException>(() => options.ReferenceVoices = null!);
        Assert.Empty(options.ReferenceVoices);
    }

    /// <summary>Proves library user voices are appended after explicit and directory voices, with the first id winning.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_StoreVoices_MergedAfterExplicitAndDirectoryFirstIdWins()
    {
        // Arrange
        using var library = new ReferenceVoiceLibrary(
            new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") }));
        var user = library.EnsureUserDirectory();
        WriteWav(Path.Combine(user, "a.wav"));
        WriteWav(Path.Combine(user, "dir_voice.wav"));
        WriteWav(Path.Combine(user, "mine.wav"));
        var directory = Path.Combine(_root, "voices");
        Directory.CreateDirectory(directory);
        WriteWav(Path.Combine(directory, "dir_voice.wav"));

        // Act
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions
            {
                ReferenceVoices = [Voice("a")],
                ReferenceVoiceDirectory = directory,
                ReferenceVoiceLibrary = library,
            });

        // Assert
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        Assert.Equal(["a", "dir_voice", "mine"], choice.Options.Select(option => option.Value));
        Assert.Equal("a", choice.Default);
    }

    /// <summary>Proves two explicit voices with the same id are rejected.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DuplicateExplicitIds_ThrowsArgumentException()
    {
        // Act and Assert
        Assert.Throws<ArgumentException>(() => new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("same"), Voice("same")], IncludeStoreVoices = false }));
    }

    /// <summary>Proves a voice-directory file whose id matches an explicit voice is rejected.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DirectoryVoiceDuplicatesExplicitId_ThrowsArgumentException()
    {
        // Arrange
        var directory = Path.Combine(_root, "dup-explicit");
        Directory.CreateDirectory(directory);
        WriteWav(Path.Combine(directory, "same.wav"));

        // Act and Assert
        Assert.Throws<ArgumentException>(() => new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("same")], ReferenceVoiceDirectory = directory, IncludeStoreVoices = false }));
    }

    /// <summary>Proves two voice-directory files with the same stem (for example a.wav and a.flac) are rejected.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DirectoryWavAndFlacSameStem_ThrowsArgumentException()
    {
        // Arrange
        var directory = Path.Combine(_root, "dup-stem");
        Directory.CreateDirectory(directory);
        WriteWav(Path.Combine(directory, "a.wav"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "TestData", "voice-mono-16-16000.flac"), Path.Combine(directory, "a.flac"));

        // Act and Assert
        Assert.Throws<ArgumentException>(() => new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoiceDirectory = directory, IncludeStoreVoices = false }));
    }

    /// <summary>Proves a library voice hidden by an earlier voice is skipped and reported at Info level.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_LibraryVoiceDuplicate_IsSkippedWithInfoDiagnostic()
    {
        // Arrange
        using var library = new ReferenceVoiceLibrary(
            new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") }));
        WriteWav(Path.Combine(library.EnsureUserDirectory(), "taken.wav"));
        var diagnostics = new RecordingDiagnostics();

        // Act
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("taken")], ReferenceVoiceLibrary = library, Diagnostics = diagnostics });

        // Assert
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        Assert.Equal(["taken"], choice.Options.Select(option => option.Value));
        var message = Assert.Single(diagnostics.Messages);
        Assert.Equal(SpeechDiagnosticLevel.Info, message.Level);
        Assert.Contains("taken", message.Text, StringComparison.Ordinal);
    }

    /// <summary>Proves a library user voice whose label clashes with an earlier voice gets a "(user)" suffix.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_UserVoiceLabelClash_AddsUserSuffix()
    {
        // Arrange
        using var library = new ReferenceVoiceLibrary(
            new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") }));
        WriteWav(Path.Combine(library.EnsureUserDirectory(), "alan_x.wav"));
        var explicitVoice = ChatterboxTurboReferenceVoice.FromSamples("explicit", new float[24000], 24000, displayName: "Alan X");

        // Act
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoices = [explicitVoice], ReferenceVoiceLibrary = library });

        // Assert
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        Assert.Equal(["Alan X", "Alan X (user)"], choice.Options.Select(option => option.Label));
        Assert.Equal(["explicit", "alan_x"], choice.Options.Select(option => option.Value));
    }

    /// <summary>Proves installed pack voices are listed after user voices and a clashing label gets a "(pack)" suffix.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_PackVoices_AreListedWithPackSuffixOnClash()
    {
        // Arrange: a user voice and a pack voice whose ids differ but whose humanized labels are both "Alan X"
        using var library = new ReferenceVoiceLibrary(
            new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") }));
        WriteWav(Path.Combine(library.EnsureUserDirectory(), "alan_x.wav"));
        var packVoices = Path.Combine(library.RootPath, ReferenceVoicePack.VoiceZero10.Id, "current", "voices");
        Directory.CreateDirectory(packVoices);
        WriteWav(Path.Combine(packVoices, "alan-x.wav"));
        WriteWav(Path.Combine(packVoices, "zoe.wav"));
        File.WriteAllText(
            Path.Combine(library.RootPath, ReferenceVoicePack.VoiceZero10.Id, "install-manifest.json"),
            "{\"TotalSizeBytes\":1,\"InstalledAtUtc\":\"2026-01-01T00:00:00+00:00\"}");

        // Act
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions { ReferenceVoiceLibrary = library });

        // Assert
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        Assert.Equal(["alan_x", "alan-x", "zoe"], choice.Options.Select(option => option.Value));
        Assert.Equal(["Alan X", "Alan X (pack)", "Zoe"], choice.Options.Select(option => option.Label));
    }

    /// <summary>Proves undefined variant values are rejected when assigned to the options.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_VariantSetters_UndefinedValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new ChatterboxTurboModelOptions();

        // Act and Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => options.LanguageModelVariant = (ChatterboxTurboLanguageModelVariant)99);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.AuxiliaryModelVariant = (ChatterboxTurboAuxiliaryModelVariant)99);
    }

    /// <summary>Proves store voices are loaded lazily: a corrupt user file does not fail construction.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_CorruptStoreVoice_DoesNotFailConstruction()
    {
        // Arrange
        using var library = new ReferenceVoiceLibrary(
            new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") }));
        File.WriteAllText(Path.Combine(library.EnsureUserDirectory(), "broken.wav"), "not audio");

        // Act
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions { ReferenceVoiceLibrary = library });

        // Assert
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        Assert.Equal(["broken"], choice.Options.Select(option => option.Value));
    }

    /// <summary>Proves <c>IncludeStoreVoices = false</c> ignores the library entirely.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_IncludeStoreVoicesFalse_IgnoresLibrary()
    {
        // Arrange
        using var library = new ReferenceVoiceLibrary(
            new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") }));
        WriteWav(Path.Combine(library.EnsureUserDirectory(), "mine.wav"));

        // Act
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoiceLibrary = library, IncludeStoreVoices = false });

        // Assert
        Assert.Empty(model.Parameters);
    }

    /// <summary>Proves the fallback library is used when the options name none, and an explicit library takes precedence.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DefaultLibrary_UsedOnlyWhenOptionsNameNone()
    {
        // Arrange
        using var fallback = new ReferenceVoiceLibrary(
            new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "fallback") }));
        using var explicitLibrary = new ReferenceVoiceLibrary(
            new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "explicit") }));
        WriteWav(Path.Combine(fallback.EnsureUserDirectory(), "from_default.wav"));
        WriteWav(Path.Combine(explicitLibrary.EnsureUserDirectory(), "from_options.wav"));

        // Act
        var withDefault = new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions(), fallback);
        var withExplicit = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoiceLibrary = explicitLibrary },
            fallback);
        var disabled = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { IncludeStoreVoices = false },
            fallback);

        // Assert
        var defaultChoice = Assert.IsType<ChoiceParameter>(Assert.Single(withDefault.Parameters));
        Assert.Equal(["from_default"], defaultChoice.Options.Select(option => option.Value));
        var explicitChoice = Assert.IsType<ChoiceParameter>(Assert.Single(withExplicit.Parameters));
        Assert.Equal(["from_options"], explicitChoice.Options.Select(option => option.Value));
        Assert.Empty(disabled.Parameters);
    }

    /// <summary>Creates a one-second voice.</summary>
    private static ChatterboxTurboReferenceVoice Voice(string id) =>
        ChatterboxTurboReferenceVoice.FromSamples(id, new float[24000], 24000);

    /// <summary>Collects diagnostics messages for assertions.</summary>
    private sealed class RecordingDiagnostics : ISpeechDiagnostics
    {
        /// <summary>Gets the recorded messages.</summary>
        public List<(SpeechDiagnosticLevel Level, string Text)> Messages { get; } = [];

        /// <inheritdoc/>
        public void Report(SpeechDiagnosticLevel level, string category, string message) => Messages.Add((level, message));
    }

    /// <summary>Installs synthetic graphs and weights files under the real file names.</summary>
    private void InstallSynthetic(bool halfCache, string languageModelName) =>
        ChatterboxTurboGraphFixture.Write(Path.Combine(_root, "onnx"), languageModelName, "_q4", halfCache);

    /// <summary>Writes a one-second silent WAV file.</summary>
    private static void WriteWav(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        const int Samples = 24000;
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + (Samples * 2));
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(24000);
        writer.Write(48000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(Samples * 2);
        writer.Write(new byte[Samples * 2]);
    }

    /// <summary>Reads the session's private disposed flag, since the type exposes no public equivalent.</summary>
    private static bool IsDisposed(InferenceSession session)
    {
        var field = typeof(InferenceSession).GetField("_disposed", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.NotNull(field);
        return (bool)field.GetValue(session)!;
    }
}
