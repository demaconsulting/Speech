using System.IO.Compression;
using System.Text.Json;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="OnnxNemotronMultilingualRecognitionModel"/>: catalog metadata,
///     the <c>models</c>-release bundle and its 11 pinned vendor files, the language parameter, the
///     engine-options overload, and error handling. Real-model transcription is covered by the system tests.
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

    /// <summary>
    ///     Proves the bundle's contents pin the 11 vendor files with valid checksums and record their original
    ///     Hugging Face URIs.
    /// </summary>
    [Fact]
    public void OnnxNemotronMultilingualRecognitionModel_BundleContents_PinsEveryVendorFile()
    {
        // Arrange
        var bundle = Assert.Single(OnnxNemotronMultilingualRecognitionModel.Bundles);

        // Act
        var files = bundle.Contents.Files;

        // Assert
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

    /// <summary>
    ///     Proves the model downloads exactly one zip, the pinned Nemotron bundle on the project's <c>models</c>
    ///     release.
    /// </summary>
    [Fact]
    public void OnnxNemotronMultilingualRecognitionModel_DownloadDescriptor_IsModelsReleaseBundle()
    {
        // Arrange
        var model = new OnnxNemotronMultilingualRecognitionModel();

        // Act
        var file = Assert.Single(model.DownloadDescriptor.Files);

        // Assert
        var bundle = Assert.Single(OnnxNemotronMultilingualRecognitionModel.Bundles);
        Assert.Equal("nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1", bundle.Name);
        Assert.Equal(
            new Uri("https://github.com/demaconsulting/Speech/releases/download/models/nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1.zip"),
            file.Uri);
        Assert.Equal("733375bb604712afac6c4adbd6a9149ebc414fc9a5bee39ed3bdfc3c104048b2", file.Sha256Checksum);
        Assert.Equal("nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1.zip", file.RelativeInstallPath);
    }

    /// <summary>
    ///     Proves installing a well-formed bundle whose vendor bytes do not match the compiled pins throws
    ///     <see cref="InvalidDataException"/>, so a tampered bundle is never installed.
    /// </summary>
    [Fact]
    public async Task OnnxNemotronMultilingualRecognitionModel_InstallAsync_BundleWithWrongFile_Throws()
    {
        // Arrange: stage the model's bundle with every vendor file replaced by other bytes
        var model = new OnnxNemotronMultilingualRecognitionModel();
        var stagingDirectory = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Onnx.NemotronStt.Tests", Guid.NewGuid().ToString("N"));
        StageBundleWithWrongFiles(Assert.Single(OnnxNemotronMultilingualRecognitionModel.Bundles), stagingDirectory);
        try
        {
            // Act / Assert: verification rejects the extracted bytes
            await Assert.ThrowsAsync<InvalidDataException>(
                () => model.InstallAsync(stagingDirectory, TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(stagingDirectory, recursive: true);
        }
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

    /// <summary>
    ///     Stages a well-formed bundle zip for <paramref name="bundle"/> whose <c>model.json</c> declares every vendor
    ///     file at its compiled pin, but whose vendor entries hold other bytes - so only the extracted-bytes
    ///     verification can reject it.
    /// </summary>
    /// <param name="bundle">The bundle to imitate.</param>
    /// <param name="directory">The staging directory to write <c>{bundle.FileName}</c> into.</param>
    private static void StageBundleWithWrongFiles(SpeechModelBundle bundle, string directory)
    {
        var wrongBytes = "these are not the vendor bytes"u8.ToArray();
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1,
            bundle = bundle.Name,
            files = bundle.Contents.Files
                .Select(file => new { path = file.RelativeInstallPath, size = wrongBytes.Length, sha256 = file.Sha256Checksum })
                .ToArray(),
        });

        Directory.CreateDirectory(directory);
        using var archive = ZipFile.Open(Path.Join(directory, bundle.FileName), ZipArchiveMode.Create);
        WriteEntry(archive, "README.txt", "Test bundle."u8.ToArray());
        WriteEntry(archive, "LICENSE", "Test license."u8.ToArray());
        WriteEntry(archive, "model.json", manifest);
        foreach (var file in bundle.Contents.Files)
        {
            WriteEntry(archive, file.RelativeInstallPath, wrongBytes);
        }
    }

    /// <summary>Writes one stored entry into a zip being created.</summary>
    /// <param name="archive">The archive in create mode.</param>
    /// <param name="name">The entry name.</param>
    /// <param name="content">The entry bytes.</param>
    private static void WriteEntry(ZipArchive archive, string name, byte[] content)
    {
        using var stream = archive.CreateEntry(name, CompressionLevel.NoCompression).Open();
        stream.Write(content, 0, content.Length);
    }
}
