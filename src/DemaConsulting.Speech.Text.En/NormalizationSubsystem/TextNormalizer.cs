using System.Text;

namespace DemaConsulting.Speech.Text.En.NormalizationSubsystem;

/// <summary>
///     Decides, token by token, which spans of an English text are spoken differently from how
///     they are written, and records those decisions as an <see cref="AlignedText"/>; the text
///     itself is never rewritten.
/// </summary>
/// <remarks>
///     One linear pass over the tokens applies an ordered list of rules, the first match winning:
///     invisible characters, clitics, lexicon abbreviations, acronyms and initialisms, symbols and
///     marks, and elision apostrophes. Every rule looks at a constant number of neighboring
///     tokens, so the pass is linear in the text length whatever the text contains. The rules are
///     lexical and orthographic only; the part-of-speech tagger is never consulted, so the result
///     does not depend on which annotations the analyzer was asked for. Numbers, dates, currency,
///     units, URLs, markup and hyphen policy are deliberately not handled yet and pass through
///     untouched. An instance keeps no per-request state and is thread-safe.
/// </remarks>
internal sealed class TextNormalizer
{
    /// <summary>The number of tokens visited between two cancellation checks.</summary>
    internal const int CancellationCheckInterval = 256;

    /// <summary>The words after which an <c>'s</c> is a contraction rather than a possessive.</summary>
    private static readonly HashSet<string> ContractionHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "it", "he", "she", "that", "there", "here", "what", "who", "where", "when", "how", "why", "which",
        "everyone", "everybody", "someone", "somebody", "anyone", "anybody", "nobody", "everything",
        "something", "anything", "nothing",
    };

    /// <summary>The words that make a following <c>'s</c> mean <c>has</c>.</summary>
    private static readonly HashSet<string> HasFollowers = new(StringComparer.OrdinalIgnoreCase)
    {
        "been", "got", "gotten", "had", "done", "gone", "seen", "made", "taken", "come", "become",
    };

    /// <summary>The words that, after a noun or name, make a following <c>'s</c> a contraction.</summary>
    private static readonly HashSet<string> PossessiveContractionNext = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "not", "no", "been", "got", "gotten", "going", "gone", "being", "here", "there",
        "so", "too", "very", "all", "only", "back", "out", "up", "down", "over", "in", "on", "at",
    };

    /// <summary>The adverbs skipped when looking for the word after an <c>'s</c>.</summary>
    private static readonly HashSet<string> SkippedAfterS = new(StringComparer.OrdinalIgnoreCase)
    {
        "never", "always", "just", "already", "also", "still", "really", "now",
    };

    /// <summary>The adverbs skipped when looking for the word after a <c>'d</c>.</summary>
    private static readonly HashSet<string> SkippedAfterD = new(StringComparer.OrdinalIgnoreCase)
    {
        "never", "always", "just", "already", "also",
    };

    /// <summary>The words that make a following <c>'d</c> mean <c>had</c>.</summary>
    private static readonly HashSet<string> HadFollowers = new(StringComparer.OrdinalIgnoreCase)
    {
        "better", "been", "had", "got", "gone", "done", "seen", "taken", "made", "come",
    };

    /// <summary>The words that follow a leading apostrophe in an elision such as <c>'tis</c>.</summary>
    private static readonly HashSet<string> LeadingElisions = new(StringComparer.OrdinalIgnoreCase)
    {
        "tis", "twas", "til", "cause", "em",
    };

    /// <summary>The lazily created default normalizer; creation loads both embedded lexicons.</summary>
    private static readonly Lazy<TextNormalizer> DefaultNormalizer =
        new(static () => new TextNormalizer(AbbreviationResolver.Default, AcronymRecognizer.Default), LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>The abbreviation lexicon and its context rules.</summary>
    private readonly AbbreviationResolver _abbreviations;

    /// <summary>The acronym and initialism recognizer.</summary>
    private readonly AcronymRecognizer _acronyms;

    /// <summary>An optional observer called with the index of each token as it is visited; tests use it to cancel at an exact point.</summary>
    private readonly Action<int>? _tokenVisited;

    /// <summary>
    ///     Initializes a new instance over the given lexicons.
    /// </summary>
    /// <param name="abbreviations">The abbreviation resolver. Must not be null.</param>
    /// <param name="acronyms">The acronym recognizer. Must not be null.</param>
    /// <param name="tokenVisited">
    ///     An optional observer called with the zero-based index of each token right before it is
    ///     processed; a test seam (production passes null).
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="abbreviations"/> or <paramref name="acronyms"/> is null.</exception>
    internal TextNormalizer(AbbreviationResolver abbreviations, AcronymRecognizer acronyms, Action<int>? tokenVisited = null)
    {
        ArgumentNullException.ThrowIfNull(abbreviations);
        ArgumentNullException.ThrowIfNull(acronyms);
        _abbreviations = abbreviations;
        _acronyms = acronyms;
        _tokenVisited = tokenVisited;
    }

    /// <summary>
    ///     Gets the normalizer over the embedded lexicons, created once on first use.
    /// </summary>
    internal static TextNormalizer Default => DefaultNormalizer.Value;

    /// <summary>
    ///     Normalizes one text.
    /// </summary>
    /// <param name="text">
    ///     The text, in which every audio-tag placeholder has already been replaced by a space.
    ///     Must not be null. Offsets of the result index exactly this string.
    /// </param>
    /// <param name="tokens">
    ///     The tokens of the text in order, non-overlapping and inside the text, with
    ///     <see cref="NormalizationToken.StartsSentence"/> set on the first token of each
    ///     tokenizer sentence.
    /// </param>
    /// <param name="classifyClitics">
    ///     Whether to record the grammatical role of each clitic; off on the hot path because
    ///     only the detailed analysis reports it.
    /// </param>
    /// <param name="cancellationToken">
    ///     Checked before any work and every <see cref="CancellationCheckInterval"/> tokens.
    /// </param>
    /// <returns>The aligned result; its spans, break edits and clitic marks all index <paramref name="text"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    /// <exception cref="ArgumentException">A token is empty, out of order or outside the text.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is canceled.</exception>
    internal AlignedText Normalize(string text, ReadOnlySpan<NormalizationToken> tokens, bool classifyClitics, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();

        // The rules index the text through the tokens, so a malformed token list is rejected up
        // front rather than surfacing as an out-of-range read in the middle of a rule.
        var previousEnd = 0;
        foreach (var token in tokens)
        {
            // Start is checked against the text length and Length against what remains after it,
            // so the range is validated without ever computing Start + Length (which can
            // overflow for a hostile token and wrap around to look in bounds).
            if (token.Length <= 0
                || token.Start < previousEnd
                || token.Start > text.Length
                || token.Length > text.Length - token.Start)
            {
                throw new ArgumentException("A token is empty, out of order or outside the text.", nameof(tokens));
            }

            previousEnd = token.End;
        }

        var builder = new AlignedTextBuilder(text);
        var pass = new Pass(this, text, tokens, classifyClitics, builder, cancellationToken);
        pass.Run();
        return builder.Build();
    }

    /// <summary>
    ///     The state and rules of one normalization pass. A ref struct because it holds the token
    ///     span; it lives on the caller's stack and is never shared, which is what keeps the
    ///     normalizer itself free of per-request state.
    /// </summary>
    private ref struct Pass
    {
        /// <summary>The owning normalizer, for its lexicons and test seam.</summary>
        private readonly TextNormalizer _owner;

        /// <summary>The text being normalized.</summary>
        private readonly string _text;

        /// <summary>The tokens of the text.</summary>
        private readonly ReadOnlySpan<NormalizationToken> _tokens;

        /// <summary>Whether to record clitic roles.</summary>
        private readonly bool _classifyClitics;

        /// <summary>The collector of the pass's decisions.</summary>
        private readonly AlignedTextBuilder _builder;

        /// <summary>The cancellation token.</summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>The index of the first token of the current tokenizer sentence.</summary>
        private int _sentenceStart;

        /// <summary>Whether the current sentence is all capitals: 0 not yet computed, 1 no, 2 yes.</summary>
        private int _allCapitals;

        /// <summary>The number of tokens visited, for the periodic cancellation check.</summary>
        private int _visited;

        /// <summary>
        ///     Initializes a new pass.
        /// </summary>
        /// <param name="owner">The owning normalizer.</param>
        /// <param name="text">The text.</param>
        /// <param name="tokens">The validated tokens.</param>
        /// <param name="classifyClitics">Whether to record clitic roles.</param>
        /// <param name="builder">The collector of decisions.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        internal Pass(TextNormalizer owner, string text, ReadOnlySpan<NormalizationToken> tokens, bool classifyClitics, AlignedTextBuilder builder, CancellationToken cancellationToken)
        {
            _owner = owner;
            _text = text;
            _tokens = tokens;
            _classifyClitics = classifyClitics;
            _builder = builder;
            _cancellationToken = cancellationToken;
            _sentenceStart = 0;
            _allCapitals = 0;
            _visited = 0;
        }

        /// <summary>
        ///     Walks every token once, applying the rules.
        /// </summary>
        internal void Run()
        {
            var index = 0;
            while (index < _tokens.Length)
            {
                // Look at the cancellation token periodically; the interval counts visited
                // tokens so a multi-token span cannot skip a check.
                _visited++;
                if (_visited % CancellationCheckInterval == 0)
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                }

                _owner._tokenVisited?.Invoke(index);

                // A new tokenizer sentence invalidates the cached all-capitals decision.
                if (index == 0 || _tokens[index].StartsSentence)
                {
                    _sentenceStart = index;
                    _allCapitals = 0;
                }

                index = ProcessToken(index);
            }
        }

        /// <summary>
        ///     Applies the first matching rule to the token at <paramref name="index"/>.
        /// </summary>
        /// <param name="index">The token's index.</param>
        /// <returns>The index of the next token still to be processed.</returns>
        private int ProcessToken(int index)
        {
            var token = _tokens[index];
            var span = token.In(_text);

            // Rule 1: invisible characters (and a standalone bullet) are dropped explicitly. A token
            // that merely contains them is otherwise left alone.
            if (ContainsDropped(span) || IsBullet(span))
            {
                EmitDropped(token, span);
                return index + 1;
            }

            // Rule 2: clitics ('s, n't, ...) get their apostrophe canonicalized and, on request, a role.
            if (TryGetClitic(span, out var cliticKind, out var apostropheOffset))
            {
                if (!IsPlainApostrophe(span[apostropheOffset]))
                {
                    Emit(token.Start + apostropheOffset, 1, "'", NormalizationKind.Apostrophe);
                }

                if (_classifyClitics)
                {
                    _builder.MarkClitic(new CliticMark(token.Start, token.Length, ClassifyClitic(index, cliticKind)));
                }

                return index + 1;
            }

            // Rule 3: lexicon abbreviations, resolved from their neighbors.
            if (_owner._abbreviations.TryResolve(_text, _tokens, index, out var abbreviation))
            {
                var periodInToken = !abbreviation.IncludesPeriodToken && span[^1] == '.';
                return ApplyPeriodRules(index, abbreviation.Spoken, NormalizationKind.Abbreviation, abbreviation.Class, periodInToken, abbreviation.IncludesPeriodToken, abbreviation.UsedDefault);
            }

            // Rule 4: acronyms and initialisms.
            if (TryApplyAcronym(index, span, out var next))
            {
                return next;
            }

            // Rule 5: symbols and marks, standalone or inside a word.
            if (TryApplySymbol(token, span))
            {
                return index + 1;
            }

            // Rule 6: elision apostrophes inside a word; everything else is identity.
            EmitElisionApostrophes(index, span);
            return index + 1;
        }

        /// <summary>
        ///     Emits the span of an abbreviation or initialism and applies the period and sentence
        ///     rules that go with its class.
        /// </summary>
        /// <param name="index">The index of the candidate's first token.</param>
        /// <param name="spoken">The spoken replacement.</param>
        /// <param name="kind">What produced the replacement.</param>
        /// <param name="abbreviationClass">How the abbreviation relates to the words around it.</param>
        /// <param name="periodInToken">Whether the token itself ends with the period.</param>
        /// <param name="periodToken">Whether a separate, adjacent period token follows.</param>
        /// <param name="ambiguous">Whether the reading is to be counted as ambiguous.</param>
        /// <returns>The index of the next token still to be processed.</returns>
        private int ApplyPeriodRules(int index, string spoken, NormalizationKind kind, AbbreviationClass abbreviationClass, bool periodInToken, bool periodToken, bool ambiguous)
        {
            var token = _tokens[index];
            var last = periodToken ? index + 1 : index;
            var keepPeriod = false;

            if (periodInToken || periodToken)
            {
                // Find the first token after the abbreviation that is not a closing quote or
                // bracket, remembering where the tokenizer put a sentence break, if anywhere.
                var following = last + 1;
                var breakStart = -1;
                while (following < _tokens.Length && IsClosingMark(_tokens[following]))
                {
                    if (breakStart < 0 && _tokens[following].StartsSentence)
                    {
                        breakStart = _tokens[following].Start;
                    }

                    following++;
                }

                var followingExists = following < _tokens.Length;
                if (followingExists && breakStart < 0 && _tokens[following].StartsSentence)
                {
                    breakStart = _tokens[following].Start;
                }

                var endsSentence = !followingExists || breakStart >= 0;

                // A name suffix or credential that directly continues into another one ("Jr.
                // Ph.D.") belongs to the same name and never ends the sentence: it behaves like
                // a prefix (period consumed, tokenizer break removed).
                var continuesName = abbreviationClass != AbbreviationClass.Prefix
                    && followingExists
                    && following == last + 1
                    && AbbreviationResolver.IsNameContinuation(_text, _tokens, index)
                    && AbbreviationResolver.IsNameContinuation(_text, _tokens, following);
                switch (continuesName ? AbbreviationClass.Prefix : abbreviationClass)
                {
                    case AbbreviationClass.Prefix:
                        // A title or similar never ends a sentence: its period is consumed and a
                        // tokenizer break after it is removed.
                        if (endsSentence && breakStart >= 0)
                        {
                            _builder.SuppressBreak(breakStart);
                        }

                        break;

                    case AbbreviationClass.Suffix:
                    case AbbreviationClass.Word:
                        // A suffix ends the sentence when a capitalized word follows; the period
                        // is then kept as the sentence's terminator.
                        if (!endsSentence && followingExists && char.IsUpper(_text[_tokens[following].Start]))
                        {
                            _builder.ForceBreak(_tokens[following].Start);
                            endsSentence = true;
                        }

                        keepPeriod = endsSentence;
                        break;

                    default:
                        // A free abbreviation takes the tokenizer's opinion of the sentence end.
                        keepPeriod = endsSentence;
                        break;
                }
            }

            if (ambiguous)
            {
                _builder.CountAmbiguous();
            }

            // The word reading ("No." as the word) changes nothing but the break above.
            if (abbreviationClass != AbbreviationClass.Word)
            {
                var length = token.Length;
                if (periodToken && !keepPeriod)
                {
                    length++;
                }
                else if (periodInToken && keepPeriod)
                {
                    length--;
                }

                Emit(token.Start, length, spoken, kind);
            }

            return last + 1;
        }

        /// <summary>
        ///     Tries to treat a token as an acronym or initialism.
        /// </summary>
        /// <param name="index">The token's index.</param>
        /// <param name="span">The token's characters.</param>
        /// <param name="next">The index of the next token still to be processed when this method returns <see langword="true"/>.</param>
        /// <returns><see langword="true"/> when a span was emitted.</returns>
        private bool TryApplyAcronym(int index, ReadOnlySpan<char> span, out int next)
        {
            next = index + 1;

            // The shouting guard matters only for plain capitals of two to six letters, so the
            // sentence is examined only for those.
            var plainCapital = span.Length <= 6 && char.IsUpper(span[0]);
            if (!_owner._acronyms.TryRecognize(span, plainCapital && IsSentenceAllCapitals(), out var match))
            {
                return false;
            }

            var token = _tokens[index];
            if (match.TokenEndsWithPeriod)
            {
                next = ApplyPeriodRules(index, match.Spoken, NormalizationKind.Initialism, AbbreviationClass.Free, periodInToken: true, periodToken: false, ambiguous: false);
                return true;
            }

            // A dotted form without its own final period is an initialism only when the tokenizer
            // split the period off as a separate adjacent token.
            if (span.Contains('.'))
            {
                if (!HasAdjacentPeriodToken(index))
                {
                    return false;
                }

                next = ApplyPeriodRules(index, match.Spoken, NormalizationKind.Initialism, AbbreviationClass.Free, periodInToken: false, periodToken: true, ambiguous: false);
                return true;
            }

            Emit(token.Start, token.Length, match.Spoken, NormalizationKind.Initialism);
            return true;
        }

        /// <summary>
        ///     Tries to treat a token as a symbol or mark, or to find a symbol inside a word.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="span">The token's characters.</param>
        /// <returns><see langword="true"/> when at least one span was emitted.</returns>
        private bool TryApplySymbol(NormalizationToken token, ReadOnlySpan<char> span)
        {
            // A single-character token is an operator (only when set apart by whitespace, so
            // "<b>" and "a=b" are left alone) or a mark (which may touch a word).
            if (span.Length == 1)
            {
                var character = span[0];
                if (OperatorWords(character) is { } operatorWords)
                {
                    var leftOpen = token.Start == 0 || char.IsWhiteSpace(_text[token.Start - 1]);
                    var rightOpen = token.End == _text.Length || char.IsWhiteSpace(_text[token.End]);
                    if (!leftOpen || !rightOpen)
                    {
                        return false;
                    }

                    Emit(token.Start, 1, operatorWords, NormalizationKind.Symbol);
                    return true;
                }

                if (MarkWords(character) is { } markWords)
                {
                    Emit(token.Start, 1, markWords, NormalizationKind.Symbol);
                    return true;
                }

                return false;
            }

            // Inside a word: a mark, an ampersand between two letters, or the single slash of a word
            // pair. One left-to-right pass keeps the spans in text order.
            var pairIndex = span.Contains('/') && IsWordPair(span, out var slash) ? slash : -1;
            var emitted = false;
            for (var i = 0; i < span.Length; i++)
            {
                if (MarkWords(span[i]) is { } words)
                {
                    Emit(token.Start + i, 1, words, NormalizationKind.Symbol);
                    emitted = true;
                }
                else if (span[i] == '&' && i > 0 && i < span.Length - 1 && Rune.IsLetter(RuneBefore(span, i)) && Rune.IsLetter(RuneAt(span, i + 1)))
                {
                    Emit(token.Start + i, 1, " and ", NormalizationKind.Symbol);
                    emitted = true;
                }
                else if (i == pairIndex)
                {
                    Emit(token.Start + i, 1, " slash ", NormalizationKind.Symbol);
                    emitted = true;
                }
            }

            return emitted;
        }

        /// <summary>
        ///     Replaces typographic apostrophes between two letters, and the leading apostrophe of
        ///     <c>'tis</c>-style elisions (whole, or split off by the tokenizer as a lone
        ///     apostrophe token), by the plain apostrophe.
        /// </summary>
        /// <param name="index">The token's index.</param>
        /// <param name="span">The token's characters.</param>
        private void EmitElisionApostrophes(int index, ReadOnlySpan<char> span)
        {
            var token = _tokens[index];

            // A lone typographic apostrophe that touches a following elision word ("’" "til").
            if (span.Length == 1 && IsTypographicApostrophe(span[0]))
            {
                var touchesElision = index + 1 < _tokens.Length
                    && _tokens[index + 1].Start == token.End
                    && LeadingElisions.Contains(_tokens[index + 1].In(_text).ToString());
                var leading = !Rune.IsLetterOrDigit(RuneBefore(_text, token.Start));
                if (touchesElision && leading)
                {
                    Emit(token.Start, 1, "'", NormalizationKind.Apostrophe);
                }

                return;
            }

            for (var i = 0; i < span.Length; i++)
            {
                if (!IsTypographicApostrophe(span[i]))
                {
                    continue;
                }

                var betweenLetters = i > 0 && i < span.Length - 1 && Rune.IsLetter(RuneBefore(span, i)) && Rune.IsLetter(RuneAt(span, i + 1));
                var leadingElision = i == 0 && span.Length > 1 && LeadingElisions.Contains(span[1..].ToString());
                if (betweenLetters || leadingElision)
                {
                    Emit(token.Start + i, 1, "'", NormalizationKind.Apostrophe);
                }
            }
        }

        /// <summary>
        ///     Emits one dropped span per maximal run of invisible characters in a token (the
        ///     whole token for a bullet).
        /// </summary>
        /// <param name="token">The token.</param>
        /// <param name="span">The token's characters.</param>
        private void EmitDropped(NormalizationToken token, ReadOnlySpan<char> span)
        {
            if (IsBullet(span))
            {
                Emit(token.Start, token.Length, string.Empty, NormalizationKind.Dropped);
                return;
            }

            var i = 0;
            while (i < span.Length)
            {
                if (!IsDroppedCharacter(span[i]))
                {
                    i++;
                    continue;
                }

                var runStart = i;
                while (i < span.Length && IsDroppedCharacter(span[i]))
                {
                    i++;
                }

                Emit(token.Start + runStart, i - runStart, string.Empty, NormalizationKind.Dropped);
            }
        }

        /// <summary>
        ///     Reads the Unicode scalar that ends immediately before an index, so boundary checks
        ///     see a supplementary-plane character as one character rather than a lone low surrogate.
        /// </summary>
        /// <param name="text">The text to read.</param>
        /// <param name="index">The index the scalar must end at; may be 0 or the text length.</param>
        /// <returns>
        ///     The scalar, or <see cref="Rune.ReplacementChar"/> (neither a letter nor a digit)
        ///     when there is none before <paramref name="index"/> or the text is malformed there.
        /// </returns>
        private static Rune RuneBefore(ReadOnlySpan<char> text, int index)
        {
            Rune.DecodeLastFromUtf16(text[..index], out var rune, out _);
            return rune;
        }

        /// <summary>
        ///     Reads the Unicode scalar that starts at an index; the counterpart of
        ///     <see cref="RuneBefore"/> for the character after a span.
        /// </summary>
        /// <param name="text">The text to read.</param>
        /// <param name="index">The index the scalar starts at; may equal the text length.</param>
        /// <returns>
        ///     The scalar, or <see cref="Rune.ReplacementChar"/> (neither a letter nor a digit)
        ///     when <paramref name="index"/> is at the end of the text or the text is malformed there.
        /// </returns>
        private static Rune RuneAt(ReadOnlySpan<char> text, int index)
        {
            Rune.DecodeFromUtf16(text[index..], out var rune, out _);
            return rune;
        }

        /// <summary>
        ///     Adds a span, giving the replacement one separating space on its left where a letter or
        ///     digit touches the span, or the previous replacement touches it and ends with a letter,
        ///     and on its right where a letter or digit touches the span; the replacement must itself
        ///     start or end with a letter, so spoken words never run together and two adjacent
        ///     replacements are separated by exactly one space.
        /// </summary>
        /// <param name="start">The span's start.</param>
        /// <param name="length">The span's length.</param>
        /// <param name="spoken">The replacement before padding.</param>
        /// <param name="kind">What produced the replacement.</param>
        private readonly void Emit(int start, int length, string spoken, NormalizationKind kind)
        {
            // Dropped and apostrophe replacements are never padded: one is empty and the other
            // must stay glued to its word.
            if (kind is not (NormalizationKind.Dropped or NormalizationKind.Apostrophe))
            {
                var end = start + length;
                // The neighbors are read as whole Unicode scalars, not UTF-16 code units, so a
                // supplementary-plane letter or digit (a surrogate pair) counts as one.
                var padLeft = char.IsLetter(spoken[0])
                    && (Rune.IsLetterOrDigit(RuneBefore(_text, start)) || _builder.TouchesPreviousSpokenLetter(start));
                var padRight = Rune.IsLetterOrDigit(RuneAt(_text, end)) && char.IsLetter(spoken[^1]);
                if (padLeft || padRight)
                {
                    spoken = (padLeft ? " " : string.Empty) + spoken + (padRight ? " " : string.Empty);
                }
            }

            _builder.Add(new NormalizedSpan(start, length, spoken, kind));
        }

        /// <summary>
        ///     Determines the grammatical role of a clitic from the words around it.
        /// </summary>
        /// <param name="index">The clitic token's index.</param>
        /// <param name="provisional">
        ///     The role the clitic's spelling alone gives; <see cref="CliticKind.Is"/> stands for an
        ///     <c>'s</c> and <see cref="CliticKind.Would"/> for a <c>'d</c>, which need context.
        /// </param>
        /// <returns>The final role; never <see cref="CliticKind.None"/>.</returns>
        private readonly CliticKind ClassifyClitic(int index, CliticKind provisional)
        {
            if (provisional == CliticKind.Would)
            {
                // 'd is "had" before a past participle (optionally after one adverb), else "would".
                var following = NextWord(index, SkippedAfterD);
                return following is not null && HadFollowers.Contains(following) ? CliticKind.Had : CliticKind.Would;
            }

            if (provisional != CliticKind.Is)
            {
                return provisional;
            }

            // 's: "let's", a contraction (is/has) or a possessive, from the host and the next word.
            var host = index > 0 && _tokens[index - 1].End == _tokens[index].Start && char.IsLetter(_text[_tokens[index - 1].Start])
                ? _tokens[index - 1].In(_text).ToString()
                : null;
            if (host is not null && string.Equals(host, "let", StringComparison.OrdinalIgnoreCase))
            {
                return CliticKind.Us;
            }

            var next = NextWord(index, SkippedAfterS);
            var contraction = host is not null && ContractionHosts.Contains(host);
            if (!contraction)
            {
                // A noun or name host: a contraction only when the next word says so.
                contraction = next is not null
                    && (PossessiveContractionNext.Contains(next) || (next.Length >= 5 && next.EndsWith("ing", StringComparison.OrdinalIgnoreCase)));
            }

            if (!contraction)
            {
                return CliticKind.Possessive;
            }

            return next is not null && HasFollowers.Contains(next) ? CliticKind.Has : CliticKind.Is;
        }

        /// <summary>
        ///     Finds the word after a clitic, skipping at most one of the given adverbs, without
        ///     leaving the sentence.
        /// </summary>
        /// <param name="index">The clitic token's index.</param>
        /// <param name="skipped">The adverbs that may be skipped.</param>
        /// <returns>The word, or <see langword="null"/> when there is none.</returns>
        private readonly string? NextWord(int index, HashSet<string> skipped)
        {
            var word = WordAt(index + 1);
            if (word is not null && skipped.Contains(word))
            {
                word = WordAt(index + 2);
            }

            return word;
        }

        /// <summary>
        ///     Returns the text of a word token that does not start a new sentence.
        /// </summary>
        /// <param name="index">The token's index.</param>
        /// <returns>The word, or <see langword="null"/> when the token does not exist, starts a sentence or is not a word.</returns>
        private readonly string? WordAt(int index)
        {
            if (index >= _tokens.Length || _tokens[index].StartsSentence || !char.IsLetter(_text[_tokens[index].Start]))
            {
                return null;
            }

            return _tokens[index].In(_text).ToString();
        }

        /// <summary>
        ///     Tells whether the current tokenizer sentence is written entirely in capitals: at
        ///     least three alphabetic tokens of two or more letters, all upper-case. Computed
        ///     once per sentence.
        /// </summary>
        /// <returns><see langword="true"/> for a shouted sentence.</returns>
        private bool IsSentenceAllCapitals()
        {
            if (_allCapitals != 0)
            {
                return _allCapitals == 2;
            }

            var words = 0;
            var allUpper = true;
            for (var i = _sentenceStart; i < _tokens.Length && allUpper; i++)
            {
                if (i > _sentenceStart && _tokens[i].StartsSentence)
                {
                    break;
                }

                var token = _tokens[i].In(_text);
                if (token.Length >= 2 && IsAllLetters(token))
                {
                    words++;
                    allUpper = IsAllUpper(token);
                }
            }

            _allCapitals = allUpper && words >= 3 ? 2 : 1;
            return _allCapitals == 2;
        }

        /// <summary>
        ///     Tells whether the token after <paramref name="index"/> is a lone period touching it.
        /// </summary>
        /// <param name="index">The token's index.</param>
        /// <returns><see langword="true"/> when a separate period token follows without a gap.</returns>
        private readonly bool HasAdjacentPeriodToken(int index) =>
            index + 1 < _tokens.Length
            && _tokens[index + 1].Length == 1
            && _tokens[index + 1].Start == _tokens[index].End
            && _text[_tokens[index].End] == '.';

        /// <summary>
        ///     Tells whether a token is a closing quote or bracket.
        /// </summary>
        /// <param name="token">The token.</param>
        /// <returns><see langword="true"/> for a single closing character.</returns>
        private readonly bool IsClosingMark(NormalizationToken token) =>
            token.Length == 1 && _text[token.Start] is ')' or ']' or '}' or '"' or '\'' or '\u201D' or '\u2019' or '\u00BB';
    }

    /// <summary>
    ///     Recognizes a clitic token by its spelling.
    /// </summary>
    /// <param name="span">The token's characters.</param>
    /// <param name="kind">
    ///     The role the spelling gives: a fixed one, <see cref="CliticKind.Is"/> for an <c>'s</c>
    ///     or <see cref="CliticKind.Would"/> for a <c>'d</c> (both refined by context later).
    /// </param>
    /// <param name="apostropheOffset">The offset of the apostrophe inside the token.</param>
    /// <returns><see langword="true"/> for <c>n't</c>, <c>'re</c>, <c>'ve</c>, <c>'ll</c>, <c>'m</c>, <c>'s</c> and <c>'d</c> in any apostrophe style.</returns>
    internal static bool TryGetClitic(ReadOnlySpan<char> span, out CliticKind kind, out int apostropheOffset)
    {
        kind = CliticKind.None;
        apostropheOffset = 0;

        // "n't": the apostrophe is the second character.
        if (span.Length == 3 && char.ToLowerInvariant(span[0]) == 'n' && IsApostrophe(span[1]) && char.ToLowerInvariant(span[2]) == 't')
        {
            kind = CliticKind.Not;
            apostropheOffset = 1;
            return true;
        }

        // Every other clitic starts with the apostrophe.
        if (span.Length is 2 or 3 && IsApostrophe(span[0]))
        {
            var rest = span[1..];
            kind = rest.Length == 1
                ? char.ToLowerInvariant(rest[0]) switch
                {
                    's' => CliticKind.Is,
                    'd' => CliticKind.Would,
                    'm' => CliticKind.Am,
                    _ => CliticKind.None,
                }
                : (char.ToLowerInvariant(rest[0]), char.ToLowerInvariant(rest[1])) switch
                {
                    ('r', 'e') => CliticKind.Are,
                    ('v', 'e') => CliticKind.Have,
                    ('l', 'l') => CliticKind.Will,
                    _ => CliticKind.None,
                };
            return kind != CliticKind.None;
        }

        return false;
    }

    /// <summary>
    ///     Tells whether a token consists of a lone <c>/</c>-separated pair of words such as
    ///     <c>he/she</c>: two to many ASCII letters, one slash, two to many ASCII letters.
    /// </summary>
    /// <param name="span">The token's characters.</param>
    /// <param name="slashIndex">The offset of the slash when this method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> for a word pair.</returns>
    internal static bool IsWordPair(ReadOnlySpan<char> span, out int slashIndex)
    {
        slashIndex = span.IndexOf('/');
        if (slashIndex < 2 || span.Length - slashIndex - 1 < 2)
        {
            return false;
        }

        for (var i = 0; i < span.Length; i++)
        {
            if (i != slashIndex && !(span[i] is >= 'A' and <= 'Z' or >= 'a' and <= 'z'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Gets the words an operator symbol is spoken as.
    /// </summary>
    /// <param name="character">The symbol.</param>
    /// <returns>The spoken words, or <see langword="null"/> when the character is not a handled operator.</returns>
    internal static string? OperatorWords(char character) => character switch
    {
        '&' => "and",
        '@' => "at",
        '+' => "plus",
        '=' => "equals",
        '<' => "less than",
        '>' => "greater than",
        '/' => "slash",
        '\\' => "backslash",
        '\u00D7' => "times",
        '\u00F7' => "divided by",
        '\u00B1' => "plus or minus",
        '\u2248' => "approximately equal to",
        '\u2260' => "not equal to",
        '\u2264' => "less than or equal to",
        '\u2265' => "greater than or equal to",
        _ => null,
    };

    /// <summary>
    ///     Gets the words a mark is spoken as.
    /// </summary>
    /// <param name="character">The mark.</param>
    /// <returns>The spoken words, or <see langword="null"/> when the character is not a handled mark.</returns>
    internal static string? MarkWords(char character) => character switch
    {
        '\u00A9' => "copyright",
        '\u00AE' => "registered",
        '\u2122' => "trademark",
        '\u00A7' => "section",
        '\u00B6' => "paragraph",
        _ => null,
    };

    /// <summary>
    ///     Tells whether a span contains a character that is spoken as nothing.
    /// </summary>
    /// <param name="span">The characters.</param>
    /// <returns><see langword="true"/> when any character is in the dropped set.</returns>
    internal static bool ContainsDropped(ReadOnlySpan<char> span)
    {
        foreach (var character in span)
        {
            if (IsDroppedCharacter(character))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Tells whether a character is an invisible format character that is spoken as nothing.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns>
    ///     <see langword="true"/> for the soft hyphen, zero-width space, word joiner, byte-order
    ///     mark, the left-to-right and right-to-left marks and the bidirectional embedding and
    ///     isolate controls. The zero-width joiners are not included because they glue emoji.
    /// </returns>
    internal static bool IsDroppedCharacter(char character) =>
        character is '\u00AD' or '\u200B' or '\u2060' or '\uFEFF' or '\u200E' or '\u200F'
        || character is >= '\u202A' and <= '\u202E'
        || character is >= '\u2066' and <= '\u2069';

    /// <summary>
    ///     Tells whether a token is a standalone bullet.
    /// </summary>
    /// <param name="span">The token's characters.</param>
    /// <returns><see langword="true"/> for a single bullet character.</returns>
    internal static bool IsBullet(ReadOnlySpan<char> span) =>
        span.Length == 1 && span[0] is '\u2022' or '\u2023' or '\u25E6' or '\u2043' or '\u25AA' or '\u25CF';

    /// <summary>
    ///     Tells whether a character is any apostrophe style.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for U+0027, U+2018, U+2019, U+201B and U+02BC.</returns>
    internal static bool IsApostrophe(char character) => character == '\'' || IsTypographicApostrophe(character);

    /// <summary>
    ///     Tells whether a character is the plain ASCII apostrophe.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for U+0027.</returns>
    internal static bool IsPlainApostrophe(char character) => character == '\'';

    /// <summary>
    ///     Tells whether a character is a typographic apostrophe.
    /// </summary>
    /// <param name="character">The character.</param>
    /// <returns><see langword="true"/> for U+2018, U+2019, U+201B and U+02BC.</returns>
    internal static bool IsTypographicApostrophe(char character) =>
        character is '\u2018' or '\u2019' or '\u201B' or '\u02BC';

    /// <summary>
    ///     Tells whether every character is a letter.
    /// </summary>
    /// <param name="span">The characters.</param>
    /// <returns><see langword="true"/> when all characters are letters.</returns>
    internal static bool IsAllLetters(ReadOnlySpan<char> span)
    {
        foreach (var character in span)
        {
            if (!char.IsLetter(character))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Tells whether every character is upper-case.
    /// </summary>
    /// <param name="span">The characters.</param>
    /// <returns><see langword="true"/> when all characters are upper-case.</returns>
    internal static bool IsAllUpper(ReadOnlySpan<char> span)
    {
        foreach (var character in span)
        {
            if (!char.IsUpper(character))
            {
                return false;
            }
        }

        return true;
    }
}
