using DemaConsulting.PartOfSpeech;

namespace DemaConsulting.Speech.Text.En.AnalysisSubsystem;

/// <summary>
///     One token as the adapter reports it: its span in the analyzed text and, when the text was
///     tagged, its part of speech.
/// </summary>
/// <param name="Start">The UTF-16 offset of the token's first character in the analyzed text.</param>
/// <param name="Length">The number of UTF-16 code units of the token; always positive.</param>
/// <param name="Tag">
///     The Universal Dependencies tag name (for example <c>"NOUN"</c>), or <see langword="null"/>
///     when the text was only split, not tagged.
/// </param>
/// <param name="Confidence">The tag's confidence from 0 to 1, or 0 when the token was not tagged.</param>
/// <param name="IsKnownWord">
///     Whether the tagger's lexicon has an entry for the token's spelling; <see langword="false"/>
///     when the token was not tagged.
/// </param>
internal readonly record struct AdaptedToken(int Start, int Length, string? Tag, double Confidence, bool IsKnownWord);

/// <summary>
///     Isolates every use of the <c>DemaConsulting.PartOfSpeech</c> package behind one internal
///     seam: it splits a text into sentences and tokens and (optionally) tags them, and hands
///     the analyzer neutral values so no PartOfSpeech type reaches any public signature.
/// </summary>
/// <remarks>
///     Keeping the dependency in one place means a change to the tagger package (it is a
///     pre-release package) touches exactly one unit. The tagger is obtained lazily and, once
///     obtained, kept: it is created at most once per adapter instance, under a lock with a
///     null check, so concurrent first requests never construct it twice. A failed creation is
///     not remembered (unlike <see cref="Lazy{T}"/> in its default mode, which caches the
///     exception for the process lifetime): the next request that needs the tagger tries again.
///     The process-wide <see cref="Shared"/> adapter obtains <c>Tagger.Default</c>, which the
///     package documents as a thread-safe, immutable, shared instance whose first use inflates
///     about 1.3 MB of embedded data. Nothing is loaded until text that actually contains
///     tokens is tagged: an analyzer that is never invoked, an empty text, and a request that
///     only needs sentence boundaries all leave the tagger untouched. The adapter keeps no
///     per-request state and is thread-safe.
///     <para>
///     Cancellation is checked before the text is split, after the tagger is obtained, after each
///     sentence is tagged, and periodically while a sentence's tokens are copied into the
///     result, so a canceled request stops promptly wherever it is, including during the first
///     use of the tagger and during the last sentence.
///     </para>
/// </remarks>
internal sealed class PartOfSpeechAdapter
{
    /// <summary>The number of tokens copied between two cancellation checks.</summary>
    internal const int CancellationCheckInterval = 256;

    /// <summary>Creates the tagger on first use; invoked only while <see cref="_gate"/> is held.</summary>
    private readonly Func<Tagger> _taggerFactory;

    /// <summary>Guards creation of the tagger so it is constructed by one caller at a time.</summary>
    private readonly object _gate = new();

    /// <summary>An optional observer called with the index of each sentence right after it was tagged; tests use it to cancel at exact points.</summary>
    private readonly Action<int>? _sentenceTagged;

    /// <summary>The tagger once created; null until a creation succeeds. Published with a volatile write.</summary>
    private Tagger? _tagger;

    /// <summary>
    ///     Initializes a new instance that obtains its tagger from <paramref name="taggerFactory"/>
    ///     on first use.
    /// </summary>
    /// <param name="taggerFactory">
    ///     Creates the tagger; invoked only when text with tokens is tagged, never concurrently
    ///     with itself, and at most once successfully (a factory that throws is invoked again by
    ///     the next request that needs the tagger). Tests inject a counting factory; production
    ///     uses <see cref="Shared"/>. Must not be null.
    /// </param>
    /// <param name="sentenceTagged">
    ///     An optional observer called with the zero-based index of each sentence immediately
    ///     after it was tagged, before cancellation is checked; used by tests to cancel at an
    ///     exact point. Production passes null.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="taggerFactory"/> is null.</exception>
    internal PartOfSpeechAdapter(Func<Tagger> taggerFactory, Action<int>? sentenceTagged = null)
    {
        ArgumentNullException.ThrowIfNull(taggerFactory);
        _taggerFactory = taggerFactory;
        _sentenceTagged = sentenceTagged;
    }

    /// <summary>
    ///     Gets the process-wide adapter over <c>Tagger.Default</c>; creating it does not load the
    ///     tagger.
    /// </summary>
    internal static PartOfSpeechAdapter Shared { get; } = new(static () => Tagger.Default);

