using DemaConsulting.Speech.Text.En.NormalizationSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.NormalizationSubsystem;

/// <summary>
///     Unit tests for <see cref="AbbreviationResolver"/>: the embedded lexicon, the context cues
///     that choose among the readings of <c>St.</c>, <c>Dr.</c>, <c>Sr.</c> and <c>No.</c>, the
///     neighbor guards, the default-and-count rule, the strict loader and the concurrent use of the
///     default lexicon. Tokens come from the real tokenizer through the test harness.
/// </summary>
public sealed class AbbreviationResolverTests
{
    /// <summary>Resolves the first token of <paramref name="text"/> that starts with <paramref name="word"/>.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The start of the candidate token.</param>
    /// <returns>Whether it resolved, and the resolution.</returns>
    private static (bool Found, AbbreviationMatch Match) Resolve(string text, string word)
    {
        var tokens = NormalizationHarness.Tokenize(text);
        var index = Array.FindIndex(tokens, token => token.In(text).StartsWith(word, StringComparison.Ordinal));
        Assert.True(index >= 0, $"no token starts with '{word}' in '{text}'");
        var found = AbbreviationResolver.Default.TryResolve(text, tokens, index, out var match);
        return (found, match);
    }

    /// <summary>Proves the shipped lexicon loads and has rows for every documented group.</summary>
    [Fact]
    public void AbbreviationResolver_Default_Lexicon_LoadsWithRows()
    {
        // Act
        var resolver = AbbreviationResolver.Default;

        // Assert
        Assert.True(resolver.Count >= 70);
    }

    /// <summary>Proves each unambiguous lexicon form resolves to its spoken form in a neutral context.</summary>
    /// <param name="text">A sentence containing the form.</param>
    /// <param name="word">The start of the form's token.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("Mrs. Smith left.", "Mrs.", "Missus")]
    [InlineData("Messrs. Smith and Jones left.", "Messrs.", "Messieurs")]
    [InlineData("Mmes. Smith and Jones left.", "Mmes", "Mesdames")]
    [InlineData("Rev. Smith left.", "Rev.", "Reverend")]
    [InlineData("Gen. Smith left.", "Gen.", "General")]
    [InlineData("Lt. Smith left.", "Lt.", "Lieutenant")]
    [InlineData("Cpl. Smith left.", "Cpl.", "Corporal")]
    [InlineData("Maj. Smith left.", "Maj.", "Major")]
    [InlineData("Cmdr. Smith left.", "Cmdr.", "Commander")]
    [InlineData("Adm. Smith left.", "Adm.", "Admiral")]
    [InlineData("Gov. Smith left.", "Gov.", "Governor")]
    [InlineData("Pres. Smith left.", "Pres.", "President")]
    [InlineData("Supt. Smith left.", "Supt.", "Superintendent")]
    [InlineData("Prof Smith left.", "Prof", "Professor")]
    [InlineData("Mrs Smith left.", "Mrs", "Missus")]
    [InlineData("Messrs Smith and Jones left.", "Messrs", "Messieurs")]
    [InlineData("Walk down Elm Ln. today.", "Ln", "Lane")]
    [InlineData("Walk down Elm Rd. today.", "Rd", "Road")]
    [InlineData("Walk down Elm Ct. today.", "Ct", "Court")]
    [InlineData("Walk down Elm Pl. today.", "Pl", "Place")]
    [InlineData("Walk down Elm Sq. today.", "Sq", "Square")]
    [InlineData("Walk down Elm Hwy. today.", "Hwy", "Highway")]
    [InlineData("Walk down Elm Pkwy. today.", "Pkwy", "Parkway")]
    [InlineData("Call Apt. today.", "Apt", "Apartment")]
    [InlineData("Call Ste. today.", "Ste", "Suite")]
    [InlineData("Call Bldg. today.", "Bldg", "Building")]
    [InlineData("Call Dept. today.", "Dept", "Department")]
    [InlineData("Acme Ltd. today.", "Ltd", "Limited")]
    [InlineData("Acme Corp. today.", "Corp", "Corporation")]
    [InlineData("Mark Twain Esq. today.", "Esq", "Esquire")]
    [InlineData("Say it viz. now.", "viz", "namely")]
    [InlineData("Some misc. items.", "misc", "miscellaneous")]
    [InlineData("See cf. the text.", "cf", "compare")]
    [InlineData("Read Vol. 2 now.", "Vol", "Volume")]
    [InlineData("E.g. apples are good.", "E.g.", "For example")]
    [InlineData("I.e. apples are good.", "I.e.", "That is")]
    [InlineData("Price n/a today.", "n/a", "not applicable")]
    [InlineData("Send c/o Smith now.", "c/o", "care of")]
    public void AbbreviationResolver_TryResolve_ListedForm_ReturnsSpokenForm(string text, string word, string spoken)
    {
        // Act
        var (found, match) = Resolve(text, word);

        // Assert
        Assert.True(found, $"'{word}' in '{text}' was not recognized");
        Assert.Equal(spoken, match.Spoken);
        Assert.False(match.UsedDefault);
    }

