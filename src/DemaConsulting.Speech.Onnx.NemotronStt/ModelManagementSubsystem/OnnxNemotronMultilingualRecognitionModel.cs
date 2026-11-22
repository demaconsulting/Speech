using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.Onnx.NemotronStt.RecognitionSubsystem;
using DemaConsulting.Speech.Onnx.OnnxRuntimeSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;
using Microsoft.ML.OnnxRuntime;

namespace DemaConsulting.Speech.Onnx.NemotronStt.ModelManagementSubsystem;

/// <summary>
///     Production <see cref="IRecognitionModel"/> for NVIDIA Nemotron 3.5 ASR streaming (0.6B
///     parameters, int4-quantized ONNX export), run directly through ONNX Runtime with no bundled
///     native inference engine, unlike the sibling <c>DemaConsulting.Speech.Sherpa</c> package's
///     sherpa-onnx-backed Nemotron model.
/// </summary>
/// <remarks>
///     <b>Download provenance</b>: the files are originally published at
///     <c>https://huggingface.co/onnx-community/nemotron-3.5-asr-streaming-0.6b-onnx-int4</c>, and their
///     <c>resolve/main/...</c> URLs are recorded below. The SHA-256 checksums and sizes in
///     <see cref="BundleContents"/> were computed directly against the downloaded files, not
///     copied from a third-party listing. Eleven files (about 790 MB) are installed: the encoder,
///     decoder and joint graphs, each with its external-data file (<c>*.onnx.data</c>, which must
///     sit beside its <c>.onnx</c> under the identical base name for ONNX Runtime to find it), the
///     vocabulary, and the tokenizer/configuration files that document the export. They are
///     downloaded, unmodified, as one bundle from this project's <c>models</c> GitHub release
///     (<see cref="Bundles"/>, <c>nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1.zip</c>), so a user
///     needs no Hugging Face access; the zip is pinned by its own SHA-256 and every extracted file is
///     checked again against its pin in <see cref="BundleContents"/>.
///     <para>
///     <b>One catalog entry, a language parameter</b>: the download mirror layout is
///     <c>&lt;mirror&gt;/&lt;model id&gt;/&lt;bundle&gt;.zip</c>, so the model id must equal the mirror's one
///     folder name, and every locale shares the same ~790 MB of files. This model therefore
///     exposes one catalog id with a <c>language</c> <see cref="ChoiceParameter"/> (<c>en-US</c>
///     default, <c>en-GB</c>) rather than one id per locale. The encoder's <c>lang_id</c> is
///     derived at load time from the vocabulary's locale marker tokens (see
///     <see cref="NemotronVocabulary"/>), never hard-coded. Adding a locale is one entry in
///     <see cref="Languages"/>. An unrecognized or missing selection degrades to <c>en-US</c>.
///     </para>
///     <para>
///     <b>License</b>: NVIDIA publishes the model weights (<c>nvidia/nemotron-3.5-asr-streaming-0.6b</c>)
///     under the OpenMDW License Agreement 1.1, so <see cref="LicenseName"/> is
///     <c>OpenMDW-1.1</c> and <see cref="LicenseUrl"/> points at its text. The int4 ONNX
///     conversion is downloaded unchanged from the <c>onnx-community</c> Hugging Face
///     organization; that repository's MIT tag covers only the conversion, not the weights. The
///     bundle ships the OpenMDW-1.1 and MIT texts and a <c>NOTICE</c> with the files, installed under
///     <c>licenses/&lt;bundle&gt;/</c>.
///     </para>
///     <para>
///     <b>Execution providers</b>: the encoder honors the preferred execution provider names with
///     a probe inference and CPU fallback (see <see cref="OnnxExecutionProviderSelector"/>); the
///     tiny decoder and joint networks always run on the CPU with one thread and no spinning.
///     </para>
/// </remarks>
public sealed class OnnxNemotronMultilingualRecognitionModel : IRecognitionModel
{
    /// <summary>The stable catalog identifier; equal to the download mirror's folder name.</summary>
    public const string ModelId = "nemotron-3.5-asr-streaming-0.6b-onnx-int4";

