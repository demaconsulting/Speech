using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="OnnxNemotronMultilingualRecognitionModel"/>: catalog metadata,
///     the 11-file download descriptor, the language parameter, and error handling. A real-model
///     transcription test runs only when the model files are present locally.
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
        Assert.Equal("NVIDIA Open Model License", model.LicenseName);
        Assert.NotNull(model.LicenseUrl);
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

    /// <summary>
    ///     Transcribes the repository's spike audio with the real model when the files are present;
    ///     otherwise returns without asserting (the model is ~790 MB and never a unit-test requirement).
    /// </summary>
    [Fact]
    public void CreateBackend_RealModel_TranscribesWhenPresent()
    {
        var modelDirectory = FindModelDirectory();
        if (modelDirectory is null)
        {
            return;
        }

        var wav = Path.Join(Path.GetDirectoryName(modelDirectory)!, "yes.wav");
        if (!File.Exists(wav))
        {
            return;
        }

        using var backend = new OnnxNemotronMultilingualRecognitionModel().CreateBackend(modelDirectory);
        var bytes = File.ReadAllBytes(wav);
        var samples = new List<float>();
        for (var i = 44; i + 1 < bytes.Length; i += 2)
        {
            samples.Add(BitConverter.ToInt16(bytes, i) / 32768f);
        }

        // The spike WAV is 24 kHz; decimate 3:2 by linear interpolation to the model's 16 kHz.
        var resampled = new float[samples.Count * 2 / 3];
        for (var i = 0; i < resampled.Length; i++)
        {
            var position = i * 1.5;
            var index = (int)position;
            var fraction = (float)(position - index);
            var next = Math.Min(index + 1, samples.Count - 1);
            resampled[i] = (samples[index] * (1 - fraction)) + (samples[next] * fraction);
        }

        backend.AcceptSamples(resampled);
        backend.AcceptSamples(new float[16000]);
        var found = backend.TryFlush(out var result);

        Assert.True(found);
        Assert.Contains("yes", result!.Text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Locates the local model directory via NEMOTRON_MODEL_DIR or the repository spike folder.</summary>
    private static string? FindModelDirectory()
    {
        var configured = Environment.GetEnvironmentVariable("NEMOTRON_MODEL_DIR");
        if (!string.IsNullOrEmpty(configured) && File.Exists(Path.Join(configured, "encoder.onnx")))
        {
            return configured;
        }

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Join(directory.FullName, "tools", "NemotronSpike", "models");
            if (File.Exists(Path.Join(candidate, "encoder.onnx")))
            {
                return candidate;
            }
        }

        return null;
    }
}