    /// <summary>Proves only the listed name suffixes and credentials continue a name, with or without their final period.</summary>
    /// <param name="word">The token's text.</param>
    /// <param name="expected">Whether the token continues a name.</param>
    [Theory]
    [InlineData("Jr", true)]
    [InlineData("Jr.", true)]
    [InlineData("Ph.D", true)]
    [InlineData("Ph.D.", true)]
    [InlineData("III", true)]
    [InlineData("M.D.", true)]
    [InlineData("Ave", false)]
    [InlineData("Smith", false)]
    [InlineData("V", false)]
    [InlineData("He", false)]
    [InlineData("jr", false)]
    public void AbbreviationResolver_IsNameContinuation_ListedFormsOnly(string word, bool expected)
    {
        // Arrange: the word is the token that follows "x"
        var text = "x " + word;
        var tokens = NormalizationHarness.Tokenize(text);

        // Act / Assert
        Assert.Equal(expected, AbbreviationResolver.IsNameContinuation(text, tokens, 1));
    }

    /// <summary>Proves the neighbor cues flip the reading of the ambiguous abbreviations in both directions.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The start of the candidate token.</param>
    /// <param name="spoken">The expected spoken form.</param>
    [Theory]
    [InlineData("Visit St. Louis. Then go.", "St", "Saint")]
    [InlineData("Visit Elm St. today", "St", "Street")]
    [InlineData("Dr. Smith lives here.", "Dr", "Doctor")]
    [InlineData("Maple Dr. He likes it.", "Dr", "Drive")]
    [InlineData("Take 5th St. Louis is ahead.", "St", "Street")]
    [InlineData("Take Wall St. Louis is ahead.", "St", "Saint")]
    [InlineData("The Dr. said hello.", "Dr", "Doctor")]
    [InlineData("Go to 123 Elm Dr. Smith lives there.", "Dr", "Drive")]
    [InlineData("Sr. Maria prayed.", "Sr", "Sister")]
    [InlineData("John Smith Sr. He left.", "Sr", "Senior")]
    [InlineData("John Smith Sr. Ph.D. spoke.", "Sr", "Senior")]
    [InlineData("Joe Smith Sr. Esq. arrived.", "Sr", "Senior")]
    [InlineData("St. Patrick's St. is closed.", "St", "Saint")]
    [InlineData("Mount St. Elias erupted.", "St", "Saint")]
    public void AbbreviationResolver_TryResolve_AmbiguousForm_FollowsTheNeighborCues(string text, string word, string spoken)
    {
        // Act
        var (found, match) = Resolve(text, word);

        // Assert
        Assert.True(found);
        Assert.Equal(spoken, match.Spoken);
    }

    /// <summary>Proves the second abbreviation of <c>St. Patrick's St.</c> reads as Street (a possessive before it is skipped).</summary>
    [Fact]
    public void AbbreviationResolver_TryResolve_PossessiveBeforeAbbreviation_IsSkippedForContext()
    {
        // Arrange
        const string text = "St. Patrick's St. is closed.";
        var tokens = NormalizationHarness.Tokenize(text);
        var second = Array.FindLastIndex(tokens, token => token.In(text).StartsWith("St", StringComparison.Ordinal));

        // Act
        var found = AbbreviationResolver.Default.TryResolve(text, tokens, second, out var match);

        // Assert
        Assert.True(found);
        Assert.Equal("Street", match.Spoken);
        Assert.Equal(AbbreviationClass.Suffix, match.Class);
    }

