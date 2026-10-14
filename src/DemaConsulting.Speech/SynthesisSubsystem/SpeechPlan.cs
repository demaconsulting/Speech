namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Layer 2's rendering output: an ordered set of synthesis calls
///     (<see cref="SpeechSegment"/>) plus the real inserted silence between them, produced by an
///     <see cref="IModelCapabilityProfile"/> from a Layer 1 parsed span sequence.
/// </summary>
/// <param name="Segments">
///     The ordered segments making up the plan. Never null and never containing a null element
///     (both are rejected on construction); may be empty when the source text was empty or
///     resolved to nothing. The plan keeps its own copy, so later changes to the caller's list
///     do not affect it.
/// </param>
/// <remarks>
///     Per this library's "chunked, low-latency streaming synthesis and playback" decision,
///     each segment is synthesized and played back independently and in order, with playback of
///     an earlier segment beginning while later segments are still being synthesized. This record
///     is immutable and safe to share across threads. Public, mirroring
///     <see cref="IModelCapabilityProfile"/>'s public visibility, since a third-party
///     <see cref="IModelCapabilityProfile"/> implementation must be able to return one.
/// </remarks>
/// <exception cref="ArgumentNullException"><paramref name="Segments"/> is null.</exception>
/// <exception cref="ArgumentException"><paramref name="Segments"/> contains a null element.</exception>
public sealed record SpeechPlan(IReadOnlyList<SpeechSegment> Segments)
{
    private readonly IReadOnlyList<SpeechSegment> _segments = ValidateSegments(Segments);

    /// <summary>
    ///     Gets the ordered segments making up the plan.
    /// </summary>
    public IReadOnlyList<SpeechSegment> Segments
    {
        get => _segments;
        init => _segments = ValidateSegments(value);
    }

    /// <summary>
    ///     Validates the segments and returns a private copy.
    /// </summary>
    /// <param name="segments">The candidate segments.</param>
    /// <returns>A private, read-only copy of the segments.</returns>
    private static IReadOnlyList<SpeechSegment> ValidateSegments(IReadOnlyList<SpeechSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var copy = segments.ToArray();
        if (Array.IndexOf(copy, null) >= 0)
        {
            throw new ArgumentException("Segments must not contain a null element.", nameof(segments));
        }

        return Array.AsReadOnly(copy);
    }
}
