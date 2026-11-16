using System.Text;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     The default, generically-correct <see cref="IModelCapabilityProfile"/> every synthesis
///     model gets for free, driven purely by its declared
///     <see cref="ISpeechModel.AudioTagSupport"/> and <see cref="ISpeechModel.Parameters"/>.
/// </summary>
/// <remarks>
///     For each tag encountered: a <see cref="NaturalLanguageAudioTagKind.Pause"/> tag always
///     renders as real inserted silence, regardless of declared support
///     ("Pauses always render as real inserted silence, since that requires no model
///     cooperation"). Otherwise, <see cref="SpeechModelAudioTagSupport.Native"/> passes the tag
///     through verbatim as canonical bracket text inline with the surrounding words (the model
///     was trained to understand it); <see cref="SpeechModelAudioTagSupport.ParameterMapped"/>
///     maps a recognized pace or delivery-volume tag onto one of the model's own declared numeric
///     parameters for the segment it precedes, via the conservative, built-in conventions in
///     <see cref="SpeechParameterConventions"/>, and silently strips any tag with no recognized
///     convention or no matching declared parameter; <see cref="SpeechModelAudioTagSupport.None"/>
///     always strips the tag. Stripping never removes the plain words themselves, so a reply is
///     never worse than plain narration regardless of what a given model can honor. Long runs of
///     plain text are split into playback-sized segments by <see cref="SentenceChunker"/>; a
///     segment whose chunk ends in a genuine ellipsis (per
///     <see cref="SentenceChunk.EndsWithEllipsis"/>) renders a longer, distinct
///     <see cref="EllipsisPauseMilliseconds"/> pause, separate from the tag-triggered
///     <see cref="NaturalLanguageAudioTag.ShortPause"/>/<see cref="NaturalLanguageAudioTag.LongPause"/>
///     mechanism above; every other chunk boundary still adds zero silence.
///     <para>
///     The type is stateless, so the shared <see cref="Instance"/> is safe to reuse for every
///     model and safe to share across threads. It is public so a model's own
///     <see cref="IModelCapabilityProfile"/> can wrap or delegate to it, and so the chunk-then-render
///     step (<see cref="RenderText"/>) and the pause durations are reusable instead of copied.
///     </para>
///     <para>
///     Plain-text spans may carry text-analysis <see cref="TaggedTextSpan.Annotations"/> and
///     <see cref="TaggedTextSpan.SentenceBreaks"/>. Consecutive plain text (and any passed-through
///     native tag text) is buffered into one string, so each span's offsets are shifted by the
///     text already buffered before it - this is what re-bases them across stripped and native
///     tags - and the annotations then travel with the segment that contains them. Pause and
///     parameter-mapped tags flush the buffer first, so an annotation never crosses a segment
///     boundary that carries different overrides. Authoritative analyzer sentence breaks replace
///     the punctuation heuristic for the buffered text. A tag span may also carry a break
///     (<c>0</c> before, <c>1</c> after the tag): it is recorded at the buffer's length at that
///     moment, so a break between consecutive tags lands at the same place whether the tags are
///     stripped, passed through as native text, or already flushed as segment boundaries. With no
///     annotations and no breaks the output is identical to the output before annotations existed.
///     </para>
/// </remarks>
public sealed class DefaultModelCapabilityProfile : IModelCapabilityProfile
{
    /// <summary>The real silence, in milliseconds, a <see cref="NaturalLanguageAudioTag.ShortPause"/> renders as.</summary>
    public const int ShortPauseMilliseconds = 300;

    /// <summary>The real silence, in milliseconds, a <see cref="NaturalLanguageAudioTag.LongPause"/> renders as.</summary>
    public const int LongPauseMilliseconds = 900;