    /// <summary>Proves <c>No.</c> is the number before a digit and the plain word otherwise.</summary>
    [Fact]
    public void AbbreviationResolver_TryResolve_No_IsNumberBeforeDigitAndWordOtherwise()
    {
        // Act
        var (_, beforeDigit) = Resolve("No. 5 is here.", "No");
        var (_, beforeWord) = Resolve("No. I don't know.", "No");

        // Assert
        Assert.Equal("Number", beforeDigit.Spoken);
        Assert.Equal(AbbreviationClass.Prefix, beforeDigit.Class);
        Assert.Equal(AbbreviationClass.Word, beforeWord.Class);
        Assert.False(beforeDigit.UsedDefault || beforeWord.UsedDefault);
    }

    /// <summary>Proves the neighbor guards switch rows on and off.</summary>
    [Fact]
    public void AbbreviationResolver_TryResolve_Guards_RestrictReadingsToTheirContext()
    {
        // Act / Assert: Ft. needs a capitalized next word and no number before it
        Assert.True(Resolve("Ft. Worth is big.", "Ft").Found);
        Assert.False(Resolve("It is 5 Ft. Smith nearby.", "Ft").Found);
        Assert.False(Resolve("Ft. lower is big.", "Ft").Found);

        // Fig. and Vol. need a digit next
        Assert.True(Resolve("See Fig. 3 now.", "Fig").Found);
        Assert.False(Resolve("Fig. leaves fall.", "Fig").Found);
        Assert.False(Resolve("Vol. alone fell.", "Vol").Found);

        // titles written without a period need a capitalized next word
        Assert.True(Resolve("Mr Smith left.", "Mr").Found);
        Assert.False(Resolve("Mr alone left.", "Mr").Found);
        Assert.False(Resolve("Ms is fine.", "Ms").Found);
    }

    /// <summary>Proves the date guards restrict the month and weekday readings: a prefix reading needs a date next, <c>Mar.</c> and <c>Sun.</c> need a number before them, and only ASCII digits count.</summary>
    [Fact]
    public void AbbreviationResolver_TryResolve_DateGuards_RestrictMonthAndWeekdayReadings()
    {
        // Assert: a date next makes the prefix reading
        var (marchFound, march) = Resolve("Mar. 5 is late.", "Mar");
        Assert.True(marchFound);
        Assert.Equal("March", march.Spoken);
        Assert.Equal(AbbreviationClass.Prefix, march.Class);
        var (sundayFound, sunday) = Resolve("Sun. Mar. 5 is late.", "Sun");
        Assert.True(sundayFound);
        Assert.Equal(AbbreviationClass.Prefix, sunday.Class);

        // Assert: Mar. and Sun. have no unguarded reading: bare, they are left alone (the verb, the star)
        Assert.False(Resolve("Mar.", "Mar").Found);
        Assert.False(Resolve("Do not Mar. the floor.", "Mar").Found);
        Assert.False(Resolve("Sun.", "Sun").Found);
        Assert.False(Resolve("The Sun. rose.", "Sun").Found);

        // Assert: a number before them makes the suffix reading
        var (afterNumber, suffixMarch) = Resolve("5 Mar.", "Mar");
        Assert.True(afterNumber);
        Assert.Equal(AbbreviationClass.Suffix, suffixMarch.Class);
        var (sunAfter, suffixSun) = Resolve("5 Sun.", "Sun");
        Assert.True(sunAfter);
        Assert.Equal(AbbreviationClass.Suffix, suffixSun.Class);

        // Assert: the other months and weekdays take the suffix reading when no date follows, the prefix reading when one does
        Assert.Equal(AbbreviationClass.Suffix, Resolve("in Dec. She left.", "Dec").Match.Class);
        Assert.Equal(AbbreviationClass.Prefix, Resolve("Dec. 25 is near.", "Dec").Match.Class);
        Assert.Equal(AbbreviationClass.Suffix, Resolve("He left Mon. He came.", "Mon").Match.Class);
        Assert.Equal(AbbreviationClass.Prefix, Resolve("Mon. 5 Jan is near.", "Mon").Match.Class);
        Assert.Equal(AbbreviationClass.Prefix, Resolve("Sat. Jan. 5 is near.", "Sat").Match.Class);

        // Assert: only ASCII digits make a date (an Arabic-Indic digit does not), for both guards
        Assert.Equal(AbbreviationClass.Suffix, Resolve("Dec. \u0665 is odd.", "Dec").Match.Class);
        Assert.False(Resolve("\u0665 Mar.", "Mar").Found);
        Assert.False(Resolve("\uFF15 Sun.", "Sun").Found);

        // Assert: the loader accepts the new guard names, alone and joined, and still rejects an unknown one
        var resolver = AbbreviationResolver.FromTsv(new StringReader(
            Header + "Zz.\tZed\tprefix\tno\tnext-date+prev-number\tn\nZz.\tZed\tsuffix\tyes\t-\tn\n"));
        Assert.Equal(2, resolver.Count);
        Assert.Throws<InvalidDataException>(() => AbbreviationResolver.FromTsv(new StringReader(Header + "Zz.\tZed\tprefix\tyes\tnext-time\tn\n")));
    }

