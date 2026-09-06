using DemaConsulting.Speech.AudioSubsystem;
using SherpaOnnx;

namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Contract for a speech model that performs speech-to-text (STT) recognition, describing
///     both its catalog identity (through <see cref="ISpeechModel"/>) and how to construct the
///     streaming recognition engine that runs it.
/// </summary>
/// <remarks>
///     Sub-phase 2b defined this interface as an empty role marker so
///     <see cref="SpeechModelCatalog"/> could already report and filter models by role via a type
///     check, and stated that Phase 3 would add "the real recognition-engine-configuration
///     member here". This pass fulfills exactly that: <see cref="AudioFormat"/> and
///     <see cref="CreateEngineConfig"/> are new members added to an interface that still has no
///     production implementations, so nothing defined in Sub-phase 2b was replaced or broken.
///     <para>
///     The two members deliberately have different visibility. <see cref="AudioFormat"/> is
///     public because it is a plain library-owned data value that leaks no native engine type and
///     lets callers compose an audio device before loading the engine. By contrast,
///     <see cref="CreateEngineConfig"/> remains <see langword="internal"/> because it returns the
///     real sherpa-onnx <see cref="OnlineRecognizerConfig"/> type. This keeps the public
///     recognition surface swappable while still letting each model own its native-engine
///     configuration. Only assemblies granted <c>InternalsVisibleTo</c> (the library itself and
///     its test project) can implement the interface. That restriction is intentional and matches
///     this library's "one backing class per model; a new model requires a new library
///     release" decision.
///     </para>
/// </remarks>
public interface IRecognitionModel : ISpeechModel
{
    /// <summary>
    ///     Gets the mono audio format that this model's recognition engine requires its input
    ///     audio to be supplied at.
    /// </summary>
    /// <remarks>
    ///     Streaming sherpa-onnx models are trained at a fixed feature sample rate (commonly
    ///     16000 Hz, but declared per model rather than assumed) and produce unusable results if
    ///     fed audio at any other rate. Exposing the full format as a model-declared fact - instead
    ///     of hard-coding only a sample rate - lets the recognition subsystem and host composition
    ///     code request a capture device already opened in the model's own format. Current models are
    ///     mono, so <see cref="AudioFormat.ChannelCount"/> is presently <c>1</c>; the value must
    ///     agree with the feature configuration returned by <see cref="CreateEngineConfig"/>.
    ///     Reading this property never throws.
    /// </remarks>
    public AudioFormat AudioFormat { get; }

    /// <summary>
    ///     Builds the sherpa-onnx streaming-recognizer configuration for this model, resolved
    ///     against the directory its verified files were installed into.
    /// </summary>
    /// <param name="installedModelDirectory">
    ///     The absolute path of the directory holding this model's installed files (the store's
    ///     <c>current/</c> directory for this model). Must not be null or empty; the
    ///     implementation combines it with its own known relative file names to produce the
    ///     absolute encoder/decoder/joiner/tokens paths the engine requires.
    /// </param>
    /// <returns>
    ///     A fully populated <see cref="OnlineRecognizerConfig"/> describing this model's
    ///     architecture, file locations, feature configuration, and decoding options.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="installedModelDirectory"/> is null or empty.
    /// </exception>
    /// <remarks>
    ///     This member is pure with respect to library state: it reads nothing but its own
    ///     compiled-in constants and the supplied directory path, allocates no native resources,
    ///     and never loads the model itself - constructing the native recognizer from the
    ///     returned configuration is the recognition subsystem's responsibility, so a model whose
    ///     files are missing or whose native runtime is absent fails there (and degrades to an
    ///     unavailable recognizer) rather than here. Implementations must be safe to call
    ///     concurrently.
    /// </remarks>
    internal OnlineRecognizerConfig CreateEngineConfig(string installedModelDirectory);

