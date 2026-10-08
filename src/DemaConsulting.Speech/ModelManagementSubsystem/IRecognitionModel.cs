using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

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
///     <see cref="CreateBackend(string)"/> are new members added to an interface that still has no
///     production implementations, so nothing defined in Sub-phase 2b was replaced or broken.
///     <para>
///     The full interface, including <see cref="CreateBackend(string)"/>, is public by design:
///     third-party extension of the Speech library is a confirmed goal, and a host application
///     composes its catalog by calling <see cref="SpeechModelCatalog.AddModels(ISpeechModel[])"/>
///     with models it supplies itself. A public contract lets an external package implement
///     <see cref="IRecognitionModel"/> to add an entirely new recognition backend - not merely
///     another instance of an existing sherpa-onnx-backed model - without requiring
///     <c>InternalsVisibleTo</c> access to this assembly. The sibling
///     <c>DemaConsulting.Speech.Sherpa</c> package is simply the first such implementer, supplying
///     this library's built-in sherpa-onnx-backed models; it has no special access that a
///     third-party package lacks.
///     </para>
/// </remarks>
public interface IRecognitionModel : ISpeechModel
{
    /// <summary>
    ///     Gets the mono audio format that this model's recognition engine requires its input
    ///     audio to be supplied at.
    /// </summary>
    /// <remarks>
    ///     Streaming models are trained at a fixed feature sample rate (commonly
    ///     16000 Hz, but declared per model rather than assumed) and produce unusable results if
    ///     fed audio at any other rate. Exposing the full format as a model-declared fact - instead
    ///     of hard-coding only a sample rate - lets the recognition subsystem and host composition
    ///     code request a capture device already opened in the model's own format. Current models are
    ///     mono, so <see cref="AudioFormat.ChannelCount"/> is presently <c>1</c>; the value must
    ///     agree with the feature configuration used by <see cref="CreateBackend(string)"/>.
    ///     Reading this property never throws.
    /// </remarks>
    public AudioFormat AudioFormat { get; }

    /// <summary>
    ///     Builds a loaded, model-specific <see cref="IRecognitionBackend"/> for this model,
    ///     resolved against the directory its verified files were installed into.
    /// </summary>
    /// <param name="installedModelDirectory">
    ///     The absolute path of the directory holding this model's installed files (the store's
    ///     <c>current/</c> directory for this model). Must not be null or empty; the
    ///     implementation combines it with its own known relative file names to produce the
    ///     absolute encoder/decoder/joiner/tokens paths the engine requires.
    /// </param>
    /// <returns>
    ///     A loaded <see cref="IRecognitionBackend"/> ready to accept samples for this model.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="installedModelDirectory"/> is null or empty.
    /// </exception>
    /// <remarks>
    ///     This member allocates real native inference resources: constructing the native
    ///     recognizer is now the model's own responsibility, so a model whose files are missing or
    ///     whose native runtime is absent throws here, and the recognition subsystem degrades to
    ///     an unavailable recognizer by catching that failure. Implementations must be safe to call
    ///     concurrently.
    /// </remarks>
    IRecognitionBackend CreateBackend(string installedModelDirectory);

