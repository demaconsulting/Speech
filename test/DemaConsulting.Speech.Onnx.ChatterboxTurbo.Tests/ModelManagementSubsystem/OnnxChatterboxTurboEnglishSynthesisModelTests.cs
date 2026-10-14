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

        // Act
        var format = synthesis.PreferredAudioFormat;
        var profile = synthesis.CapabilityProfile;

        // Assert
        Assert.Equal("chatterbox-turbo-onnx-en", model.Id);
        Assert.Contains("English", model.DisplayName, StringComparison.Ordinal);
        Assert.Equal(SpeechModelRole.Synthesis, model.Role);
        Assert.Equal("MIT", model.LicenseName);
        Assert.Equal(SpeechModelAudioTagSupport.Native, model.AudioTagSupport);
        Assert.Equal(24000, format.SampleRate);
        Assert.Equal(1, format.ChannelCount);
        Assert.NotNull(profile);
    }

    /// <summary>Proves the default download descriptor lists the eight default-variant files, each with a SHA-256 checksum.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_DownloadDescriptor_DefaultVariant_ListsEightFiles()
    {
        // Arrange
        var model = new OnnxChatterboxTurboEnglishSynthesisModel();

        // Act
        var files = model.DownloadDescriptor.Files;

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
        Assert.All(files, file => Assert.Matches("^[0-9a-fA-F]{64}$", file.Sha256Checksum));
        Assert.Equal(8, files.Select(file => file.Sha256Checksum.ToLowerInvariant()).Distinct().Count());
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
        var fp32Fp16Id = new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions
        {
            LanguageModelVariant = ChatterboxTurboLanguageModelVariant.Fp32,
            AuxiliaryModelVariant = ChatterboxTurboAuxiliaryModelVariant.Fp16,
        }).Id;

        // Assert
        Assert.Equal(6, models.Select(model => model.Id).Distinct().Count());
        Assert.All(models, model => Assert.Equal(8, model.DownloadDescriptor.Files.Count));
        Assert.All(models, model => Assert.All(
            model.DownloadDescriptor.Files,
            file => Assert.Matches("^[0-9a-fA-F]{64}$", file.Sha256Checksum)));
        Assert.All(models, model => Assert.Equal(
            8,
            model.DownloadDescriptor.Files.Select(file => file.Sha256Checksum.ToLowerInvariant()).Distinct().Count()));
        Assert.Equal(
            6,
            models.Select(model => string.Join('|', model.DownloadDescriptor.Files.Select(file => file.RelativeInstallPath))).Distinct().Count());
        Assert.Equal("chatterbox-turbo-onnx-en-lm-fp32-aux-fp16", fp32Fp16Id);
    }

    /// <summary>Proves no voice parameter is exposed without voices, and a voice choice is exposed with them.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Parameters_ReflectSuppliedVoices()
    {
        // Arrange
        var none = new OnnxChatterboxTurboEnglishSynthesisModel();
        var two = new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a"), Voice("b")] });

        // Act
        var noneParameters = none.Parameters;
        var twoParameters = two.Parameters;

        // Assert
        Assert.Empty(noneParameters);
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(twoParameters));
        Assert.Equal(OnnxChatterboxTurboEnglishSynthesisModel.VoiceParameterId, choice.Id);
        Assert.Equal(["a", "b"], choice.Options.Select(option => option.Value));
        Assert.Equal("a", choice.Default);
    }

    /// <summary>Proves a supplied voice id resolves to the index of that voice.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_ResolveSpeakerId_MapsVoiceIdToIndex()
    {
        // Arrange
        ISynthesisModel model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a"), Voice("b")] });

        // Act
        var index = model.ResolveSpeakerId(new Dictionary<string, object> { ["voice"] = "b" });

        // Assert
        Assert.Equal(1, index);
    }

    /// <summary>Proves an unknown, absent or null voice id falls back to the first voice (index 0).</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_ResolveSpeakerId_UnknownOrAbsentVoice_FallsBackToFirstVoice()
    {
        // Arrange
        ISynthesisModel model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a"), Voice("b")] });

        // Act
        var unknown = model.ResolveSpeakerId(new Dictionary<string, object> { ["voice"] = "zzz" });
        var absent = model.ResolveSpeakerId(new Dictionary<string, object>());
        var wrongType = model.ResolveSpeakerId(new Dictionary<string, object> { ["voice"] = 1 });
        var nullBag = model.ResolveSpeakerId(null);

        // Assert
        Assert.Equal(0, unknown);
        Assert.Equal(0, absent);
        Assert.Equal(0, wrongType);
        Assert.Equal(0, nullBag);
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
    }

    /// <summary>Proves a voice directory that does not exist is reported with a <see cref="DirectoryNotFoundException"/>.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_MissingVoiceDirectory_ThrowsDirectoryNotFound()
    {
        // Arrange
        var options = new ChatterboxTurboModelOptions { ReferenceVoiceDirectory = Path.Combine(_root, "nope") };

        // Act
        var exception = Record.Exception(() => new OnnxChatterboxTurboEnglishSynthesisModel(options));

        // Assert
        Assert.IsType<DirectoryNotFoundException>(exception);
    }

    /// <summary>Proves a damaged file in the voice directory never fails construction and fails, naming the file, at first use of that voice.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DamagedVoiceFile_DoesNotFailConstruction()
    {
        // Arrange
        var directory = Path.Combine(_root, "damaged-voices");
        Directory.CreateDirectory(directory);
        WriteWav(Path.Combine(directory, "good.wav"));
        File.WriteAllBytes(Path.Combine(directory, "bad.wav"), [1, 2, 3, 4]);
        InstallSynthetic(halfCache: true, "language_model_fp16");

        // Act
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoiceDirectory = directory, IncludeStoreVoices = false, MaxNewTokens = 2, IntraOpThreads = 1 });
        using var backend = model.CreateBackend(_root);
        var badIndex = ((ISynthesisModel)model).ResolveSpeakerId(new Dictionary<string, object> { ["voice"] = "bad" });
        var firstUse = Record.Exception(() => backend.Generate("Hello.", 1.0f, badIndex));

        // Assert
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        Assert.Equal(["bad", "good"], choice.Options.Select(option => option.Value));
        var invalid = Assert.IsType<InvalidDataException>(firstUse);
        Assert.Contains("bad.wav", invalid.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves the language-model variant default.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_Defaults_LanguageModelVariantIsFp16AndAuxiliaryIsQ4()
    {
        // Act
        var options = new ChatterboxTurboModelOptions();

        // Assert
        Assert.Equal(ChatterboxTurboLanguageModelVariant.Fp16, options.LanguageModelVariant);
        Assert.Equal(ChatterboxTurboAuxiliaryModelVariant.Q4, options.AuxiliaryModelVariant);
    }

    /// <summary>Proves the default cap on generated speech tokens.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_Defaults_MaxNewTokensIs1024()
    {
        // Act
        var options = new ChatterboxTurboModelOptions();

        // Assert
        Assert.Equal(1024, options.MaxNewTokens);
    }

    /// <summary>Proves the default cap on speech tokens per text token.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_Defaults_MaxAudioTokensPerTextTokenIs25()
    {
        // Act
        var options = new ChatterboxTurboModelOptions();

        // Assert
        Assert.Equal(25.0, options.MaxAudioTokensPerTextToken);
    }

    /// <summary>Proves the default cap on consecutive repeated speech tokens.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_Defaults_MaxConsecutiveSpeechTokenRepeatsIs12()
    {
        // Act
        var options = new ChatterboxTurboModelOptions();

        // Assert
        Assert.Equal(12, options.MaxConsecutiveSpeechTokenRepeats);
    }

    /// <summary>Proves the default intra-op thread count is half the logical processors between one and six (a default, not a clamp).</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_Defaults_IntraOpThreadsIsHalfTheProcessorsBetweenOneAndSix()
    {
        // Act
        var options = new ChatterboxTurboModelOptions();
        options.IntraOpThreads = 64;

        // Assert
        Assert.Equal(Math.Clamp(Environment.ProcessorCount / 2, 1, 6), ChatterboxTurboModelOptions.DefaultIntraOpThreads);
        Assert.Equal(64, options.IntraOpThreads);
        Assert.Equal(ChatterboxTurboModelOptions.DefaultIntraOpThreads, new ChatterboxTurboModelOptions().IntraOpThreads);
    }

    /// <summary>Proves the default end-of-sentence tag policy moves the tag before the final word.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_Defaults_EndOfSentenceTagPolicyIsMoveBeforeFinalWord()
    {
        // Act
        var options = new ChatterboxTurboModelOptions();

        // Assert
        Assert.Equal(ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord, options.EndOfSentenceTagPolicy);
    }

    /// <summary>Proves no explicit reference voices are configured by default.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_Defaults_NoExplicitVoices()
    {
        // Act
        var options = new ChatterboxTurboModelOptions();

        // Assert
        Assert.Empty(options.ReferenceVoices);
    }

    /// <summary>Proves the default mood join silence is 150 ms.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_Defaults_MoodJoinSilenceIs150Milliseconds()
    {
        // Act
        var options = new ChatterboxTurboModelOptions();

        // Assert
        Assert.Equal(150, options.MoodJoinSilenceMilliseconds);
    }

    /// <summary>Proves store voices are included by default.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_Defaults_StoreVoicesAreIncluded()
    {
        // Act
        var options = new ChatterboxTurboModelOptions();

        // Assert
        Assert.True(options.IncludeStoreVoices);
    }

    /// <summary>Proves no voice directory, voice library or diagnostics sink is configured by default.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_Defaults_NoDirectoryLibraryOrDiagnostics()
    {
        // Act
        var options = new ChatterboxTurboModelOptions();

        // Assert
        Assert.Null(options.ReferenceVoiceDirectory);
        Assert.Null(options.ReferenceVoiceLibrary);
        Assert.Null(options.Diagnostics);
    }

    /// <summary>Proves out-of-range numeric option values are rejected when assigned, and the boundary values are accepted.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_Properties_OutOfRangeValues_ThrowArgumentOutOfRangeException()
    {
        // Arrange
        var options = new ChatterboxTurboModelOptions();

        // Act
        var joinNegative = Record.Exception(() => options.MoodJoinSilenceMilliseconds = -1);
        var joinTooLarge = Record.Exception(() => options.MoodJoinSilenceMilliseconds = 1001);
        var threads = Record.Exception(() => options.IntraOpThreads = 0);
        var maxNewTokens = Record.Exception(() => options.MaxNewTokens = 0);
        var ratio = Record.Exception(() => options.MaxAudioTokensPerTextToken = 0);
        var ratioInfinite = Record.Exception(() => options.MaxAudioTokensPerTextToken = double.PositiveInfinity);
        var repeats = Record.Exception(() => options.MaxConsecutiveSpeechTokenRepeats = -1);
        options.MoodJoinSilenceMilliseconds = 0;
        options.MaxConsecutiveSpeechTokenRepeats = 0;

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(joinNegative);
        Assert.IsType<ArgumentOutOfRangeException>(joinTooLarge);
        Assert.IsType<ArgumentOutOfRangeException>(threads);
        Assert.IsType<ArgumentOutOfRangeException>(maxNewTokens);
        Assert.IsType<ArgumentOutOfRangeException>(ratio);
        Assert.IsType<ArgumentOutOfRangeException>(ratioInfinite);
        Assert.IsType<ArgumentOutOfRangeException>(repeats);
        Assert.Equal(0, options.MoodJoinSilenceMilliseconds);
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

        // Act
        var exception = Record.Exception(() => model.CreateBackend(_root));

        // Assert
        Assert.IsType<InvalidOperationException>(exception);
    }

    /// <summary>Proves creating a backend from an empty model directory path is rejected.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_EmptyDirectory_ThrowsArgumentException()
    {
        // Arrange
        var model = new OnnxChatterboxTurboEnglishSynthesisModel();

        // Act
        var exception = Record.Exception(() => model.CreateBackend(string.Empty));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    /// <summary>Proves creating a backend from a null model directory path is rejected with an <see cref="ArgumentNullException"/>.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_NullDirectory_ThrowsArgumentNullException()
    {
        // Arrange
        var model = new OnnxChatterboxTurboEnglishSynthesisModel();

        // Act
        var exception = Record.Exception(() => model.CreateBackend(null!));

        // Assert
        Assert.IsType<ArgumentNullException>(exception);
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
            // Act
            var exception = Record.Exception(() => model.CreateBackend(_root));

            // Assert
            Assert.IsType<FileNotFoundException>(exception);
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
        var audio = backend.Generate("Hello.", 1.0f, 0);

        // Assert
        Assert.NotEmpty(audio.Samples);
    }

    /// <summary>
    ///     Proves each language-model and auxiliary-graph combination loads exactly the graph files its file names
    ///     select: only files with the expected names (and suffix) are installed, so a backend loads all four sessions
    ///     only when the model opens those names.
    /// </summary>
    /// <param name="languageModel">The language-model variant.</param>
    /// <param name="auxiliary">The auxiliary-graph variant.</param>
    /// <param name="languageModelName">The expected language-model graph base name.</param>
    /// <param name="suffix">The expected auxiliary-graph suffix.</param>
    [Theory]
    [InlineData(ChatterboxTurboLanguageModelVariant.Fp16, ChatterboxTurboAuxiliaryModelVariant.Fp16, "language_model_fp16", "_fp16")]
    [InlineData(ChatterboxTurboLanguageModelVariant.Fp32, ChatterboxTurboAuxiliaryModelVariant.Fp32, "language_model", "")]
    [InlineData(ChatterboxTurboLanguageModelVariant.Fp16, ChatterboxTurboAuxiliaryModelVariant.Fp32, "language_model_fp16", "")]
    [InlineData(ChatterboxTurboLanguageModelVariant.Fp32, ChatterboxTurboAuxiliaryModelVariant.Fp16, "language_model", "_fp16")]
    public void OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_AuxiliaryVariants_LoadMatchingGraphFiles(
        ChatterboxTurboLanguageModelVariant languageModel,
        ChatterboxTurboAuxiliaryModelVariant auxiliary,
        string languageModelName,
        string suffix)
    {
        // Arrange
        var halfAuxiliary = auxiliary == ChatterboxTurboAuxiliaryModelVariant.Fp16;
        ChatterboxTurboGraphFixture.Write(
            Path.Combine(_root, "onnx"),
            languageModelName,
            suffix,
            halfCache: languageModel == ChatterboxTurboLanguageModelVariant.Fp16,
            halfEmbeddings: halfAuxiliary,
            halfAuxiliary: halfAuxiliary);
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions
        {
            ReferenceVoices = [Voice("a")],
            LanguageModelVariant = languageModel,
            AuxiliaryModelVariant = auxiliary,
            IntraOpThreads = 1,
        });
        var created = new List<InferenceSession>();
        OnnxChatterboxTurboEnglishSynthesisModel.OnSessionCreated = created.Add;
        try
        {
            // Act
            using var backend = model.CreateBackend(_root);

            // Assert
            Assert.Equal(4, created.Count);
            Assert.Equal(
                [
                    $"onnx/{languageModelName}.onnx", $"onnx/{languageModelName}.onnx_data",
                    $"onnx/embed_tokens{suffix}.onnx", $"onnx/embed_tokens{suffix}.onnx_data",
                    $"onnx/speech_encoder{suffix}.onnx", $"onnx/speech_encoder{suffix}.onnx_data",
                    $"onnx/conditional_decoder{suffix}.onnx", $"onnx/conditional_decoder{suffix}.onnx_data",
                ],
                model.DownloadDescriptor.Files.Select(file => file.RelativeInstallPath));
        }
        finally
        {
            OnnxChatterboxTurboEnglishSynthesisModel.OnSessionCreated = null;
        }
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
            // Act
            var exception = Record.Exception(() => model.CreateBackend(_root));

            // Assert
            Assert.IsType<InvalidOperationException>(exception);
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

        // Act
        var exception = Record.Exception(() => options.ReferenceVoices = null!);

        // Assert
        Assert.IsType<ArgumentNullException>(exception);
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
        // Arrange
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("same"), Voice("same")], IncludeStoreVoices = false };

        // Act
        var exception = Record.Exception(() => new OnnxChatterboxTurboEnglishSynthesisModel(options));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    /// <summary>Proves a voice-directory file whose id matches an explicit voice is rejected.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_DirectoryVoiceDuplicatesExplicitId_ThrowsArgumentException()
    {
        // Arrange
        var directory = Path.Combine(_root, "dup-explicit");
        Directory.CreateDirectory(directory);
        WriteWav(Path.Combine(directory, "same.wav"));
        var options = new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("same")], ReferenceVoiceDirectory = directory, IncludeStoreVoices = false };

        // Act
        var exception = Record.Exception(() => new OnnxChatterboxTurboEnglishSynthesisModel(options));

        // Assert
        Assert.IsType<ArgumentException>(exception);
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
        var options = new ChatterboxTurboModelOptions { ReferenceVoiceDirectory = directory, IncludeStoreVoices = false };

        // Act
        var exception = Record.Exception(() => new OnnxChatterboxTurboEnglishSynthesisModel(options));

        // Assert
        Assert.IsType<ArgumentException>(exception);
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

        // Act
        var languageModel = Record.Exception(() => options.LanguageModelVariant = (ChatterboxTurboLanguageModelVariant)99);
        var auxiliary = Record.Exception(() => options.AuxiliaryModelVariant = (ChatterboxTurboAuxiliaryModelVariant)99);

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(languageModel);
        Assert.IsType<ArgumentOutOfRangeException>(auxiliary);
        Assert.Equal(ChatterboxTurboLanguageModelVariant.Fp16, options.LanguageModelVariant);
        Assert.Equal(ChatterboxTurboAuxiliaryModelVariant.Q4, options.AuxiliaryModelVariant);
    }

    /// <summary>Proves an undefined end-of-sentence tag policy is rejected when assigned and leaves the policy unchanged.</summary>
    [Fact]
    public void ChatterboxTurboModelOptions_EndOfSentenceTagPolicy_UndefinedValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new ChatterboxTurboModelOptions();

        // Act
        var exception = Record.Exception(() => options.EndOfSentenceTagPolicy = (ChatterboxTurboEndOfSentenceTagPolicy)99);

        // Assert
        Assert.IsType<ArgumentOutOfRangeException>(exception);
        Assert.Equal(ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord, options.EndOfSentenceTagPolicy);
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

    /// <summary>Proves a corrupt library voice fails only at first use with an error naming its file, and another library voice stays usable.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Generate_CorruptStoreVoice_FailsAtFirstUseNamingFileAndOtherVoiceWorks()
    {
        // Arrange
        InstallSynthetic(halfCache: true, "language_model_fp16");
        using var library = new ReferenceVoiceLibrary(
            new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") }));
        var brokenPath = Path.Combine(library.EnsureUserDirectory(), "broken.wav");
        File.WriteAllText(brokenPath, "not audio");
        WriteWav(Path.Combine(library.EnsureUserDirectory(), "good_one.wav"));
        ISynthesisModel model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoiceLibrary = library, MaxNewTokens = 4, IntraOpThreads = 1 });
        using var backend = model.CreateBackend(_root);
        var brokenId = model.ResolveSpeakerId(new Dictionary<string, object> { ["voice"] = "broken" });
        var goodId = model.ResolveSpeakerId(new Dictionary<string, object> { ["voice"] = "good_one" });

        // Act
        var failure = Record.Exception(() => backend.Generate("Hello there.", 1.0f, brokenId));
        var audio = backend.Generate("Hello there.", 1.0f, goodId);

        // Assert
        Assert.NotEqual(brokenId, goodId);
        var invalid = Assert.IsType<InvalidDataException>(failure);
        Assert.Contains("broken.wav", invalid.Message, StringComparison.Ordinal);
        Assert.NotEmpty(audio.Samples);
    }

    /// <summary>Proves repeated display-name collisions each receive a unique numbered source suffix.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_RepeatedLabelClashes_AssignsUniqueLabels()
    {
        // Arrange: one explicit voice plus a user voice and two pack voices that all humanize to "Alan X"
        using var library = new ReferenceVoiceLibrary(
            new SpeechModelStore(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") }));
        WriteWav(Path.Combine(library.EnsureUserDirectory(), "alan_x.wav"));
        var packVoices = Path.Combine(library.RootPath, ReferenceVoicePack.VoiceZero10.Id, "current", "voices");
        Directory.CreateDirectory(packVoices);
        WriteWav(Path.Combine(packVoices, "alan-x.wav"));
        WriteWav(Path.Combine(packVoices, "Alan X.wav"));
        File.WriteAllText(
            Path.Combine(library.RootPath, ReferenceVoicePack.VoiceZero10.Id, "install-manifest.json"),
            "{\"TotalSizeBytes\":1,\"InstalledAtUtc\":\"2026-01-01T00:00:00+00:00\"}");
        var explicitVoice = ChatterboxTurboReferenceVoice.FromSamples("explicit", new float[24000], 24000, displayName: "Alan X");

        // Act
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoices = [explicitVoice], ReferenceVoiceLibrary = library });

        // Assert
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        var labels = choice.Options.Select(option => option.Label).ToArray();
        Assert.Equal(labels.Length, labels.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("Alan X", labels[0]);
        Assert.Equal("Alan X (user)", labels[1]);
        Assert.Equal("Alan X (pack)", labels[2]);
        Assert.Equal("Alan X (pack 2)", labels[3]);
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