    /// <summary>
    ///     The real silence, in milliseconds, appended after a plain-text chunk whose text ends
    ///     in a genuine ellipsis (see <see cref="SentenceChunk.EndsWithEllipsis"/>).
    ///     This models the natural, longer trailing-off pause a speaker takes at a genuine
    ///     ellipsis, and is a distinct, chunk-boundary-triggered mechanism from the explicit
    ///     <see cref="NaturalLanguageAudioTag.ShortPause"/>/<see cref="NaturalLanguageAudioTag.LongPause"/>
    ///     Natural Language Audio Tag path above: it is driven purely by the shape of the text
    ///     <see cref="SentenceChunker"/> already chunked, not by any explicit
    ///     <c>[pause]</c>/<c>[long-pause]</c> tag, so it deliberately does not reuse
    ///     <see cref="ShortPauseMilliseconds"/> or <see cref="LongPauseMilliseconds"/>. Chosen
    ///     between those two values (300ms/900ms) as a middle-ground pause appropriate for a
    ///     trailing-off ellipsis rather than a full deliberate pause.
    /// </summary>
    public const int EllipsisPauseMilliseconds = 500;

    /// <summary>
    ///     The conservative pace/volume tag mapping table: for each recognized tag, the parameter
    ///     naming convention to search for and the fraction of the parameter's declared range to
    ///     shift its default value by. Tags absent from this table (all emotion, non-verbal, and
    ///     emphasis tags, plus <see cref="NaturalLanguageAudioTag.Breathy"/>) have no built-in
    ///     numeric convention and are always silently stripped under
    ///     <see cref="SpeechModelAudioTagSupport.ParameterMapped"/>.
    /// </summary>
    private static readonly Dictionary<NaturalLanguageAudioTag, (IReadOnlyList<string> Keywords, double Fraction)> TagParameterRules =
        new Dictionary<NaturalLanguageAudioTag, (IReadOnlyList<string>, double)>
        {
            [NaturalLanguageAudioTag.Fast] = (SpeechParameterConventions.SpeedKeywords, 0.15),
            [NaturalLanguageAudioTag.VeryFast] = (SpeechParameterConventions.SpeedKeywords, 0.30),
            [NaturalLanguageAudioTag.Slow] = (SpeechParameterConventions.SpeedKeywords, -0.15),
            [NaturalLanguageAudioTag.VerySlow] = (SpeechParameterConventions.SpeedKeywords, -0.30),
            [NaturalLanguageAudioTag.Loud] = (SpeechParameterConventions.VolumeKeywords, 0.20),
            [NaturalLanguageAudioTag.Soft] = (SpeechParameterConventions.VolumeKeywords, -0.20),
            [NaturalLanguageAudioTag.Whispers] = (SpeechParameterConventions.VolumeKeywords, -0.35),
        };

    /// <summary>
    ///     Gets the single shared default capability profile.
    /// </summary>
    public static DefaultModelCapabilityProfile Instance { get; } = new();

    /// <summary>
    ///     Prevents external construction; callers use the shared <see cref="Instance"/> instead
    ///     since the type carries no state and multiple instances would provide no value.
    /// </summary>
    private DefaultModelCapabilityProfile()
    {
    }

