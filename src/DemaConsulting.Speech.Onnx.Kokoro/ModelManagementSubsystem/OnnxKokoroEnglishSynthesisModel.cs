using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.Kokoro.SynthesisSubsystem;
using DemaConsulting.Speech.Onnx.OnnxRuntimeSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using Microsoft.ML.OnnxRuntime;

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
///     <b>Voice subset (Stage 2)</b>: this release ships 54 named voices across multiple
///     languages; this class declares only its 29 American/British <b>English</b> voices,
///     because its embedded <see cref="KokoroLexiconPhonemizer"/> only converts English text
///     to phonemes - feeding, say, the model's Japanese or Mandarin voices through an
///     English-only phonemizer would mispronounce their own language's text, so those 25
///     non-English voices are deliberately left undeclared rather than silently mismatched to
///     a phonemizer that cannot serve them. A later class supporting another source language
///     would phonemize differently and could then declare its own matching voice subset.
///     Adding a further English voice later is purely additive: download and verify its
///     <c>.bin</c> file, add one <see cref="VoiceOrder"/>/<see cref="VoiceLabels"/> entry and
///     one <see cref="DownloadDescriptor"/> file entry; no other code in this class needs to
///     change.
///     </para>
///     <para>
///     <b>Style vectors are indexed by utterance length, not just voice identity</b>: each
///     voice's <c>.bin</c> file is a flattened <c>(rows, 256)</c> float32 array, and the row
///     selected for one utterance depends on that utterance's own phoneme-token count - see
///     <see cref="OnnxKokoroSynthesisEngine.Generate"/>'s row-selection logic, which mirrors
///     the proven Python reference pipeline exactly. Every named voice (for example
///     <c>af_heart</c>) is <c>(510, 256)</c> (522,240 bytes - confirmed by exact arithmetic:
///     <c>510 * 256 * 4 bytes = 522,240 bytes</c>); the one exception is the bundled default
///     blend voice, <c>af</c> (with no name suffix), which is <c>(512, 256)</c>
///     (524,288 bytes) - a genuinely different shape confirmed directly against its downloaded
///     bytes, not a data-entry mistake.
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
    ///     Test-only observation hook invoked with the freshly created ONNX
    ///     <see cref="InferenceSession"/>, immediately after
    ///     <see cref="CreateBackend"/> creates it and before any voice-style file is read.
    /// </summary>
    /// <remarks>
    ///     Lets a test capture the session reference so it can independently verify the session is
    ///     disposed when a later step (reading a voice file, constructing the vocabulary or
    ///     phonemizer) fails - something otherwise unobservable from outside this method, since the
    ///     local <c>session</c> variable itself is never exposed to callers on a failure path.
    ///     Production callers never set this; it exists solely for
    ///     <c>DemaConsulting.Speech.Onnx.Kokoro.Tests</c> (see <c>InternalsVisibleTo</c> in this
    ///     project's <c>.csproj</c>), mirroring <c>OnnxExecutionProviderSelector.OnCandidateOptionsCreated</c>'s
    ///     identical pattern.
    /// </remarks>
    internal static Action<InferenceSession>? OnSessionCreated { get; set; }

    /// <summary>
    ///     This package's own verified voice subset, in <see cref="DownloadDescriptor"/>/speaker-id
    ///     order (index = the integer speaker id <see cref="ISynthesisModel.ResolveSpeakerId"/>
    ///     resolves a selection to). Limited to the model's American/British English voices,
    ///     because this class's embedded <see cref="KokoroLexiconPhonemizer"/> only phonemizes
    ///     English text - see the type-level remarks for why the model's remaining
    ///     non-English-language voices are out of scope for this class.
    /// </summary>
    private static readonly IReadOnlyList<string> VoiceOrder =
    [
        "af_heart", "af_alloy", "af_aoede", "af_bella", "af_jessica", "af_kore", "af_nicole",
        "af_nova", "af_river", "af_sarah", "af_sky", "af",
        "am_adam", "am_echo", "am_eric", "am_fenrir", "am_liam", "am_michael", "am_onyx",
        "am_puck", "am_santa",
        "bf_alice", "bf_emma", "bf_isabella", "bf_lily",
        "bm_daniel", "bm_fable", "bm_george", "bm_lewis",
    ];

    /// <summary>The human-readable labels shown for each entry in <see cref="VoiceOrder"/>, in the same order.</summary>
    private static readonly IReadOnlyList<string> VoiceLabels =
    [
        "Heart (US female)", "Alloy (US female)", "Aoede (US female)", "Bella (US female)",
        "Jessica (US female)", "Kore (US female)", "Nicole (US female)", "Nova (US female)",
        "River (US female)", "Sarah (US female)", "Sky (US female)", "Default blend (US female)",
        "Adam (US male)", "Echo (US male)", "Eric (US male)", "Fenrir (US male)",
        "Liam (US male)", "Michael (US male)", "Onyx (US male)", "Puck (US male)",
        "Santa (US male)",
        "Alice (British female)", "Emma (British female)", "Isabella (British female)",
        "Lily (British female)",
        "Daniel (British male)", "Fable (British male)", "George (British male)",
        "Lewis (British male)",
    ];

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
    public string DisplayName => "Kokoro ONNX v1.0 English (fp16, 29 voices)";

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
    ///     Declares the ONNX model graph plus this model's 29 verified English voice style
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
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/af_alloy.bin"),
            "c4a6b876047fd7fb472edf4ebd63cfac7c3b958a7cae7c106e8f038ca6308c45",
            "voices/af_alloy.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/af_aoede.bin"),
            "4a004c33430762e2461eedb2013fad808ef4ab3121f5300f554476caf58d8361",
            "voices/af_aoede.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/af_bella.bin"),
            "f69d836209b78eb8c66e75e3cda491e26ea838a3674257e9d4e5703cbaf55c8b",
            "voices/af_bella.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/af_jessica.bin"),
            "a240a5e3c15b43563d6e923bdca8ef5613a23471d9b77653694012435df23bd8",
            "voices/af_jessica.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/af_kore.bin"),
            "9be5221b6a941c04b561959b8ff0b06e809444dcc4ab7e75a7b23606f691819e",
            "voices/af_kore.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/af_nicole.bin"),
            "cd2191ab31b914ed7b318416b0e4440fdf392ddad9106a060819aa600a64f59a",
            "voices/af_nicole.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/af_nova.bin"),
            "18778272caa0d0eebaea251c35fd635f038434f9eee5e691d02a174bd328414f",
            "voices/af_nova.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/af_river.bin"),
            "00a2bcf82b1d86e8f19902ede58c65ccf6c0e43b44b7d74fad54e5d8933c9c30",
            "voices/af_river.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/af_sarah.bin"),
            "4409fbc125afabacc615d94db5398d847006a737b0247d6892b7a9a0007a2f0a",
            "voices/af_sarah.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/af_sky.bin"),
            "4435255c9744f3f31659e0d714ab7689bf65d9e77ec1cce060f083912614f0b9",
            "voices/af_sky.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/af.bin"),
            "a4f11d9d055a12bfa0db2668a3e4f0ef8fd1f1ccca69494479718e44dbf9e41a",
            "voices/af.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/am_adam.bin"),
            "162b035ed91cfc48b6046982184c645f72edcdd1b82843347f605d7bf7b15716",
            "voices/am_adam.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/am_echo.bin"),
            "3968b92c3c4cd1c4416dbded36c13eaa388a90d5788d02a13e4d781f5f8cf3c3",
            "voices/am_echo.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/am_eric.bin"),
            "e8b5be17edd1e3636901ce7598baafe2dc8dd8ff707a0c23bf9e461add7e2832",
            "voices/am_eric.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/am_fenrir.bin"),
            "c27989f741f7ee34d273a39d8a595cc0837d35f5ced9a29b7cc162614616df43",
            "voices/am_fenrir.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/am_liam.bin"),
            "52403be32fd047c6a44517cb0bcd6b134f2a18baa73e70ef41651e0eab921ade",
            "voices/am_liam.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/am_michael.bin"),
            "1d1f21dd8da39c30705cd4c75d039d265e9bc4a2a93ed09bc9e1b1225eb95ba1",
            "voices/am_michael.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/am_onyx.bin"),
            "da5d135b424164916d75a68ffb4c2abce3d7d5ccc82dd1ee6cf447ce286145e6",
            "voices/am_onyx.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/am_puck.bin"),
            "fcf73c989033e9233e0b98713eca600c8c74dcc1614b37009d5450ff4a2274a0",
            "voices/am_puck.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/am_santa.bin"),
            "61150cf726ab6c5ed7a99f90a304f91f5a72c00c592e89ec94e5df11c319227a",
            "voices/am_santa.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/bf_alice.bin"),
            "08afa6ba24da61ea5e8efa139e5aadc938d83f0a6da5a900adaf763ac1da5573",
            "voices/bf_alice.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/bf_emma.bin"),
            "669fe0647f9dd04fcab92f1439a40eeb4c8b4ab1f82e4996fe3d918ce4a63b73",
            "voices/bf_emma.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/bf_isabella.bin"),
            "3754352c4aaa46d17f27654ab7518d65b62ad6163a0f55a5f4330c2da2c4e94f",
            "voices/bf_isabella.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/bf_lily.bin"),
            "5e0ee32ebe64a467124976b14e69590746f1c4ce41a12b587a50c862edfea335",
            "voices/bf_lily.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/bm_daniel.bin"),
            "6b3194bbceffb746733cbc22c8f593dd44e401a71d53895a2dca891bc595a1e8",
            "voices/bm_daniel.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/bm_fable.bin"),
            "f889083196807b4adb15e9204252165f503b8d33d3982e681c52443c49d798f1",
            "voices/bm_fable.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/bm_george.bin"),
            "c4b235a4c1f2cd3b939fed08b899ce9385638b763f7b73a59616c4fc9bd6c9bc",
            "voices/bm_george.bin"),
        new SpeechModelDownloadFile(
            new Uri("https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/main/voices/bm_lewis.bin"),
            "b8f671cef828c30e66fdf0b0756a76bba58f6bb3398cbbf27058642acbcedb97",
            "voices/bm_lewis.bin"),
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
    /// <remarks>
    ///     The ONNX session is created before any voice-style file is read, so a missing or
    ///     unreadable voice file (or any other failure reading voices/constructing the vocabulary
    ///     or phonemizer) can still occur after the session already exists. This method holds the
    ///     session in a local variable across that later work and disposes it itself in a
    ///     <see langword="catch"/> block before rethrowing if any of it fails, since the engine
    ///     that would otherwise own the session's lifetime is never constructed on that path -
    ///     this is the only way to avoid leaking the native session handle on a partial failure.
    /// </remarks>
    /// <inheritdoc/>
    public ISynthesisBackend CreateBackend(string installedModelDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        var modelPath = Path.Combine(installedModelDirectory, Path.Combine(ModelRelativeInstallPath.Split('/')));
        var session = OnnxExecutionProviderSelector.Create(
            modelPath,
            _preferredExecutionProviderNames,
            OnnxKokoroSynthesisEngine.RunProbeInference);
        OnSessionCreated?.Invoke(session);

        try
        {
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
        catch
        {
            // Voice-file loading (or vocabulary/phonemizer construction) failed after the ONNX
            // session was already created: ownership of the session never passed to the engine,
            // since the engine's constructor is never reached, so this method - not the
            // never-constructed engine - must dispose it here rather than leak the native
            // session handle.
            session.Dispose();
            throw;
        }
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
