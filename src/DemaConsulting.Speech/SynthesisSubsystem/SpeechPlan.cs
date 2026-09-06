namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Layer 2's rendering output: an ordered set of synthesis calls
///     (<see cref="SpeechSegment"/>) plus the real inserted silence between them, produced by an
///     <see cref="IModelCapabilityProfile"/> from a Layer 1 parsed span sequence.
/// </summary>
/// <param name="Segments">
///     The ordered segments making up the plan. Never null; may be empty when the source text
///     was empty or resolved to nothing.
/// </param>
/// <remarks>
///     Per architecture.md's "chunked, low-latency streaming synthesis and playback" decision,
///     each segment is synthesized and played back independently and in order, with playback of
///     an earlier segment beginning while later segments are still being synthesized. This record
///     is immutable and safe to share across threads.
/// </remarks>
internal sealed record SpeechPlan(IReadOnlyList<SpeechSegment> Segments);
