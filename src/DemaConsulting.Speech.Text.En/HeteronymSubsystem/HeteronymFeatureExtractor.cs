namespace DemaConsulting.Speech.Text.En.HeteronymSubsystem;

/// <summary>
///     Turns one tagged sentence and a target word into the hashed features the heteronym model
///     scores, deterministically and without allocating.
/// </summary>
/// <remarks>
///     <para>
///     <b>Feature set 1.</b> The templates are the part-of-speech-assisted set measured in the
///     heteronym spike: the bias, the target's form, suffixes and shape; the words at offsets -3 to
///     +3, their bigrams and the form around the target; the shapes and suffixes of the nearest
///     neighbors; a bag of content words within eight tokens; the closed classes around the target
///     and the distance to the nearest determiner, modal, <c>to</c>, auxiliary and negation on its
///     left; the position in the sentence and the punctuation next to the target; and the target's
///     and its neighbors' part of speech. A feature is <i>conjoined</i> (scored through the label salt of
///     one specific reading, so the weight belongs to that reading of that word), <i>shared</i>
///     (scored through the role salt, so every lemma with a <c>noun</c> reading shares the
///     evidence) or both. Differences from the spike: the context is the target's own sentence (positions
///     beyond it are the markers <c>&lt;S&gt;</c> and <c>&lt;/S&gt;</c>, class 255, tag 0), and no separate
///     homograph hash is mixed into a conjoined feature, because the reading's label salt already
///     starts with the lemma.
///     </para>
///     <para>
///     <b>Output.</b> The extractor writes feature hashes, not buckets: the classifier binds each
///     hash to a candidate reading with that reading's salt and masks it to the model's hash bits.
///     The counts never exceed <see cref="MaxConjoined"/> and <see cref="MaxShared"/>; a caller
///     that supplies smaller buffers gets an <see cref="InvalidOperationException"/>, which is a
///     defect, never a data condition.
///     </para>
///     <para>
///     Nothing here depends on the culture, the operating system or the process; the type is
///     static and thread-safe. Any change to a template, a number or a hash must raise
///     <see cref="FeatureSetVersion"/>, which makes every older model unusable.
///     </para>
/// </remarks>
internal static class HeteronymFeatureExtractor
{
    /// <summary>The version of the feature set; a model records it and the loader rejects any other.</summary>
    internal const int FeatureSetVersion = 1;

    /// <summary>The size of the conjoined feature buffer that is always enough.</summary>
    internal const int MaxConjoined = 96;

    /// <summary>The size of the shared feature buffer that is always enough.</summary>
    internal const int MaxShared = 48;

    /// <summary>The farthest offset of the content-word bag.</summary>
    private const int BagRadius = 8;

    /// <summary>The offset base that keeps an offset non-negative inside a hash argument.</summary>
    private const int OffsetBase = 8;

    /// <summary>The farthest distance looked at for the closed-class distance features.</summary>
    private const int DistanceRadius = 3;

    /// <summary>The template numbers; part of the feature-set definition.</summary>
    private const uint TplBias = 0, TplTarget = 1, TplTargetSuffix = 2, TplTargetShape = 3, TplWord = 4, TplPrevBigram = 5,
        TplNextBigram = 6, TplSurround = 7, TplNeighborShape = 8, TplNeighborSuffix2 = 9, TplNeighborSuffix3 = 10,
        TplClass = 11, TplClassPairLR = 12, TplClassPairLL = 13, TplClassPairRR = 14, TplFuncWord = 15, TplDetDist = 16,
        TplModalDist = 17, TplToDist = 18, TplAuxDist = 19, TplNegDist = 20, TplInitial = 21, TplPosIdx = 22,
        TplPunctPrev = 23, TplPunctNext = 24, TplLastWord = 25, TplBag = 26, TplTargetUpos = 27, TplTargetUposOnly = 28,
        TplNeighborUpos = 29, TplUposBigram = 30, TplPrevWordTarget = 31;

    /// <summary>The hash of the sentence-start marker word.</summary>
    private static readonly uint StartMarker = FeatureHashing.Fnv1a("<S>");

    /// <summary>The hash of the sentence-end marker word.</summary>
    private static readonly uint EndMarker = FeatureHashing.Fnv1a("</S>");

