namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     An <see cref="ISynthesisBackend"/> that can additionally use the
///     <see cref="SegmentAnnotation"/>s a text analysis produced for a segment.
/// </summary>
/// <remarks>
///     A separate derived interface (rather than a default member on
///     <see cref="ISynthesisBackend"/>) keeps every existing backend, hand-written fake and mock
///     unchanged: a backend that does not implement this interface never sees annotations. The
///     session calls <see cref="GenerateAnnotated"/> instead of <see cref="ISynthesisBackend.Generate"/>
///     only for a segment that carries at least one annotation. Threading and disposal rules
///     are inherited from <see cref="ISynthesisBackend"/>.
/// </remarks>
public interface IAnnotatedSynthesisBackend : ISynthesisBackend
{
    /// <summary>
    ///     Synthesizes one segment of text, given the annotations that apply to it.
    /// </summary>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="annotations">
    ///     The segment's annotations: non-null, non-empty, each within <c>[0, text.Length]</c>,
    ///     with offsets relative to <paramref name="text"/>. An implementation MUST ignore kinds
    ///     it does not recognize and MUST still produce audio for <paramref name="text"/> even
    ///     when it ignores every annotation (equivalent to <see cref="ISynthesisBackend.Generate"/>).
    /// </param>
    /// <param name="speed">
    ///     The speaking-rate multiplier for this call, where <c>1.0</c> is the model's normal
    ///     rate. Must be greater than zero.
    /// </param>
    /// <param name="speakerId">The speaker/voice id to use, for a multi-speaker model.</param>
    /// <returns>The synthesized audio for the segment.</returns>
    /// <exception cref="ObjectDisposedException">Thrown when the backend has been disposed.</exception>
    BackendAudio GenerateAnnotated(string text, IReadOnlyList<SegmentAnnotation> annotations, float speed, int speakerId);
}