    /// <summary>Proves the month and weekday abbreviations resolve in their prefix, suffix and no-period forms to the full name.</summary>
    [Fact]
    public void AbbreviationResolver_TryResolve_MonthAndWeekday_PrefixSuffixAndBareForms()
    {
        // Arrange: (text, token start, expected spoken, expected class, includes a period token)
        (string Text, string Word, string Spoken, AbbreviationClass Class)[] cases =
        [
            ("Jan. 5 is cold.", "Jan", "January", AbbreviationClass.Prefix),
            ("It was Jan.", "Jan", "January", AbbreviationClass.Suffix),
            ("Jan 5 is cold.", "Jan", "January", AbbreviationClass.Prefix),
            ("Feb 3 is cold.", "Feb", "February", AbbreviationClass.Prefix),
            ("Sept 11 was sad.", "Sept", "September", AbbreviationClass.Prefix),
            ("Sept. 11 was sad.", "Sept", "September", AbbreviationClass.Prefix),
            ("Sep. 9 was sad.", "Sep", "September", AbbreviationClass.Prefix),
            ("Oct. 5 is near.", "Oct", "October", AbbreviationClass.Prefix),
            ("Thurs. 7 is near.", "Thurs", "Thursday", AbbreviationClass.Prefix),
            ("Thur. 7 is near.", "Thur", "Thursday", AbbreviationClass.Prefix),
            ("Tues. 7 is near.", "Tues", "Tuesday", AbbreviationClass.Prefix),
            ("He left Fri.", "Fri", "Friday", AbbreviationClass.Suffix),
            ("He left Wed.", "Wed", "Wednesday", AbbreviationClass.Suffix),
        ];

        // Act / Assert
        foreach (var (text, word, spoken, abbreviationClass) in cases)
        {
            var (found, match) = Resolve(text, word);
            Assert.True(found, text);
            Assert.Equal(spoken, match.Spoken);
            Assert.Equal(abbreviationClass, match.Class);
            Assert.False(match.UsedDefault, text);
        }

        // Assert: May is a word and never an abbreviation; lowercase months and weekdays written without a period are left alone
        Assert.False(Resolve("May 5 is near.", "May").Found);
        Assert.False(Resolve("jan. 5 is near.", "jan").Found);
        Assert.False(Resolve("Mon 5 is near.", "Mon").Found);
        Assert.False(Resolve("Jan is near.", "Jan").Found);
    }

    /// <summary>
    ///     Proves every month and weekday abbreviation of an independent expected table resolves to its full name: before a date as a
    ///     prefix reading, and elsewhere as a suffix reading, with the documented guards for a bare <c>Mar.</c> and <c>Sun.</c>.
    /// </summary>
    [Fact]
    public void AbbreviationResolver_TryResolve_MonthAndWeekdayAbbreviations_ResolveToFullNamesInDateAndNonDateContexts()
    {
        // Arrange: the expected table is written out here, independent of the lexicon and of the numeric recognizer
        (string Abbreviation, string FullName)[] months =
        [
            ("Jan", "January"), ("Feb", "February"), ("Mar", "March"), ("Apr", "April"), ("Jun", "June"), ("Jul", "July"),
            ("Aug", "August"), ("Sep", "September"), ("Sept", "September"), ("Oct", "October"), ("Nov", "November"), ("Dec", "December"),
        ];
        (string Abbreviation, string FullName)[] weekdays =
        [
            ("Mon", "Monday"), ("Tue", "Tuesday"), ("Tues", "Tuesday"), ("Wed", "Wednesday"), ("Thu", "Thursday"),
            ("Thur", "Thursday"), ("Thurs", "Thursday"), ("Fri", "Friday"), ("Sat", "Saturday"), ("Sun", "Sunday"),
        ];

        foreach (var (abbreviation, fullName) in months.Concat(weekdays))
        {
            // Act
            var (datedFound, dated) = Resolve($"It was {abbreviation}. 5 then.", abbreviation);
            var (numberedFound, numbered) = Resolve($"It was 5 {abbreviation}.", abbreviation);

            // Assert: in a date context the abbreviation is read as the prefix of the date; after a number it is read as a suffix
            Assert.True(datedFound, abbreviation);
            Assert.Equal(fullName, dated.Spoken);
            Assert.Equal(AbbreviationClass.Prefix, dated.Class);
            Assert.True(numberedFound, abbreviation);
            Assert.Equal(fullName, numbered.Spoken);
            Assert.Equal(AbbreviationClass.Suffix, numbered.Class);
        }

        // Assert: outside a date context a bare Mar. and Sun. stay as written, and May is a word
        Assert.False(Resolve("They Mar. it.", "Mar").Found);
        Assert.False(Resolve("The Sun. It rose.", "Sun").Found);
        Assert.False(Resolve("May 5 is near.", "May").Found);
    }

