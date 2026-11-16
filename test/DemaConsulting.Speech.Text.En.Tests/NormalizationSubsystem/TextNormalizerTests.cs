using DemaConsulting.Speech.Text.En.NormalizationSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.NormalizationSubsystem;

/// <summary>
///     Unit tests for <see cref="TextNormalizer"/>: symbols and marks, apostrophes and clitics,
///     dropped characters, the period and sentence-break rules, cancellation, argument validation
///     and determinism. Its collaborators (the abbreviation resolver, the acronym recognizer and
///     the alignment model) are real; tokens come from the real tokenizer through the harness.
/// </summary>
public sealed class TextNormalizerTests
{
    /// <summary>Normalizes a text and returns the spoken form.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with every normalization applied.</returns>
    private static string Spoken(string text) => NormalizationHarness.Normalize(text).ApplySpokenForms();

    /// <summary>Proves standalone operators are spoken, but only when whitespace sets them apart.</summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("a & b", "a and b")]
    [InlineData("a @ b", "a at b")]
    [InlineData("a + b", "a plus b")]
    [InlineData("a = b", "a equals b")]
    [InlineData("a < b", "a less than b")]
    [InlineData("a > b", "a greater than b")]
    [InlineData("a / b", "a slash b")]
    [InlineData("a \\ b", "a backslash b")]
    [InlineData("a \u00D7 b", "a times b")]
    [InlineData("a \u00F7 b", "a divided by b")]
    [InlineData("a \u00B1 b", "a plus or minus b")]
    [InlineData("a \u2248 b", "a approximately equal to b")]
    [InlineData("a \u2260 b", "a not equal to b")]
    [InlineData("a \u2264 b", "a less than or equal to b")]
    [InlineData("a \u2265 b", "a greater than or equal to b")]
    [InlineData("&", "and")]
    [InlineData("a&b", "a and b")]
    [InlineData("a=b", "a=b")]
    [InlineData("(+)", "(+)")]
    [InlineData("a +b", "a +b")]
    public void TextNormalizer_Normalize_Operators_AreSpokenOnlyWhenSetApart(string text, string spoken)
    {
        // Act / Assert
        Assert.Equal(spoken, Spoken(text));
    }

    /// <summary>Proves marks are spoken whether they stand alone or touch a word, with a separating space where a letter touches.</summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("\u00A9 2025", "copyright 2025")]
    [InlineData("Acme\u00AE rocks", "Acme registered rocks")]
    [InlineData("Brand\u2122 rocks", "Brand trademark rocks")]
    [InlineData("\u00A7 5", "section 5")]
    [InlineData("\u00B6 3", "paragraph 3")]
    [InlineData("A\u2122B", "A trademark B")]
    [InlineData("Brand\u2122&Co", "Brand trademark&Co")]
    [InlineData("A\u2122\u00AEB", "A trademark registered B")]
    [InlineData("A\u2122\u00AE", "A trademark registered")]
    [InlineData("\u2122\u00AE", "trademark registered")]
    [InlineData("\u00A9\u2122\u00AE", "copyright trademark registered")]
    [InlineData("\u00A7\u00A7 5", "section section 5")]
    [InlineData("x\u2122\u00A92025", "x trademark copyright 2025")]
    public void TextNormalizer_Normalize_Marks_AreSpoken(string text, string spoken)
    {
        // Act / Assert
        Assert.Equal(spoken, Spoken(text));
    }

    /// <summary>Proves adjacent word-producing replacements are separated by exactly one space, with the source offsets unchanged.</summary>
    [Fact]
    public void TextNormalizer_Normalize_AdjacentMarks_PadExactlyOneSpaceAndKeepOffsets()
    {
        // Act
        var aligned = NormalizationHarness.Normalize("A\u2122\u00AEB");

        // Assert: the later span pads its own left side; the offsets are those of the marks
        Assert.Equal([" trademark", " registered "], aligned.Spans.Select(span => span.Spoken));
        Assert.Equal([1, 2], aligned.Spans.Select(span => span.Start));
        Assert.Equal([1, 1], aligned.Spans.Select(span => span.Length));
        var spoken = aligned.ApplySpokenForms();
        Assert.Equal("A trademark registered B", spoken);
        Assert.DoesNotContain("  ", spoken, StringComparison.Ordinal);
    }

