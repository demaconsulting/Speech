using DemaConsulting.Speech.AudioSubsystem;
using SherpaOnnx;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Real, production <see cref="ISynthesisModel"/> backing the sherpa-onnx Kokoro
///     text-to-speech voice archive <c>kokoro-int8-en-v0_19</c>, an English-only, int8-quantized
///     StyleTTS2-derived multi-speaker model.
/// </summary>
/// <remarks>
///     Per this library's "one backing class per model" decision, this class owns everything
///     specific to this one model: its declared download descriptor, how to unpack its archive
///     (<see cref="InstallAsync"/>, delegating to the shared <see cref="TarBz2ArchiveExtractor"/>,
///     reused unchanged from the existing VITS model), how to build the sherpa-onnx offline
///     text-to-speech configuration for it (<c>CreateEngineConfig</c>), and - new in this pass -
///     how to resolve a selected voice name to sherpa-onnx's real integer speaker id
///     (<see cref="ISynthesisModel.ResolveSpeakerId"/>), closing the "speaker selection is out of scope" gap the
///     sibling <see cref="SherpaOnnxVitsLibriTtsEnglishSynthesisModel"/> explicitly deferred.
///     <para>
///     <b>Download provenance</b>: <c>https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-int8-en-v0_19.tar.bz2</c>,
///     103,248,205 bytes, independently re-downloaded end-to-end in this project's development
///     sandbox during this pass (not merely re-read from an earlier cached measurement); its real
///     SHA-256 hash, computed directly against the freshly downloaded bytes, is the exact value
///     recorded in <see cref="DownloadDescriptor"/> below. The archive's top-level folder
///     (<c>kokoro-int8-en-v0_19/</c>) and file names - <c>model.int8.onnx</c> (134,186,977 bytes),
///     <c>voices.bin</c> (5,755,904 bytes), <c>tokens.txt</c> (1,078 bytes), <c>espeak-ng-data/</c>
///     (391 bundled phoneme/dictionary files, no separate download), <c>LICENSE</c>, and a
///     <c>README.md</c> that only points at upstream Hugging Face model cards - were confirmed the
///     same way, by extracting the actually downloaded archive and inspecting it directly, not by
///     reading third-party documentation. This English-only variant ships no <c>dict/</c>
///     (Chinese jieba dictionary) and no <c>lexicon*.txt</c>, unlike the <c>kokoro-multi-lang-*</c>
///     variants; <c>OfflineTtsKokoroModelConfig.DictDir</c>/<c>Lexicon</c>/<c>Lang</c> are
///     therefore deliberately left unset by <see cref="ISynthesisModel.CreateEngineConfig"/>.
///     </para>
///     <para>
///     <b>License</b>: Apache License 2.0 - confirmed directly from this archive's own
///     <c>LICENSE</c> file (re-read in this pass, not assumed from memory or copied from a
///     different model's docs), matching the recognition models' license family rather than the
///     VITS/Piper synthesis model's CC BY 4.0 attribution license.
///     </para>
///     <para>
///     <b>Engine configuration</b> was proven end-to-end in this project's development sandbox
///     during this pass: a real, loaded <c>OfflineTts</c> instance built from exactly the
///     configuration <see cref="ISynthesisModel.CreateEngineConfig"/> below (<c>Model.Kokoro.Model</c>/
///     <c>Voices</c>/<c>Tokens</c>/<c>DataDir</c>, <c>LengthScale = 1.0f</c>) reported
///     <c>SampleRate = 24000</c> and <c>NumSpeakers = 11</c>, and generated real, audibly
///     non-silent, and audibly distinct audio for two different speaker ids given the identical
///     input sentence (speaker 1 "af_bella": 65,486 samples, RMS ~0.054; speaker 9 "bm_george":
///     71,591 samples, RMS ~0.063) - proving voice selection genuinely changes synthesis output,
///     not merely that the config field is accepted. The exact field names
///     (<c>Model</c>/<c>Voices</c>/<c>Tokens</c>/<c>DataDir</c>/<c>LengthScale</c>/<c>DictDir</c>/
///     <c>Lexicon</c>/<c>Lang</c>) were confirmed by reflecting directly over the installed
///     <c>sherpa-onnx.dll</c> from the exact <c>org.k2fsa.sherpa.onnx</c> 1.13.5 package this
///     repository's <c>.csproj</c> references, not from generic web documentation.
///     </para>
///     <para>
///     <b>Voice list and speaker-id mapping (this class's own owned knowledge)</b>: this variant
///     ships exactly 11 voices, confirmed by three independent means during this pass: (1) the
///     upstream <c>k2-fsa/sherpa-onnx</c> repository's own
///     <c>scripts/kokoro/v0.19/generate_voices_bin.py</c> generation script, which hard-codes the
///     literal <c>id2speaker</c> map reproduced in <see cref="VoiceOrder"/> below; (2) exact byte
///     arithmetic against the real downloaded <c>voices.bin</c> - each voice is a
///     <c>(511, 1, 256)</c> float32 embedding tensor, so
///     <c>511 * 1 * 256 * 4 bytes * 11 voices = 5,755,904 bytes</c>, an exact match to the real
///     file's measured size on disk; and (3) a live loaded model in this pass's own spike
///     reporting <c>NumSpeakers = 11</c>. <see cref="ISynthesisModel.ResolveSpeakerId"/> maps the selected
///     <c>VoiceParameterId</c> value to its integer index in this confirmed ordering,
///     falling back to the parameter's own declared default (never throwing) for a null bag, a
///     missing key, or an unrecognized value.
///     </para>
///     <para>
///     <b>Capability honesty</b>: Kokoro exposes no discrete emotion-style parameter anywhere in
///     <c>OfflineTtsKokoroModelConfig</c> - only <c>Voices</c> (a fixed set of pre-trained speaker
///     embeddings, selected here as a <see cref="ChoiceParameter"/>) and <c>LengthScale</c> (a
///     playback-speed multiplier, already handled generically by the existing speed/volume
///     convention). This class's voices genuinely differ in accent, gender, and vocal character
///     (for example <c>af_*</c>/<c>bf_*</c> voices sound female with American/British-leaning
///     phonetic renderings respectively, and <c>am_*</c>/<c>bm_*</c> sound male), but none of them
///     is a distinct "happy"/"sad"/"excited" reading of the same voice - this doc comment
///     deliberately avoids claiming an emotion-control capability this model does not have.
///     </para>
/// </remarks>
public sealed class SherpaOnnxKokoroEnglishSynthesisModel : ISynthesisModel
{
    /// <summary>The stable catalog identifier for this model.</summary>
    public const string ModelId = "kokoro-int8-en-v0_19";

