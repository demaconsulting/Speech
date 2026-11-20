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

    /// <summary>Proves standalone operators are spoken, but only when whitespace sets them apart (the golden file holds the other operators).</summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("a & b", "a and b")]
    [InlineData("a < b", "a less than b")]
    [InlineData("a=b", "a=b")]
    [InlineData("a +b", "a +b")]
    public void TextNormalizer_Normalize_Operators_AreSpokenOnlyWhenSetApart(string text, string spoken)
    {
        // Act / Assert
        Assert.Equal(spoken, Spoken(text));
    }

    /// <summary>Proves marks are spoken whether they stand alone or touch a word, with a separating space where a letter touches (the golden file holds the other marks).</summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("\u00A9 2025", "copyright twenty twenty five")]
    [InlineData("A\u2122B", "A trademark B")]
    [InlineData("\u2122\u00AE", "trademark registered")]
    public void TextNormalizer_Normalize_Marks_AreSpoken(string text, string spoken)
    {
        // Act / Assert
        Assert.Equal(spoken, Spoken(text));
    }

    /// <summary>
    ///     Proves a lone number sign after a word is the word <i>number</i> before a colon or a number, and is left alone at the
    ///     start of a line, before a word or with no word before it.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("Item #: SCP-184", "Item number: S C P one hundred eighty four")]
    [InlineData("Case # 12 is open.", "Case number twelve is open.")]
    [InlineData("See item #12 now.", "See item number twelve now.")]
    [InlineData("# 5 Reasons", "# five Reasons")]
    [InlineData("Item # Title", "Item # Title")]
    [InlineData("Item\n# 5", "Item\n# five")]
    [InlineData("5 # 6", "five # six")]
    public void TextNormalizer_Normalize_NumberSign_IsTheWordNumberOnlyAfterAWordBeforeAColonOrNumber(string text, string spoken)
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

    /// <summary>Proves a name suffix directly before a credential consumes its period and keeps the name in one sentence, removing a tokenizer break between two credentials, while a suffix before a word that starts a sentence still ends the sentence.</summary>
    [Fact]
    public void TextNormalizer_Normalize_NameSuffixBeforeCredential_ConsumesPeriodAndKeepsNameTogether()
    {
        // Arrange
        const string suffixAndCredential = "John Smith Jr. Ph.D. spoke.";
        const string twoCredentials = "Ann Lee Ph.D. M.D. spoke.";
        const string suffixBeforeStarter = "I met John Smith Jr. He left.";
        var tokenizerBreaks = NormalizationHarness.TokenizerBreaks(twoCredentials);

        // Act
        var first = NormalizationHarness.Normalize(suffixAndCredential);
        var second = NormalizationHarness.Normalize(twoCredentials);
        var third = NormalizationHarness.Normalize(suffixBeforeStarter);

        // Assert: "Jr." is consumed with its period and no break is forced after it
        Assert.Equal("Jr.", suffixAndCredential.Substring(first.Spans[0].Start, first.Spans[0].Length));
        Assert.Empty(first.ForcedBreaks);
        Assert.Empty(first.ReconcileBreaks(NormalizationHarness.TokenizerBreaks(suffixAndCredential)));

        // Assert: the tokenizer's break between the two credentials is removed
        Assert.NotEmpty(tokenizerBreaks);
        Assert.Equal(tokenizerBreaks, second.SuppressedBreaks);
        Assert.Empty(second.ReconcileBreaks(tokenizerBreaks));

        // Assert: a suffix before a word that starts a sentence still forces the break before "He"
        Assert.Equal([suffixBeforeStarter.IndexOf("He", StringComparison.Ordinal)], third.ReconcileBreaks(NormalizationHarness.TokenizerBreaks(suffixBeforeStarter)));
    }

    /// <summary>Proves a slash between two plain words is spoken, and every other slash use is left alone (the golden file holds the other slash forms).</summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("he/she", "he slash she")]
    [InlineData("km/h", "km/h")]
    [InlineData("and/or", "and or")]
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
    [InlineData("a\u200B\u200B\u200Bb", "ab", 1)]
    [InlineData("a\u200Bb\u00ADc", "abc", 2)]
    [InlineData("a\u200Db", "a\u200Db", 0)]
    public void TextNormalizer_Normalize_InvisibleCharacters_AreDroppedExplicitly(string text, string spoken, int spans)
    {
        // Act
        var aligned = NormalizationHarness.Normalize(text);

        // Assert
        Assert.Equal(spoken, aligned.ApplySpokenForms());
        Assert.Equal(spans, aligned.Spans.Count);
        Assert.All(aligned.Spans, span => Assert.Equal(NormalizationKind.Dropped, span.Kind));
    }

    /// <summary>Proves typographic apostrophes are canonicalized inside clitics and elisions, and quote marks stay (the golden file holds the other apostrophe forms).</summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("it\u2019s", "it's")]
    [InlineData("\u2019Tis the season", "'Tis the season")]
    [InlineData("dogs\u2019", "dogs\u2019")]
    [InlineData("\u2018quoted\u2019", "\u2018quoted\u2019")]
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
    [InlineData("It's cold", "'s", CliticKind.Is)]
    [InlineData("It's been cold", "'s", CliticKind.Has)]
    [InlineData("He's never been", "'s", CliticKind.Has)]
    [InlineData("Let's go", "'s", CliticKind.Us)]
    [InlineData("John's book", "'s", CliticKind.Possessive)]
    [InlineData("John's going home", "'s", CliticKind.Is)]
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

    /// <summary>Proves a dotted initialism without its own period needs the separate period token, and consumes or keeps it by the sentence rule; a dotted form with no period token after it is left alone.</summary>
    [Fact]
    public void TextNormalizer_Normalize_DottedInitialismWithSeparatePeriod_IsHandled()
    {
        // Act
        var aligned = NormalizationHarness.Normalize("Meet at 5 p.m. Then sleep.");
        var withoutPeriodToken = NormalizationHarness.Normalize("the x.y thing");

        // Assert: the hour is read by the numeric rule and the dotted form by the initialism rule
        Assert.Equal(2, aligned.Spans.Count);
        Assert.Equal(NormalizationKind.Number, aligned.Spans[0].Kind);
        Assert.Equal("five", aligned.Spans[0].Spoken);
        var span = aligned.Spans[1];
        Assert.Equal(NormalizationKind.Initialism, span.Kind);
        Assert.Equal("P M", span.Spoken);
        Assert.Empty(withoutPeriodToken.Spans);
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

    /// <summary>Proves cancellation requested while a long text is being normalized ends the request with OperationCanceledException instead of a result.</summary>
    [Fact]
    public void TextNormalizer_Normalize_CanceledMidPass_ThrowsOperationCanceledException()
    {
        // Arrange: 1,000 one-letter tokens; the observer cancels while an early token is visited
        var text = string.Concat(Enumerable.Repeat("a ", 1000));
        var tokens = Enumerable.Range(0, 1000).Select(i => new NormalizationToken(i * 2, 1, i == 0)).ToArray();
        using var cancellation = new CancellationTokenSource();
        var normalizer = new TextNormalizer(
            AbbreviationResolver.Default,
            AcronymRecognizer.Default,
            index =>
            {
                if (index == 10)
                {
                    cancellation.Cancel();
                }
            });

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => normalizer.Normalize(text, tokens, false, cancellation.Token));
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
    }

    /// <summary>Proves a URL, an e-mail address and a bare domain are spoken as their pieces before any other rule can claim the token, while file names and dotted names with an unknown ending are left as written.</summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("Visit https://www.example.com/docs?id=7 today.", "Visit H T T P S:W W W dot example dot com slash docs question mark id equals 7 today.")]
    [InlineData("Mail john.smith@example.com, now.", "Mail john dot smith at example dot com, now.")]
    [InlineData("See www.example.org/a-b_c.", "See W W W dot example dot org slash a dash b underscore c.")]
    [InlineData("Visit example.com. Then relax.", "Visit example dot com. Then relax.")]
    [InlineData("Open report.pdf or Node.js now.", "Open report.pdf or Node.js now.")]
    public void TextNormalizer_Normalize_WebAddresses_AreSpokenAsTheirPieces(string text, string spoken)
    {
        // Act
        var aligned = NormalizationHarness.Normalize(text);

        // Assert
        Assert.Equal(spoken, aligned.ApplySpokenForms());
        Assert.Equal(spoken == text, aligned.Spans.Count == 0);
    }

    /// <summary>Proves an identifier of capital letters and digits has its letters spelled and its digits read, and a name joined to a number by a hyphen has only the number read.</summary>
    /// <param name="text">The text.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("Use paper A4 and vitamin B12 with H2O and MP3.", "Use paper A four and vitamin B twelve with H two O and M P three.")]
    [InlineData("COVID-19 and Catch-22 spread.", "COVID nineteen and Catch twenty two spread.")]
    [InlineData("Item SCP-184 and the F-16.", "Item S C P one hundred eighty four and the F sixteen.")]
    [InlineData("The COVID19 wave.", "The COVID19 wave.")]
    public void TextNormalizer_Normalize_LettersAndDigits_AreSpelledAndRead(string text, string spoken)
    {
        // Act
        var aligned = NormalizationHarness.Normalize(text);

        // Assert
        Assert.Equal(spoken, aligned.ApplySpokenForms());
        Assert.All(aligned.Spans, span => Assert.Equal(span.Kind == NormalizationKind.Initialism, span.Spoken.Trim().All(character => character is >= 'A' and <= 'Z' or ' ')));
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

    /// <summary>Proves a token whose end would overflow, or that starts past the text, is rejected with the documented ArgumentException rather than passing the range check.</summary>
    /// <param name="start">The token's start.</param>
    /// <param name="length">The token's length.</param>
    [Theory]
    [InlineData(int.MaxValue, 1)]
    [InlineData(7, 1)]
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

    /// <summary>Proves a directive wins over every built-in rule for its token: symbol-bearing token, lexicon abbreviation, listed acronym and ampersand initialism.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The token that gets a skip directive.</param>
    [Theory]
    [InlineData("he/she left", "he/she")]
    [InlineData("Mr Smith left", "Mr")]
    [InlineData("US troops", "US")]
    [InlineData("the AT&T network", "AT&T")]
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

        // Assert: the built-in rule fired without the directive, and never with it
        Assert.DoesNotContain(skipped.Spans, span => span.Start >= tokens[index].Start && span.End <= tokens[index].End);
        Assert.Equal(1, skipped.OverridesApplied);
        var overrideSpan = Assert.Single(replaced.Spans, span => span.Kind == NormalizationKind.Override);
        Assert.Equal(tokens[index].Start, overrideSpan.Start);
        Assert.Equal(tokens[index].Length, overrideSpan.Length);
        Assert.DoesNotContain(replaced.Spans, span => span.Kind != NormalizationKind.Override && span.Start >= tokens[index].Start && span.End <= tokens[index].End);
        Assert.NotEmpty(builtIn.Spans);
    }

    /// <summary>Proves a directive on a token that an earlier multi-token match consumed (a unit or a magnitude word) is ignored and counted, and the match is unchanged.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The consumed token that gets a directive.</param>
    [Theory]
    [InlineData("5 kg of rice", "kg")]
    [InlineData("$5 million", "million")]
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
    [InlineData(-1, 0)]
    [InlineData(0, 2)]
    [InlineData(0, int.MaxValue)]
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
    [InlineData("We saw the plot in Fig. It was good.", "Fig", 0, "We saw the plot in Fig.¦It was good.")]
    [InlineData("Dr. Smith lives on Maple Dr. He likes it.", "Dr", 1, "Dr. Smith lives on Maple Dr.¦He likes it.")]
    [InlineData("He said No. I don't know.", "No", 0, "He said No.¦I don't know.")]
    [InlineData("Hello\u2014Dr. Smith left. Bye.", "Dr", 0, "Hello\u2014Dr. Smith left.¦Bye.")]
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
    [InlineData("Hello\u2014Dr. Smith left.", "Dr", "Pronounce")]
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
}