    /// <summary>
    ///     Extracts the features of one target word.
    /// </summary>
    /// <param name="sentence">The tokens of the target's sentence in text order, punctuation included.</param>
    /// <param name="target">The index of the target token within <paramref name="sentence"/>.</param>
    /// <param name="conjoined">Receives the conjoined feature hashes; at least <see cref="MaxConjoined"/> long.</param>
    /// <param name="shared">Receives the shared feature hashes; at least <see cref="MaxShared"/> long.</param>
    /// <param name="conjoinedCount">The number of conjoined hashes written.</param>
    /// <param name="sharedCount">The number of shared hashes written.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="target"/> is not inside <paramref name="sentence"/>.</exception>
    /// <exception cref="InvalidOperationException">A buffer is too small for the features (a defect: use the maximum sizes).</exception>
    /// <remarks>Allocation-free and thread-safe; the same input always writes the same hashes in the same order.</remarks>
    internal static void Extract(
        ReadOnlySpan<ContextToken> sentence,
        int target,
        Span<uint> conjoined,
        Span<uint> shared,
        out int conjoinedCount,
        out int sharedCount)
    {
        if ((uint)target >= (uint)sentence.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(target), "The target is not a token of the sentence.");
        }

        var writer = new FeatureWriter(conjoined, shared);
        var t = sentence[target];

        // The target itself: bias, form (which captures inflection), suffixes and shape.
        writer.Conjoined(TplBias, 0, 0);
        writer.Conjoined(TplTarget, t.LowerHash, 0);
        for (uint k = 1; k <= 3; k++)
        {
            if (t.LowerLength >= k)
            {
                writer.Both(TplTargetSuffix, k, SuffixHash(t, k));
            }
        }

        writer.Conjoined(TplTargetShape, t.Shape, 0);

        // The words at -3..+3, the bigrams and the form around the target.
        for (var offset = -3; offset <= 3; offset++)
        {
            if (offset != 0)
            {
                writer.Conjoined(TplWord, (uint)(offset + OffsetBase), WordHash(sentence, target, offset));
            }
        }

        writer.Conjoined(TplPrevBigram, WordHash(sentence, target, -2), WordHash(sentence, target, -1));
        writer.Conjoined(TplNextBigram, WordHash(sentence, target, 1), WordHash(sentence, target, 2));
        writer.Conjoined(TplSurround, WordHash(sentence, target, -1), WordHash(sentence, target, 1));
        writer.Conjoined(TplPrevWordTarget, WordHash(sentence, target, -1), t.LowerHash);

        // The shape and the endings of the nearest neighbors.
        for (var offset = -2; offset <= 2; offset++)
        {
            if (offset == 0)
            {
                continue;
            }

            var found = TryGet(sentence, target, offset, out var neighbor);
            writer.Conjoined(TplNeighborShape, (uint)(offset + OffsetBase), found ? neighbor.Shape : 0u);
            if (found && neighbor.IsWord)
            {
                if (neighbor.LowerLength > 2)
                {
                    writer.Conjoined(TplNeighborSuffix2, (uint)(offset + OffsetBase), neighbor.Suffix2Hash);
                }

                if (neighbor.LowerLength > 3)
                {
                    writer.Conjoined(TplNeighborSuffix3, (uint)(offset + OffsetBase), neighbor.Suffix3Hash);
                }
            }
        }

        // The bag of content words within eight tokens: topical evidence for lexical heteronyms.
        for (var offset = -BagRadius; offset <= BagRadius; offset++)
        {
            if (offset != 0
                && TryGet(sentence, target, offset, out var content)
                && content.IsWord && content.Class == HeteronymContext.ClassNone && content.LowerLength > 2)
            {
                writer.Conjoined(TplBag, content.LowerHash, 0);
            }
        }

        // The closed classes around the target, which the reading's role shares across lemmas.
        for (var offset = -3; offset <= 3; offset++)
        {
            if (offset != 0)
            {
                writer.Both(TplClass, (uint)(offset + OffsetBase), ClassAt(sentence, target, offset));
            }
        }

        writer.Both(TplClassPairLR, ClassAt(sentence, target, -1), ClassAt(sentence, target, 1));
        writer.Both(TplClassPairLL, ClassAt(sentence, target, -2), ClassAt(sentence, target, -1));
        writer.Both(TplClassPairRR, ClassAt(sentence, target, 1), ClassAt(sentence, target, 2));
        for (var offset = -2; offset <= 2; offset++)
        {
            if (offset != 0
                && TryGet(sentence, target, offset, out var functionWord)
                && functionWord.Class >= HeteronymContext.ClassDet)
            {
                writer.Both(TplFuncWord, (uint)(offset + OffsetBase), functionWord.LowerHash);
            }
        }