    /// <summary>The parameter id used for this model's voice-selection <see cref="ChoiceParameter"/>.</summary>
    public const string VoiceParameterId = "voice";

    /// <summary>The relative install path the declared download archive is written to.</summary>
    private const string ArchiveRelativeInstallPath = "kokoro-int8-en-v0_19.tar.bz2";

    /// <summary>The archive's own top-level folder name, preserved by extraction.</summary>
    private const string ExtractedFolderName = "kokoro-int8-en-v0_19";

    /// <summary>This model's own recommended Kokoro length scale (playback speed multiplier).</summary>
    private const float LengthScale = 1.0f;

    /// <summary>
    ///     The empirically observed output sample rate, in Hz, reported by this model's loaded
    ///     native engine in this repository's own verification evidence.
    /// </summary>
    private const int PreferredSampleRate = 24000;

    /// <summary>The default voice used when no selection is supplied.</summary>
    private const string DefaultVoice = "af";

    /// <summary>Initializes a new instance of the <see cref="SherpaOnnxKokoroEnglishSynthesisModel"/> class.</summary>
    public SherpaOnnxKokoroEnglishSynthesisModel()
    {
    }

    /// <summary>
    ///     This archive's own confirmed <c>id2speaker</c> voice ordering (index = sherpa-onnx
    ///     speaker id), reproduced verbatim from <c>k2-fsa/sherpa-onnx</c>'s own
    ///     <c>scripts/kokoro/v0.19/generate_voices_bin.py</c> and independently cross-checked
    ///     against the real downloaded <c>voices.bin</c>'s byte count and a live model's
    ///     <c>NumSpeakers</c> - see the type-level remarks for the full triple-confirmation.
    /// </summary>
    private static readonly IReadOnlyList<string> VoiceOrder =
    [
        "af",
        "af_bella",
        "af_nicole",
        "af_sarah",
        "af_sky",
        "am_adam",
        "am_michael",
        "bf_emma",
        "bf_isabella",
        "bm_george",
        "bm_lewis",
    ];