    /// <summary>The parameter id of this model's language-selection <see cref="ChoiceParameter"/>.</summary>
    public const string LanguageParameterId = "language";

    /// <summary>The default (and fallback) locale.</summary>
    public const string DefaultLanguage = "en-US";

    /// <summary>The relative install path of the encoder graph.</summary>
    private const string EncoderPath = "encoder.onnx";

    /// <summary>The relative install path of the decoder graph.</summary>
    private const string DecoderPath = "decoder.onnx";

    /// <summary>The relative install path of the joint graph.</summary>
    private const string JointPath = "joint.onnx";

    /// <summary>The relative install path of the vocabulary.</summary>
    private const string VocabularyPath = "vocab.txt";

    /// <summary>The declared locales and their display labels, in presentation order.</summary>
    private static readonly (string Value, string Label)[] Languages =
    [
        (DefaultLanguage, "English (US)"),
        ("en-GB", "English (UK)"),
    ];

    /// <summary>The execution provider names the encoder attempts before falling back to CPU.</summary>
    private readonly IReadOnlyList<string>? _preferredExecutionProviderNames;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxNemotronMultilingualRecognitionModel"/>
    ///     class, using CPU-only execution.
    /// </summary>
    public OnnxNemotronMultilingualRecognitionModel()
        : this(null)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="OnnxNemotronMultilingualRecognitionModel"/>
    ///     class, attempting <paramref name="preferredExecutionProviderNames"/> for the encoder
    ///     before falling back to CPU.
    /// </summary>
    /// <param name="preferredExecutionProviderNames">
    ///     The ordered, accelerated execution provider names to attempt first (for example
    ///     <c>"DmlExecutionProvider"</c>), or <see langword="null"/> for CPU-only. A host
    ///     application that wants acceleration adds the matching native runtime package itself.
    /// </param>
    public OnnxNemotronMultilingualRecognitionModel(IReadOnlyList<string>? preferredExecutionProviderNames)
    {
        _preferredExecutionProviderNames = preferredExecutionProviderNames;
    }

    /// <inheritdoc/>
    public string Id => ModelId;

    /// <inheritdoc/>
    public string DisplayName => "NVIDIA Nemotron 3.5 ASR Streaming 0.6B (int4 ONNX, English)";

    /// <summary>
    ///     <inheritdoc/>
    ///     See the type-level remarks' "License" paragraph.
    /// </summary>
    public string LicenseName => "OpenMDW-1.1";

    /// <inheritdoc/>
    public Uri? LicenseUrl { get; } = new("https://openmdw.ai/license/1-1/");

    /// <inheritdoc/>
    public SpeechModelRole Role => SpeechModelRole.Recognition;

    /// <inheritdoc/>
    public IReadOnlyList<ISpeechModelParameter> Parameters { get; } =
    [
        new ChoiceParameter(
            LanguageParameterId,
            "Language",
            "Selects the spoken language the model should expect.",
            Languages.Select(language => new ChoiceParameterOption(language.Value, language.Label)).ToList(),
            DefaultLanguage),
    ];

    /// <inheritdoc/>
    public SpeechModelAudioTagSupport AudioTagSupport => SpeechModelAudioTagSupport.None;

    /// <inheritdoc/>
    public AudioFormat AudioFormat => AudioFormat.Mono(OnnxNemotronRecognitionEngine.SampleRate);

#pragma warning disable S1075 // This model's download URLs are intentionally compiled-in, reviewed literals - the whole point of a catalog entry is to name specific, checksum-verified release assets.
    /// <summary>
    ///     The name of the <c>models</c>-release bundle that carries this model's unmodified vendor
    ///     files (see <see cref="BundleContents"/>).
    /// </summary>
    internal const string BundleName = "nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1";

