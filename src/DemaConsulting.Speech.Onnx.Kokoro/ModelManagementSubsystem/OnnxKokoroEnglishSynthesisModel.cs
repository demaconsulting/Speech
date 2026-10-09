using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.SynthesisSubsystem;
using DemaConsulting.Speech.Onnx.OnnxRuntimeSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.Kokoro.ModelManagementSubsystem;

/// <summary>
///     Real, production <see cref="ISynthesisModel"/> backing Kokoro-82M v1.0, run directly
///     through ONNX Runtime with no bundled native inference engine, unlike the sibling
///     <c>DemaConsulting.Speech.Sherpa</c> package's sherpa-onnx-backed
///     <c>SherpaOnnxKokoroEnglishSynthesisModel</c> (the earlier, StyleTTS2-derived v0.19
///     variant).
/// </summary>
/// <remarks>
///     <b>Why a second, distinct Kokoro model class</b>: this is not a duplicate of the sherpa
///     variant. v1.0 is a materially different release (upstream <c>hexgrad/Kokoro-82M</c> v1.0,
///     re-exported as bare ONNX by <c>onnx-community/Kokoro-82M-v1.0-ONNX</c>) with its own
///     tokenizer, its own per-voice style-vector file format, and - critically - no bundled
///     phonemizer at all: the sherpa-onnx v0.19 archive bundles <c>espeak-ng-data</c> and performs
///     phonemization entirely inside its native library, whereas this v1.0 ONNX export performs
///     only the final waveform-generation forward pass, leaving text-to-phoneme conversion
///     entirely to the caller. This class owns that responsibility itself, via the embedded
///     <see cref="KokoroLexiconPhonemizer"/> - see that class's remarks for the full rationale.
///     <para>
///     <b>Download provenance</b>: real files are published at
///     <c>https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX</c> (resolved via its
///     <c>resolve/main/...</c> raw-file URLs below). This project's development sandbox could not
///     reach Hugging Face directly (network policy), so the exact same bytes were instead fetched
///     through a locally configured download mirror and verified in place; the SHA-256 checksums
///     recorded in <see cref="DownloadDescriptor"/> were computed directly against those
///     downloaded bytes in this sandbox, not copied from any third-party listing. The
///     <c>fp16</c> ONNX graph variant (163,234,740 bytes) was chosen over the available
///     <c>quantized</c>/<c>int8</c> variants because it is the exact variant this package's own
///     Python validation spike measured end-to-end (real-time factor ~0.22 on CPU, i.e. audio
///     synthesized about 4.5x faster than real-time) - a smaller quantized variant may be added
///     later once independently measured, rather than assumed equivalent.
///     </para>
///     <para>
///     <b>License</b>: Apache License 2.0, matching the sherpa-onnx v0.19 Kokoro variant's
///     license family (both trace back to the same upstream <c>hexgrad/Kokoro-82M</c> model
///     lineage).
///     </para>
///     <para>
///     <b>Voice subset (Stage 1)</b>: this release ships 54 named voices; this class declares
///     only the two downloaded and verified in this development sandbox so far
///     (<c>af_heart</c>, <c>am_adam</c>) rather than declaring - and silently failing to install -
///     52 voices never actually fetched or verified. Adding a further voice later is purely
///     additive: download and verify its <c>.bin</c> file, add one <see cref="VoiceOrder"/>/
///     <see cref="VoiceLabels"/> entry and one <see cref="DownloadDescriptor"/> file entry; no
///     other code in this class needs to change.
///     </para>
///     <para>
///     <b>Style vectors are indexed by utterance length, not just voice identity</b>: each voice's
///     <c>.bin</c> file is a flattened <c>(510, 256)</c> float32 array (522,240 bytes - confirmed
///     by exact arithmetic against both downloaded voice files:
///     <c>510 * 256 * 4 bytes = 522,240 bytes</c>), and the row selected for one utterance depends
///     on that utterance's own phoneme-token count - see
///     <see cref="OnnxKokoroSynthesisEngine.Generate"/>'s row-selection logic, which mirrors the
///     proven Python reference pipeline exactly.
///     </para>
/// </remarks>
public sealed class OnnxKokoroEnglishSynthesisModel : ISynthesisModel
{
    /// <summary>The stable catalog identifier for this model.</summary>
    public const string ModelId = "kokoro-onnx-v1_0-en";