    /// <summary>Proves a resolution that fell back to the default, or chose with both cues present, is flagged for counting; clear ones are not.</summary>
    [Fact]
    public void AbbreviationResolver_TryResolve_InconclusiveContext_UsesDefaultAndIsCounted()
    {
        // Act
        var (_, neither) = Resolve("They St. said so.", "St");
        var (_, both) = Resolve("Go see Jones St. Smith", "St");
        var (_, clear) = Resolve("Dr. Smith lives here.", "Dr");
        var (_, drNeither) = Resolve("They Dr. said so.", "Dr");

        // Assert
        Assert.Equal("Street", neither.Spoken);
        Assert.True(neither.UsedDefault);
        Assert.Equal("Saint", both.Spoken);
        Assert.True(both.UsedDefault);
        Assert.False(clear.UsedDefault);
        Assert.Equal("Doctor", drNeither.Spoken);
        Assert.True(drNeither.UsedDefault);
    }

    /// <summary>Proves the word-plus-period token shape is reported, and a whole-token form is not.</summary>
    [Fact]
    public void AbbreviationResolver_TryResolve_PeriodShapes_AreReported()
    {
        // Act
        var (_, separate) = Resolve("Walk down Elm Ave. today.", "Ave");
        var (_, whole) = Resolve("Mr. Smith left.", "Mr.");

        // Assert
        Assert.True(separate.IncludesPeriodToken);
        Assert.False(whole.IncludesPeriodToken);
    }

    /// <summary>Proves a single-letter initial before a name or another initial is the letter, and before a sentence-starting word it is not.</summary>
    [Fact]
    public void AbbreviationResolver_TryResolve_Initials_ResolveOnlyBeforeNames()
    {
        // Act
        var (foundName, name) = Resolve("J. Smith left.", "J.");
        var (foundInitial, initial) = Resolve("A. B. Jones left.", "A.");
        var (foundStarter, _) = Resolve("Add vitamin C. The end.", "C.");

        // Assert
        Assert.True(foundName);
        Assert.Equal("J", name.Spoken);
        Assert.Equal(AbbreviationClass.Prefix, name.Class);
        Assert.True(foundInitial);
        Assert.Equal("A", initial.Spoken);
        Assert.False(foundStarter);
    }

    /// <summary>Proves ordinary words, lower-case words and words that merely start like a form are not abbreviations.</summary>
    /// <param name="text">The text.</param>
    /// <param name="word">The start of the token.</param>
    [Theory]
    [InlineData("The cat sat.", "cat")]
    [InlineData("A mister came.", "mister")]
    [InlineData("Doctors work.", "Doctors")]
    [InlineData("Drive home.", "Drive")]
    [InlineData("Stop here.", "Stop")]
    [InlineData("a very long unlisted expression here.", "expression")]
    public void AbbreviationResolver_TryResolve_OrdinaryWords_AreNotAbbreviations(string text, string word)
    {
        // Act
        var (found, _) = Resolve(text, word);

        // Assert
        Assert.False(found);
    }

