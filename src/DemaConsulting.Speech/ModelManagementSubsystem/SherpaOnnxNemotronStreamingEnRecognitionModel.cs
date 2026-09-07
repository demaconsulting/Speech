using DemaConsulting.Speech.AudioSubsystem;
using SherpaOnnx;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Real, production <see cref="IRecognitionModel"/> backing the sherpa-onnx streaming
///     NVIDIA Nemotron cache-aware FastConformer-RNNT transducer model
///     <c>nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25</c> (int8-quantized, 560ms
///     chunk latency, 16 kHz mono input).
/// </summary>
/// <remarks>
///     Structurally, this class is deliberately near-identical to
///     <see cref="SherpaOnnxZipformerEnRecognitionModel"/>: both this model's file layout
///     (encoder/decoder/joiner triple plus <c>tokens.txt</c>) and its sherpa-onnx configuration
///     surface (<see cref="OnlineModelConfig"/>'s single <c>Transducer</c> sub-struct) are the
///     same shape used by every transducer-family streaming online model, Zipformer and
///     NeMo/Nemotron alike - tracing sherpa-onnx's own native dispatch source
///     (<c>OnlineRecognizerImpl::Create</c> in <c>online-recognizer-impl.cc</c>) confirms the
///     native library chooses the correct internal NeMo-cache-aware decoding path automatically,
///     by inspecting metadata baked into the decoder <c>.onnx</c> file itself at load time - no
///     distinct <see cref="OnlineModelConfig"/> field or sub-struct exists for that choice on the
///     CPU provider path this project uses. Only literal file paths, license/display metadata,
///     and (per that same tracing) the omission of a Zipformer-specific
///     <see cref="OnlineModelConfig.ModelType"/> value differ from the Zipformer class.
///     Like Zipformer, this model expects 16 kHz mono input (the common convention for this
///     model family, matching the sample rate reported by <see cref="IRecognitionModel.AudioFormat"/>);
///     that value has not been independently confirmed against an unreachable HuggingFace model
///     card, but is consistent with sherpa-onnx's own shipped configuration for this archive.
///     <para>
///     <b>Download provenance</b>: <c>https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25.tar.bz2</c>,
///     463,945,051 bytes, confirmed directly against the live GitHub Releases asset listing and
///     downloaded end-to-end in this project's development sandbox, whose real SHA-256 hash is
///     the exact value recorded in <see cref="DownloadDescriptor"/> below. The archive's
///     top-level folder and file names (<c>encoder.int8.onnx</c>, <c>decoder.int8.onnx</c>,
///     <c>joiner.int8.onnx</c>, <c>tokens.txt</c>) were confirmed the same way, by inspecting the
///     actually downloaded archive's contents (<c>tar -tvjf</c>) - this model ships int8-only
///     (no fp32 variant), so unlike the Zipformer model there is no smaller-download alternative
///     to choose between.
///     </para>
///     <para>
///     <b>License - materially different from every other model in this pass</b>: the underlying
///     NVIDIA model (<c>nvidia/nemotron-speech-streaming-en-0.6b</c> on HuggingFace) is released
///     under the <b>NVIDIA Open Model License</b>
///     (<c>https://www.nvidia.com/en-us/agreements/enterprise-software/nvidia-open-model-license/</c>),
///     a custom, NVIDIA-authored license - <em>not</em> Apache-2.0, MIT, or any other widely-used
///     OSI-approved permissive license like <see cref="SherpaOnnxZipformerEnRecognitionModel"/>'s
///     license. This project does not bundle or redistribute NVIDIA's model weights: this class
///     stores only metadata (identity, description, capability profile) and a
///     <see cref="SpeechModelDownloadDescriptor"/> pointing at sherpa-onnx's own official GitHub
///     Releases mirror; the actual model bytes are fetched by the consumer's own machine, on
///     their own explicit <see cref="SpeechModelCatalog.DownloadAsync"/> call, exactly like every
///     other model in this catalog. Whether the NVIDIA Open Model License's field-of-use and
///     redistribution terms permit a given consumer's specific intended use is a legal/compliance
///     question outside this library's competence to certify - a maintainer/legal reviewer should
///     read the license text directly before relying on this model in a context requiring a
///     legally certain determination. This is flagged as a named risk in the phase 7 planning
///     report (<c>.agent-logs/planning-speech-phase7-c48a07.md</c>, Risk #1) and is not resolved
///     by this class's existence - it is a build-time metadata/download registration only.
///     </para>
///     <para>
///     <b>Text normalization - intentionally the pass-through default, not a restorer override</b>:
///     unlike <see cref="SherpaOnnxZipformerEnRecognitionModel"/>, this class does not override
///     <see cref="IRecognitionModel.NormalizeText"/>. This was decided empirically, not assumed:
///     synthesizing real audio with the shipped LibriTTS voice and feeding it through the real
///     recognizer showed this model's raw output already reads as normal prose - mixed case with
///     the sentence-initial word capitalized and contractions already apostrophized (for example,
///     <c>"Don't think that's working and I can't tell why it isn't"</c>), unlike the Zipformer
///     model's UPPERCASE "yelling" raw output. The only gap observed was missing terminal
///     punctuation, which is outside the "yelling" raw-output style
///     <see cref="UppercaseTranscriptRestorer"/> targets, so this model keeps
///     <see cref="IRecognitionModel"/>'s pass-through <c>NormalizeText</c> default.
///     </para>
/// </remarks>
public sealed class SherpaOnnxNemotronStreamingEnRecognitionModel : IRecognitionModel
{
    /// <summary>The stable catalog identifier for this model.</summary>
    public const string ModelId = "nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25";