    /// <summary>Proves a name suffix directly before a credential consumes its period and keeps the name in one sentence, removing a tokenizer break between two credentials.</summary>
    [Fact]
    public void TextNormalizer_Normalize_NameSuffixBeforeCredential_ConsumesPeriodAndKeepsNameTogether()
    {
        // Arrange
        const string suffixAndCredential = "John Smith Jr. Ph.D. spoke.";
        const string twoCredentials = "Ann Lee Ph.D. M.D. spoke.";
        var tokenizerBreaks = NormalizationHarness.TokenizerBreaks(twoCredentials);

        // Act
        var first = NormalizationHarness.Normalize(suffixAndCredential);
        var second = NormalizationHarness.Normalize(twoCredentials);

        // Assert: "Jr." is consumed with its period and no break is forced after it
        Assert.Equal("Jr.", suffixAndCredential.Substring(first.Spans[0].Start, first.Spans[0].Length));
        Assert.Empty(first.ForcedBreaks);
        Assert.Empty(first.ReconcileBreaks(NormalizationHarness.TokenizerBreaks(suffixAndCredential)));

        // Assert: the tokenizer's break between the two credentials is removed
        Assert.NotEmpty(tokenizerBreaks);
        Assert.Equal(tokenizerBreaks, second.SuppressedBreaks);
        Assert.Empty(second.ReconcileBreaks(tokenizerBreaks));
    }

    /// <summary>Proves a name suffix before a word that starts a sentence still ends the sentence.</summary>
    [Fact]
    public void TextNormalizer_Normalize_NameSuffixBeforeSentenceStarter_StillForcesBreak()
    {
        // Arrange
        const string text = "I met John Smith Jr. He left.";

        // Act
        var aligned = NormalizationHarness.Normalize(text);

        // Assert: the sentence break falls before "He"
        Assert.Equal([text.IndexOf("He", StringComparison.Ordinal)], aligned.ReconcileBreaks(NormalizationHarness.TokenizerBreaks(text)));
    }

    /// <summary>Proves a slash between two plain words is spoken, and every other slash use is left alone.</summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("he/she", "he slash she")]
    [InlineData("input/output", "input slash output")]
    [InlineData("km/h", "km/h")]
    [InlineData("a/b", "a/b")]
    [InlineData("24/7", "24/7")]
    [InlineData("a/b/cc", "a/b/cc")]
    [InlineData("and/or", "and or")]
    [InlineData("w/o", "without")]
    [InlineData("he/sh3", "he/sh3")]
    public void TextNormalizer_Normalize_Slash_IsSpokenOnlyBetweenWords(string text, string spoken)
    {
        // Act / Assert
        Assert.Equal(spoken, Spoken(text));
    }

    /// <summary>Proves invisible characters are dropped explicitly (one span per run) and the joiners that glue emoji are kept.</summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    /// <param name="spans">The expected number of spans.</param>
    [Theory]
    [InlineData("a\u200Bb", "ab", 1)]
    [InlineData("a\u200B\u200B\u200Bb", "ab", 1)]
    [InlineData("a\u200Bb\u00ADc", "abc", 2)]
    [InlineData("\uFEFFHello", "Hello", 1)]
    [InlineData("x \u200B y", "x  y", 1)]
    [InlineData("\u2022 item", " item", 1)]
    [InlineData("\u202Ahi\u202C", "hi", 2)]
    [InlineData("a\u200Db", "a\u200Db", 0)]
    [InlineData("a\u200Cb", "a\u200Cb", 0)]
    public void TextNormalizer_Normalize_InvisibleCharacters_AreDroppedExplicitly(string text, string spoken, int spans)
    {
        // Act
        var aligned = NormalizationHarness.Normalize(text);

        // Assert
        Assert.Equal(spoken, aligned.ApplySpokenForms());
        Assert.Equal(spans, aligned.Spans.Count);
        Assert.All(aligned.Spans, span => Assert.Equal(NormalizationKind.Dropped, span.Kind));
    }

    /// <summary>Proves a token that contains an invisible character is otherwise left alone (no other rule applies to it).</summary>
    [Fact]
    public void TextNormalizer_Normalize_TokenWithInvisibleCharacter_GetsNoOtherRule()
    {
        // Act
        var aligned = NormalizationHarness.Normalize("AT&T\u200B");

        // Assert: only the dropped run, not the ampersand initialism
        Assert.Equal(NormalizationKind.Dropped, Assert.Single(aligned.Spans).Kind);
    }

