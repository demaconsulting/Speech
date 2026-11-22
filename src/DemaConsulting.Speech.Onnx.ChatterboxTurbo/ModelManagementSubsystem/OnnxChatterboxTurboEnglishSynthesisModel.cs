using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Diagnostics;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.ChatterboxTurbo.SynthesisSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Onnx.ChatterboxTurbo.ModelManagementSubsystem;

/// <summary>
///     Real, production <see cref="ISynthesisModel"/> backing Chatterbox Turbo (English) with reference-clip
///     voice cloning, run directly through ONNX Runtime on the CPU.
/// </summary>
/// <remarks>
///     <b>Download provenance</b>: the graphs are originally published at
///     <c>https://huggingface.co/ResembleAI/chatterbox-turbo-ONNX</c> (MIT license, Copyright (c) 2025 Resemble AI).
///     The SHA-256 checksums in <see cref="BundleContents"/> were computed directly from a local copy of those
///     files; a release checklist item is to cross-check them against the repository's published LFS checksums. Each
///     <c>.onnx</c> graph keeps its <c>.onnx_data</c> weights file beside it. The tokenizer is embedded in
///     this package, not downloaded.
///     <para>
///     <b>Variants</b>: the default is the half-precision language model with 4-bit auxiliary graphs
///     (about 1.1 GB, 8 files). A non-default variant gets a distinct <see cref="Id"/> so the model store's
///     installed-file check stays correct for the selected file set. The files are grouped into component bundles
///     on this project's <c>models</c> GitHub release: one per language-model precision and one per auxiliary
///     precision. A variant downloads its two bundles (unmodified files plus the MIT <c>LICENSE</c>, a
///     <c>NOTICE</c>, a <c>README.txt</c> and a <c>model.json</c>) from the release when both are published (see
///     <see cref="PublishedBundles"/>; today only the default variant's two bundles are), and otherwise downloads
///     its eight files individually from Hugging Face, as before. A configured download mirror serves a
///     bundle-installed variant's zips at <c>{mirror}/{model id}/{bundle}.zip</c> and an individually downloaded
///     variant's files at <c>{mirror}/{model id}/onnx/{file}</c>.
///     </para>
///     <para>
///     <b>Voices</b>: nothing is bundled in this package. Voices come from <see cref="ChatterboxTurboModelOptions"/>
///     (explicit voices, then a voice directory) followed by the model-agnostic <see cref="ReferenceVoiceLibrary"/>
///     (the user folder, then installed packs such as the CC0 voice-zero pack), which are read lazily - a clip is
///     decoded when its voice is first used. The first voice is the default. With no voices the model still
///     enumerates and downloads, but
///     <see cref="CreateBackend"/> throws <see cref="InvalidOperationException"/>, which the core library turns
///     into an unavailable synthesizer.
///     </para>
///     <para>
///     <b>Honest expectations</b>: CPU only; about 0.8x real time; loading the four graphs takes
///     roughly 7-20 seconds; synthesis is per sentence rather than live streaming; the output is not
///     watermarked.
///     </para>
/// </remarks>
public sealed class OnnxChatterboxTurboEnglishSynthesisModel : ISynthesisModel
{
    /// <summary>The stable catalog identifier of the default-variant model.</summary>
    public const string BaseModelId = "chatterbox-turbo-onnx-en";

    /// <summary>The parameter id used for the voice-selection <see cref="ChoiceParameter"/>.</summary>
    public const string VoiceParameterId = "voice";

    /// <summary>The Hugging Face resolve URL prefix of the model repository's ONNX folder.</summary>
#pragma warning disable S1075 // Download URLs are intentionally compiled-in, reviewed literals.
    private const string RepositoryOnnxUrl = "https://huggingface.co/ResembleAI/chatterbox-turbo-ONNX/resolve/main/onnx/";
#pragma warning restore S1075

    /// <summary>The embedded tokenizer resource name suffix.</summary>
    private const string TokenizerResourceSuffix = "chatterbox-turbo-tokenizer.json.gz";

    /// <summary>The diagnostics category used for voice-resolution messages.</summary>
    private const string DiagnosticsCategory = "ModelManagementSubsystem";