    /// <summary>The relative install path the declared download archive is written to.</summary>
    private const string ArchiveRelativeInstallPath =
        "sherpa-onnx-nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25.tar.bz2";

    /// <summary>The archive's own top-level folder name, preserved by extraction.</summary>
    private const string ExtractedFolderName =
        "sherpa-onnx-nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25";

    /// <summary>
    ///     The sample rate, in Hz, this streaming NeMo/FastConformer model expects (the common
    ///     convention for this model family; not independently confirmed against an unreachable
    ///     HuggingFace model card - see the type-level remarks).
    /// </summary>
    private const int ModelSampleRate = 16000;

    /// <summary>Initializes a new instance of the <see cref="SherpaOnnxNemotronStreamingEnRecognitionModel"/> class.</summary>
    public SherpaOnnxNemotronStreamingEnRecognitionModel()
    {
    }

    /// <inheritdoc/>
    public string Id => ModelId;

    /// <inheritdoc/>
    public string DisplayName => "NVIDIA Nemotron English (Streaming, 560ms)";

    /// <summary>
    ///     <inheritdoc/>
    ///     See the type-level remarks' "License - materially different from every other model
    ///     in this pass" paragraph - this is a custom, NVIDIA-authored license, not Apache-2.0,
    ///     MIT, or any other widely-used OSI-approved permissive license.
    /// </summary>
    public string LicenseName => "NVIDIA Open Model License";

    /// <inheritdoc/>
    public Uri? LicenseUrl { get; } =
        new("https://www.nvidia.com/en-us/agreements/enterprise-software/nvidia-open-model-license/");

    /// <inheritdoc/>
    public SpeechModelRole Role => SpeechModelRole.Recognition;

    /// <inheritdoc/>
    /// <remarks>This model declares no tunable parameters beyond its fixed engine configuration.</remarks>
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; } = [];

    /// <inheritdoc/>
    /// <remarks>A plain streaming transducer ASR model has no inline Natural Language Audio Tag concept - that vocabulary applies to synthesis input text, not recognition output.</remarks>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

    /// <inheritdoc/>
    /// <remarks>
    ///     Declares exactly one file: the model's own published <c>.tar.bz2</c> archive, fetched
    ///     directly from sherpa-onnx's/k2-fsa's own official GitHub Releases mirror - this
    ///     project never bundles or redistributes the underlying NVIDIA model weights itself. See
    ///     the type-level remarks for the exact byte count, SHA-256 provenance, and the
    ///     NVIDIA Open Model License caveat.
    /// </remarks>