    /// <summary>Proves typographic apostrophes are canonicalized inside clitics and elisions, and quote marks stay.</summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("it\u2019s", "it's")]
    [InlineData("don\u2019t", "don't")]
    [InlineData("we\u2019re we\u2019ve we\u2019ll I\u2019m I\u2019d", "we're we've we'll I'm I'd")]
    [InlineData("it\u2018s", "it's")]
    [InlineData("y\u2019all", "y'all")]
    [InlineData("o\u2019clock", "o'clock")]
    [InlineData("rock\u2019n\u2019roll", "rock'n'roll")]
    [InlineData("\u2019Tis the season", "'Tis the season")]
    [InlineData("wait \u2019til dawn", "wait 'til dawn")]
    [InlineData("tea \u2019cause", "tea 'cause")]
    [InlineData("dogs\u2019", "dogs\u2019")]
    [InlineData("\u2018quoted\u2019", "\u2018quoted\u2019")]
    [InlineData("it's don't", "it's don't")]
    public void TextNormalizer_Normalize_Apostrophes_AreCanonicalizedOnlyInsideWords(string text, string spoken)
    {
        // Act / Assert
        Assert.Equal(spoken, Spoken(text));
    }

    /// <summary>Proves clitics are classified from the neighboring words only.</summary>
    /// <param name="text">The text.</param>
    /// <param name="clitic">The apostrophe-bearing token.</param>
    /// <param name="expected">The expected role.</param>
    [Theory]
    [InlineData("I don't know", "n't", CliticKind.Not)]
    [InlineData("we're here", "'re", CliticKind.Are)]
    [InlineData("we've gone", "'ve", CliticKind.Have)]
    [InlineData("we'll go", "'ll", CliticKind.Will)]
    [InlineData("I'm here", "'m", CliticKind.Am)]
    [InlineData("I'd go", "'d", CliticKind.Would)]
    [InlineData("I'd better go", "'d", CliticKind.Had)]
    [InlineData("I'd never been", "'d", CliticKind.Had)]
    [InlineData("they'd gone", "'d", CliticKind.Had)]
    [InlineData("It's cold", "'s", CliticKind.Is)]
    [InlineData("It's been cold", "'s", CliticKind.Has)]
    [InlineData("He's never been", "'s", CliticKind.Has)]
    [InlineData("Let's go", "'s", CliticKind.Us)]
    [InlineData("What's up", "'s", CliticKind.Is)]
    [InlineData("Nobody's home", "'s", CliticKind.Is)]
    [InlineData("Who's been here", "'s", CliticKind.Has)]
    [InlineData("John's book", "'s", CliticKind.Possessive)]
    [InlineData("The company's new plan", "'s", CliticKind.Possessive)]
    [InlineData("John's going home", "'s", CliticKind.Is)]
    [InlineData("The dog's barking loudly", "'s", CliticKind.Is)]
    [InlineData("John's been here", "'s", CliticKind.Has)]
    [InlineData("John's not here", "'s", CliticKind.Is)]
    [InlineData("It\u2019s cold", "\u2019s", CliticKind.Is)]
    public void TextNormalizer_Normalize_Clitics_AreClassifiedFromContext(string text, string clitic, CliticKind expected)
    {
        // Arrange
        var tokens = NormalizationHarness.Tokenize(text);

        // Act
        var aligned = NormalizationHarness.Normalize(text, classifyClitics: true);

        // Assert: exactly one clitic is marked, it is the expected token, and its role is the expected one
        var mark = Assert.Single(aligned.Clitics);
        Assert.Equal(clitic, text.Substring(mark.Start, mark.Length));
        Assert.Equal(expected, mark.Kind);
        Assert.Contains(tokens, token => token.Start == mark.Start && token.Length == mark.Length);
    }

    /// <summary>Proves clitics are not classified unless requested, while apostrophes are still canonicalized.</summary>
    [Fact]
    public void TextNormalizer_Normalize_CliticsNotRequested_RecordsNoCliticsButStillCanonicalizes()
    {
        // Act
        var aligned = NormalizationHarness.Normalize("It\u2019s cold and I don\u2019t mind", classifyClitics: false);

        // Assert
        Assert.Empty(aligned.Clitics);
        Assert.Equal("It's cold and I don't mind", aligned.ApplySpokenForms());
    }