    /// <summary>SHA-256 digests by file name, for every file of every supported variant.</summary>
    private static readonly IReadOnlyDictionary<string, string> Checksums = new Dictionary<string, string>
    {
        ["language_model_fp16.onnx"] = "396b35570dd1dbc5f537ffc932c3e69130ab8a09bf1390469d92338c8eae7da4",
        ["language_model_fp16.onnx_data"] = "d730d6437126d3e232747b26e2a7adece68844f22133277ca5012298ffe36f93",
        ["language_model.onnx"] = "c12e31df78c74f9589b165c8d51e65171f5028b77b7fedb41900f55f7f410dc8",
        ["language_model.onnx_data"] = "67db106868f5354b2e425651f1791aef36ae3e6f00ac5e1d91e32c985cad6b39",
        ["embed_tokens_q4.onnx"] = "fd6ba1d22902e8f539d3dd6d7c1c44b98ebb4c84ebbb5e47fcb826ddcf667561",
        ["embed_tokens_q4.onnx_data"] = "f54a51e234b509b64c3a03bb79e1149fba7e2eba6c2d9c222f18883379e1f5d8",
        ["embed_tokens_fp16.onnx"] = "f7a7a83e91337e10add2fad054544421f9080e27b72af1aef27f339a1a66776c",
        ["embed_tokens_fp16.onnx_data"] = "cdda886a9e58ad39059fe8f6cf5a3a628994a1a2dfc1a8b58502c252fec70327",
        ["embed_tokens.onnx"] = "27796e8252f36b463b0421cafdcc35b5f1e670ab0d96c9182f37ac6571c2f4bc",
        ["embed_tokens.onnx_data"] = "a1c37edc6ec6adb655351f02e958da297221b50211c2c01b69312cb6f008a293",
        ["speech_encoder_q4.onnx"] = "37956c20b67bed85a0da4bc83509d67b5969a1b257d1c546516a5236a17ad71e",
        ["speech_encoder_q4.onnx_data"] = "58956db217c6443e49c91bdd54d7cf76b4a243f225c748b7bf746459fc27bc7d",
        ["speech_encoder_fp16.onnx"] = "5544f87c3e1615a31f5dc3ccdf9ba52a9f5fb8f6018e72796235fc1cfd5ebc9f",
        ["speech_encoder_fp16.onnx_data"] = "d24b817ff61489373ab13033cbdc28a5ba8347341248a00f9a63f10888e81d73",
        ["speech_encoder.onnx"] = "4d66128037517dd51d370edc9b89ce36d42c75dcbd96e7216c7fb45dfae36045",
        ["speech_encoder.onnx_data"] = "c9915ff6c529e7bb80983b525255e6744d6c39c7e35b12720925ba99ed0d0a2f",
        ["conditional_decoder_q4.onnx"] = "dccb7a6cea3472dc7f7d070eeb70ade18e6327fb4ec61a3d62cf211bfed90ea2",
        ["conditional_decoder_q4.onnx_data"] = "b5c5317e0b79a1a19dd3d5e2b2091ea06b15716716ab801a54eaeb906c6971ec",
        ["conditional_decoder_fp16.onnx"] = "cbdc0281548eb90a02fa2430647237df63fa0cb695e42b91c10b860b9b6d2230",
        ["conditional_decoder_fp16.onnx_data"] = "c6c3e79e6ff86bc41f77381a3d67b8edaa16b13e43a2700ca8159d0894bd594a",
        ["conditional_decoder.onnx"] = "8c43f3a1d0ddb1a86e226a244d7cda5396c67f5c6412789c23900c646e3ffc50",
        ["conditional_decoder.onnx_data"] = "05f162a519f3e9abaf0b7337ae037f4af8b2b30c4455d39b2c61ed3a9b2b5476",
    };