    /// <summary>
    ///     Gets a value that indicates whether this adapter has already obtained its tagger.
    /// </summary>
    internal bool IsTaggerCreated => Volatile.Read(ref _tagger) is not null;

    /// <summary>
    ///     Splits <paramref name="text"/> into sentences and tokens and, when
    ///     <paramref name="tag"/> is <see langword="true"/>, tags every token.
    /// </summary>
    /// <param name="text">
    ///     The text to split. Token offsets index exactly this string. Must not be null.
    /// </param>
    /// <param name="tag">
    ///     Whether to tag the tokens (which loads the tagger on first use) or only split them,
    ///     which needs no tagger.
    /// </param>
    /// <param name="cancellationToken">
    ///     Checked before the text is split, before each sentence, after the tagger is obtained,
    ///     after each sentence is tagged and periodically while tokens are copied.
    /// </param>
    /// <returns>
    ///     The sentences in text order, each the list of its tokens in text order; empty for a
    ///     text without tokens. Every sentence holds at least one token.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    internal IReadOnlyList<IReadOnlyList<AdaptedToken>> Analyze(string text, bool tag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();

        // The tokenizer is a static, model-free splitter, so sentence boundaries cost no tagger
        // load. Each sentence is then tagged on its own (the same work Tagger.Tag(string) does)
        // so cancellation can be honored between sentences.
        var sentences = Tokenizer.Tokenize(text);
        var result = new List<IReadOnlyList<AdaptedToken>>(sentences.Count);
        var sentenceIndex = 0;
        foreach (var sentence in sentences)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (sentence.Tokens.Count == 0)
            {
                continue;
            }

            if (!tag)
            {
                var split = new AdaptedToken[sentence.Tokens.Count];
                for (var i = 0; i < split.Length; i++)
                {
                    ThrowIfCancellationDue(i, cancellationToken);
                    var token = sentence.Tokens[i];
                    split[i] = new AdaptedToken(token.Start, token.Length, null, 0.0, false);
                }

                result.Add(split);
                sentenceIndex++;
                continue;
            }

            // Initialization may be the expensive step, and it cannot itself be interrupted, so
            // look at the token again as soon as it returns.
            var tagger = GetTagger();
            cancellationToken.ThrowIfCancellationRequested();

            var tagged = tagger.Tag(sentence);
            _sentenceTagged?.Invoke(sentenceIndex);
            cancellationToken.ThrowIfCancellationRequested();

            var adapted = new AdaptedToken[tagged.Tokens.Count];
            for (var i = 0; i < adapted.Length; i++)
            {
                ThrowIfCancellationDue(i, cancellationToken);
                var token = tagged.Tokens[i];
                adapted[i] = new AdaptedToken(token.Start, token.Length, token.Tag.ToTagString(), token.Confidence, token.IsKnownWord);
            }

            result.Add(adapted);
            sentenceIndex++;
        }

        // The last sentence's copy is part of the work too: a request canceled during it must not
        // return a result.
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <summary>
    ///     Gets the tagger, creating it on the first call that needs it.
    /// </summary>
    /// <returns>The tagger.</returns>
    /// <exception cref="InvalidOperationException">The factory returned null.</exception>
    /// <remarks>
    ///     Double-checked: a successfully created tagger is read without locking; a failed
    ///     creation leaves nothing behind, so the next call retries. The factory runs under the
    ///     lock because construction is expensive and must not race.
    /// </remarks>
    private Tagger GetTagger()
    {
        var existing = Volatile.Read(ref _tagger);
        if (existing is not null)
        {
            return existing;
        }

        lock (_gate)
        {
            var current = _tagger;
            if (current is null)
            {
                current = _taggerFactory()
                    ?? throw new InvalidOperationException("The tagger factory returned no tagger.");
                Volatile.Write(ref _tagger, current);
            }

            return current;
        }
    }

    /// <summary>
    ///     Throws when cancellation is requested, but only every
    ///     <see cref="CancellationCheckInterval"/> tokens, so the check stays cheap.
    /// </summary>
    /// <param name="tokenIndex">The index of the token about to be copied.</param>
    /// <param name="cancellationToken">The token to check.</param>
    /// <exception cref="OperationCanceledException">Cancellation is requested and a check is due.</exception>
    private static void ThrowIfCancellationDue(int tokenIndex, CancellationToken cancellationToken)
    {
        if (tokenIndex % CancellationCheckInterval == CancellationCheckInterval - 1)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