    /// <summary>Proves a sentence-final suffix keeps its period outside the span and a mid-sentence one consumes it, for both token shapes.</summary>
    [Fact]
    public void TextNormalizer_Normalize_SuffixPeriod_IsKeptAtSentenceEndAndConsumedMidSentence()
    {
        // Arrange
        const string ending = "I live on Main St. It is nice.";
        const string middle = "Elm St. is closed.";
        const string separate = "Walk down Elm Ave. today.";
        const string separateEnding = "We met on Sunset Blvd. Everyone came.";

        // Act
        var kept = NormalizationHarness.Normalize(ending).Spans.Single();
        var consumed = NormalizationHarness.Normalize(middle).Spans.Single();
        var separateConsumed = NormalizationHarness.Normalize(separate).Spans.Single();
        var separateKept = NormalizationHarness.Normalize(separateEnding).Spans.Single();

        // Assert: spans name the exact source characters
        Assert.Equal("St", ending.Substring(kept.Start, kept.Length));
        Assert.Equal("St.", middle.Substring(consumed.Start, consumed.Length));
        Assert.Equal("Ave.", separate.Substring(separateConsumed.Start, separateConsumed.Length));
        Assert.Equal("Blvd", separateEnding.Substring(separateKept.Start, separateKept.Length));
    }

    /// <summary>Proves a prefix title always consumes its period and removes a tokenizer break after it.</summary>
    [Fact]
    public void TextNormalizer_Normalize_PrefixTitle_ConsumesPeriodAndSuppressesTokenizerBreak()
    {
        // Arrange: the tokenizer splits "Sen" "." and breaks before "Smith"
        const string text = "Sen. Smith spoke.";
        var tokenizerBreaks = NormalizationHarness.TokenizerBreaks(text);

        // Act
        var aligned = NormalizationHarness.Normalize(text);

        // Assert
        Assert.Equal("Sen.", text.Substring(aligned.Spans[0].Start, aligned.Spans[0].Length));
        Assert.Equal(tokenizerBreaks, aligned.SuppressedBreaks);
        Assert.Empty(aligned.ReconcileBreaks(tokenizerBreaks));
    }

    /// <summary>Proves a suffix forces a break before a capitalized next word only when none exists, and never before a lower-case or digit word.</summary>
    [Fact]
    public void TextNormalizer_Normalize_SuffixBeforeNextWord_ForcesBreakOnlyBeforeCapitalizedWord()
    {
        // Act
        var capital = NormalizationHarness.Normalize("Maple Dr. He likes it.");
        var lower = NormalizationHarness.Normalize("Maple Dr. is nice.");
        var digit = NormalizationHarness.Normalize("Maple Dr. 5 times.");

        // Assert
        Assert.Equal([10], capital.ForcedBreaks);
        Assert.Empty(lower.ForcedBreaks);
        Assert.Empty(digit.ForcedBreaks);
    }

    /// <summary>Proves the word reading of <c>No.</c> adds a break but no span.</summary>
    [Fact]
    public void TextNormalizer_Normalize_WordReading_ForcesBreakWithoutSpan()
    {
        // Act
        var aligned = NormalizationHarness.Normalize("No. I don't know.");

        // Assert
        Assert.Empty(aligned.Spans);
        Assert.Equal([4], aligned.ForcedBreaks);
    }

    /// <summary>Proves the ambiguity counter counts defaults and competing cues once each and not clear readings.</summary>
    [Fact]
    public void TextNormalizer_Normalize_AmbiguousResolutions_AreCountedOnce()
    {
        // Act / Assert
        Assert.Equal(0, NormalizationHarness.Normalize("Dr. Smith lives on Main St. It is nice.").AmbiguousCount);
        Assert.Equal(1, NormalizationHarness.Normalize("They St. said so.").AmbiguousCount);
        Assert.Equal(2, NormalizationHarness.Normalize("They St. said so. They Dr. said so.").AmbiguousCount);
    }

    /// <summary>Proves the shouting guard applies per sentence: a capitalized initialism in a normal sentence is spelled, in an all-capital sentence it is not.</summary>
    [Fact]
    public void TextNormalizer_Normalize_AllCapitalSentence_SuspendsAmbiguousInitialisms()
    {
        // Act / Assert
        Assert.Equal("The U S team won. LET US GO NOW.", Spoken("The US team won. LET US GO NOW."));
    }

