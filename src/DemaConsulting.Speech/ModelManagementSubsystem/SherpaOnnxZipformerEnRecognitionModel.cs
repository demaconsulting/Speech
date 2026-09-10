using DemaConsulting.Speech.AudioSubsystem;
using SherpaOnnx;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Real, production <see cref="IRecognitionModel"/> backing the sherpa-onnx streaming
///     Zipformer2 transducer model <c>streaming-zipformer-en-2023-06-26</c>, converted from
///     k2-fsa/icefall's LibriSpeech-trained streaming Zipformer checkpoint.
/// </summary>
/// <remarks>
///     Per this library's "one backing class per model; a new model requires a new library
///     release" decision, this class owns everything specific to this one model: its declared
///     download descriptor (a single <c>.tar.bz2</c> archive fetched from sherpa-onnx's own
///     GitHub Releases mirror), how to unpack that archive (<see cref="InstallAsync"/>,
///     delegating to the shared <see cref="TarBz2ArchiveExtractor"/>), and how to build the
///     sherpa-onnx streaming-recognizer configuration for it (<c>CreateEngineConfig</c>).
///     <para>
///     <b>Download provenance</b>: <c>https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-streaming-zipformer-en-2023-06-26.tar.bz2</c>,
///     310,414,022 bytes, confirmed directly against the live GitHub Releases asset listing and
///     downloaded end-to-end in this project's development sandbox, whose real SHA-256 hash is
///     the exact value recorded in <see cref="DownloadDescriptor"/> below. The archive's
///     top-level folder (<c>sherpa-onnx-streaming-zipformer-en-2023-06-26/</c>) and file names
///     were confirmed the same way, by inspecting the actually downloaded archive's contents
///     (<c>tar -tvjf</c>), not merely by reading third-party documentation.
///     </para>
///     <para>
///     <b>Engine configuration</b> deliberately wires the <em>int8-quantized</em> encoder/
///     decoder/joiner triple (~70 MiB total), not the fp32 files the same archive also contains,
///     for a leaner default download - this exact int8 configuration (16 kHz input,
///     <c>ModelType = "zipformer2"</c>, <c>DecodingMethod = "greedy_search"</c>,
///     <c>EnableEndpoint = 1</c>) has been proven end-to-end in this project's development
///     sandbox: a real WAV file fed through the real <c>SherpaOnnxRecognitionEngine</c> using
///     this exact model and configuration produced an exact word-for-word transcript match
///     against the model's own published <c>test_wavs/trans.txt</c> ground truth.
///     </para>
///     <para>
///     <b>License</b>: Apache-2.0 - rated LIKELY, not independently confirmed against an
///     explicit model-specific <c>LICENSE</c>/model-card file, since <c>huggingface.co</c> (the
///     model's origin, <c>Zengwei/icefall-asr-librispeech-streaming-zipformer-2023-05-17</c>) is
///     unreachable from this project's development sandboxes. This rating is corroborated by
///     k2-fsa/icefall's own repository license (Apache-2.0, confirmed by directly reading
///     <c>https://raw.githubusercontent.com/k2-fsa/icefall/master/LICENSE</c>) and by independent
///     web-search corroboration, but a maintainer with access to the HuggingFace model card
///     should re-confirm before this model is relied upon in a context requiring a legally
///     certain license determination.
///     </para>
/// </remarks>
public sealed class SherpaOnnxZipformerEnRecognitionModel : IRecognitionModel
{
    /// <summary>The stable catalog identifier for this model.</summary>
    public const string ModelId = "streaming-zipformer-en-2023-06-26";

    /// <summary>The relative install path the declared download archive is written to.</summary>
    private const string ArchiveRelativeInstallPath = "sherpa-onnx-streaming-zipformer-en-2023-06-26.tar.bz2";

    /// <summary>The archive's own top-level folder name, preserved by extraction.</summary>
    private const string ExtractedFolderName = "sherpa-onnx-streaming-zipformer-en-2023-06-26";

    /// <summary>The sample rate, in Hz, this streaming Zipformer model was trained at.</summary>
    private const int ModelSampleRate = 16000;

    /// <summary>The feature dimension this streaming Zipformer model was trained at (icefall/LibriSpeech convention).</summary>
    private const int ModelFeatureDim = 80;

    /// <summary>Initializes a new instance of the <see cref="SherpaOnnxZipformerEnRecognitionModel"/> class.</summary>
    public SherpaOnnxZipformerEnRecognitionModel()
    {
    }

    /// <inheritdoc/>
    public string Id => ModelId;

    /// <inheritdoc/>
    public string DisplayName => "Zipformer English (Streaming)";

    /// <summary>
    ///     <inheritdoc/>
    ///     See the type-level remarks' "License" paragraph for why this is rated LIKELY rather
    ///     than certain, and why that uncertainty is preserved verbatim in this string.
    /// </summary>
    public string LicenseName => "Apache-2.0 (likely)";

    /// <inheritdoc/>
    public Uri? LicenseUrl { get; } = new("https://www.apache.org/licenses/LICENSE-2.0");

    /// <inheritdoc/>
    public SpeechModelRole Role => SpeechModelRole.Recognition;

