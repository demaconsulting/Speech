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
///     renders as real inserted silence, regardless of declared support, per architecture.md
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
///     plain text are split into playback-sized segments by <see cref="SentenceChunker"/>.
///     <para>
///     The type is stateless, so the shared <see cref="Instance"/> is safe to reuse for every
///     model and safe to share across threads.
///     </para>
/// </remarks>
internal sealed class DefaultModelCapabilityProfile : IModelCapabilityProfile
{
    /// <summary>The real silence, in milliseconds, a <see cref="NaturalLanguageAudioTag.ShortPause"/> renders as.</summary>
    private const int ShortPauseMilliseconds = 300;

    /// <summary>The real silence, in milliseconds, a <see cref="NaturalLanguageAudioTag.LongPause"/> renders as.</summary>
    private const int LongPauseMilliseconds = 900;

    /// <summary>
    ///     The conservative pace/volume tag mapping table: for each recognized tag, the parameter
    ///     naming convention to search for and the fraction of the parameter's declared range to
    ///     shift its default value by. Tags absent from this table (all emotion, non-verbal, and
    ///     emphasis tags, plus <see cref="NaturalLanguageAudioTag.Breathy"/>) have no built-in
    ///     numeric convention and are always silently stripped under
    ///     <see cref="SpeechModelAudioTagSupport.ParameterMapped"/>.
    /// </summary>
    private static readonly IReadOnlyDictionary<NaturalLanguageAudioTag, (IReadOnlyList<string> Keywords, double Fraction)> TagParameterRules =
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

    /// <inheritdoc/>
    public SpeechPlan Render(IReadOnlyList<TaggedTextSpan> spans, ISpeechModel model)
    {
        ArgumentNullException.ThrowIfNull(spans);
        ArgumentNullException.ThrowIfNull(model);

        var segments = new List<SpeechSegment>();
        var buffer = new StringBuilder();
        Dictionary<string, object>? pendingOverrides = null;

        void Flush()
        {
            if (buffer.Length == 0)
            {
                pendingOverrides = null;
                return;
            }

            var text = buffer.ToString();
            buffer.Clear();
            var overrides = pendingOverrides;
            pendingOverrides = null;

            var chunks = SentenceChunker.Chunk(text);
            foreach (var chunk in chunks)
            {
                segments.Add(new SpeechSegment(chunk, 0, 0, overrides));
            }
        }

        foreach (var span in spans)
        {
            if (span.Kind == TaggedTextSpanKind.PlainText)
            {
                buffer.Append(span.Text);
                continue;
            }

            var tag = span.Tag!.Value;
            var kind = ResolveKind(tag);
            if (kind == NaturalLanguageAudioTagKind.Pause)
            {
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
                    ApplyParameterMapping(tag, model, ref pendingOverrides);
                    break;

                case SpeechModelAudioTagSupport.None:
                default:
                    // Stripped: the plain words that follow are still spoken, per the
                    // "never worse than plain narration" guarantee.
                    break;
            }
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
