using DemaConsulting.Speech.Text.En.NormalizationSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.NormalizationSubsystem;

/// <summary>
///     Unit tests for <see cref="AbbreviationResolver"/>: the embedded lexicon, the context cues
///     that choose among the readings of <c>St.</c>, <c>Dr.</c>, <c>Sr.</c> and <c>No.</c>, the
///     neighbor guards, the default-and-count rule, the strict loader and the thread-safe lazy
///     default. Tokens come from the real tokenizer through the test harness.
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
        Assert.Same(resolver, AbbreviationResolver.Default);
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

    /// <summary>Proves many threads racing on the lazy default all get the one fully loaded instance.</summary>
    [Fact]
    public async Task AbbreviationResolver_Default_ConcurrentFirstUse_ReturnsOneInstance()
    {
        // Act
        var instances = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() => AbbreviationResolver.Default, TestContext.Current.CancellationToken)));

        // Assert
        Assert.All(instances, instance => Assert.Same(instances[0], instance));
        Assert.True(instances[0].Count > 0);
    }

    /// <summary>The header line of a lexicon, used to build small custom tables.</summary>
    private const string Header = "form\tspoken\tclass\tdefault\tguard\tnote\n";
}