    /// <inheritdoc/>
    /// <remarks>This model declares no tunable parameters beyond its fixed, proven engine configuration.</remarks>
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; } = [];

    /// <inheritdoc/>
    /// <remarks>A plain streaming transducer ASR model has no inline Natural Language Audio Tag concept - that vocabulary applies to synthesis input text, not recognition output.</remarks>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

    /// <inheritdoc/>
    /// <remarks>
    ///     Declares exactly one file: the model's own published <c>.tar.bz2</c> archive. See the
    ///     type-level remarks for the exact byte count, SHA-256 provenance, and download URL
    ///     confirmation performed in this project's development sandbox.
    /// </remarks>
#pragma warning disable S1075 // This model's download URL is intentionally a compiled-in, reviewed literal - the whole point of a catalog entry is to name one specific, checksum-verified release asset.
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = new(
    [
        new SpeechModelDownloadFile(
            new Uri("https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-streaming-zipformer-en-2023-06-26.tar.bz2"),
            "639e25b578e9e997131402199419c13a941f8e4e198e2da1ce57dbf5cf401282",
            ArchiveRelativeInstallPath),
    ]);
#pragma warning restore S1075

    /// <inheritdoc/>
    AudioFormat IRecognitionModel.AudioFormat => AudioFormat.Mono(ModelSampleRate);

    /// <summary>
    ///     Restores casing, contractions, and terminal punctuation on this model's raw
    ///     recognition output using the shared <see cref="UppercaseTranscriptRestorer"/>.
    /// </summary>
    /// <remarks>
    ///     <inheritdoc/>
    ///     This model's raw output was empirically confirmed - by synthesizing real audio with the
    ///     shipped LibriTTS voice and feeding it through the real recognizer - to be UPPERCASE and
    ///     unpunctuated (for example, <c>"I DON'T THINK THAT'S WORKING AND I CAN'T TELL WHY IT
    ///     ISN'T"</c> rather than <c>"I don't think that's working, and I can't tell why it
    ///     isn't."</c>). Apostrophes within contractions are, in practice, already present in the
    ///     raw output, so the contraction-restoration stage is typically a no-op for this model;
    ///     it still lower-cases, recapitalizes, and adds terminal punctuation, matching the
    ///     "yelling" raw-output style <see cref="UppercaseTranscriptRestorer"/> is designed to fix.
    ///     Finalized results are fully restored (<see cref="UppercaseTranscriptRestorer.RestoreFinal"/>);
    ///     provisional results receive only cheap casing
    ///     (<see cref="UppercaseTranscriptRestorer.RestoreProvisional"/>) since they may still be
    ///     revised before settling.
    /// </remarks>
    string IRecognitionModel.NormalizeText(string text, bool isFinal) =>
        isFinal ? UppercaseTranscriptRestorer.RestoreFinal(text) : UppercaseTranscriptRestorer.RestoreProvisional(text);

    /// <summary>
    ///     Extracts the downloaded <c>.tar.bz2</c> archive in place using the shared
    ///     <see cref="TarBz2ArchiveExtractor"/>, then deletes the archive file, leaving the
    ///     archive's own top-level folder (<see cref="ExtractedFolderName"/>) containing every
    ///     file <c>CreateEngineConfig</c> references.
    /// </summary>
    /// <inheritdoc/>
    public Task InstallAsync(string stagedFilesDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(stagedFilesDirectory);

        var archivePath = Path.Join(stagedFilesDirectory, ArchiveRelativeInstallPath);
        return TarBz2ArchiveExtractor.ExtractAndDeleteAsync(archivePath, stagedFilesDirectory, cancellationToken);
    }

    /// <summary>
    ///     Builds the proven int8 streaming Zipformer2 transducer configuration for this model,
    ///     resolving every file path against the archive's extracted top-level folder within
    ///     <paramref name="installedModelDirectory"/>.
    /// </summary>
    /// <inheritdoc/>
    OnlineRecognizerConfig IRecognitionModel.CreateEngineConfig(string installedModelDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        var modelDirectory = Path.Join(installedModelDirectory, ExtractedFolderName);

        var config = new OnlineRecognizerConfig();
        config.FeatConfig.SampleRate = ModelSampleRate;
        config.FeatConfig.FeatureDim = ModelFeatureDim;

        // The int8-quantized triple, not the same archive's fp32 files, for a leaner default
        // download - see the type-level remarks for the end-to-end proof of this exact config.
        config.ModelConfig.Transducer.Encoder =
            Path.Join(modelDirectory, "encoder-epoch-99-avg-1-chunk-16-left-128.int8.onnx");
        config.ModelConfig.Transducer.Decoder =
            Path.Join(modelDirectory, "decoder-epoch-99-avg-1-chunk-16-left-128.int8.onnx");
        config.ModelConfig.Transducer.Joiner =
            Path.Join(modelDirectory, "joiner-epoch-99-avg-1-chunk-16-left-128.int8.onnx");
        config.ModelConfig.Tokens = Path.Join(modelDirectory, "tokens.txt");
        config.ModelConfig.ModelType = "zipformer2";
        config.ModelConfig.Provider = "cpu";
        config.ModelConfig.NumThreads = 1;

        config.DecodingMethod = "greedy_search";
        config.EnableEndpoint = 1;
        config.Rule1MinTrailingSilence = 2.4f;
        config.Rule2MinTrailingSilence = 1.2f;
        config.Rule3MinUtteranceLength = 20f;

        return config;
    }
}