#pragma warning disable S1075 // This model's download URL is intentionally a compiled-in, reviewed literal - the whole point of a catalog entry is to name one specific, checksum-verified release asset.
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = new(
    [
        new SpeechModelDownloadFile(
            new Uri("https://github.com/k2-fsa/sherpa-onnx/releases/download/asr-models/sherpa-onnx-nemotron-speech-streaming-en-0.6b-560ms-int8-2026-04-25.tar.bz2"),
            "78e2b79fcf7271553a74402a76b771b09ea40117a39566a79f52235b23db6358",
            ArchiveRelativeInstallPath),
    ]);
#pragma warning restore S1075

    /// <inheritdoc/>
    AudioFormat IRecognitionModel.AudioFormat => AudioFormat.Mono(ModelSampleRate);

    /// <summary>
    ///     The empirically validated post-endpoint warm-up-replay window, in milliseconds, for
    ///     this model only.
    /// </summary>
    /// <remarks>
    ///     <inheritdoc/>
    ///     This model - unlike <see cref="SherpaOnnxZipformerEnRecognitionModel"/> - has a
    ///     confirmed, reported word-loss defect: its endpoint detector can fire as a false
    ///     positive mid-utterance, and the resulting <c>Reset()</c> exposes a measured ~550ms
    ///     encoder warm-up blackout that silently swallows genuinely spoken audio landing in that
    ///     window (reproduced on two real user recordings, one of them a genuine live-failure
    ///     capture). A window-size sweep from 400ms-1500ms against both real recordings found
    ///     600ms the minimum needed to reliably recover the lost words, with no duplication
    ///     observed at any tested size up to 1500ms; <b>800ms</b> was chosen as the production
    ///     value - comfortable margin above the 600ms minimum, well below where any issue was
    ///     observed - and recorded exactly as validated in
    ///     <c>.agent-logs/planning-nemotron-endpoint-word-loss-warmup-replay-fix-9d4b71.md</c>.
    ///     <c>Rule1MinTrailingSilence</c>/<c>Rule2MinTrailingSilence</c>/<c>Rule3MinUtteranceLength</c>
    ///     above are deliberately left unchanged - this fix decouples "when reset fires" from
    ///     "whether audio is lost across it" rather than tuning endpoint sensitivity.
    /// </remarks>
    int IRecognitionModel.PostEndpointWarmupWindowMs => 800;

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

        var archivePath = Path.Combine(stagedFilesDirectory, ArchiveRelativeInstallPath);
        return TarBz2ArchiveExtractor.ExtractAndDeleteAsync(archivePath, stagedFilesDirectory, cancellationToken);
    }

    /// <summary>
    ///     Builds the same <see cref="OnlineModelConfig"/>.<c>Transducer</c> configuration shape
    ///     used by <see cref="SherpaOnnxZipformerEnRecognitionModel"/>, resolving every file path
    ///     against the archive's extracted top-level folder within
    ///     <paramref name="installedModelDirectory"/>. Deliberately leaves
    ///     <see cref="OnlineModelConfig.ModelType"/> unset: sherpa-onnx's native dispatch code
    ///     chooses the NeMo-cache-aware decoding path automatically from the decoder file's own
    ///     metadata, not from this field, on the CPU provider path this project uses (see the
    ///     type-level remarks).
    /// </summary>
    /// <inheritdoc/>
    OnlineRecognizerConfig IRecognitionModel.CreateEngineConfig(string installedModelDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        var modelDirectory = Path.Combine(installedModelDirectory, ExtractedFolderName);

        var config = new OnlineRecognizerConfig();
        config.FeatConfig.SampleRate = ModelSampleRate;

        config.ModelConfig.Transducer.Encoder = Path.Combine(modelDirectory, "encoder.int8.onnx");
        config.ModelConfig.Transducer.Decoder = Path.Combine(modelDirectory, "decoder.int8.onnx");
        config.ModelConfig.Transducer.Joiner = Path.Combine(modelDirectory, "joiner.int8.onnx");
        config.ModelConfig.Tokens = Path.Combine(modelDirectory, "tokens.txt");
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