    /// <summary>
    ///     Chunks plain text with <see cref="SentenceChunker"/> and renders each chunk as a
    ///     <see cref="SpeechSegment"/>: the reusable "chunk-then-render" building block a model's
    ///     own profile can call after handling its tags itself.
    /// </summary>
    /// <param name="text">The plain text to chunk and render. Must not be <see langword="null"/>.</param>
    /// <param name="annotations">
    ///     Annotations relative to <paramref name="text"/>, or <see langword="null"/> for none;
    ///     each segment receives the ones wholly inside its chunk, re-based to the chunk text.
    /// </param>
    /// <param name="sentenceBreaks">
    ///     Authoritative sentence breaks relative to <paramref name="text"/>, or
    ///     <see langword="null"/> to use the punctuation heuristic; see
    ///     <see cref="SentenceChunker.ChunkAligned"/>.
    /// </param>
    /// <param name="parameterOverrides">
    ///     The parameter overrides every produced segment carries, or <see langword="null"/>.
    /// </param>
    /// <returns>
    ///     The ordered segments; a chunk ending in a genuine ellipsis gets
    ///     <see cref="EllipsisPauseMilliseconds"/> of trailing silence. Empty when the text is
    ///     empty or all whitespace.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public static IReadOnlyList<SpeechSegment> RenderText(
        string text,
        IReadOnlyList<SegmentAnnotation>? annotations = null,
        IReadOnlyList<int>? sentenceBreaks = null,
        IReadOnlyDictionary<string, object>? parameterOverrides = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        var chunks = SentenceChunker.ChunkAligned(text, annotations, sentenceBreaks);
        var segments = new List<SpeechSegment>(chunks.Count);
        foreach (var chunk in chunks)
        {
            var postSilenceMs = chunk.EndsWithEllipsis ? EllipsisPauseMilliseconds : 0;
            segments.Add(new SpeechSegment(chunk.Text, 0, postSilenceMs, parameterOverrides)
            {
                Annotations = chunk.Annotations,
            });
        }

