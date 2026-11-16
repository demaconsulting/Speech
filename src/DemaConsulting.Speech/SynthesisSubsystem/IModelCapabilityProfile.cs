using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Seam for Layer 2 of this library's "two-layer tag rendering" design: renders a Layer 1
///     parsed span sequence into a per-model <see cref="SpeechPlan"/>.
/// </summary>
/// <remarks>
///     Generalizes the reference implementation's <c>SpeechAudioHint</c> →
///     <c>IModelCapabilityProfile</c> → <c>SpeechPlan</c> pattern. A model gets a generically
///     correct implementation for free from <see cref="DefaultModelCapabilityProfile"/>, driven
///     entirely by its own declared <see cref="ISpeechModel.AudioTagSupport"/> and
///     <see cref="ISpeechModel.Parameters"/>, and may supply a bespoke implementation via
///     <see cref="ISynthesisModel.CapabilityProfile"/> only when it needs non-generic rendering.
///     This seam is public, mirroring <see cref="ISynthesisModel.CapabilityProfile"/>'s public
///     visibility: a third-party synthesis model needing bespoke tag rendering must be able to
///     implement this interface without needing any special assembly access.
///     <para>
///     The spans handed to <see cref="Render"/> may carry text-analysis
///     <see cref="TaggedTextSpan.Annotations"/> and <see cref="TaggedTextSpan.SentenceBreaks"/>.
///     A bespoke profile may ignore them, or copy them onto the segments it produces (the public
///     <see cref="DefaultModelCapabilityProfile"/> and <see cref="SentenceChunker"/> building
///     blocks re-base them for it). <see cref="TaggedTextSpan.SentenceBreaks"/> may also appear on
///     a tag span (a break adjacent to the tag, <c>0</c> before or <c>1</c> after it); a profile
///     that ignores them only loses those breaks and never faults.
///     </para>
/// </remarks>
public interface IModelCapabilityProfile
{
    /// <summary>
    ///     Renders a Layer 1 parsed span sequence into an ordered <see cref="SpeechPlan"/> for one
    ///     model.
    /// </summary>
    /// <param name="spans">
    ///     The ordered literal-text and recognized-tag spans produced by
    ///     <see cref="AudioTagParser.Parse"/>. Must not be null.
    /// </param>
    /// <param name="model">
    ///     The model to render for, whose declared <see cref="ISpeechModel.AudioTagSupport"/> and
    ///     <see cref="ISpeechModel.Parameters"/> determine how each tag is realized. Must not be
    ///     null.
    /// </param>
    /// <returns>
    ///     A <see cref="SpeechPlan"/> whose segments, played back in order with their declared
    ///     silence, reconstruct an expressive rendering of <paramref name="spans"/> that is never
    ///     worse than plain narration of the underlying words.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="spans"/> or <paramref name="model"/> is null.
    /// </exception>
    SpeechPlan Render(IReadOnlyList<TaggedTextSpan> spans, ISpeechModel model);
}