    /// <summary>
    ///     The vendor files of every Chatterbox Turbo component bundle on the <c>models</c> release, keyed by
    ///     bundle name: one language-model bundle per <see cref="ChatterboxTurboLanguageModelVariant"/> and one
    ///     auxiliary bundle (embedding, speech encoder and conditional decoder) per
    ///     <see cref="ChatterboxTurboAuxiliaryModelVariant"/>.
    /// </summary>
    /// <remarks>
    ///     Built from <see cref="Checksums"/> and the repository URL, so it is the single source of truth for the
    ///     per-file pins: <see cref="InstalledFiles"/> is the selected language-model bundle's files followed by
    ///     the selected auxiliary bundle's files, a published bundle in <see cref="PublishedBundles"/> uses its entry
    ///     as its contents, and the non-shipped <c>DemaConsulting.Speech.ModelBundler</c> tool
    ///     (which sees this member through <c>InternalsVisibleTo</c>) packs exactly these files into each bundle, so
    ///     no SHA-256 is declared twice. Each graph is followed by its weights file. Immutable and safe to share
    ///     across threads.
    /// </remarks>
    internal static IReadOnlyDictionary<string, SpeechModelDownloadDescriptor> BundleContents { get; } =
        new Dictionary<string, SpeechModelDownloadDescriptor>(StringComparer.Ordinal)
        {
            [GetLanguageModelBundleName(ChatterboxTurboLanguageModelVariant.Fp16)] = BuildVendorFiles("language_model_fp16"),
            [GetLanguageModelBundleName(ChatterboxTurboLanguageModelVariant.Fp32)] = BuildVendorFiles("language_model"),
            [GetAuxiliaryModelBundleName(ChatterboxTurboAuxiliaryModelVariant.Q4)] =
                BuildVendorFiles(GetAuxiliaryGraphNames(ChatterboxTurboAuxiliaryModelVariant.Q4)),
            [GetAuxiliaryModelBundleName(ChatterboxTurboAuxiliaryModelVariant.Fp16)] =
                BuildVendorFiles(GetAuxiliaryGraphNames(ChatterboxTurboAuxiliaryModelVariant.Fp16)),
            [GetAuxiliaryModelBundleName(ChatterboxTurboAuxiliaryModelVariant.Fp32)] =
                BuildVendorFiles(GetAuxiliaryGraphNames(ChatterboxTurboAuxiliaryModelVariant.Fp32)),
        };