    /// <summary>
    ///     The vendor files of the <see cref="BundleName"/> bundle: the eleven upstream files, each
    ///     with its original <c>resolve/main</c> URL, SHA-256 pin and install path.
    /// </summary>
    /// <remarks>
    ///     This is the single source of truth for the per-file pins: <see cref="Bundles"/> uses it as
    ///     the bundle's contents (so the installer checks every extracted file against these pins), and
    ///     the non-shipped <c>DemaConsulting.Speech.ModelBundler</c> tool (which
    ///     sees this member through <c>InternalsVisibleTo</c>) packs exactly these files into the
    ///     bundle, so no SHA-256 is declared twice. Byte sizes: encoder.onnx 2,677,548;
    ///     encoder.onnx.data 690,089,984; decoder.onnx 4,696; decoder.onnx.data 59,785,216; joint.onnx
    ///     2,136; joint.onnx.data 37,830,656; vocab.txt 64,024; tokenizer.json 642,525;
    ///     tokenizer_config.json 183; genai_config.json 1,892; audio_processor_config.json 413.
    ///     Immutable and safe to share across threads.
    /// </remarks>
    internal static SpeechModelDownloadDescriptor BundleContents { get; } = new(
    [
        Declare("encoder.onnx", "0b05217594ec0bda442e43a90a298ac2471a3bdcea9b169de34214e61a730e17"),
        Declare("encoder.onnx.data", "2f27295855aeb99ab1f8cd2254418d9ad7a087ea8dbe85f5596b4d887ea7d630"),
        Declare("decoder.onnx", "6a9f608dcbab71ebd81ffa4c198e82a5b6bb10f1c1830a94c752c5f543454df3"),
        Declare("decoder.onnx.data", "e5fd55cbeeb268f9d383e2ee72735b9fbbb13aea4bc7cd38cb73b8e16f1366c7"),
        Declare("joint.onnx", "e2c7d2fa40a243bf82eaca36c15698c52129de9361d2875d7f223f67fcd9482d"),
        Declare("joint.onnx.data", "2e0fb1c060f3777a1a76e78d5589dd54f01505a06dffbd2588e315508b402c12"),
        Declare("vocab.txt", "ca88922ac5a92c911b79985b69634d7a4c2ef604d61b71bbe2982210dd77cd43"),
        Declare("tokenizer.json", "24e1e8335c8396884a86f06880271376ae46a29381cfc35c82c6295d407acec7"),
        Declare("tokenizer_config.json", "ea4b35353f468fea11f436f837d9621a29b4ba9d1c73c1ed0aa5743f5a53919e"),
        Declare("genai_config.json", "39568fbeebbe848696a1e2a01c7f33df000f72c29f2285509fd12442bda9571e"),
        Declare("audio_processor_config.json", "ab28d41eb87ce3922006edeb9c3fad4d5ce451f9a56a12d84f470f02a5ec157b"),
    ]);
#pragma warning restore S1075

    /// <summary>
    ///     The <c>models</c>-release bundle this model installs from: one zip holding the unmodified
    ///     files of <see cref="BundleContents"/>, pinned by the zip's own SHA-256.
    /// </summary>
    /// <remarks>
    ///     Public so a host or a mirror administrator can list exactly which zip (name, URL and
    ///     SHA-256) this model downloads. The zip also carries the OpenMDW-1.1 and MIT license texts, a
    ///     <c>NOTICE</c>, a <c>README.txt</c> and a <c>model.json</c> that records each file's original
    ///     URL. The bundle bytes are reproducible, so the pin never changes for this bundle revision.
    ///     Declared after <see cref="BundleContents"/> because static initializers run in textual
    ///     order. Immutable and safe to share across threads.
    /// </remarks>
    public static IReadOnlyList<SpeechModelBundle> Bundles { get; } =
    [
        new SpeechModelBundle(
            new Uri(SpeechModelBundle.ModelsReleaseBaseUri, BundleName + ".zip"),
            "733375bb604712afac6c4adbd6a9149ebc414fc9a5bee39ed3bdfc3c104048b2",
            BundleContents),
    ];