    /// <summary>Proves a dotted initialism without its own period needs the separate period token, and consumes or keeps it by the sentence rule.</summary>
    [Fact]
    public void TextNormalizer_Normalize_DottedInitialismWithSeparatePeriod_IsHandled()
    {
        // Act
        var aligned = NormalizationHarness.Normalize("Meet at 5 p.m. Then sleep.");

        // Assert
        var span = Assert.Single(aligned.Spans);
        Assert.Equal(NormalizationKind.Initialism, span.Kind);
        Assert.Equal("P M", span.Spoken);
    }

    /// <summary>Proves a dotted form with no period token after it is left alone.</summary>
    [Fact]
    public void TextNormalizer_Normalize_DottedFormWithoutPeriodToken_IsIdentity()
    {
        // Act
        var aligned = NormalizationHarness.Normalize("the x.y thing");

        // Assert
        Assert.Empty(aligned.Spans);
    }

    /// <summary>Proves a pre-canceled token throws before any work.</summary>
    [Fact]
    public void TextNormalizer_Normalize_PreCanceledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        const string text = "Dr. Smith";
        var tokens = NormalizationHarness.Tokenize(text);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => TextNormalizer.Default.Normalize(text, tokens, false, cancellation.Token));
    }

    /// <summary>Proves cancellation requested mid-pass (by the token-visited observer) is observed at the next periodic check.</summary>
    [Fact]
    public void TextNormalizer_Normalize_CanceledMidPass_ThrowsOperationCanceledException()
    {
        // Arrange: 1,000 one-letter tokens; the observer cancels while token 300 is visited
        var text = string.Concat(Enumerable.Repeat("a ", 1000));
        var tokens = Enumerable.Range(0, 1000).Select(i => new NormalizationToken(i * 2, 1, i == 0)).ToArray();
        using var cancellation = new CancellationTokenSource();
        var visited = 0;
        var normalizer = new TextNormalizer(
            AbbreviationResolver.Default,
            AcronymRecognizer.Default,
            index =>
            {
                visited++;
                if (index == 300)
                {
                    cancellation.Cancel();
                }
            });

        // Act / Assert: the pass stops at the next checkpoint instead of visiting every token
        Assert.Throws<OperationCanceledException>(() => normalizer.Normalize(text, tokens, false, cancellation.Token));
        Assert.InRange(visited, 301, 600);
    }

    /// <summary>Proves the token-visited observer is called once per processed token, in order.</summary>
    [Fact]
    public void TextNormalizer_Normalize_Observer_SeesEveryTokenInOrder()
    {
        // Arrange
        const string text = "a b c";
        NormalizationToken[] tokens = [new(0, 1, true), new(2, 1, false), new(4, 1, false)];
        var seen = new List<int>();
        var normalizer = new TextNormalizer(AbbreviationResolver.Default, AcronymRecognizer.Default, seen.Add);

        // Act
        normalizer.Normalize(text, tokens, false, CancellationToken.None);

        // Assert
        Assert.Equal([0, 1, 2], seen);
    }

    /// <summary>Proves bad arguments are rejected: null text, null collaborators, and malformed token lists.</summary>
    [Fact]
    public void TextNormalizer_NormalizeAndConstructor_InvalidArguments_Throw()
    {
        // Arrange
        const string text = "abc def";

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => new TextNormalizer(null!, AcronymRecognizer.Default));
        Assert.Throws<ArgumentNullException>(() => new TextNormalizer(AbbreviationResolver.Default, null!));
        Assert.Throws<ArgumentNullException>(() => TextNormalizer.Default.Normalize(null!, [], false, CancellationToken.None));
        Assert.Throws<ArgumentException>(() => TextNormalizer.Default.Normalize(text, [new NormalizationToken(0, 0, true)], false, CancellationToken.None));
        Assert.Throws<ArgumentException>(() => TextNormalizer.Default.Normalize(text, [new NormalizationToken(0, 5, true), new NormalizationToken(3, 2, false)], false, CancellationToken.None));
        Assert.Throws<ArgumentException>(() => TextNormalizer.Default.Normalize(text, [new NormalizationToken(5, 5, true)], false, CancellationToken.None));
    }

    /// <summary>Proves a mark touching a supplementary-plane letter (a surrogate pair) is padded exactly as it is next to a BMP letter, on each side.</summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("\uD835\uDD38\u2122\uD835\uDD39", "\uD835\uDD38 trademark \uD835\uDD39")]
    [InlineData("\uD835\uDD38\u2122", "\uD835\uDD38 trademark")]
    [InlineData("\u2122\uD835\uDD39", "trademark \uD835\uDD39")]
    [InlineData("\uD835\uDD38\u2122\u00AE\uD835\uDD39", "\uD835\uDD38 trademark registered \uD835\uDD39")]
    [InlineData("\uD835\uDFD8\u2122\uD835\uDFD9", "\uD835\uDFD8 trademark \uD835\uDFD9")]
    public void TextNormalizer_Normalize_MarkNextToSupplementaryLetter_IsPaddedOnTheTouchingSide(string text, string spoken)
    {
        // Act / Assert
        Assert.Equal(spoken, Spoken(text));
    }

    /// <summary>Proves an ampersand or typographic apostrophe between two supplementary-plane letters is treated as between letters.</summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("\uD835\uDD38&\uD835\uDD39", "\uD835\uDD38 and \uD835\uDD39")]
    [InlineData("\uD835\uDD38\u2019\uD835\uDD39", "\uD835\uDD38'\uD835\uDD39")]
    public void TextNormalizer_Normalize_SeparatorBetweenSupplementaryLetters_IsRecognizedAsBetweenLetters(string text, string spoken)
    {
        // Act / Assert
        Assert.Equal(spoken, Spoken(text));
    }

    /// <summary>Proves a token whose end would overflow, or that starts past the text, is rejected with the documented ArgumentException rather than passing the range check.</summary>
    /// <param name="start">The token's start.</param>
    /// <param name="length">The token's length.</param>
    [Theory]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(1, int.MaxValue)]
    [InlineData(7, 1)]
    [InlineData(8, 1)]
    [InlineData(-1, 2)]
    public void TextNormalizer_Normalize_TokenRangeOverflowingOrOutsideText_ThrowsArgumentException(int start, int length)
    {
        // Arrange: a seven-character text and one malformed token
        const string text = "abc def";
        var tokens = new[] { new NormalizationToken(start, length, true) };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => TextNormalizer.Default.Normalize(text, tokens, false, CancellationToken.None));
    }

    /// <summary>Proves a malformed token that follows a valid one is rejected even when the first token's end is the previous end.</summary>
    [Fact]
    public void TextNormalizer_Normalize_SecondTokenOverflowing_ThrowsArgumentException()
    {
        // Arrange
        const string text = "abc def";
        var tokens = new[] { new NormalizationToken(0, 3, true), new NormalizationToken(int.MaxValue - 1, 4, false) };

        // Act / Assert
        Assert.Throws<ArgumentException>(() => TextNormalizer.Default.Normalize(text, tokens, false, CancellationToken.None));
    }

    /// <summary>Proves an empty text and an empty token list normalize to nothing.</summary>
    [Fact]
    public void TextNormalizer_Normalize_EmptyTextAndTokens_ProducesNothing()
    {
        // Act
        var aligned = TextNormalizer.Default.Normalize(string.Empty, [], true, CancellationToken.None);

        // Assert
        Assert.Empty(aligned.Spans);
        Assert.Empty(aligned.Clitics);
        Assert.Equal(0, aligned.AmbiguousCount);
    }

    /// <summary>Proves repeated normalization of one text is deterministic, and a custom lexicon changes the outcome.</summary>
    [Fact]
    public void TextNormalizer_Normalize_RepeatedAndCustomLexicons_AreDeterministic()
    {
        // Arrange
        const string text = "Dr. Smith & Mr. Jones live on Main St. The FBI said it\u2019s w/o proof.";
        var custom = new TextNormalizer(
            AbbreviationResolver.FromTsv(new StringReader("form\tspoken\tclass\tdefault\tguard\tnote\nDr.\tDoc\tprefix\tyes\t-\tn\n")),
            AcronymRecognizer.FromTsv(new StringReader("form\tspoken\tambiguous\n")));

        // Act
        var first = NormalizationHarness.Normalize(text).ApplySpokenForms();
        var second = NormalizationHarness.Normalize(text).ApplySpokenForms();
        var neutralized = NormalizationHarness.Neutralize(text);
        var customSpoken = custom.Normalize(neutralized, NormalizationHarness.Tokenize(neutralized), false, CancellationToken.None).ApplySpokenForms();

        // Assert
        Assert.Equal(first, second);
        Assert.StartsWith("Doctor Smith and Mister Jones live on Main Street.", first, StringComparison.Ordinal);
        Assert.StartsWith("Doc Smith and Mr. Jones live on Main St. The FBI said it's w/o proof.", customSpoken, StringComparison.Ordinal);
    }
}