    /// <summary>
    ///     Builds a loaded, model-specific <see cref="IRecognitionBackend"/> for this model,
    ///     resolved against the directory its verified files were installed into and an optional
    ///     session-level parameter value bag. The default implementation ignores
    ///     <paramref name="parameterValues"/> entirely and forwards to the single-argument
    ///     <see cref="CreateBackend(string)"/> overload, identical to every existing model's
    ///     current parameter-less behavior.
    /// </summary>
    /// <param name="installedModelDirectory">
    ///     The absolute path of the directory holding this model's installed files (the store's
    ///     <c>current/</c> directory for this model). Must not be null or empty; the
    ///     implementation combines it with its own known relative file names to produce the
    ///     absolute encoder/decoder/joiner/tokens paths the engine requires.
    /// </param>
    /// <param name="parameterValues">
    ///     The untyped key-value bag supplied to <c>SpeechRecognizerFactory.LoadAsync</c> (for
    ///     example built from a host's settings UI via a declared <see cref="ISpeechModel.Parameters"/>
    ///     entry), or <see langword="null"/> when the caller supplied none.
    /// </param>
    /// <returns>
    ///     A loaded <see cref="IRecognitionBackend"/> ready to accept samples for this model.
    /// </returns>
    /// <remarks>
    ///     Added so a future tunable recognition model (for example, one offering language
    ///     selection, beam width, or noise-suppression strength) can own its own parameter
    ///     interpretation entirely inside its own backing class, mirroring
    ///     <see cref="ISynthesisModel.ResolveSpeakerId"/>'s and
    ///     <see cref="ISynthesisModel.CapabilityProfile"/>'s "generically correct for free,
    ///     override only for bespoke per-model behavior" default-hook pattern. Neither of today's
    ///     two sibling-package recognition models (the Zipformer and Nemotron streaming models
    ///     supplied by <c>DemaConsulting.Speech.Sherpa</c>) declares any
    ///     <see cref="ISpeechModel.Parameters"/> entry, so both need zero code to keep today's
    ///     exact behavior through this default hook.
    /// </remarks>
    IRecognitionBackend CreateBackend(
        string installedModelDirectory,
        IReadOnlyDictionary<string, object>? parameterValues) =>
        CreateBackend(installedModelDirectory);

    /// <summary>
    ///     Gets the duration, in milliseconds, of pre-endpoint audio the recognition engine
    ///     should buffer and silently replay into a freshly reset stream immediately after an
    ///     endpoint fires, to pre-warm the model's internal decoding state before genuinely new
    ///     (post-pause) audio arrives. Defaults to <c>0</c>, meaning the feature is disabled and
    ///     the engine behaves exactly as it always has: a hard <c>Reset()</c> with no replay.
    /// </summary>
    /// <remarks>
    ///     This member exists to fix a real, confirmed defect specific to the sibling
    ///     <c>DemaConsulting.Speech.Sherpa</c> package's Nemotron streaming recognition model: its
    ///     streaming encoder has a measured ~550ms "cold" warm-up blackout immediately after
    ///     <c>Reset()</c>, during which genuinely spoken audio arriving in that window can be
    ///     silently lost, and its endpoint detector was observed to fire as a false positive
    ///     mid-utterance more often than the sibling Zipformer model's, making the defect
    ///     reproducible on real recordings (see
    ///     <c>.agent-logs/planning-nemotron-endpoint-word-loss-warmup-replay-fix-9d4b71.md</c>
    ///     for the full evidence). Replaying a rolling buffer of the audio immediately preceding
    ///     the endpoint - with its recognized text always suppressed, never surfaced as a
    ///     <see cref="RecognitionSubsystem.SpeechRecognitionResult"/> - pre-warms that same
    ///     encoder state before live audio resumes, without requiring any change to endpoint
    ///     sensitivity.
    ///     <para>
    ///     <b>Deliberately opt-in, not a global default.</b> The same investigation found that
    ///     forcing this feature on for the sibling Zipformer model produced a genuine regression -
    ///     duplicated text (for example, "THAT THAT IS THE QUESTION") - because a replayed word's
    ///     still-forming onset can cause the underlying transducer decoder to commit a token
    ///     during the "silently suppressed" replay, which the model then treats as a distinct word
    ///     when the same content's genuine continuation arrives live afterward. Suppressing
    ///     <c>GetResult()</c> output during replay hides the text from callers but cannot prevent
    ///     that internal token commit. Because no model benefits from this risk unless it has an
    ///     independently confirmed word-loss defect to offset it, every model other than the one
    ///     that opts in must keep today's exact behavior - hence the safe, zero-cost default of
    ///     <c>0</c> here rather than a library-wide constant.
    ///     </para>
    /// </remarks>
    int PostEndpointWarmupWindowMs => 0;

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