    /// <summary>
    ///     The zip SHA-256 of every component bundle already published on the <c>models</c> release, keyed by
    ///     bundle name.
    /// </summary>
    /// <remarks>
    ///     Only the default variant's two bundles (half-precision language model, 4-bit auxiliary graphs) are
    ///     published so far. A variant installs from bundles only when both of its component bundles appear here
    ///     (see the constructor), so publishing a further bundle and adding its pin here switches the variants
    ///     that use it from Hugging Face to the release with no other code change. Declared after
    ///     <see cref="BundleContents"/> because static initializers run in textual order.
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> PublishedBundleChecksums =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [GetLanguageModelBundleName(ChatterboxTurboLanguageModelVariant.Fp16)] =
                "2c2e05d62e18453fa8c726d57b8c970948f5d693400f303fc2e9dc97838a1584",
            [GetAuxiliaryModelBundleName(ChatterboxTurboAuxiliaryModelVariant.Q4)] =
                "1981e10e65bb4f2954b99dee6c9b9e74242428cd87ed0dadf085ca6f8f229581",
        };

    /// <summary>
    ///     The component bundles published on the <c>models</c> release, each pinned by its zip SHA-256 and holding
    ///     the unmodified files of its <see cref="BundleContents"/> entry, in ordinal name order.
    /// </summary>
    /// <remarks>
    ///     Public so a host or a mirror administrator can list exactly which zips (name, URL and SHA-256) the
    ///     bundle-installed variants download. Today these are <c>chatterbox-turbo-lm-20261014-fp16-b1</c> and
    ///     <c>chatterbox-turbo-aux-20261014-int4-b1</c>, the two bundles of the default variant. Immutable and safe
    ///     to share across threads.
    /// </remarks>
    public static IReadOnlyList<SpeechModelBundle> PublishedBundles { get; } =
    [
        .. PublishedBundleChecksums
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => CreateBundle(entry.Key, entry.Value)),
    ];

    /// <summary>The configuration this instance was created with.</summary>
    private readonly ChatterboxTurboModelOptions _options;

    /// <summary>The resolved voices, index = speaker id.</summary>
    private readonly IReadOnlyList<ChatterboxTurboReferenceVoice> _voices;

    /// <summary>The capability profile configured with the sentence-final tag policy.</summary>
    private readonly ChatterboxTurboCapabilityProfile _profile;

    /// <summary>Graph base names (without extension) for the selected variants.</summary>
    private readonly string _languageModelName;

    /// <summary>Embedding graph base name.</summary>
    private readonly string _embedTokensName;

    /// <summary>Speech encoder graph base name.</summary>
    private readonly string _speechEncoderName;

    /// <summary>Conditional decoder graph base name.</summary>
    private readonly string _conditionalDecoderName;

    /// <summary>
    ///     The two published component bundles (language model, then auxiliary graphs) this variant installs from,
    ///     or <see langword="null"/> when at least one of them is not yet published and the variant downloads its
    ///     files individually from Hugging Face.
    /// </summary>
    private readonly IReadOnlyList<SpeechModelBundle>? _bundles;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxChatterboxTurboEnglishSynthesisModel"/> class.
    /// </summary>
    /// <param name="options">The configuration, or <see langword="null"/> for defaults (which supply no voices).</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when two explicit or voice-directory voices share an id (ordinal comparison), or a variant is not a defined value.
    ///     A voice from the voice library whose id is already present is skipped and reported at
    ///     <see cref="SpeechDiagnosticLevel.Info"/> instead.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when <see cref="ChatterboxTurboModelOptions.ReferenceVoiceDirectory"/> does not exist.</exception>
    /// <remarks>
    ///     Files in <see cref="ChatterboxTurboModelOptions.ReferenceVoiceDirectory"/> are decoded lazily, so a damaged
    ///     file never fails construction: it fails at first use of that voice with an <see cref="InvalidDataException"/>
    ///     naming the file.
    /// </remarks>
    public OnnxChatterboxTurboEnglishSynthesisModel(ChatterboxTurboModelOptions? options = null)
        : this(options, defaultLibrary: null)
    {
    }

    /// <summary>
    ///     Initializes a new instance with a fallback voice library (used by the catalog extension, which supplies a
    ///     library over the catalog's own store when the options name none).
    /// </summary>
    /// <param name="options">The configuration, or <see langword="null"/> for defaults.</param>
    /// <param name="defaultLibrary">The library used when <see cref="ChatterboxTurboModelOptions.ReferenceVoiceLibrary"/> is <see langword="null"/>.</param>
    internal OnnxChatterboxTurboEnglishSynthesisModel(ChatterboxTurboModelOptions? options, ReferenceVoiceLibrary? defaultLibrary)
    {
        _options = options ?? new ChatterboxTurboModelOptions();
        if (!Enum.IsDefined(_options.LanguageModelVariant) || !Enum.IsDefined(_options.AuxiliaryModelVariant))
        {
            throw new ArgumentException("The model variants must be defined values.", nameof(options));
        }

        _profile = new ChatterboxTurboCapabilityProfile(_options.EndOfSentenceTagPolicy, _options.MoodJoinSilenceMilliseconds);

        // Graph names follow the repository's suffix convention (none = full precision)
        var auxiliaryGraphNames = GetAuxiliaryGraphNames(_options.AuxiliaryModelVariant);
        _languageModelName = _options.LanguageModelVariant == ChatterboxTurboLanguageModelVariant.Fp16
            ? "language_model_fp16"
            : "language_model";
        _embedTokensName = auxiliaryGraphNames[0];
        _speechEncoderName = auxiliaryGraphNames[1];
        _conditionalDecoderName = auxiliaryGraphNames[2];

        _voices = ResolveVoices(_options, defaultLibrary);

        Id = _options is { LanguageModelVariant: ChatterboxTurboLanguageModelVariant.Fp16, AuxiliaryModelVariant: ChatterboxTurboAuxiliaryModelVariant.Q4 }
            ? BaseModelId
            : $"{BaseModelId}-lm-{_options.LanguageModelVariant.ToString().ToLowerInvariant()}-aux-{_options.AuxiliaryModelVariant.ToString().ToLowerInvariant()}";

        // The voice parameter only exists when there is at least one voice (a choice needs an option)
        Parameters = _voices.Count == 0
            ? []
            :
            [
                new ChoiceParameter(
                    VoiceParameterId,
                    "Voice",
                    "Selects the reference voice the model clones. The first voice is the default. " + ReferenceVoiceLibrary.ConsentNotice,
                    [.. _voices.Select(voice => new ChoiceParameterOption(voice.Id, voice.DisplayName))],
                    _voices[0].Id),
            ];

        // A variant installs from the models release only when both of its component bundles are published;
        // otherwise it keeps downloading its eight files individually, so no variant ever mixes the two sources
        var languageModelBundleName = GetLanguageModelBundleName(_options.LanguageModelVariant);
        var auxiliaryBundleName = GetAuxiliaryModelBundleName(_options.AuxiliaryModelVariant);
        InstalledFiles =
        [
            .. BundleContents[languageModelBundleName].Files,
            .. BundleContents[auxiliaryBundleName].Files,
        ];
        var languageModelBundle = PublishedBundles.FirstOrDefault(bundle => string.Equals(bundle.Name, languageModelBundleName, StringComparison.Ordinal));
        var auxiliaryBundle = PublishedBundles.FirstOrDefault(bundle => string.Equals(bundle.Name, auxiliaryBundleName, StringComparison.Ordinal));
        _bundles = languageModelBundle is not null && auxiliaryBundle is not null
            ? [languageModelBundle, auxiliaryBundle]
            : null;
        DownloadDescriptor = _bundles is not null
            ? SpeechModelBundle.CreateDescriptor(_bundles)
            : new SpeechModelDownloadDescriptor(InstalledFiles);
    }

    /// <inheritdoc/>
    public string Id { get; }

    /// <inheritdoc/>
    public string DisplayName => "Chatterbox Turbo English (voice cloning, ONNX, CPU)";

    /// <inheritdoc/>
    public string LicenseName => "MIT";

    /// <inheritdoc/>