    /// <summary>The parameter id used for this model's voice-selection <see cref="ChoiceParameter"/>.</summary>
    public const string VoiceParameterId = "voice";

    /// <summary>The relative install path of the downloaded ONNX model graph.</summary>
    private const string ModelRelativeInstallPath = "onnx/model_fp16.onnx";

    /// <summary>The default voice used when no selection is supplied.</summary>
    private const string DefaultVoice = "af_heart";

    /// <summary>
    ///     This package's own verified voice subset, in <see cref="DownloadDescriptor"/>/speaker-id
    ///     order (index = the integer speaker id <see cref="ISynthesisModel.ResolveSpeakerId"/>
    ///     resolves a selection to). See the type-level remarks for why this is a subset of the
    ///     model's full 54-voice catalog.
    /// </summary>
    private static readonly IReadOnlyList<string> VoiceOrder = ["af_heart", "am_adam"];

    /// <summary>The human-readable labels shown for each entry in <see cref="VoiceOrder"/>, in the same order.</summary>
    private static readonly IReadOnlyList<string> VoiceLabels = ["Heart (US female)", "Adam (US male)"];

    /// <summary>
    ///     The execution provider names this instance attempts, in order, before falling back to
    ///     CPU - forwarded unchanged to <see cref="OnnxExecutionProviderSelector.Create"/>. See
    ///     this class's constructor remarks.
    /// </summary>
    private readonly IReadOnlyList<string>? _preferredExecutionProviderNames;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxKokoroEnglishSynthesisModel"/> class,
    ///     using <see cref="OnnxExecutionProviderSelector.DefaultProviderNames"/> (CPU-only).
    /// </summary>
    public OnnxKokoroEnglishSynthesisModel()
        : this(null)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxKokoroEnglishSynthesisModel"/> class,
    ///     attempting <paramref name="preferredExecutionProviderNames"/> before falling back to
    ///     CPU.
    /// </summary>
    /// <param name="preferredExecutionProviderNames">
    ///     The ordered, accelerated execution provider names to attempt first (for example
    ///     <c>"DmlExecutionProvider"</c>), or <see langword="null"/> to use
    ///     <see cref="OnnxExecutionProviderSelector.DefaultProviderNames"/> (CPU-only). A host
    ///     application that wants GPU acceleration adds the matching native runtime package
    ///     itself (for example <c>Microsoft.ML.OnnxRuntime.DirectML</c>) and passes the matching
    ///     provider name here; this class never references a provider-specific package itself -
    ///     see <see cref="OnnxExecutionProviderSelector"/>'s remarks for the full rationale.
    /// </param>
    public OnnxKokoroEnglishSynthesisModel(IReadOnlyList<string>? preferredExecutionProviderNames)
    {
        _preferredExecutionProviderNames = preferredExecutionProviderNames;
    }

    /// <inheritdoc/>
    public string Id => ModelId;

    /// <inheritdoc/>
    public string DisplayName => "Kokoro ONNX v1.0 English (fp16, 2 voices)";

    /// <summary>
    ///     <inheritdoc/>
    ///     See the type-level remarks' "License" paragraph.
    /// </summary>
    public string LicenseName => "Apache-2.0";

    /// <inheritdoc/>
    public Uri? LicenseUrl { get; } = new("https://www.apache.org/licenses/LICENSE-2.0");

    /// <inheritdoc/>
    public SpeechModelRole Role => SpeechModelRole.Synthesis;