    /// <inheritdoc/>
    /// <remarks>
    ///     The single zip of <see cref="Bundles"/>; <see cref="InstallAsync"/> extracts the eleven
    ///     files of <see cref="BundleContents"/>, whose remarks list each file's byte size.
    /// </remarks>
    public SpeechModelDownloadDescriptor DownloadDescriptor { get; } = SpeechModelBundle.CreateDescriptor(Bundles);

    /// <summary>
    ///     Extracts and verifies the downloaded bundle of <see cref="Bundles"/> in the staging
    ///     directory, leaving the eleven vendor files at the install root and the bundle's license
    ///     files under <c>licenses/nemotron-3.5-asr-streaming-0.6b-20261011-int4-b1/</c>.
    /// </summary>
    /// <param name="stagedFilesDirectory">The staging directory holding the downloaded, checksum-verified zip.</param>
    /// <param name="cancellationToken">A token to observe while extracting.</param>
    /// <returns>A task that completes once the bundle is extracted and the zip is deleted.</returns>
    /// <remarks>
    ///     Delegates to <see cref="SpeechModelBundle.InstallAsync"/>, which rejects any unsafe entry
    ///     name, any extra or missing file, and any file whose SHA-256 differs from its pin in
    ///     <see cref="BundleContents"/>; an exception here leaves no partial install.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stagedFilesDirectory"/> is null or empty.</exception>
    /// <exception cref="InvalidDataException">Thrown when the zip or one of its files fails verification.</exception>
    public Task InstallAsync(string stagedFilesDirectory, CancellationToken cancellationToken = default) =>
        SpeechModelBundle.InstallAsync(Bundles, stagedFilesDirectory, cancellationToken);

    /// <inheritdoc/>
    public IRecognitionBackend CreateBackend(string installedModelDirectory) =>
        CreateBackend(installedModelDirectory, null);

    /// <summary>
    ///     Builds a loaded <see cref="OnnxNemotronRecognitionEngine"/>: reads the vocabulary,
    ///     derives the selected language's <c>lang_id</c>, and creates the three ONNX Runtime
    ///     sessions.
    /// </summary>
    /// <param name="installedModelDirectory">The installed model directory.</param>
    /// <param name="parameterValues">The parameter bag; may contain <see cref="LanguageParameterId"/>.</param>
    /// <returns>The loaded backend.</returns>
    /// <remarks>
    ///     A missing or unrecognized language selection degrades to <see cref="DefaultLanguage"/>.
    ///     The sessions are created one after another; if a later one fails, the earlier ones are
    ///     disposed before the exception propagates, since no engine exists yet to own them.
    /// </remarks>
    public IRecognitionBackend CreateBackend(
        string installedModelDirectory,
        IReadOnlyDictionary<string, object>? parameterValues) =>
        CreateBackend(installedModelDirectory, parameterValues, new NemotronEngineOptions());

