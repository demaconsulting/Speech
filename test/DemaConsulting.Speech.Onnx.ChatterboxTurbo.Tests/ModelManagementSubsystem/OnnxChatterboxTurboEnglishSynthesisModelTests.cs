using DemaConsulting.Speech.AudioSubsystem;
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

    /// <summary>Proves voices are loaded from a directory of WAV files in ordinal order after explicit voices.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_Constructor_VoiceDirectory_LoadsWavFiles()
    {
        // Arrange
        var directory = Path.Combine(_root, "voices");
        Directory.CreateDirectory(directory);
        WriteWav(Path.Combine(directory, "zed.wav"));
        WriteWav(Path.Combine(directory, "amy.wav"));
        File.WriteAllText(Path.Combine(directory, "ignored.txt"), "x");

        // Act
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("first")], ReferenceVoiceDirectory = directory });

        // Assert
        var choice = Assert.IsType<ChoiceParameter>(Assert.Single(model.Parameters));
        Assert.Equal(["first", "amy", "zed"], choice.Options.Select(option => option.Value));
        Assert.Throws<DirectoryNotFoundException>(() => new OnnxChatterboxTurboEnglishSynthesisModel(
            new ChatterboxTurboModelOptions { ReferenceVoiceDirectory = Path.Combine(_root, "nope") }));
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
        Assert.InRange(options.IntraOpThreads, 1, 6);
        Assert.Equal(ChatterboxTurboEndOfSentenceTagPolicy.MoveBeforeFinalWord, options.EndOfSentenceTagPolicy);
        Assert.Empty(options.ReferenceVoices);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.IntraOpThreads = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxNewTokens = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxAudioTokensPerTextToken = 0);
    }

    /// <summary>Proves creating a backend without voices fails with a clear message.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_NoVoices_Throws()
    {
        // Arrange
        var model = new OnnxChatterboxTurboEnglishSynthesisModel();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => model.CreateBackend(_root));
        Assert.Throws<ArgumentException>(() => model.CreateBackend(string.Empty));
    }

    /// <summary>Proves a missing graph file is reported before any session is created.</summary>
    [Fact]
    public void OnnxChatterboxTurboEnglishSynthesisModel_CreateBackend_MissingFile_ThrowsFileNotFound()
    {
        // Arrange
        var model = new OnnxChatterboxTurboEnglishSynthesisModel(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a")] });

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => model.CreateBackend(_root));
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

    /// <summary>Proves the extension method registers the model and returns the same catalog.</summary>
    [Fact]
    public void SpeechModelCatalogChatterboxTurboExtensions_AddChatterboxTurboModels_RegistersModelAndReturnsCatalog()
    {
        // Arrange
        using var catalog = new SpeechModelCatalog(new SpeechModelStoreOptions { RootPathOverride = Path.Combine(_root, "store") });

        // Act
        var result = catalog.AddChatterboxTurboModels(new ChatterboxTurboModelOptions { ReferenceVoices = [Voice("a")] });

        // Assert
        Assert.Same(catalog, result);
        var model = Assert.Single(catalog.Enumerate());
        Assert.Equal("chatterbox-turbo-onnx-en", model.Id);
        Assert.Throws<ArgumentNullException>(() => SpeechModelCatalogChatterboxTurboExtensions.AddChatterboxTurboModels(null!));
    }

    /// <summary>Creates a one-second voice.</summary>
    private static ChatterboxTurboReferenceVoice Voice(string id) =>
        ChatterboxTurboReferenceVoice.FromSamples(id, new float[24000], 24000);

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
