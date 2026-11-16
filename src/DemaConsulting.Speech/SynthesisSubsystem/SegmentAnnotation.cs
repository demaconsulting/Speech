namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     A language-neutral, opaque annotation over a half-open range of a text: <c>Length</c>
///     UTF-16 code units starting at <c>Start</c>, tagged with a model-defined
///     <c>Kind</c> and <c>Value</c>.
/// </summary>
/// <param name="Start">
///     The zero-based UTF-16 code unit index of the first annotated character, relative to the
///     text of the container the annotation belongs to (a <see cref="SpeechSegment"/>'s
///     <see cref="SpeechSegment.Text"/>, a <see cref="TaggedTextSpan"/>'s text, or the analyzed
///     text handed to <see cref="ITextAnalyzer"/>). Must not be negative.
/// </param>
/// <param name="Length">
///     The number of UTF-16 code units the annotation covers. Must be greater than zero, and
///     <paramref name="Start"/> plus <paramref name="Length"/> must not overflow
///     <see cref="int"/>.
/// </param>
/// <param name="Kind">
///     An opaque, case-sensitive (ordinal) annotation kind chosen by whoever produces the
///     annotation (for example <c>"reading"</c> or <c>"pos"</c>). The core library attaches no
///     meaning to it. Must not be null, empty or whitespace.
/// </param>
/// <param name="Value">
///     The opaque annotation payload for <paramref name="Kind"/>. Must not be null; may be empty.
/// </param>
/// <remarks>
///     The core library is language-neutral on purpose: it carries annotations from the text
///     analysis step to the backend that can use them and re-bases their offsets as the text is
///     split, but never interprets <c>Kind</c> or <c>Value</c>. Consumers MUST ignore kinds they
///     do not recognize. Annotations should begin and end on non-whitespace characters, because
///     chunking trims whitespace and drops an annotation that starts or ends on a trimmed
///     character. Like <see cref="SpeechSegment"/>, validation also runs for <c>with</c>
///     expressions. The record is immutable and therefore safe to share across threads.
/// </remarks>
/// <exception cref="ArgumentOutOfRangeException">
///     <paramref name="Start"/> is negative, <paramref name="Length"/> is not positive, or the
///     range end overflows <see cref="int"/>.
/// </exception>
/// <exception cref="ArgumentNullException">
///     <paramref name="Kind"/> or <paramref name="Value"/> is null.
/// </exception>
/// <exception cref="ArgumentException"><paramref name="Kind"/> is empty or whitespace.</exception>
public sealed record SegmentAnnotation(int Start, int Length, string Kind, string Value)
{
    /// <summary>Backing field for <see cref="Start"/>.</summary>
    private readonly int _start = RequireStart(Start);

    /// <summary>Backing field for <see cref="Length"/>.</summary>
    private readonly int _length = RequireLength(Length, Start);

    /// <summary>Backing field for <see cref="Kind"/>.</summary>
    private readonly string _kind = RequireKind(Kind);

    /// <summary>Backing field for <see cref="Value"/>.</summary>
    private readonly string _value = RequireValue(Value);

    /// <summary>
    ///     Gets the zero-based UTF-16 code unit index of the first annotated character.
    /// </summary>
    public int Start
    {
        get => _start;
        init
        {
            _start = RequireStart(value);
            RequireRangeFits(_start, _length);
        }
    }

    /// <summary>
    ///     Gets the number of UTF-16 code units the annotation covers; always positive.
    /// </summary>
    public int Length
    {
        get => _length;
        init => _length = RequireLength(value, _start);
    }

    /// <summary>
    ///     Gets the opaque, case-sensitive annotation kind; never null, empty or whitespace.
    /// </summary>
    public string Kind
    {
        get => _kind;
        init => _kind = RequireKind(value);
    }

    /// <summary>
    ///     Gets the opaque annotation payload; never null, may be empty.
    /// </summary>
    public string Value
    {
        get => _value;
        init => _value = RequireValue(value);
    }

    /// <summary>
    ///     Gets the exclusive end index: <see cref="Start"/> plus <see cref="Length"/>.
    /// </summary>
    public int End => _start + _length;

    /// <summary>
    ///     Returns a copy of this annotation moved by <paramref name="delta"/> code units.
    /// </summary>
    /// <param name="delta">
    ///     The signed shift to add to <see cref="Start"/>. The caller guarantees the result stays
    ///     non-negative and within range.
    /// </param>
    /// <returns>The shifted annotation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The shifted start would be negative or overflow.</exception>
    internal SegmentAnnotation Shifted(int delta) => this with { Start = _start + delta };

    /// <summary>
    ///     Rejects a negative start.
    /// </summary>
    /// <param name="start">The candidate start.</param>
    /// <returns>The validated start.</returns>
    private static int RequireStart(int start)
    {
        if (start < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Start), start, "Start must not be negative.");
        }

        return start;
    }

    /// <summary>
    ///     Rejects a non-positive length, or a length whose range end would overflow.
    /// </summary>
    /// <param name="length">The candidate length.</param>
    /// <param name="start">The start the length is paired with.</param>
    /// <returns>The validated length.</returns>
    private static int RequireLength(int length, int start)
    {
        if (length <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Length), length, "Length must be greater than zero.");
        }

        RequireRangeFits(start, length);
        return length;
    }

    /// <summary>
    ///     Rejects a start/length pair whose exclusive end would exceed <see cref="int.MaxValue"/>.
    /// </summary>
    /// <param name="start">The start.</param>
    /// <param name="length">The length.</param>
    private static void RequireRangeFits(int start, int length)
    {
        if (length > int.MaxValue - start)
        {
            throw new ArgumentOutOfRangeException(nameof(Length), length, "The annotation range end must not overflow.");
        }
    }

    /// <summary>
    ///     Rejects a null, empty or whitespace kind.
    /// </summary>
    /// <param name="kind">The candidate kind.</param>
    /// <returns>The validated kind.</returns>
    private static string RequireKind(string kind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        return kind;
    }

    /// <summary>
    ///     Rejects a null value.
    /// </summary>
    /// <param name="value">The candidate value.</param>
    /// <returns>The validated value.</returns>
    private static string RequireValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value;
    }
}