        writer.Both(TplDetDist, DistanceToClass(sentence, target, HeteronymContext.ClassDet), 0);
        writer.Both(TplModalDist, DistanceToClass(sentence, target, HeteronymContext.ClassModal), 0);
        writer.Both(TplToDist, DistanceToClass(sentence, target, HeteronymContext.ClassTo), 0);
        writer.Both(TplAuxDist, DistanceToClass(sentence, target, HeteronymContext.ClassAux), 0);
        writer.Both(TplNegDist, DistanceToClass(sentence, target, HeteronymContext.ClassNeg), 0);

        // The position in the sentence: words before (capped at three) and whether any follow.
        var wordsBefore = 0;
        for (var i = 0; i < target && wordsBefore < 3; i++)
        {
            if (sentence[i].IsWord)
            {
                wordsBefore++;
            }
        }

        var anyAfter = false;
        for (var i = target + 1; i < sentence.Length && !anyAfter; i++)
        {
            anyAfter = sentence[i].IsWord;
        }

        writer.Both(TplInitial, wordsBefore == 0 ? 1u : 0u, 0);
        writer.Both(TplPosIdx, (uint)wordsBefore, 0);
        writer.Both(TplLastWord, anyAfter ? 0u : 1u, 0);
        writer.Both(TplPunctPrev, PunctuationFeature(sentence, target, -1), 0);
        writer.Both(TplPunctNext, PunctuationFeature(sentence, target, 1), 0);

        // The part of speech of the target and its neighbors (the tagger's own tokens, so alignment is exact).
        writer.Both(TplTargetUpos, t.Upos, t.ConfidenceBucket);
        writer.Both(TplTargetUposOnly, t.Upos, 0);
        for (var offset = -2; offset <= 2; offset++)
        {
            if (offset != 0)
            {
                writer.Both(TplNeighborUpos, (uint)(offset + OffsetBase), UposAt(sentence, target, offset));
            }
        }

        writer.Both(TplUposBigram, UposAt(sentence, target, -1) | 0x100u, UposAt(sentence, target, 0));
        writer.Both(TplUposBigram, UposAt(sentence, target, 0) | 0x200u, UposAt(sentence, target, 1));
        writer.Both(TplUposBigram, UposAt(sentence, target, -1) | 0x300u, UposAt(sentence, target, 1));
        writer.Both(TplUposBigram, UposAt(sentence, target, -2) | 0x400u, UposAt(sentence, target, -1));
        writer.Both(TplUposBigram, UposAt(sentence, target, 1) | 0x500u, UposAt(sentence, target, 2));