    /// <summary>
    ///     Gets the duration, in milliseconds, of pre-endpoint audio the recognition engine
    ///     should buffer and silently replay into a freshly reset stream immediately after an
    ///     endpoint fires, to pre-warm the model's internal decoding state before genuinely new
    ///     (post-pause) audio arrives. Defaults to <c>0</c>, meaning the feature is disabled and
    ///     the engine behaves exactly as it always has: a hard <c>Reset()</c> with no replay.
    /// </summary>
    /// <remarks>
    ///     This member exists to fix a real, confirmed defect specific to
    ///     <see cref="SherpaOnnxNemotronStreamingEnRecognitionModel"/>: its streaming encoder has
    ///     a measured ~550ms "cold" warm-up blackout immediately after <c>Reset()</c>, during
    ///     which genuinely spoken audio arriving in that window can be silently lost, and its
    ///     endpoint detector was observed to fire as a false positive mid-utterance more often
    ///     than <see cref="SherpaOnnxZipformerEnRecognitionModel"/>'s, making the defect
    ///     reproducible on real recordings (see
    ///     <c>.agent-logs/planning-nemotron-endpoint-word-loss-warmup-replay-fix-9d4b71.md</c>
    ///     for the full evidence). Replaying a rolling buffer of the audio immediately preceding
    ///     the endpoint - with its recognized text always suppressed, never surfaced as a
    ///     <see cref="RecognitionSubsystem.SpeechRecognitionResult"/> - pre-warms that same
    ///     encoder state before live audio resumes, without requiring any change to endpoint
    ///     sensitivity.
    ///     <para>
    ///     <b>Deliberately opt-in, not a global default.</b> The same investigation found that
    ///     forcing this feature on for <see cref="SherpaOnnxZipformerEnRecognitionModel"/>
    ///     produced a genuine regression - duplicated text (for example, "THAT THAT IS THE
    ///     QUESTION") - because a replayed word's still-forming onset can cause the underlying
    ///     transducer decoder to commit a token during the "silently suppressed" replay, which
    ///     the model then treats as a distinct word when the same content's genuine continuation
    ///     arrives live afterward. Suppressing <c>GetResult()</c> output during replay hides the
    ///     text from callers but cannot prevent that internal token commit. Because no model
    ///     benefits from this risk unless it has an independently confirmed word-loss defect to
    ///     offset it, every model other than the one that opts in must keep today's exact
    ///     behavior - hence the safe, zero-cost default of <c>0</c> here rather than a
    ///     library-wide constant.
    ///     </para>
    /// </remarks>
    internal int PostEndpointWarmupWindowMs => 0;

    /// <summary>
    ///     Applies this model's own text normalization/correction to one recognized result's
    ///     text before it is raised to consumers, distinguishing a finalized (committed) result
    ///     from a provisional (still-forming) one. The default implementation forwards to the
    ///     inherited <see cref="ISpeechModel.NormalizeText(string)"/> identity hook, so a model
    ///     that produces already-cased, already-punctuated text needs zero code to keep today's
    ///     pass-through behavior for both finalized and provisional results.
    /// </summary>
    /// <param name="text">The recognized result's text to normalize.</param>
    /// <param name="isFinal">
    ///     <see langword="true"/> when <paramref name="text"/> is a committed, finalized result
    ///     (<see cref="RecognitionSubsystem.SpeechRecognitionResult.IsFinal"/> is
    ///     <see langword="true"/>); <see langword="false"/> when it is a still-forming
    ///     provisional hypothesis that may be revised or superseded.
    /// </param>
    /// <returns>The normalized text; by default, <paramref name="text"/> unchanged.</returns>
    /// <remarks>
    ///     This overload is deliberately declared only on <see cref="IRecognitionModel"/>, not on
    ///     the shared <see cref="ISpeechModel"/> base: the finalized/provisional distinction is a
    ///     recognition-only concept with no meaning on the synthesis side, so growing the shared
    ///     single-argument member would leak a recognition-only concern into
    ///     <see cref="ISynthesisModel"/>. A model overriding this member typically restores
    ///     casing/contractions/punctuation fully for finalized results and applies only cheap
    ///     casing to provisional ones, since a provisional hypothesis may still be revised many
    ///     times before it settles.
    /// </remarks>
    string NormalizeText(string text, bool isFinal) => NormalizeText(text);
}
