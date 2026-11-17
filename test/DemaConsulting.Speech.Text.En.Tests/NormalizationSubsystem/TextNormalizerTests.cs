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
    [InlineData("\u00A9 2025", "copyright twenty twenty five")]
    [InlineData("Acme\u00AE rocks", "Acme registered rocks")]
    [InlineData("Brand\u2122 rocks", "Brand trademark rocks")]
    [InlineData("\u00A7 5", "section five")]
    [InlineData("\u00B6 3", "paragraph three")]
    [InlineData("A\u2122B", "A trademark B")]
    [InlineData("Brand\u2122&Co", "Brand trademark&Co")]
    [InlineData("A\u2122\u00AEB", "A trademark registered B")]
    [InlineData("A\u2122\u00AE", "A trademark registered")]
    [InlineData("\u2122\u00AE", "trademark registered")]
    [InlineData("\u00A9\u2122\u00AE", "copyright trademark registered")]
    [InlineData("\u00A7\u00A7 5", "section section five")]
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

        // Assert: the hour is read by the numeric rule and the dotted form by the initialism rule
        Assert.Equal(2, aligned.Spans.Count);
        Assert.Equal(NormalizationKind.Number, aligned.Spans[0].Kind);
        Assert.Equal("five", aligned.Spans[0].Spoken);
        var span = aligned.Spans[1];
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

    /// <summary>Proves the periodic cancellation check counts the tokens a numeric match consumed: repeated two-token expressions are checked within 256 visited tokens.</summary>
    [Fact]
    public void TextNormalizer_Normalize_RepeatedMultiTokenMatches_CheckCancellationEvery256VisitedTokens()
    {
        // Arrange: 400 "5 kg" matches, 800 tokens; the observer cancels while the first token is visited
        var text = string.Concat(Enumerable.Repeat("5 kg ", 400));
        var tokens = NormalizationHarness.Tokenize(text);
        using var cancellation = new CancellationTokenSource();
        var seen = new List<int>();
        var normalizer = new TextNormalizer(
            AbbreviationResolver.Default,
            AcronymRecognizer.Default,
            index =>
            {
                seen.Add(index);
                cancellation.Cancel();
            });

        // Act / Assert: the check fires at the 256th visited token (inside the 128th match), not at the 256th match
        Assert.Equal(800, tokens.Length);
        Assert.Throws<OperationCanceledException>(() => normalizer.Normalize(text, tokens, false, cancellation.Token));
        Assert.Equal(TextNormalizer.CancellationCheckInterval / 2, seen.Count);
        Assert.True(seen[^1] < TextNormalizer.CancellationCheckInterval);
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

    /// <summary>Proves a numeric token is recognized before the abbreviation lexicon: <c>5 ft.</c> is a unit while <c>Ft. Worth</c> is still Fort, and <c>No. 5</c> reads both.</summary>
    [Fact]
    public void TextNormalizer_Normalize_NumericTokens_AreRecognizedBeforeAbbreviations()
    {
        // Act
        var unit = NormalizationHarness.Normalize("He is 5 ft. tall.");
        var fort = NormalizationHarness.Normalize("Ft. Worth is far.");
        var reference = NormalizationHarness.Normalize("Item No. 5 shipped.");

        // Assert: the unit rule owns "ft." after a number; the lexicon owns it before a name; both rules fire in a reference
        Assert.Equal("He is five feet tall.", unit.ApplySpokenForms());
        Assert.Equal([NormalizationKind.Number, NormalizationKind.Unit], unit.Spans.Select(span => span.Kind));
        Assert.Equal("Fort Worth is far.", fort.ApplySpokenForms());
        Assert.Equal(NormalizationKind.Abbreviation, Assert.Single(fort.Spans).Kind);
        Assert.Equal("Item Number five shipped.", reference.ApplySpokenForms());
        Assert.Equal([NormalizationKind.Abbreviation, NormalizationKind.Number], reference.Spans.Select(span => span.Kind));
    }

    /// <summary>Proves a token consumed by a numeric match (the unit after a number, both tokens of a currency pair) is not visited again.</summary>
    [Fact]
    public void TextNormalizer_Normalize_ConsumedFollowUpToken_IsNotVisitedAgain()
    {
        // Arrange: "$5 million" is three tokens and one match; "3 kg" two tokens and one match; "a" is visited on its own
        const string text = "$5 million a 3 kg";
        var tokens = NormalizationHarness.Tokenize(text);
        var seen = new List<int>();
        var normalizer = new TextNormalizer(AbbreviationResolver.Default, AcronymRecognizer.Default, seen.Add);

        // Act
        var aligned = normalizer.Normalize(text, tokens, false, CancellationToken.None);

        // Assert: tokens 0 ($, 5, million), 3 (a) and 4 (3, kg) are visited; the consumed ones are skipped
        Assert.Equal(6, tokens.Length);
        Assert.Equal([0, 3, 4], seen);
        Assert.Equal("five million dollars a three kilograms", aligned.ApplySpokenForms());
    }

    /// <summary>Proves a numeric-looking token that contains an invisible character is left to the invisible-character rule: only a dropped span, no number.</summary>
    [Fact]
    public void TextNormalizer_Normalize_NumericToken_WithInvisibleCharacter_StaysIdentity()
    {
        // Act
        var aligned = NormalizationHarness.Normalize("5\u200B0 and $\u200B5 and 1,\u00AD234");

        // Assert: only the invisible characters are dropped, and nothing is read as a number
        Assert.All(aligned.Spans, span => Assert.Equal(NormalizationKind.Dropped, span.Kind));
        Assert.Equal("50 and $5 and 1,234", aligned.ApplySpokenForms());
    }

    /// <summary>Proves two adjacent numeric spans are separated by exactly one space, with the source offsets unchanged.</summary>
    [Fact]
    public void TextNormalizer_Normalize_AdjacentNumericSpans_PadExactlyOneSpace()
    {
        // Act
        var percent = NormalizationHarness.Normalize("50%");
        var range = NormalizationHarness.Normalize("5\u201310");

        // Assert: "fifty" and " percent"; "five", " to " and "ten"
        Assert.Equal(["fifty", " percent"], percent.Spans.Select(span => span.Spoken));
        Assert.Equal([(0, 2), (2, 1)], percent.Spans.Select(span => (span.Start, span.Length)));
        Assert.Equal("fifty percent", percent.ApplySpokenForms());
        Assert.Equal(["five", " to ", "ten"], range.Spans.Select(span => span.Spoken));
        Assert.Equal("five to ten", range.ApplySpokenForms());
    }

    /// <summary>Proves a unit with an abbreviation period consumes the period mid-sentence and keeps it, with a forced break, where a capitalized word follows or the text ends.</summary>
    [Fact]
    public void TextNormalizer_Normalize_UnitWithPeriod_ConsumesMidSentenceAndKeepsAtSentenceEnd()
    {
        // Act
        var middle = NormalizationHarness.Normalize("He is 6 ft. tall.");
        var end = NormalizationHarness.Normalize("He is 6 ft.");
        var capital = NormalizationHarness.Normalize("He is 6 ft. Then left.");
        var separate = NormalizationHarness.Normalize("It weighs 180 lbs. now.");
        var separateEnd = NormalizationHarness.Normalize("It weighs 180 lbs.");
        var glued = NormalizationHarness.Normalize("He is 6ft. tall.");

        // Assert: consumed, kept, kept with a forced break
        Assert.Equal("He is six feet tall.", middle.ApplySpokenForms());
        Assert.Equal("He is six feet.", end.ApplySpokenForms());
        Assert.Equal("He is six feet. Then left.", capital.ApplySpokenForms());
        Assert.Equal([capital.Source.IndexOf("Then", StringComparison.Ordinal)], capital.ForcedBreaks);
        Assert.Empty(middle.ForcedBreaks);
        Assert.Equal("It weighs one hundred eighty pounds now.", separate.ApplySpokenForms());
        Assert.Equal("It weighs one hundred eighty pounds.", separateEnd.ApplySpokenForms());
        Assert.Equal("He is six feet tall.", glued.ApplySpokenForms());
    }

    /// <summary>Proves cancellation requested in the middle of a long numeric text is observed at the next periodic check.</summary>
    [Fact]
    public void TextNormalizer_Normalize_CanceledDuringNumericPass_ThrowsOperationCanceledException()
    {
        // Arrange: 1,000 one-digit numbers; the observer cancels while token 300 is visited
        var text = string.Concat(Enumerable.Repeat("5 ", 1000));
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

        // Act / Assert: the numeric pass checks the token like every other pass
        Assert.Throws<OperationCanceledException>(() => normalizer.Normalize(text, tokens, false, cancellation.Token));
        Assert.InRange(visited, 301, 600);
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

    // ---------------------------------------------------------------- user directives and spelled spans

    /// <summary>Finds the index of the first token whose characters equal a word.</summary>
    /// <param name="text">The (neutralized) text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="word">The word.</param>
    /// <returns>The token index.</returns>
    private static int IndexOf(string text, NormalizationToken[] tokens, string word)
    {
        for (var i = 0; i < tokens.Length; i++)
        {
            if (tokens[i].In(text).SequenceEqual(word))
            {
                return i;
            }
        }

        throw new InvalidOperationException($"No token '{word}'.");
    }

    /// <summary>Normalizes a text with one directive on the first token equal to a word.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The word whose token gets the directive.</param>
    /// <param name="kind">The directive kind.</param>
    /// <param name="value">The directive value.</param>
    /// <returns>The aligned result.</returns>
    private static AlignedText WithDirective(string text, string word, TokenDirectiveKind kind, string? value = null)
    {
        var tokens = NormalizationHarness.Tokenize(text);
        return NormalizationHarness.Normalize(text, false, [new TokenDirective(IndexOf(text, tokens, word), kind, value)]);
    }

    /// <summary>Proves a skip directive emits nothing for its token and is counted as applied.</summary>
    [Fact]
    public void TextNormalizer_Normalize_SkipDirective_EmitsNothingAndCountsApplied()
    {
        // Act
        var aligned = WithDirective("US troops", "US", TokenDirectiveKind.Skip);

        // Assert
        Assert.Empty(aligned.Spans);
        Assert.Empty(aligned.Pronunciations);
        Assert.Equal(1, aligned.OverridesApplied);
        Assert.Equal(0, aligned.OverridesIgnored);
        Assert.Equal("US troops", aligned.ApplySpokenForms());
    }

    /// <summary>Proves a reading directive speaks the token as written (like a skip), bypasses the built-in rules for it, and is counted as applied.</summary>
    [Fact]
    public void TextNormalizer_Normalize_ReadingDirective_SpokenAsWrittenAndCountsApplied()
    {
        // Act
        var aligned = WithDirective("US troops", "US", TokenDirectiveKind.Reading, "record.noun");

        // Assert: the built-in initialism rule would have spelled "US"; the directive leaves it, emitting nothing
        Assert.Empty(aligned.Spans);
        Assert.Empty(aligned.Pronunciations);
        Assert.Equal(1, aligned.OverridesApplied);
        Assert.Equal(0, aligned.OverridesIgnored);
        Assert.Equal("US troops", aligned.ApplySpokenForms());
    }

    /// <summary>Proves a replace directive emits one override span with the value, padded with exactly one space where a previous spoken letter touches it.</summary>
    [Fact]
    public void TextNormalizer_Normalize_ReplaceDirective_EmitsOverrideSpanWithSinglePadding()
    {
        // Arrange: the second text has a mark directly before the word, as two tokens
        const string touchingText = "\u2122Nginx";
        NormalizationToken[] touchingTokens = [new(0, 1, true), new(1, 5, false)];

        // Act
        var plain = WithDirective("use Nginx now", "Nginx", TokenDirectiveKind.Replace, "engine ex");
        var touching = TextNormalizer.Default.Normalize(touchingText, touchingTokens, false, CancellationToken.None, [new TokenDirective(1, TokenDirectiveKind.Replace, "engine ex")]);

        // Assert
        var span = Assert.Single(plain.Spans);
        Assert.Equal(new NormalizedSpan(4, 5, "engine ex", NormalizationKind.Override), span);
        Assert.False(span.Spelled);
        Assert.Equal(["trademark ", "engine ex"], touching.Spans.Select(item => item.Spoken));
        Assert.Equal("trademark engine ex", touching.ApplySpokenForms());
        Assert.DoesNotContain("  ", touching.ApplySpokenForms(), StringComparison.Ordinal);
    }

    /// <summary>Proves a spell directive spells the token's letters in capitals as a spelled override span, and a token that is not ASCII letters is left as written and counted as ignored.</summary>
    [Fact]
    public void TextNormalizer_Normalize_SpellDirective_SpellsAsciiLettersOnly()
    {
        // Arrange: the padded case has a mark directly before the word, as two tokens
        NormalizationToken[] touchingTokens = [new(0, 1, true), new(1, 3, false)];

        // Act
        var spelled = WithDirective("a gif b", "gif", TokenDirectiveKind.Spell);
        var padded = TextNormalizer.Default.Normalize("\u2122gif", touchingTokens, false, CancellationToken.None, [new TokenDirective(1, TokenDirectiveKind.Spell, null)]);
        var notLetters = WithDirective("a caf\u00E9 b", "caf\u00E9", TokenDirectiveKind.Spell);

        // Assert
        Assert.Equal(new NormalizedSpan(2, 3, "G I F", NormalizationKind.Override, true), Assert.Single(spelled.Spans));
        Assert.Equal(1, spelled.OverridesApplied);
        Assert.Equal(["trademark ", "G I F"], padded.Spans.Select(item => item.Spoken));
        Assert.True(padded.Spans[^1].Spelled);
        Assert.Equal("trademark G I F", padded.ApplySpokenForms());
        Assert.Empty(notLetters.Spans);
        Assert.Equal(0, notLetters.OverridesApplied);
        Assert.Equal(1, notLetters.OverridesIgnored);
    }

    /// <summary>Proves a pronounce directive attaches a pronunciation mark to the exact token, emits no span and leaves the spoken text unchanged.</summary>
    [Fact]
    public void TextNormalizer_Normalize_PronounceDirective_AddsMarkWithoutSpan()
    {
        // Act
        var aligned = WithDirective("food Polish food", "Polish", TokenDirectiveKind.Pronounce, "ipa:abc");

        // Assert
        Assert.Empty(aligned.Spans);
        Assert.Equal(new PronunciationMark(5, 6, "ipa:abc"), Assert.Single(aligned.Pronunciations));
        Assert.Equal("food Polish food", aligned.ApplySpokenForms());
        Assert.Equal(1, aligned.OverridesApplied);
    }

    /// <summary>Proves a directive wins over every built-in rule for its token: clitic stem, symbol-bearing token, lexicon abbreviation, listed acronym, vowel-less rule, elision and ampersand initialism.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The token that gets a skip directive.</param>
    [Theory]
    [InlineData("NASA's launch", "NASA")]
    [InlineData("he/she left", "he/she")]
    [InlineData("a\u00A9b", "a\u00A9b")]
    [InlineData("Mr Smith left", "Mr")]
    [InlineData("US troops", "US")]
    [InlineData("The CIA left", "CIA")]
    [InlineData("a SQL query", "SQL")]
    [InlineData("hello y\u2019all", "y\u2019all")]
    [InlineData("the AT&T network", "AT&T")]
    [InlineData("the AT&T's network", "AT&T's")]
    [InlineData("I met J. Smith", "J.")]
    public void TextNormalizer_Normalize_DirectiveOnBuiltInToken_BeatsEveryBuiltInRule(string text, string word)
    {
        // Arrange
        var neutralized = NormalizationHarness.Neutralize(text);
        var tokens = NormalizationHarness.Tokenize(neutralized);
        var index = IndexOf(neutralized, tokens, word);
        var builtIn = NormalizationHarness.Normalize(text);

        // Act
        var skipped = NormalizationHarness.Normalize(text, false, [new TokenDirective(index, TokenDirectiveKind.Skip, null)]);
        var replaced = NormalizationHarness.Normalize(text, false, [new TokenDirective(index, TokenDirectiveKind.Replace, "other words")]);

        // Assert: the built-in rule fired without the directive (or the word was a clitic stem and nothing was spoken differently), and never with it
        Assert.DoesNotContain(skipped.Spans, span => span.Start >= tokens[index].Start && span.End <= tokens[index].End);
        Assert.Equal(1, skipped.OverridesApplied);
        var overrideSpan = Assert.Single(replaced.Spans, span => span.Kind == NormalizationKind.Override);
        Assert.Equal(tokens[index].Start, overrideSpan.Start);
        Assert.Equal(tokens[index].Length, overrideSpan.Length);
        Assert.DoesNotContain(replaced.Spans, span => span.Kind != NormalizationKind.Override && span.Start >= tokens[index].Start && span.End <= tokens[index].End);
        Assert.True(builtIn.Spans.Count > 0 || word == "NASA");
    }

    /// <summary>Proves a directive on a token that an earlier multi-token match consumed (a unit, a magnitude, a meridiem, at the end of the text or in the middle) is ignored and counted, and the match is unchanged.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The consumed token that gets a directive.</param>
    [Theory]
    [InlineData("5 kg of rice", "kg")]
    [InlineData("rice 5 kg", "kg")]
    [InlineData("$5 million", "million")]
    [InlineData("at 5 pm today", "pm")]
    public void TextNormalizer_Normalize_DirectiveOnConsumedToken_IsIgnoredAndCounted(string text, string word)
    {
        // Arrange
        var neutralized = NormalizationHarness.Neutralize(text);
        var tokens = NormalizationHarness.Tokenize(neutralized);
        var baseline = NormalizationHarness.Normalize(text);

        // Act
        var aligned = NormalizationHarness.Normalize(text, false, [new TokenDirective(IndexOf(neutralized, tokens, word), TokenDirectiveKind.Replace, "other words")]);

        // Assert
        Assert.Equal(baseline.Spans, aligned.Spans);
        Assert.Equal(baseline.ApplySpokenForms(), aligned.ApplySpokenForms());
        Assert.Equal(0, aligned.OverridesApplied);
        Assert.Equal(1, aligned.OverridesIgnored);
    }

    /// <summary>Proves several directives are all counted correctly in one pass: applied, ignored, and applied again after an ignored one.</summary>
    [Fact]
    public void TextNormalizer_Normalize_MixedDirectives_CountAppliedAndIgnoredSeparately()
    {
        // Arrange
        const string text = "US 5 kg FBI and $3 million";
        var tokens = NormalizationHarness.Tokenize(text);
        TokenDirective[] directives =
        [
            new(IndexOf(text, tokens, "US"), TokenDirectiveKind.Skip, null),
            new(IndexOf(text, tokens, "kg"), TokenDirectiveKind.Skip, null),
            new(IndexOf(text, tokens, "FBI"), TokenDirectiveKind.Skip, null),
            new(IndexOf(text, tokens, "million"), TokenDirectiveKind.Skip, null),
        ];

        // Act
        var aligned = NormalizationHarness.Normalize(text, false, directives);

        // Assert
        Assert.Equal(2, aligned.OverridesApplied);
        Assert.Equal(2, aligned.OverridesIgnored);
    }

    /// <summary>Proves directives that are out of order, duplicated, negative or name a token that does not exist are rejected with an ArgumentException, without arithmetic on the index.</summary>
    /// <param name="first">The first directive's token index.</param>
    /// <param name="second">The second directive's token index.</param>
    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(0, 2)]
    [InlineData(0, 3)]
    [InlineData(-1, 0)]
    [InlineData(0, int.MaxValue)]
    [InlineData(int.MinValue, 0)]
    [InlineData(2, int.MinValue)]
    public void TextNormalizer_Normalize_InvalidDirectiveIndices_ThrowArgumentException(int first, int second)
    {
        // Arrange: a text of exactly two tokens
        const string text = "ab cd";
        var tokens = NormalizationHarness.Tokenize(text);
        Assert.Equal(2, tokens.Length);
        TokenDirective[] directives = [new(first, TokenDirectiveKind.Skip, null), new(second, TokenDirectiveKind.Skip, null)];

        // Act / Assert
        Assert.Throws<ArgumentException>(() => TextNormalizer.Default.Normalize(text, tokens, false, CancellationToken.None, directives));
    }

    /// <summary>Proves a pre-canceled request with directives still throws before any work, and directives do not disturb the cancellation cadence: every visited token counts.</summary>
    [Fact]
    public void TextNormalizer_Normalize_DirectivesWithCancellation_CountEveryVisitedToken()
    {
        // Arrange: 1000 single-letter tokens, a skip directive on every 10th, canceled while token 300 is visited
        var text = string.Concat(Enumerable.Repeat("a ", 1000));
        var tokens = NormalizationHarness.Tokenize(text);
        var directives = Enumerable.Range(0, 100).Select(i => new TokenDirective(i * 10, TokenDirectiveKind.Skip, null)).ToArray();
        using var cancellation = new CancellationTokenSource();
        var visited = new List<int>();
        var normalizer = new TextNormalizer(
            AbbreviationResolver.Default,
            AcronymRecognizer.Default,
            index =>
            {
                visited.Add(index);
                if (index == 300)
                {
                    cancellation.Cancel();
                }
            });

        // Act
        Assert.Throws<OperationCanceledException>(() => normalizer.Normalize(text, tokens, false, cancellation.Token, directives));

        // Assert: stopped at the next check, the 512th visited token
        Assert.Equal(Enumerable.Range(0, 511), visited);
        Assert.Throws<OperationCanceledException>(() => normalizer.Normalize(text, tokens, false, cancellation.Token, directives));
    }

    /// <summary>Proves the shape-based spelling decision: letter-name replacements are spelled, word replacements, symbols and numbers are not.</summary>
    [Fact]
    public void TextNormalizer_Normalize_SpelledReplacements_AreDecidedByShape()
    {
        // Act
        var aligned = NormalizationHarness.Normalize("The FBI and U.S. Army met Dr. Smith & J. Jones at 5 p.m. for $5 and 50% of 3 kg.");
        var neutralized = aligned.Source;

        // Assert
        var spelled = aligned.Spans.Where(span => span.Spelled).Select(span => neutralized.Substring(span.Start, span.Length)).ToList();
        Assert.Equal(["FBI", "U.S.", "J.", "p.m."], spelled);
        Assert.All(aligned.Spans.Where(span => span.Spelled), span => Assert.Matches("^[A-Z]( [A-Z])*$", span.Spoken.Trim()));
        Assert.All(aligned.Spans.Where(span => !span.Spelled), span => Assert.DoesNotMatch("^[A-Z]( [A-Z])*$", span.Spoken.Trim()));
    }

    /// <summary>Proves an ampersand initialism is decomposed into spelled letter spans and an <c>and</c> symbol span whose spoken text is the same as before, with the possessive tail outside every span.</summary>
    [Fact]
    public void TextNormalizer_Normalize_AmpersandInitialisms_AreDecomposedWithUnchangedSpokenText()
    {
        // Act
        var possessive = NormalizationHarness.Normalize("AT&T's");
        var typographic = NormalizationHarness.Normalize("AT&T\u2019s");
        var groups = NormalizationHarness.Normalize("Q&A and R&D");

        // Assert
        Assert.Equal(
            [new NormalizedSpan(0, 2, "A T", NormalizationKind.Initialism, true), new NormalizedSpan(2, 1, " and ", NormalizationKind.Symbol), new NormalizedSpan(3, 1, "T", NormalizationKind.Initialism, true)],
            possessive.Spans);
        Assert.Equal("A T and T's", possessive.ApplySpokenForms());
        Assert.Equal("A T and T's", typographic.ApplySpokenForms());
        Assert.Equal(new NormalizedSpan(4, 1, "'", NormalizationKind.Apostrophe), typographic.Spans[^1]);
        Assert.Equal("Q and A and R and D", groups.ApplySpokenForms());
        Assert.Equal(6, groups.Spans.Count);
        Assert.Equal(4, groups.Spans.Count(span => span.Spelled));
    }

    /// <summary>Proves an ampersand initialism next to letters and replacements is padded by exactly one space and no span touches whitespace.</summary>
    [Fact]
    public void TextNormalizer_Normalize_AmpersandInitialismNextToReplacement_PadsExactlyOneSpace()
    {
        // Arrange: a mark directly before the initialism, as two tokens
        NormalizationToken[] tokens = [new(0, 1, true), new(1, 4, false)];

        // Act
        var aligned = TextNormalizer.Default.Normalize("\u2122AT&T", tokens, false, CancellationToken.None);

        // Assert
        Assert.Equal("trademark A T and T", aligned.ApplySpokenForms());
        Assert.DoesNotContain("  ", aligned.ApplySpokenForms(), StringComparison.Ordinal);
    }

    /// <summary>Proves a pass with thousands of directives over a large text is linear: it finishes within a generous budget and adds no per-token allocation for tokens without a directive.</summary>
    [Fact]
    public void TextNormalizer_Normalize_ManyDirectives_CompletesWithinBudget()
    {
        // Arrange: 200,000 tokens, a skip directive on every 20th
        var text = string.Concat(Enumerable.Repeat("word ", 200_000));
        var tokens = Enumerable.Range(0, 200_000).Select(i => new NormalizationToken(i * 5, 4, i == 0)).ToArray();
        var directives = Enumerable.Range(0, 10_000).Select(i => new TokenDirective(i * 20, TokenDirectiveKind.Skip, null)).ToArray();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var aligned = TextNormalizer.Default.Normalize(text, tokens, false, CancellationToken.None, directives);
        stopwatch.Stop();

        // Assert
        Assert.Equal(10_000, aligned.OverridesApplied);
        Assert.Empty(aligned.Spans);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), $"took {stopwatch.Elapsed}");
    }

    /// <summary>The four directive kinds, with the value each needs, that the sentence-break tests apply in turn.</summary>
    private static readonly (TokenDirectiveKind Kind, string? Value)[] AllDirectiveKinds =
    [
        (TokenDirectiveKind.Skip, null),
        (TokenDirectiveKind.Replace, "other words"),
        (TokenDirectiveKind.Spell, null),
        (TokenDirectiveKind.Pronounce, "ipa:abc"),
    ];

    /// <summary>Finds the index of the n-th token that is a word, with or without its period.</summary>
    /// <param name="text">The (neutralized) text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <param name="word">The word, without a period.</param>
    /// <param name="occurrence">The zero-based occurrence to find.</param>
    /// <returns>The token index.</returns>
    private static int IndexOfWord(string text, NormalizationToken[] tokens, string word, int occurrence)
    {
        var seen = 0;
        for (var i = 0; i < tokens.Length; i++)
        {
            var span = tokens[i].In(text);
            if ((span.SequenceEqual(word) || (span.Length == word.Length + 1 && span[^1] == '.' && span[..^1].SequenceEqual(word))) && seen++ == occurrence)
            {
                return i;
            }
        }

        throw new InvalidOperationException($"No token '{word}' number {occurrence}.");
    }

    /// <summary>Proves a directive on an abbreviation or an acronym never changes the sentence breaks: with any of the four kinds, mid-sentence or sentence-final, the reconciled breaks equal those of the text without the directive, while the spans and marks come from the directive alone.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The abbreviation, without its period.</param>
    /// <param name="occurrence">Which occurrence of the word gets the directive.</param>
    /// <param name="expectedSentences">The expected sentences, without and with the directive, joined by the broken bar.</param>
    [Theory]
    [InlineData("See Fig. 5 for details.", "Fig", 0, "See Fig. 5 for details.")]
    [InlineData("We saw the plot in Fig. It was good.", "Fig", 0, "We saw the plot in Fig.¦It was good.")]
    [InlineData("Dr. Smith lives here. He sleeps.", "Dr", 0, "Dr. Smith lives here.¦He sleeps.")]
    [InlineData("Dr. Smith lives on Maple Dr. He likes it.", "Dr", 1, "Dr. Smith lives on Maple Dr.¦He likes it.")]
    [InlineData("Visit St. Louis. Then go.", "St", 0, "Visit St. Louis.¦Then go.")]
    [InlineData("I live on Main St. It is nice.", "St", 0, "I live on Main St.¦It is nice.")]
    [InlineData("No. 5 is here. Then go.", "No", 0, "No. 5 is here.¦Then go.")]
    [InlineData("He said No. I don't know.", "No", 0, "He said No.¦I don't know.")]
    [InlineData("Jan. 5 was cold. Then go.", "Jan", 0, "Jan. 5 was cold.¦Then go.")]
    [InlineData("We met in Jan. It was cold.", "Jan", 0, "We met in Jan.¦It was cold.")]
    [InlineData("Hello\u2014Dr. Smith left.", "Dr", 0, "Hello\u2014Dr. Smith left.")]
    [InlineData("Hello\u2014Dr. Smith left. Bye.", "Dr", 0, "Hello\u2014Dr. Smith left.¦Bye.")]
    [InlineData("John Smith Jr. Ph.D. spoke.", "Ph.D", 0, "John Smith Jr. Ph.D. spoke.")]
    [InlineData("John Smith Jr. Ph.D. He spoke.", "Ph.D", 0, "John Smith Jr. Ph.D.¦He spoke.")]
    public void TextNormalizer_Normalize_DirectiveOnAbbreviation_KeepsBuiltInSentenceBreaks(string text, string word, int occurrence, string expectedSentences)
    {
        // Arrange
        var neutralized = NormalizationHarness.Neutralize(text);
        var tokens = NormalizationHarness.Tokenize(neutralized);
        var index = IndexOfWord(neutralized, tokens, word, occurrence);
        var token = tokens[index];
        var tokenizerBreaks = NormalizationHarness.TokenizerBreaks(neutralized);
        var baseline = NormalizationHarness.Normalize(text);
        var baselineBreaks = baseline.ReconcileBreaks(tokenizerBreaks);
        var periodSeparate = token.End < neutralized.Length && neutralized[token.End] == '.' && neutralized[token.End - 1] != '.';
        var reach = token.End + (periodSeparate ? 1 : 0);
        var lettersOnly = neutralized.Substring(token.Start, token.Length).All(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

        foreach (var (kind, value) in AllDirectiveKinds)
        {
            var context = $"'{text}', {word} #{occurrence}, {kind}";

            // Act
            var aligned = NormalizationHarness.Normalize(text, false, [new TokenDirective(index, kind, value)]);
            var breaks = aligned.ReconcileBreaks(tokenizerBreaks);

            // Assert: the breaks are the built-in ones, never changed by the directive
            Assert.True(baselineBreaks.SequenceEqual(breaks), $"breaks differ: {context}: [{string.Join(",", breaks)}] instead of [{string.Join(",", baselineBreaks)}]");
            Assert.Equal(expectedSentences, NormalizationHarness.JoinSentences(neutralized, baselineBreaks));
            Assert.Equal(expectedSentences, NormalizationHarness.JoinSentences(neutralized, breaks));

            // Assert: the directive is applied (a spell-out only for ASCII letters) and nothing else is spoken for the token
            var expectedApplied = kind != TokenDirectiveKind.Spell || lettersOnly;
            Assert.True(aligned.OverridesApplied == (expectedApplied ? 1 : 0) && aligned.OverridesIgnored == (expectedApplied ? 0 : 1), $"counts: {context}");
            Assert.True(aligned.AmbiguousCount <= baseline.AmbiguousCount, $"ambiguous: {context}");
            var touching = aligned.Spans.Where(span => span.Start < reach && span.End > token.Start).ToList();
            if (kind is TokenDirectiveKind.Replace or TokenDirectiveKind.Spell && expectedApplied)
            {
                var span = Assert.Single(touching);
                Assert.True(span.Kind == NormalizationKind.Override && span.Start == token.Start && span.Length == token.Length && span.Spelled == (kind == TokenDirectiveKind.Spell), $"span: {context}");
            }
            else
            {
                Assert.True(touching.Count == 0, $"unexpected span: {context}");
            }

            Assert.Equal(kind == TokenDirectiveKind.Pronounce ? 1 : 0, aligned.Pronunciations.Count(mark => mark.Start == token.Start && mark.Length == token.Length));
        }
    }

    /// <summary>Proves the pinned review example: with a directive on <c>Fig</c> the text <c>See Fig. 5 for details.</c> still has no sentence break, where the tokenizer alone would break after the period.</summary>
    [Fact]
    public void TextNormalizer_Normalize_DirectiveOnFig_ReviewExampleHasNoSentenceBreak()
    {
        // Arrange
        const string text = "See Fig. 5 for details.";
        var tokens = NormalizationHarness.Tokenize(text);
        var tokenizerBreaks = NormalizationHarness.TokenizerBreaks(text);

        // Act
        var plain = NormalizationHarness.Normalize(text);
        var replaced = WithDirective(text, "Fig", TokenDirectiveKind.Replace, "figure");

        // Assert
        Assert.Equal([9], tokenizerBreaks);
        Assert.Empty(plain.ReconcileBreaks(tokenizerBreaks));
        Assert.Empty(replaced.ReconcileBreaks(tokenizerBreaks));
        Assert.Equal([new NormalizedSpan(tokens[1].Start, 3, "figure", NormalizationKind.Override), new NormalizedSpan(tokens[3].Start, 1, "five", NormalizationKind.Number)], replaced.Spans);
        Assert.Equal("See figure. five for details.", replaced.ApplySpokenForms());
        Assert.Equal([9], replaced.SuppressedBreaks);
    }

    /// <summary>Proves a defective directive (a spell-out of a token that is not ASCII letters only, a replacement or pronunciation without a value) is ignored and counted, the token is spoken as written, and the built-in sentence breaks are still kept.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The token, with or without its period, that gets the defective directive.</param>
    /// <param name="kindName">The name of the defective directive's kind; it never has a value.</param>
    [Theory]
    [InlineData("See Fig. 5 for details.", "Fig", "Replace")]
    [InlineData("Hello\u2014Dr. Smith left.", "Dr", "Replace")]
    [InlineData("Hello\u2014Dr. Smith left.", "Dr", "Pronounce")]
    [InlineData("Dr. Smith lives here. He sleeps.", "Dr", "Spell")]
    [InlineData("We met in caf\u00E9. It was cold.", "caf\u00E9", "Spell")]
    public void TextNormalizer_Normalize_DefectiveDirective_StillKeepsBuiltInSentenceBreaks(string text, string word, string kindName)
    {
        // Arrange
        var kind = Enum.Parse<TokenDirectiveKind>(kindName);
        var neutralized = NormalizationHarness.Neutralize(text);
        var tokens = NormalizationHarness.Tokenize(neutralized);
        var index = IndexOfWord(neutralized, tokens, word, 0);
        var tokenizerBreaks = NormalizationHarness.TokenizerBreaks(neutralized);
        var baselineBreaks = NormalizationHarness.Normalize(text).ReconcileBreaks(tokenizerBreaks);

        // Act
        var aligned = NormalizationHarness.Normalize(text, false, [new TokenDirective(index, kind, null)]);

        // Assert
        Assert.Equal(0, aligned.OverridesApplied);
        Assert.Equal(1, aligned.OverridesIgnored);
        Assert.DoesNotContain(aligned.Spans, span => span.Start < tokens[index].End && span.End > tokens[index].Start);
        Assert.Empty(aligned.Pronunciations);
        Assert.True(baselineBreaks.SequenceEqual(aligned.ReconcileBreaks(tokenizerBreaks)), $"breaks differ for '{text}'");
    }

    /// <summary>Proves directives on abbreviations do not disturb the cancellation cadence: every token is visited exactly once, in order, as without directives.</summary>
    [Fact]
    public void TextNormalizer_Normalize_DirectiveOnAbbreviation_VisitsEveryTokenOnce()
    {
        // Arrange
        const string text = "See Fig. 5 for details. Hello\u2014Dr. Smith left. Jan. 5 was cold.";
        var tokens = NormalizationHarness.Tokenize(text);
        TokenDirective[] directives =
        [
            new(IndexOf(text, tokens, "Fig"), TokenDirectiveKind.Skip, null),
            new(IndexOf(text, tokens, "Dr"), TokenDirectiveKind.Replace, "doctor"),
            new(IndexOf(text, tokens, "Jan"), TokenDirectiveKind.Pronounce, "ipa:abc"),
        ];
        var visited = new List<int>();
        var normalizer = new TextNormalizer(AbbreviationResolver.Default, AcronymRecognizer.Default, visited.Add);

        // Act
        var aligned = normalizer.Normalize(text, tokens, false, CancellationToken.None, directives);

        // Assert
        Assert.Equal(Enumerable.Range(0, tokens.Length), visited);
        Assert.Equal(3, aligned.OverridesApplied);
    }
}