        conjoinedCount = writer.ConjoinedCount;
        sharedCount = writer.SharedCount;
    }

    /// <summary>
    ///     Gets the hash of a token's last one, two or three code units.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="count">The number of trailing code units, 1 to 3.</param>
    /// <returns>The suffix hash.</returns>
    private static uint SuffixHash(in ContextToken token, uint count)
    {
        if (count == 1)
        {
            return token.Suffix1Hash;
        }

        return count == 2 ? token.Suffix2Hash : token.Suffix3Hash;
    }

    /// <summary>
    ///     Gets the token at an offset from the target, if it is inside the sentence.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="target">The target index.</param>
    /// <param name="offset">The offset, small and either sign.</param>
    /// <param name="token">The token when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the position holds a token.</returns>
    private static bool TryGet(ReadOnlySpan<ContextToken> sentence, int target, int offset, out ContextToken token)
    {
        var index = target + offset;
        if ((uint)index < (uint)sentence.Length)
        {
            token = sentence[index];
            return true;
        }

        token = default;
        return false;
    }

    /// <summary>
    ///     Gets the lower-case form hash of the word at an offset, or a sentence marker beyond the edge.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="target">The target index.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The word hash, or the start or end marker hash outside the sentence.</returns>
    private static uint WordHash(ReadOnlySpan<ContextToken> sentence, int target, int offset)
    {
        if (TryGet(sentence, target, offset, out var token))
        {
            return token.LowerHash;
        }

        return offset < 0 ? StartMarker : EndMarker;
    }

    /// <summary>
    ///     Gets the closed class of the token at an offset.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="target">The target index.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The class code, or 255 outside the sentence.</returns>
    private static uint ClassAt(ReadOnlySpan<ContextToken> sentence, int target, int offset) =>
        TryGet(sentence, target, offset, out var token) ? token.Class : HeteronymContext.ClassOutside;

    /// <summary>
    ///     Gets the tag code of the token at an offset.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="target">The target index.</param>
    /// <param name="offset">The offset.</param>
    /// <returns>The tag code, or 0 outside the sentence or when untagged.</returns>
    private static uint UposAt(ReadOnlySpan<ContextToken> sentence, int target, int offset) =>
        TryGet(sentence, target, offset, out var token) ? token.Upos : 0u;

    /// <summary>
    ///     Finds how far to the left, within three tokens, the nearest token of a class is.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="target">The target index.</param>
    /// <param name="wanted">The class code to look for.</param>
    /// <returns>1 to 3 for the distance, or 0 when there is none inside the sentence window.</returns>
    private static uint DistanceToClass(ReadOnlySpan<ContextToken> sentence, int target, byte wanted)
    {
        for (var distance = 1; distance <= DistanceRadius; distance++)
        {
            if (!TryGet(sentence, target, -distance, out var token))
            {
                return 0;
            }

            if (token.Class == wanted)
            {
                return (uint)distance;
            }
        }

        return 0;
    }

    /// <summary>
    ///     Describes the punctuation next to the target on one side.
    /// </summary>
    /// <param name="sentence">The sentence.</param>
    /// <param name="target">The target index.</param>
    /// <param name="offset">-1 for the left neighbor, 1 for the right.</param>
    /// <returns>1 at the sentence edge, 2 plus the hash of the mark for punctuation, otherwise 0.</returns>
    private static uint PunctuationFeature(ReadOnlySpan<ContextToken> sentence, int target, int offset)
    {
        if (!TryGet(sentence, target, offset, out var neighbor))
        {
            return 1;
        }

        return neighbor.Class == HeteronymContext.ClassPunct ? unchecked(2u + neighbor.SurfaceHash) : 0u;
    }

    /// <summary>
    ///     Appends feature hashes to the two caller-supplied buffers.
    /// </summary>
    /// <remarks>
    ///     A <see langword="ref"/> struct so it can hold the spans without allocating; it checks every append
    ///     against the buffer length, so a buffer that is too small is reported, never overrun.
    /// </remarks>
    private ref struct FeatureWriter
    {
        /// <summary>The conjoined buffer.</summary>
        private readonly Span<uint> _conjoined;

        /// <summary>The shared buffer.</summary>
        private readonly Span<uint> _shared;

        /// <summary>
        ///     Initializes a new instance.
        /// </summary>
        /// <param name="conjoined">The conjoined buffer.</param>
        /// <param name="shared">The shared buffer.</param>
        internal FeatureWriter(Span<uint> conjoined, Span<uint> shared)
        {
            _conjoined = conjoined;
            _shared = shared;
            ConjoinedCount = 0;
            SharedCount = 0;
        }

        /// <summary>Gets the number of conjoined hashes written.</summary>
        internal int ConjoinedCount { get; private set; }

        /// <summary>Gets the number of shared hashes written.</summary>
        internal int SharedCount { get; private set; }

        /// <summary>
        ///     Appends a feature that is scored only through the reading's own label salt.
        /// </summary>
        /// <param name="template">The template number.</param>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        internal void Conjoined(uint template, uint first, uint second) =>
            AddConjoined(FeatureHashing.Feature(template, first, second));

        /// <summary>
        ///     Appends a feature that is scored both through the label salt and through the role salt.
        /// </summary>
        /// <param name="template">The template number.</param>
        /// <param name="first">The first argument.</param>
        /// <param name="second">The second argument.</param>
        internal void Both(uint template, uint first, uint second)
        {
            if (SharedCount >= _shared.Length)
            {
                throw new InvalidOperationException("The shared feature buffer is too small.");
            }

            var hash = FeatureHashing.Feature(template, first, second);
            AddConjoined(hash);
            _shared[SharedCount++] = hash;
        }

        /// <summary>
        ///     Appends one hash to the conjoined buffer.
        /// </summary>
        /// <param name="hash">The feature hash.</param>
        /// <exception cref="InvalidOperationException">The buffer is full.</exception>
        private void AddConjoined(uint hash)
        {
            if (ConjoinedCount >= _conjoined.Length)
            {
                throw new InvalidOperationException("The conjoined feature buffer is too small.");
            }

            _conjoined[ConjoinedCount++] = hash;
        }
    }
}
