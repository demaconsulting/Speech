using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="OnnxNemotronMultilingualRecognitionModel"/>: catalog metadata,
///     the 11-file download descriptor, the language parameter, the engine-options overload, and
///     error handling. Real-model transcription is covered by the system tests.
/// </summary>
public sealed class OnnxNemotronMultilingualRecognitionModelTests
{
    /// <summary>The expected install paths in declaration order.</summary>
    private static readonly string[] ExpectedFiles =
    [
        "encoder.onnx", "encoder.onnx.data", "decoder.onnx", "decoder.onnx.data", "joint.onnx",
        "joint.onnx.data", "vocab.txt", "tokenizer.json", "tokenizer_config.json",
        "genai_config.json", "audio_processor_config.json",
    ];

    /// <summary>Proves the identity and descriptive metadata.</summary>
    [Fact]
    public void Metadata_IsDeclared()
    {
        var model = new OnnxNemotronMultilingualRecognitionModel();

        Assert.Equal("nemotron-3.5-asr-streaming-0.6b-onnx-int4", model.Id);
        Assert.Equal(OnnxNemotronMultilingualRecognitionModel.ModelId, model.Id);
        Assert.Equal(SpeechModelRole.Recognition, model.Role);
        Assert.Equal("OpenMDW-1.1", model.LicenseName);
        Assert.Equal(new Uri("https://openmdw.ai/license/1-1/"), model.LicenseUrl);
        Assert.False(string.IsNullOrWhiteSpace(model.DisplayName));
        Assert.Equal(SpeechModelAudioTagSupport.None, model.AudioTagSupport);
        Assert.Equal(16000, model.AudioFormat.SampleRate);
        Assert.Equal(1, model.AudioFormat.ChannelCount);
    }

    /// <summary>Proves the download descriptor lists the 11 files with valid checksums and Hugging Face URIs.</summary>
    [Fact]
    public void DownloadDescriptor_ListsElevenFiles()
    {
        var files = new OnnxNemotronMultilingualRecognitionModel().DownloadDescriptor.Files;

        Assert.Equal(ExpectedFiles, files.Select(f => f.RelativeInstallPath));
        Assert.All(files, f =>
        {
            Assert.Matches("^[0-9a-f]{64}$", f.Sha256Checksum);
            Assert.Equal(
                $"https://huggingface.co/onnx-community/nemotron-3.5-asr-streaming-0.6b-onnx-int4/resolve/main/{f.RelativeInstallPath}",
                f.Uri.AbsoluteUri);
        });
        Assert.Equal(11, files.Select(f => f.Sha256Checksum).Distinct().Count());
    }

    /// <summary>Proves the single language choice parameter defaults to en-US and offers en-GB.</summary>
    [Fact]
    public void Parameters_LanguageChoice()
    {
        var parameter = Assert.IsType<ChoiceParameter>(Assert.Single(new OnnxNemotronMultilingualRecognitionModel().Parameters));

        Assert.Equal("language", parameter.Id);
        Assert.Equal("en-US", parameter.Default);
        Assert.Equal(["en-US", "en-GB"], parameter.Options.Select(o => o.Value));
    }

    /// <summary>Proves an empty model directory is rejected.</summary>
    [Fact]
    public void CreateBackend_EmptyDirectory_Throws()
    {
        var model = new OnnxNemotronMultilingualRecognitionModel();

        Assert.Throws<ArgumentException>(() => model.CreateBackend(string.Empty));
    }

    /// <summary>Proves a directory without the model files fails.</summary>
    [Fact]
    public void CreateBackend_MissingFiles_Throws()
    {
        var model = new OnnxNemotronMultilingualRecognitionModel();
        var directory = Path.Join(Path.GetTempPath(), $"nemotron-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            Assert.ThrowsAny<IOException>(() => model.CreateBackend(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Proves a vocabulary lacking the requested locale is rejected before any session is created.</summary>
    [Fact]
    public void CreateBackend_VocabularyWithoutLocale_Throws()
    {
        var model = new OnnxNemotronMultilingualRecognitionModel();
        var directory = Path.Join(Path.GetTempPath(), $"nemotron-vocab-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Join(directory, "vocab.txt"), ["<unk>", "a", "<blank>"]);
        try
        {
            Assert.Throws<InvalidOperationException>(() => model.CreateBackend(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Proves the engine-options overload rejects an invalid blank penalty before reading any file.</summary>
    [Fact]
    public void OnnxNemotronMultilingualRecognitionModel_CreateBackend_NegativeBlankPenalty_ThrowsArgumentOutOfRangeException()
    {
        // Arrange: a model and a directory that does not exist (it must never be read)
        var model = new OnnxNemotronMultilingualRecognitionModel();
        var directory = Path.Join(Path.GetTempPath(), $"nemotron-missing-{Guid.NewGuid():N}");

        // Act / Assert: the penalty is rejected first
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            model.CreateBackend(directory, null, new NemotronEngineOptions(BlankPenalty: -1f)));
    }

    /// <summary>Proves the engine-options overload rejects null options.</summary>
    [Fact]
    public void OnnxNemotronMultilingualRecognitionModel_CreateBackend_NullEngineOptions_ThrowsArgumentNullException()
    {
        // Arrange: a model
        var model = new OnnxNemotronMultilingualRecognitionModel();

        // Act / Assert: null options are rejected
        Assert.Throws<ArgumentNullException>(() => model.CreateBackend("models", null, null!));
    }
}