    /// <summary>
    ///     Builds a loaded <see cref="OnnxNemotronRecognitionEngine"/> with explicit engine
    ///     options, as <see cref="CreateBackend(string, IReadOnlyDictionary{string, object})"/>
    ///     does with the documented defaults.
    /// </summary>
    /// <param name="installedModelDirectory">The installed model directory.</param>
    /// <param name="parameterValues">The parameter bag; may contain <see cref="LanguageParameterId"/>.</param>
    /// <param name="engineOptions">
    ///     The engine options, including the blank penalty handed to the RNN-T network. Must not
    ///     be <see langword="null"/>.
    /// </param>
    /// <returns>The loaded backend.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="installedModelDirectory"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="engineOptions"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="NemotronEngineOptions.BlankPenalty"/> is negative or not finite.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the vocabulary does not declare the selected locale.</exception>
    /// <remarks>
    ///     Internal so the engine constants stay out of the public API; the evaluation tool uses
    ///     it to measure alternative settings (for example the blank penalty) on the real model.
    ///     The options are validated before any file is read or session created.
    /// </remarks>
    internal IRecognitionBackend CreateBackend(
        string installedModelDirectory,
        IReadOnlyDictionary<string, object>? parameterValues,
        NemotronEngineOptions engineOptions)
    {
        ArgumentException.ThrowIfNullOrEmpty(installedModelDirectory);
        ArgumentNullException.ThrowIfNull(engineOptions);
        NemotronRnntNetwork.ValidateBlankPenalty(engineOptions.BlankPenalty);

        var vocabulary = NemotronVocabulary.Load(ResolvePath(installedModelDirectory, VocabularyPath));
        var language = ResolveLanguage(parameterValues);
        if (!vocabulary.TryGetLanguageId(language, out var languageId))
        {
            throw new InvalidOperationException(
                $"The Nemotron vocabulary does not declare the locale '{language}'.");
        }

        InferenceSession? encoderSession = null;
        InferenceSession? decoderSession = null;
        InferenceSession? jointSession = null;
        NemotronRnntNetwork? network = null;
        try
        {
            encoderSession = OnnxExecutionProviderSelector.Create(
                ResolvePath(installedModelDirectory, EncoderPath),
                _preferredExecutionProviderNames,
                NemotronEncoder.RunProbeInference);

            using var options = NemotronRnntNetwork.CreateSessionOptions();
            decoderSession = new InferenceSession(ResolvePath(installedModelDirectory, DecoderPath), options);
            jointSession = new InferenceSession(ResolvePath(installedModelDirectory, JointPath), options);

            // The network takes ownership of the decoder and joint sessions once it exists.
            network = new NemotronRnntNetwork(decoderSession, jointSession, vocabulary.BlankId, engineOptions.BlankPenalty);
            decoderSession = null;
            jointSession = null;
            NemotronRnntGreedyDecoder? greedyDecoder = new NemotronRnntGreedyDecoder(network);
            network = null;
            NemotronEncoder? encoder = null;
            try
            {
                encoder = new NemotronEncoder(encoderSession, languageId);
                encoderSession = null;
                var engine = new OnnxNemotronRecognitionEngine(encoder, greedyDecoder, vocabulary, engineOptions);
                encoder = null;
                greedyDecoder = null;
                return engine;
            }
            catch
            {
                encoder?.Dispose();
                greedyDecoder?.Dispose();
                throw;
            }
        }
        catch
        {
            network?.Dispose();
            jointSession?.Dispose();
            decoderSession?.Dispose();
            encoderSession?.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Builds one declared download file entry served from the upstream repository's
    ///     <c>resolve/main</c> path; the install path equals the file name, which keeps each
    ///     <c>.onnx.data</c> file beside its <c>.onnx</c> as ONNX Runtime requires.
    /// </summary>
    private static SpeechModelDownloadFile Declare(string name, string sha256) =>
        new(
            new Uri($"https://huggingface.co/onnx-community/nemotron-3.5-asr-streaming-0.6b-onnx-int4/resolve/main/{name}"),
            sha256,
            name);

    /// <summary>Combines the installed directory with a declared relative install path.</summary>
    private static string ResolvePath(string installedModelDirectory, string relativeInstallPath) =>
        Path.Combine(installedModelDirectory, relativeInstallPath);

    /// <summary>Resolves the selected language, falling back to <see cref="DefaultLanguage"/>.</summary>
    private static string ResolveLanguage(IReadOnlyDictionary<string, object>? parameterValues)
    {
        if (parameterValues is not null &&
            parameterValues.TryGetValue(LanguageParameterId, out var value) &&
            value is string selected &&
            Languages.Any(language => string.Equals(language.Value, selected, StringComparison.Ordinal)))
        {
            return selected;
        }

        return DefaultLanguage;
    }
}
