using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using SherpaOnnx;

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
///     4b) fulfills exactly that: <see cref="CreateEngineConfig"/> and
///     <see cref="CapabilityProfile"/> are new members added to an interface that still has no
///     production implementations, so nothing defined in Sub-phase 2b was replaced or broken.
///     <para>
///     Both members are deliberately <see langword="internal"/> rather than public, mirroring
///     <see cref="IRecognitionModel"/>'s identical Phase 3 pattern: this library's "engine
///     backend stays swappable at the public API surface" constraint is scoped to
///     <c>ISpeechSynthesizer</c>, while each per-model backing class is architecturally
///     responsible for "sherpa-onnx configuration for its own model architecture" - so returning
///     a real <see cref="OfflineTtsConfig"/> here is consistent with the approved design. Keeping
///     the members internal means this interface's <em>public</em> surface is unchanged, no
///     sherpa-onnx type leaks into the library's public API, and only assemblies granted
///     <c>InternalsVisibleTo</c> (the library itself and its test project) can implement the
///     interface.
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
    ///     <see cref="SynthesisSubsystem.ISynthesisEngine.SampleRate"/>, which is read only after
    ///     <see cref="CreateEngineConfig"/> has been used to load the native engine. Callers must
    ///     therefore still handle a mismatch by resampling playback audio after construction.
    /// </remarks>
    public AudioFormat PreferredAudioFormat { get; }

    /// <summary>
    ///     Builds the sherpa-onnx offline text-to-speech configuration for this model, resolved
    ///     against the directory its verified files were installed into.
    /// </summary>
    /// <param name="installedModelDirectory">
    ///     The absolute path of the directory holding this model's installed files (the store's
    ///     <c>current/</c> directory for this model). Must not be null or empty; the
    ///     implementation combines it with its own known relative file names to produce the
    ///     absolute model/tokens/lexicon/etc. paths the engine requires.
    /// </param>
    /// <returns>
    ///     A fully populated <see cref="OfflineTtsConfig"/> describing this model's architecture
    ///     and file locations.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="installedModelDirectory"/> is null or empty.
    /// </exception>
    /// <remarks>
    ///     This member is pure with respect to library state: it reads nothing but its own
    ///     compiled-in constants and the supplied directory path, allocates no native resources,
    ///     and never loads the model itself - constructing the native synthesizer from the
    ///     returned configuration is the synthesis subsystem's responsibility, so a model whose
    ///     files are missing or whose native runtime is absent fails there (and degrades to an
    ///     unavailable synthesizer) rather than here. Implementations must be safe to call
    ///     concurrently.
    /// </remarks>
    internal OfflineTtsConfig CreateEngineConfig(string installedModelDirectory);

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
    internal IModelCapabilityProfile CapabilityProfile => DefaultModelCapabilityProfile.Instance;

    /// <summary>
    ///     Resolves a session-level parameter value bag to the sherpa-onnx integer speaker id to
    ///     synthesize with. The default implementation always returns <c>0</c>, identical to
    ///     every existing model's previous hard-coded behavior.
    /// </summary>
    /// <param name="parameterValues">
    ///     The untyped key-value bag supplied to <c>SpeechSynthesizerFactory.Create</c> (for
    ///     example built from a host's settings UI via a declared <see cref="ChoiceParameter"/>),
    ///     or <see langword="null"/> when the caller supplied none.
    /// </param>
    /// <returns>The sherpa-onnx speaker id to pass to <c>ISynthesisEngine.Generate</c>.</returns>
    /// <remarks>
    ///     Added so a multi-speaker model (starting with
    ///     <see cref="SherpaOnnxKokoroEnglishSynthesisModel"/>) can own its own string-to-int
    ///     voice mapping entirely inside its own backing class, mirroring
    ///     <see cref="CapabilityProfile"/>'s "generically correct for free, override only for
    ///     bespoke per-model behavior" default-hook pattern. A single-speaker model (or a model
    ///     that has not yet been extended to expose speaker selection) needs zero code to keep
    ///     today's speaker-0 behavior. Implementations must never throw: an unrecognized or
    ///     missing selection should degrade to a sensible default speaker id, never fault
    ///     synthesis.
    /// </remarks>
    internal int ResolveSpeakerId(IReadOnlyDictionary<string, object>? parameterValues) => 0;
}
