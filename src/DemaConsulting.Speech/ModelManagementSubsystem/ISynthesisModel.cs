using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Contract for a speech model that performs text-to-speech (TTS) synthesis, describing both
///     its catalog identity (through <see cref="ISpeechModel"/>) and how to construct the
///     synthesis engine that runs it and render inline Natural Language Audio Tags for it.
/// </summary>
/// <remarks>
///     Sub-phase 2b defined this interface as an empty role marker so
///     <see cref="SpeechModelCatalog"/> could already report and filter models by role via a type
///     check, and stated that Phase 4 would add "the real synthesis-engine-configuration member
///     ... and the inline Natural Language Audio Tag rendering logic here". This pass (Sub-phase
/// 4b) fulfills exactly that: <see cref="CreateBackend"/> and
///     <see cref="CapabilityProfile"/> are new members added to an interface that still has no
///     production implementations, so nothing defined in Sub-phase 2b was replaced or broken.
///     <para>
///     The full interface, including <see cref="CreateBackend"/>, <see cref="CapabilityProfile"/>,
///     and <see cref="ResolveSpeakerId"/>, is public by design, mirroring
///     <see cref="IRecognitionModel"/>'s identical rationale: third-party extension of the Speech
///     library is a confirmed goal, and a host application composes its catalog by calling
///     <see cref="SpeechModelCatalog.AddModels(ISpeechModel[])"/> with models it supplies itself.
///     A public contract lets an external package implement <see cref="ISynthesisModel"/> to add
///     an entirely new synthesis backend - not merely another instance of an existing
///     sherpa-onnx-backed model - without requiring <c>InternalsVisibleTo</c> access to this
///     assembly. The sibling <c>DemaConsulting.Speech.Sherpa</c> package is simply the first such
///     implementer, supplying this library's built-in sherpa-onnx-backed models; it has no
///     special access that a third-party package lacks.
///     </para>
/// </remarks>
public interface ISynthesisModel : ISpeechModel
{
    /// <summary>
    ///     Gets the mono audio format this model prefers a playback device to request before the
    ///     native synthesis engine has been loaded.
    /// </summary>
    /// <remarks>
    ///     This value is a best-effort hint, not an authoritative engine fact. A host may pass
    ///     it to
    ///     <see cref="AudioSubsystem.AudioDeviceFactory.CreatePlaybackDevice(AudioSubsystem.AudioDeviceSelection?, AudioFormat?)"/>
    ///     so the playback device attempts to open near the model's expected output format before
    ///     synthesis starts, potentially reducing or eliminating later resampling work. The real
    ///     source of truth remains the constructed engine's
    ///     <see cref="SynthesisSubsystem.ISynthesisBackend.SampleRate"/>, which is read only after
    ///     <see cref="CreateBackend"/> has been used to load the native engine. Callers must
    ///     therefore still handle a mismatch by resampling playback audio after construction.
    /// </remarks>
    public AudioFormat PreferredAudioFormat { get; }

    /// <summary>
    ///     Builds a loaded, model-specific <see cref="SynthesisSubsystem.ISynthesisBackend"/> for
    ///     this model, resolved against the directory its verified files were installed into.
    /// </summary>
    /// <param name="installedModelDirectory">
    ///     The absolute path of the directory holding this model's installed files (the store's
    ///     <c>current/</c> directory for this model). Must not be null or empty; the
    ///     implementation combines it with its own known relative file names to produce the
    ///     absolute model/tokens/lexicon/etc. paths the engine requires.
    /// </param>
    /// <returns>
    ///     A loaded <see cref="SynthesisSubsystem.ISynthesisBackend"/> ready to synthesize
    ///     segments for this model.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="installedModelDirectory"/> is null or empty.
    /// </exception>
    /// <remarks>
    ///     Unlike the configuration-only member this replaced, this member allocates real native
    ///     resources: constructing the native synthesizer is now the model's own responsibility,
    ///     so a model whose files are missing or whose native runtime is absent throws here, and
    ///     the synthesis subsystem degrades to an unavailable synthesizer by catching that
    ///     failure. Implementations must be safe to call concurrently.
    /// </remarks>
    ISynthesisBackend CreateBackend(string installedModelDirectory);

    /// <summary>
    ///     Gets the strategy this model uses to render an ordered sequence of parsed Natural
    ///     Language Audio Tag spans into a <see cref="SpeechPlan"/> of synthesis calls and real
    ///     inserted silence. The default implementation returns
    ///     <see cref="DefaultModelCapabilityProfile.Instance"/>, which derives generically correct
    ///     behavior purely from this model's declared <see cref="ISpeechModel.AudioTagSupport"/>
    ///     and <see cref="ISpeechModel.Parameters"/>.
    /// </summary>
    /// <remarks>
    ///     A model needs zero code to get generically-correct tag handling from this default
    ///     hook, and may override it only when it needs bespoke, non-generic per-model rendering
    ///     (for example a model whose native tag syntax differs from the library's canonical
    ///     bracket text). This mirrors <see cref="ISpeechModel.NormalizeText"/>'s default-hook
    ///     pattern.
    /// </remarks>
    IModelCapabilityProfile CapabilityProfile => DefaultModelCapabilityProfile.Instance;

    /// <summary>
    ///     Resolves a session-level parameter value bag to the engine-specific integer speaker id
    ///     to synthesize with. The default implementation always returns <c>0</c>, identical to
    ///     every existing model's previous hard-coded behavior.
    /// </summary>
    /// <param name="parameterValues">
    ///     The untyped key-value bag supplied to <c>SpeechSynthesizerFactory.LoadAsync</c> (for
    ///     example built from a host's settings UI via a declared <see cref="ChoiceParameter"/>),
    ///     or <see langword="null"/> when the caller supplied none.
    /// </param>
    /// <returns>The engine-specific speaker id to pass to <c>ISynthesisBackend.Generate</c>.</returns>
    /// <remarks>
    ///     Added so a multi-speaker model (starting with the sibling
    ///     <c>DemaConsulting.Speech.Sherpa</c> package's Kokoro synthesis model) can own its own string-to-int
    ///     voice mapping entirely inside its own backing class, mirroring
    ///     <see cref="CapabilityProfile"/>'s "generically correct for free, override only for
    ///     bespoke per-model behavior" default-hook pattern. A single-speaker model (or a model
    ///     that has not yet been extended to expose speaker selection) needs zero code to keep
    ///     today's speaker-0 behavior. Implementations must never throw: an unrecognized or
    ///     missing selection should degrade to a sensible default speaker id, never fault
    ///     synthesis.
    /// </remarks>
    int ResolveSpeakerId(IReadOnlyDictionary<string, object>? parameterValues) => 0;
}