    /// <summary>Proves a lexicon with several readings but no prefix/suffix pair is decided by its default.</summary>
    [Fact]
    public void AbbreviationResolver_FromTsv_NonPairAlternatives_UseDefaultWithoutCounting()
    {
        // Arrange: Fr. is a free reading or a word reading; no prefix/suffix pair to weigh
        var resolver = AbbreviationResolver.FromTsv(new StringReader(Header + "Fr.\tFriar\tfree\tyes\t-\tn\nFr.\tFr\tword\tno\t-\tn\n"));
        const string text = "See Fr. Tuck.";
        var tokens = NormalizationHarness.Tokenize(text);

        // Act
        var found = resolver.TryResolve(text, tokens, 1, out var match);

        // Assert
        Assert.True(found);
        Assert.Equal("Friar", match.Spoken);
        Assert.False(match.UsedDefault);
    }

    /// <summary>Proves the loader accepts comments, blank lines, CRLF endings and a custom table.</summary>
    [Fact]
    public void AbbreviationResolver_FromTsv_CommentsBlankLinesAndCrLf_AreAccepted()
    {
        // Arrange
        var content = "# comment\r\n\r\n" + Header.Replace("\n", "\r\n") + "Zz.\tZed\tprefix\tyes\t-\tnote\r\n";

        // Act
        var resolver = AbbreviationResolver.FromTsv(new StringReader(content));

        // Assert
        Assert.Equal(1, resolver.Count);
    }

    /// <summary>Proves malformed rows are rejected with an error that names the one-based line number.</summary>
    /// <param name="body">The rows after the header.</param>
    /// <param name="expectedLine">The line number the message must name.</param>
    [Theory]
    [InlineData("A.\tAy\tprefix\tyes\t-\n", 5)]
    [InlineData("\tAy\tprefix\tyes\t-\tn\n", 5)]
    [InlineData("A.\t\tprefix\tyes\t-\tn\n", 5)]
    [InlineData("A.\tAy\tsideways\tyes\t-\tn\n", 5)]
    [InlineData("A.\tAy\tprefix\tmaybe\t-\tn\n", 5)]
    [InlineData("A.\tAy\tprefix\tyes\tnext-door\tn\n", 5)]
    [InlineData("B.\tBee\tprefix\tyes\t-\tn\nA.\tAy\tprefix\tyes\t-\tn\nA.\tAy\tprefix\tyes\t-\tn\n", 7)]
    [InlineData("A.\tAy\tprefix\tyes\t-\tn\nA.\tAyy\tsuffix\tyes\t-\tn\n", 5)]
    [InlineData("A.\tAy\tprefix\tno\t-\tn\nA.\tAyy\tsuffix\tno\t-\tn\n", 5)]
    [InlineData("A.\tAy\tprefix\tno\t-\tn\n", 5)]
    public void AbbreviationResolver_FromTsv_MalformedRow_ThrowsInvalidDataExceptionNamingTheLine(string body, int expectedLine)
    {
        // Arrange: comment lines are lines 1-3, the header is line 4 and the first data row is line 5
        var content = "# one\n# two\n# three\n" + Header + body;

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => AbbreviationResolver.FromTsv(new StringReader(content)));

        // Assert
        Assert.Contains($"Line {expectedLine}", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a null reader is rejected.</summary>
    [Fact]
    public void AbbreviationResolver_FromTsv_NullReader_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => AbbreviationResolver.FromTsv(null!));
    }

    /// <summary>Proves many threads racing on the first lookups all get the documented resolutions, the same as a sequential lookup.</summary>
    [Fact]
    public async Task AbbreviationResolver_TryResolve_ConcurrentFirstLookups_ReturnDocumentedResolutions()
    {
        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(
            () =>
            {
                var title = Resolve("Dr. Smith lives here.", "Dr");
                var street = Resolve("They live on Maple Dr. He likes it.", "Dr");
                var month = Resolve("Jan. 5 is cold.", "Jan");
                return (title.Found, title.Match.Spoken, title.Match.Class, street.Match.Spoken, street.Match.Class, month.Match.Spoken);
            },
            TestContext.Current.CancellationToken)));

        // Assert
        Assert.All(results, result => Assert.Equal((true, "Doctor", AbbreviationClass.Prefix, "Drive", AbbreviationClass.Suffix, "January"), result));
    }

    /// <summary>The header line of a lexicon, used to build small custom tables.</summary>
    private const string Header = "form\tspoken\tclass\tdefault\tguard\tnote\n";
}