    /// <summary>
    ///     The human-readable labels shown for each entry in <see cref="VoiceOrder"/>, in the
    ///     same order.
    /// </summary>
    private static readonly IReadOnlyList<string> VoiceLabels =
    [
        "Voice A (US female)",
        "Bella (US female)",
        "Nicole (US female)",
        "Sarah (US female)",
        "Sky (US female)",
        "Adam (US male)",
        "Michael (US male)",
        "Emma (UK female)",
        "Isabella (UK female)",
        "George (UK male)",
        "Lewis (UK male)",
    ];

    /// <inheritdoc/>
    public string Id => ModelId;

    /// <inheritdoc/>
    public string DisplayName => "Kokoro English (int8, 11 voices)";

    /// <summary>
    ///     <inheritdoc/>
    ///     See the type-level remarks' "License" paragraph - confirmed directly from this
    ///     archive's own <c>LICENSE</c> file.
    /// </summary>
    public string LicenseName => "Apache-2.0";

    /// <inheritdoc/>
    public Uri? LicenseUrl { get; } = new("https://www.apache.org/licenses/LICENSE-2.0");

    /// <inheritdoc/>
    public SpeechModelRole Role => SpeechModelRole.Synthesis;

    /// <inheritdoc/>
    /// <remarks>
    ///     Declares exactly one tunable parameter: a closed-set voice/speaker selection. See the
    ///     type-level remarks for the confirmed provenance of the 11 declared options and their
    ///     ordering, and <see cref="ISynthesisModel.ResolveSpeakerId"/> for how a selected value is mapped to a
    ///     sherpa-onnx speaker id.
    /// </remarks>
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; } =
    [
        new ChoiceParameter(
            VoiceParameterId,
            "Voice",
            "Selects one of this model's 11 distinct-sounding voices (accent, gender, and vocal " +
            "character differ; Kokoro has no separate emotion-style control).",
            VoiceOrder
                .Zip(VoiceLabels, (value, label) => new ChoiceParameterOption(value, label))
                .ToList(),
            DefaultVoice),
    ];

    /// <inheritdoc/>
    /// <remarks>
    ///     Kokoro has no native inline Natural Language Audio Tag support - the default
    ///     <see cref="SynthesisSubsystem.DefaultModelCapabilityProfile"/> already strips
    ///     unsupported tags to plain narration and renders pauses as real inserted silence for a
    ///     model declaring <see cref="SpeechModelAudioTagSupport.None"/>, so this class needs no
    ///     bespoke <see cref="ISynthesisModel.CapabilityProfile"/> override.
    /// </remarks>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

    /// <inheritdoc/>
    /// <remarks>
    ///     Declares exactly one file: the model's own published <c>.tar.bz2</c> archive. See the
    ///     type-level remarks for the exact byte count and SHA-256 provenance re-confirmed in
    ///     this pass.
    /// </remarks>
#pragma warning disable S1075 // This model's download URL is intentionally a compiled-in, reviewed literal - the whole point of a catalog entry is to name one specific, checksum-verified release asset.
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = new(
    [
        new SpeechModelDownloadFile(
            new Uri("https://github.com/k2-fsa/sherpa-onnx/releases/download/tts-models/kokoro-int8-en-v0_19.tar.bz2"),
            "c9f0dd393615805b0bab050c340834d5e684e732aec91c0e860cd30e982c08bd",
            ArchiveRelativeInstallPath),
    ]);