#pragma warning disable S1075 // The license page is a deliberate, reviewed literal.
    public Uri? LicenseUrl { get; } = new("https://opensource.org/license/mit");
#pragma warning restore S1075

    /// <inheritdoc/>
    public SpeechModelRole Role => SpeechModelRole.Synthesis;

    /// <inheritdoc/>
    /// <remarks>One <see cref="ChoiceParameter"/> selecting the reference voice, or none when no voices were supplied.</remarks>
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; }

    /// <inheritdoc/>
    /// <remarks>
    ///     Native: the model produces some paralinguistic sounds itself. Rendering is handled by this model's own
    ///     capability profile, which maps only the tags the model supports and strips or approximates the rest.
    /// </remarks>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.Native;

    /// <inheritdoc/>
    /// <remarks>
    ///     For a variant whose two component bundles are both published (today the default variant), the two zips
    ///     of <see cref="PublishedBundles"/> it uses, language model first; <see cref="InstallAsync"/> then extracts
    ///     <see cref="InstalledFiles"/> from them. For any other variant, the eight files of
    ///     <see cref="InstalledFiles"/>, downloaded individually from Hugging Face.
    /// </remarks>
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; }

    /// <summary>
    ///     The eight vendor files this variant installs (the language-model graph and weights followed by the three
    ///     auxiliary graphs and their weights), whichever source <see cref="DownloadDescriptor"/> downloads them from.
    /// </summary>
    /// <remarks>
    ///     <see cref="CreateBackend"/> checks these files rather than <see cref="DownloadDescriptor"/>, because a
    ///     bundle-installed variant's descriptor names zips that are deleted once extracted.
    /// </remarks>
    internal IReadOnlyList<SpeechModelDownloadFile> InstalledFiles { get; }

    /// <summary>
    ///     Extracts and verifies the two downloaded component bundles of a bundle-installed variant in the staging
    ///     directory; does nothing for a variant that downloads its files individually.
    /// </summary>
    /// <param name="stagedFilesDirectory">The staging directory holding the downloaded, checksum-verified files.</param>
    /// <param name="cancellationToken">A token to observe while extracting.</param>
    /// <returns>A task that completes once the bundles are extracted and the zips are deleted.</returns>
    /// <remarks>
    ///     Delegates to <see cref="SpeechModelBundle.InstallAsync"/>, which rejects any unsafe entry name, any extra
    ///     or missing file, and any file whose SHA-256 differs from its pin, and places each bundle's license files
    ///     under <c>licenses/&lt;bundle&gt;/</c>; an exception here leaves no partial install. A variant downloading
    ///     individual files needs no post-processing, exactly as before bundles existed.
    /// </remarks>
    /// <exception cref="ArgumentException">
    ///     Thrown for a bundle-installed variant when <paramref name="stagedFilesDirectory"/> is null or empty.
    /// </exception>
    /// <exception cref="InvalidDataException">Thrown when a zip or one of its files fails verification.</exception>
    public Task InstallAsync(string stagedFilesDirectory, CancellationToken cancellationToken = default) =>
        _bundles is null
            ? Task.CompletedTask
            : SpeechModelBundle.InstallAsync(_bundles, stagedFilesDirectory, cancellationToken);

    /// <inheritdoc/>
    AudioFormat ISynthesisModel.PreferredAudioFormat => AudioFormat.Mono(OnnxChatterboxTurboSynthesisEngine.OutputSampleRate);

    /// <inheritdoc/>
    IModelCapabilityProfile ISynthesisModel.CapabilityProfile => _profile;

    /// <summary>
    ///     Builds a loaded backend: creates the four ONNX Runtime sessions, loads the embedded tokenizer and
    ///     wires the voices.
    /// </summary>
    /// <param name="installedModelDirectory">The directory the model files were installed into.</param>
    /// <returns>The backend.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the directory is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the directory is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no reference voice was supplied.</exception>
    /// <exception cref="FileNotFoundException">Thrown when an expected graph file is missing.</exception>
    public ISynthesisBackend CreateBackend(string installedModelDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);

        if (_voices.Count == 0)
        {
            throw new InvalidOperationException(
                "Chatterbox Turbo needs at least one reference voice: install the reference-voice pack (CLI: 'voices install'), add .wav or .flac files to the reference-voices 'user' folder in the model store, or supply voices through ChatterboxTurboModelOptions (for example --voices-dir).");
        }

        // Check every graph and weights file up front for a clear message
        foreach (var file in InstalledFiles)
        {
            var path = file.ResolveStagedPath(installedModelDirectory);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Chatterbox Turbo model file '{path}' is missing.", path);
            }
        }

        string GraphPath(string name) => Path.Combine(installedModelDirectory, "onnx", name + ".onnx");

        // The networks dispose their own sessions if creation fails part-way
        var networks = new OnnxChatterboxTurboNetworks(
            GraphPath(_speechEncoderName),
            GraphPath(_embedTokensName),
            GraphPath(_languageModelName),
            GraphPath(_conditionalDecoderName),
            _options.IntraOpThreads);
        try
        {
            return new OnnxChatterboxTurboSynthesisEngine(
                networks,
                LoadTokenizer(),
                _voices,
                _options.MaxNewTokens,
                _options.MaxAudioTokensPerTextToken,
                _options.MaxConsecutiveSpeechTokenRepeats);
        }
        catch
        {
            // Ownership never reached the engine
            networks.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Resolves the selected voice id to its index.
    /// </summary>
    /// <param name="parameterValues">The caller-supplied parameter values, or null.</param>
    /// <returns>The voice index; the first voice (0) for a null bag, a missing value or an unknown voice.</returns>
    int ISynthesisModel.ResolveSpeakerId(IReadOnlyDictionary<string, object>? parameterValues)
    {
        if (parameterValues is not null &&
            parameterValues.TryGetValue(VoiceParameterId, out var value) &&
            value is string voiceId)
        {
            for (var index = 0; index < _voices.Count; index++)
            {
                if (string.Equals(_voices[index].Id, voiceId, StringComparison.Ordinal))
                {
                    return index;
                }
            }
        }

        return 0;
    }

    /// <summary>Opens the embedded gzip tokenizer and builds the tokenizer from it.</summary>
    internal static ByteLevelBpeTokenizer LoadTokenizer()
    {
        using var stream = OpenTokenizerResource();
        return ByteLevelBpeTokenizer.FromGzipStream(stream);
    }

    /// <summary>Opens the embedded gzip-compressed tokenizer document.</summary>
    internal static Stream OpenTokenizerResource()
    {
        var assembly = typeof(OnnxChatterboxTurboEnglishSynthesisModel).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(TokenizerResourceSuffix, StringComparison.Ordinal));
        return assembly.GetManifestResourceStream(name)!;
    }

    /// <summary>
    ///     Combines explicit voices, those in the voice directory and those from the voice library (user folder, installed packs).
    ///     A duplicate id among the explicit and directory voices is a caller error; a library voice whose id is already present is skipped.
    /// </summary>
    private static List<ChatterboxTurboReferenceVoice> ResolveVoices(ChatterboxTurboModelOptions options, ReferenceVoiceLibrary? defaultLibrary)
    {
        var voices = new List<ChatterboxTurboReferenceVoice>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        void AddUnique(ChatterboxTurboReferenceVoice voice, string source)
        {
            if (!ids.Add(voice.Id))
            {
                throw new ArgumentException($"The reference voice id '{voice.Id}' is duplicated ({source}).");
            }

            voices.Add(voice);
        }

        foreach (var voice in options.ReferenceVoices)
        {
            AddUnique(voice, "ChatterboxTurboModelOptions.ReferenceVoices");
        }

        if (options.ReferenceVoiceDirectory is not null)
        {
            if (!Directory.Exists(options.ReferenceVoiceDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"Reference voice directory '{options.ReferenceVoiceDirectory}' was not found.");
            }

            // Match by extension, case-insensitively, against the formats the core audio reader understands
            var files = Directory.GetFiles(options.ReferenceVoiceDirectory)
                .Where(file => AudioFileReader.SupportedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
                .ToArray();
            Array.Sort(files, StringComparer.Ordinal);

            // Lazy so a damaged file never fails catalog composition: the error surfaces at first use of that voice and names the file
            foreach (var file in files)
            {
                AddUnique(ChatterboxTurboReferenceVoice.FromFileLazy(Path.GetFileNameWithoutExtension(file), file), "ChatterboxTurboModelOptions.ReferenceVoiceDirectory");
            }
        }

        if (options.IncludeStoreVoices)
        {
            AddLibraryVoices(voices, ids, options.ReferenceVoiceLibrary ?? defaultLibrary, options.Diagnostics ?? NullSpeechDiagnostics.Instance);
        }

        return voices;
    }

    /// <summary>Appends the library's voices lazily; an id that is already present wins and the skip is reported.</summary>
    private static void AddLibraryVoices(
        List<ChatterboxTurboReferenceVoice> voices,
        HashSet<string> ids,
        ReferenceVoiceLibrary? library,
        ISpeechDiagnostics diagnostics)
    {
        if (library is null)
        {
            return;
        }

        var labels = new HashSet<string>(voices.Select(voice => voice.DisplayName), StringComparer.Ordinal);
        foreach (var info in library.EnumerateVoices())
        {
            if (!ids.Add(info.Id))
            {
                diagnostics.Report(
                    SpeechDiagnosticLevel.Info,
                    DiagnosticsCategory,
                    $"Reference voice '{info.Id}' from '{info.FilePath}' is hidden by an earlier voice with the same id.");
                continue;
            }

            // Labels need not be unique, but a source suffix keeps same-named voices distinguishable; repeated
            // collisions are numbered, e.g. "Name", "Name (user)", "Name (user 2)", so every label ends up unique
            var label = info.DisplayName;
            if (!labels.Add(label))
            {
                var source = info.Source == ReferenceVoiceSource.Pack ? "pack" : "user";
                var number = 2;
                label = $"{info.DisplayName} ({source})";
                while (!labels.Add(label))
                {
                    label = $"{info.DisplayName} ({source} {number})";
                    number++;
                }
            }

            voices.Add(ChatterboxTurboReferenceVoice.FromFileLazy(info.Id, info.FilePath, label, info.MoodClips));
        }
    }

    /// <summary>
    ///     Creates one published component bundle from its name and zip SHA-256, using the bundle's
    ///     <see cref="BundleContents"/> entry as its contents.
    /// </summary>
    /// <param name="bundleName">The bundle name (the zip file name without <c>.zip</c>).</param>
    /// <param name="sha256Checksum">The SHA-256 of the published zip.</param>
    /// <returns>The bundle, served from <see cref="SpeechModelBundle.ModelsReleaseBaseUri"/>.</returns>
    private static SpeechModelBundle CreateBundle(string bundleName, string sha256Checksum) =>
        new(new Uri(SpeechModelBundle.ModelsReleaseBaseUri, bundleName + ".zip"), sha256Checksum, BundleContents[bundleName]);

    /// <summary>Gets the name of the <c>models</c>-release bundle that carries a language-model variant's files.</summary>
    /// <param name="variant">The language-model variant.</param>
    /// <returns>The bundle name, for example <c>chatterbox-turbo-lm-20261014-fp16-b1</c>.</returns>
    internal static string GetLanguageModelBundleName(ChatterboxTurboLanguageModelVariant variant) =>
        variant == ChatterboxTurboLanguageModelVariant.Fp16
            ? "chatterbox-turbo-lm-20261014-fp16-b1"
            : "chatterbox-turbo-lm-20261014-fp32-b1";

    /// <summary>Gets the name of the <c>models</c>-release bundle that carries an auxiliary-model variant's files.</summary>
    /// <remarks>The repository's <c>q4</c> graphs map to the bundle naming rule's <c>int4</c> quantization token.</remarks>
    /// <param name="variant">The auxiliary-model variant.</param>
    /// <returns>The bundle name, for example <c>chatterbox-turbo-aux-20261014-int4-b1</c>.</returns>
    internal static string GetAuxiliaryModelBundleName(ChatterboxTurboAuxiliaryModelVariant variant) =>
        variant switch
        {
            ChatterboxTurboAuxiliaryModelVariant.Q4 => "chatterbox-turbo-aux-20261014-int4-b1",
            ChatterboxTurboAuxiliaryModelVariant.Fp16 => "chatterbox-turbo-aux-20261014-fp16-b1",
            _ => "chatterbox-turbo-aux-20261014-fp32-b1",
        };

    /// <summary>
    ///     Gets the auxiliary graph base names (embedding, speech encoder, conditional decoder) of a variant, which
    ///     follow the repository's suffix convention (<c>_q4</c>, <c>_fp16</c>, or none for full precision).
    /// </summary>
    /// <param name="variant">The auxiliary-model variant.</param>
    /// <returns>The three graph base names, without extension, in that order.</returns>
    private static string[] GetAuxiliaryGraphNames(ChatterboxTurboAuxiliaryModelVariant variant)
    {
        var suffix = variant switch
        {
            ChatterboxTurboAuxiliaryModelVariant.Q4 => "_q4",
            ChatterboxTurboAuxiliaryModelVariant.Fp16 => "_fp16",
            _ => string.Empty,
        };
        return ["embed_tokens" + suffix, "speech_encoder" + suffix, "conditional_decoder" + suffix];
    }

    /// <summary>Builds the vendor files of one component bundle: each graph followed by its weights file.</summary>
    /// <param name="graphNames">The graph base names (without extension), in order.</param>
    /// <returns>The descriptor of the graphs' <c>.onnx</c> and <c>.onnx_data</c> files, installed under <c>onnx/</c>.</returns>
#pragma warning disable S1075 // Download URLs are intentionally compiled-in, reviewed literals.
    private static SpeechModelDownloadDescriptor BuildVendorFiles(params string[] graphNames)
    {
        var files = new List<SpeechModelDownloadFile>();
        foreach (var name in graphNames)
        {
            foreach (var extension in new[] { ".onnx", ".onnx_data" })
            {
                var fileName = name + extension;
                files.Add(new SpeechModelDownloadFile(
                    new Uri(RepositoryOnnxUrl + fileName),
                    Checksums[fileName],
                    "onnx/" + fileName));
            }
        }

        return new SpeechModelDownloadDescriptor(files);
    }
#pragma warning restore S1075
}