    /// <inheritdoc/>
    /// <remarks>
    ///     Declares exactly one tunable parameter: a closed-set voice/speaker selection, matching
    ///     the sibling sherpa-onnx Kokoro model's parameter shape. See the type-level remarks for
    ///     why only 2 of 54 upstream voices are currently declared.
    /// </remarks>
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; } =
    [
        new ChoiceParameter(
            VoiceParameterId,
            "Voice",
            "Selects one of this model's verified voices (accent and gender differ; this " +
            "release has no separate emotion-style control).",
            VoiceOrder
                .Zip(VoiceLabels, (value, label) => new ChoiceParameterOption(value, label))
                .ToList(),
            DefaultVoice),
    ];

    /// <inheritdoc/>
    /// <remarks>
    ///     Kokoro has no native inline Natural Language Audio Tag support - the library's own
    ///     default capability profile already strips unsupported tags to plain narration and
    ///     renders pauses as real inserted silence for a model declaring
    ///     <see cref="SpeechModelAudioTagSupport.None"/>, so this class needs no bespoke
    ///     <see cref="ISynthesisModel.CapabilityProfile"/> override.
    /// </remarks>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

    /// <inheritdoc/>
    /// <remarks>
    ///     Declares three files: the ONNX model graph and this model's two verified voice style
    ///     vectors. This model's phoneme vocabulary is not downloaded at all - it is embedded
    ///     directly in this package (see <see cref="KokoroPhonemeVocabulary"/>), since it is small,
    ///     fixed, and versioned together with this class's own token-id handling code. See the
    ///     type-level remarks for the exact byte counts and SHA-256 provenance.
    /// </remarks>
#pragma warning disable S1075 // This model's download URLs are intentionally compiled-in, reviewed literals - the whole point of a catalog entry is to name specific, checksum-verified release assets.
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = new(
    [
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/onnx/model_fp16.onnx"),
            "ba4527a874b42b21e35f468c10d326fdff3c7fc8cac1f85e9eb6c0dfc35c334a",
            ModelRelativeInstallPath),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/af_heart.bin"),
            "d583ccff3cdca2f7fae535cb998ac07e9fcb90f09737b9a41fa2734ec44a8f0b",
            "voices/af_heart.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/am_adam.bin"),
            "162b035ed91cfc48b6046982184c645f72edcdd1b82843347f605d7bf7b15716",
            "voices/am_adam.bin"),
    ]);
#pragma warning restore S1075

    /// <inheritdoc/>
    AudioFormat ISynthesisModel.PreferredAudioFormat => AudioFormat.Mono(OnnxKokoroSynthesisEngine.SampleRate);

    /// <summary>
    ///     Builds a loaded <see cref="OnnxKokoroSynthesisEngine"/> backend: creates the ONNX
    ///     Runtime session for the downloaded model graph (via
    ///     <see cref="OnnxExecutionProviderSelector"/>, using this instance's own constructor
    ///     -supplied preferred provider names), loads this model's embedded phoneme vocabulary and
    ///     lexicon phonemizer, and reads every declared voice's style-vector file into memory.
    /// </summary>
    /// <inheritdoc/>
    public ISynthesisBackend CreateBackend(string installedModelDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        var modelPath = Path.Combine(installedModelDirectory, Path.Combine(ModelRelativeInstallPath.Split('/')));
        var session = OnnxExecutionProviderSelector.Create(modelPath, _preferredExecutionProviderNames);

        var voiceStylesBySpeakerId = new Dictionary<int, float[]>();
        for (var speakerId = 0; speakerId < VoiceOrder.Count; speakerId++)
        {
            var voicePath = Path.Combine(installedModelDirectory, "voices", $"{VoiceOrder[speakerId]}.bin");
            var bytes = File.ReadAllBytes(voicePath);
            var floats = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
            voiceStylesBySpeakerId[speakerId] = floats;
        }

        return new OnnxKokoroSynthesisEngine(
            session,
            new KokoroPhonemeVocabulary(),
            new KokoroLexiconPhonemizer(),
            voiceStylesBySpeakerId);
    }

    /// <summary>
    ///     Resolves the selected <see cref="VoiceParameterId"/> value in
    ///     <paramref name="parameterValues"/> to this class's own <see cref="VoiceOrder"/> index.
    /// </summary>
    /// <param name="parameterValues">
    ///     The untyped key-value bag supplied to <c>SpeechSynthesizerFactory.LoadAsync</c>, or
    ///     <see langword="null"/>.
    /// </param>
    /// <returns>
    ///     The speaker id for the selected voice; <see cref="DefaultVoice"/>'s index (<c>0</c>)
    ///     when <paramref name="parameterValues"/> is <see langword="null"/>, does not contain
    ///     <see cref="VoiceParameterId"/>, or contains a value that is not a declared option's
    ///     value.
    /// </returns>
    /// <remarks>
    ///     Never throws: an unrecognized or missing selection degrades to this model's own
    ///     declared default voice rather than failing synthesis, mirroring the sibling sherpa-onnx
    ///     Kokoro model's identical contract.
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