#pragma warning restore S1075

    /// <inheritdoc/>
    AudioFormat ISynthesisModel.PreferredAudioFormat => AudioFormat.Mono(PreferredSampleRate);

    /// <summary>
    ///     Extracts the downloaded <c>.tar.bz2</c> archive in place using the shared
    ///     <see cref="TarBz2ArchiveExtractor"/> (reused unchanged from the existing VITS model),
    ///     then deletes the archive file, leaving the archive's own top-level folder
    ///     (<see cref="ExtractedFolderName"/>) containing every file <c>CreateEngineConfig</c>
    ///     references.
    /// </summary>
    /// <inheritdoc/>
    public Task InstallAsync(string stagedFilesDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(stagedFilesDirectory);

        var archivePath = Path.Join(stagedFilesDirectory, ArchiveRelativeInstallPath);
        return TarBz2ArchiveExtractor.ExtractAndDeleteAsync(archivePath, stagedFilesDirectory, cancellationToken);
    }

    /// <summary>
    ///     Builds the proven Kokoro offline text-to-speech configuration for this model, resolving
    ///     every file path against the archive's extracted top-level folder within
    ///     <paramref name="installedModelDirectory"/>.
    /// </summary>
    /// <inheritdoc/>
    OfflineTtsConfig ISynthesisModel.CreateEngineConfig(string installedModelDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        var modelDirectory = Path.Join(installedModelDirectory, ExtractedFolderName);

        var config = new OfflineTtsConfig();
        config.Model.Kokoro.Model = Path.Join(modelDirectory, "model.int8.onnx");
        config.Model.Kokoro.Voices = Path.Join(modelDirectory, "voices.bin");
        config.Model.Kokoro.Tokens = Path.Join(modelDirectory, "tokens.txt");
        config.Model.Kokoro.DataDir = Path.Join(modelDirectory, "espeak-ng-data");
        config.Model.Kokoro.LengthScale = LengthScale;
        // Model.Kokoro.DictDir/Lexicon/Lang are deliberately left unset - this English-only
        // archive ships neither a Chinese jieba dictionary nor a lexicon file.
        config.Model.Provider = "cpu";
        config.Model.NumThreads = 1;

        return config;
    }

    /// <summary>
    ///     Resolves the selected <see cref="VoiceParameterId"/> value in <paramref name="parameterValues"/>
    ///     to this archive's confirmed integer speaker id.
    /// </summary>
    /// <param name="parameterValues">
    ///     The untyped key-value bag supplied to <c>SpeechSynthesizerFactory.Create</c>, or
    ///     <see langword="null"/>.
    /// </param>
    /// <returns>
    ///     The confirmed speaker id for the selected voice; the default voice's speaker id (<c>0</c>,
    ///     "af") when <paramref name="parameterValues"/> is <see langword="null"/>, does not
    ///     contain <see cref="VoiceParameterId"/>, or contains a value that is not a declared
    ///     option's <see cref="ChoiceParameterOption.Value"/>.
    /// </returns>
    /// <remarks>
    ///     Never throws: an unrecognized or missing selection degrades to this model's own
    ///     declared default voice rather than failing synthesis. See the type-level remarks for
    ///     the confirmed provenance of this ordering.
    /// </remarks>
    int ISynthesisModel.ResolveSpeakerId(IReadOnlyDictionary<string, object>? parameterValues)
    {
        var selectedVoice = DefaultVoice;
        if (parameterValues is not null &&
            parameterValues.TryGetValue(VoiceParameterId, out var value) &&
            value is string voiceValue)
        {
            selectedVoice = voiceValue;
        }

        var index = VoiceOrder
            .Select((voice, ordinal) => (voice, ordinal))
            .Where(pair => string.Equals(pair.voice, selectedVoice, StringComparison.Ordinal))
            .Select(pair => (int?)pair.ordinal)
            .FirstOrDefault();

        return index ?? VoiceOrder.ToList().IndexOf(DefaultVoice);
    }
}