        return segments;
    }

    /// <inheritdoc/>
    public SpeechPlan Render(IReadOnlyList<TaggedTextSpan> spans, ISpeechModel model)
    {
        ArgumentNullException.ThrowIfNull(spans);
        ArgumentNullException.ThrowIfNull(model);

        var segments = new List<SpeechSegment>();
        var buffer = new StringBuilder();
        var bufferAnnotations = new List<SegmentAnnotation>();
        var bufferBreaks = new List<int>();
        var breaksAuthoritative = false;
        Dictionary<string, object>? pendingOverrides = null;

        void Flush()
        {
            if (buffer.Length == 0)
            {
                // An empty plain span can still have recorded breaks or annotations; drop them
                // so they cannot leak into the next non-empty segment's analysis.
                pendingOverrides = null;
                bufferAnnotations.Clear();
                bufferBreaks.Clear();
                breaksAuthoritative = false;
                return;
            }

            var text = buffer.ToString();
            buffer.Clear();
            var overrides = pendingOverrides;
            pendingOverrides = null;

            segments.AddRange(RenderText(
                text,
                bufferAnnotations.ToArray(),
                breaksAuthoritative ? bufferBreaks.ToArray() : null,
                overrides));
            bufferAnnotations.Clear();
            bufferBreaks.Clear();
            breaksAuthoritative = false;
        }

        // Records a sentence break a tag span carries at the buffer's current end: offset 0 is
        // processed immediately before the tag contributes and offset 1 immediately after, so the
        // break lands exactly where the analyzer put it in every tag-support mode. An empty
        // buffer has no sentence to break, and a pause or parameter flush already ends a segment.
        void AddTagBreak(TaggedTextSpan tagSpan, int tagOffset)
        {
            if (tagSpan.SentenceBreaks is not null && tagSpan.SentenceBreaks.Contains(tagOffset) && buffer.Length > 0)
            {
                breaksAuthoritative = true;
                bufferBreaks.Add(buffer.Length);
            }
        }

        foreach (var span in spans)
        {
            if (span.Kind == TaggedTextSpanKind.PlainText)
            {
                // An empty span contributes no text, so whatever analysis it carries (breaks at
                // offset 0, empty annotations) must not mark the buffer authoritative for the
                // text that follows.
                if (span.Text.Length == 0)
                {
                    continue;
                }

                // Span-local offsets shift by whatever is already buffered, which also accounts
                // for stripped tags (nothing buffered) and native tags (their text is buffered).
                var offset = buffer.Length;
                buffer.Append(span.Text);
                foreach (var annotation in span.Annotations)
                {
                    if (annotation.End <= span.Text.Length)
                    {
                        bufferAnnotations.Add(annotation.Shifted(offset));
                    }
                }

                if (span.SentenceBreaks is not null)
                {
                    breaksAuthoritative = true;
                    foreach (var sentenceBreak in span.SentenceBreaks)
                    {
                        if (sentenceBreak <= span.Text.Length)
                        {
                            bufferBreaks.Add(offset + sentenceBreak);
                        }
                    }
                }

                continue;
            }

            var tag = span.Tag!.Value;
            var kind = ResolveKind(tag);
            AddTagBreak(span, 0);
            if (kind == NaturalLanguageAudioTagKind.Pause)
            {
                // The flush ends the segment, so a break after a pause tag is already a boundary.
                Flush();
                var durationMs = tag == NaturalLanguageAudioTag.LongPause ? LongPauseMilliseconds : ShortPauseMilliseconds;
                segments.Add(new SpeechSegment(string.Empty, 0, durationMs, null));
                continue;
            }

            switch (model.AudioTagSupport)
            {
                case SpeechModelAudioTagSupport.Native:
                    buffer.Append(CanonicalBracketText(tag));
                    break;

                case SpeechModelAudioTagSupport.ParameterMapped:
                    // Text before the tag must keep its own overrides, so emit it first; the tag's
                    // override then applies only to the text that follows.
                    if (buffer.Length > 0)
                    {
                        Flush();
                    }

                    ApplyParameterMapping(tag, model, ref pendingOverrides);
                    break;

                case SpeechModelAudioTagSupport.None:
                default:
                    // Stripped: the plain words that follow are still spoken, per the
                    // "never worse than plain narration" guarantee.
                    break;
            }

            AddTagBreak(span, 1);
        }

        Flush();

        return new SpeechPlan(segments);
    }

    /// <summary>
    ///     Resolves the kind of a canonical tag by looking it up in
    ///     <see cref="AudioTagCatalog.Tags"/>.
    /// </summary>
    /// <param name="tag">The canonical tag to resolve.</param>
    /// <returns>The tag's kind.</returns>
    private static NaturalLanguageAudioTagKind ResolveKind(NaturalLanguageAudioTag tag) =>
        AudioTagCatalog.Tags.First(descriptor => descriptor.Tag == tag).Kind;

    /// <summary>
    ///     Reconstructs the canonical bracket text for a tag (its first declared alias), for
    ///     passthrough under <see cref="SpeechModelAudioTagSupport.Native"/>.
    /// </summary>
    /// <param name="tag">The canonical tag to reconstruct bracket text for.</param>
    /// <returns>The bracket text, for example <c>"[excited]"</c>.</returns>
    private static string CanonicalBracketText(NaturalLanguageAudioTag tag)
    {
        var descriptor = AudioTagCatalog.Tags.First(d => d.Tag == tag);
        return $"[{descriptor.Aliases[0]}]";
    }

    /// <summary>
    ///     Maps a recognized pace/volume tag onto one of the model's own declared numeric
    ///     parameters, accumulating the override for the next flushed segment; silently does
    ///     nothing when the tag has no recognized convention or the model declares no matching
    ///     parameter.
    /// </summary>
    /// <param name="tag">The tag to map.</param>
    /// <param name="model">The model whose declared parameters are searched.</param>
    /// <param name="pendingOverrides">
    ///     The overrides accumulating for the next flushed segment, created on first use.
    /// </param>
    private static void ApplyParameterMapping(
        NaturalLanguageAudioTag tag,
        ISpeechModel model,
        ref Dictionary<string, object>? pendingOverrides)
    {
        if (!TagParameterRules.TryGetValue(tag, out var rule))
        {
            return;
        }

        var parameter = model.Parameters
            .OfType<ModelManagementSubsystem.NumericParameter>()
            .FirstOrDefault(candidate => rule.Keywords.Any(
                keyword => candidate.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
        if (parameter is null)
        {
            return;
        }

        var value = Math.Clamp(
            parameter.Default + (rule.Fraction * (parameter.Maximum - parameter.Minimum)),
            parameter.Minimum,
            parameter.Maximum);

        pendingOverrides ??= [];
        pendingOverrides[parameter.Id] = value;
    }
}
