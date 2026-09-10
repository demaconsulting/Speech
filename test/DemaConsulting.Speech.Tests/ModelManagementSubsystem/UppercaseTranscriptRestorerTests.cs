using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for <see cref="UppercaseTranscriptRestorer"/>, proving its contraction
///     restoration, "deliberately ambiguous" exclusions, capitalization, terminal-punctuation
///     insertion, and the cheap-path behavior of <see cref="UppercaseTranscriptRestorer.RestoreProvisional"/>.
/// </summary>
/// <remarks>
///     Thread safety is structurally guaranteed rather than exercised by a concurrency stress
///     test: every member is a pure function over immutable, compiled, <see langword="static readonly"/>
///     regex fields with no mutable shared state, matching how other pure parsers in this
///     codebase (e.g. <c>AudioTagParser</c>) are verified.
/// </remarks>
public class UppercaseTranscriptRestorerTests
{
    /// <summary>Every contraction form this restorer is expected to restore, and its expected result.</summary>
    public static TheoryData<string, string> ContractionCases => new()
    {
        { "IM", "I'm" },
        { "IVE", "I've" },
        { "DONT", "don't" },
        { "CANT", "can't" },
        { "WONT", "won't" },
        { "DIDNT", "didn't" },
        { "DOESNT", "doesn't" },
        { "ISNT", "isn't" },
        { "ARENT", "aren't" },
        { "WASNT", "wasn't" },
        { "WERENT", "weren't" },
        { "HAVENT", "haven't" },
        { "HASNT", "hasn't" },
        { "HADNT", "hadn't" },
        { "WOULDNT", "wouldn't" },
        { "COULDNT", "couldn't" },
        { "SHOULDNT", "shouldn't" },
        { "THATS", "that's" },
        { "WHATS", "what's" },
        { "THERES", "there's" },
        { "HERES", "here's" },
        { "YOURE", "you're" },
        { "YOULL", "you'll" },
        { "YOUVE", "you've" },
        { "THEYRE", "they're" },
        { "THEYLL", "they'll" },
        { "THEYVE", "they've" },
        { "WEVE", "we've" },
    };

    /// <summary>Every form deliberately left alone, ported from the reference exclusion list.</summary>
    public static TheoryData<string> DeliberatelyAmbiguousCases =>
        [.. UppercaseTranscriptRestorer.DeliberatelyAmbiguousForms()];

    /// <summary>
    ///     Proves that <see cref="UppercaseTranscriptRestorer.RestoreFinal"/> restores every
    ///     ported contraction form as a whole word, case-insensitively.
    /// </summary>
    [Theory]
    [MemberData(nameof(ContractionCases))]
    public void RestoreFinal_ContractionForm_RestoresApostrophe(string rawWord, string expectedWord)
    {
        // Act
        var restored = UppercaseTranscriptRestorer.RestoreFinal($"{rawWord} WORKING");

        // Assert: the contraction is restored and the sentence is capitalized/punctuated
        var expectedFirstWord = char.ToUpperInvariant(expectedWord[0]) + expectedWord[1..];
        Assert.Equal($"{expectedFirstWord} working.", restored);
    }

    /// <summary>
    ///     Proves that every form in the "deliberately ambiguous" exclusion list is left
    ///     unchanged (aside from lowercasing/capitalization), never rewritten to a contraction.
    /// </summary>
    [Theory]
    [MemberData(nameof(DeliberatelyAmbiguousCases))]
    public void RestoreFinal_DeliberatelyAmbiguousForm_IsLeftAlone(string ambiguousWord)
    {
        // Act
        var restored = UppercaseTranscriptRestorer.RestoreFinal($"{ambiguousWord.ToUpperInvariant()} WORKING");

        // Assert: only capitalization/terminal punctuation applied; no apostrophe introduced
        var expectedFirstWord = char.ToUpperInvariant(ambiguousWord[0]) + ambiguousWord[1..];
        Assert.Equal($"{expectedFirstWord} working.", restored);
    }

    /// <summary>
    ///     Proves that the standalone word "i" is capitalized to "I".
    /// </summary>
    [Fact]
    public void RestoreFinal_StandaloneI_IsCapitalized()
    {
        // Act
        var restored = UppercaseTranscriptRestorer.RestoreFinal("I THINK I KNOW");

        // Assert
        Assert.Equal("I think I know.", restored);
    }

    /// <summary>
    ///     Proves that the first alphabetic character of the text is capitalized.
    /// </summary>
    [Fact]
    public void RestoreFinal_FirstLetter_IsCapitalized()
    {
        // Act
        var restored = UppercaseTranscriptRestorer.RestoreFinal("hello there");

        // Assert
        Assert.Equal("Hello there.", restored);
    }

    /// <summary>
    ///     Proves that text already ending in a period, question mark, or exclamation mark is
    ///     left with that exact terminator, and no second one is appended.
    /// </summary>
    [Theory]
    [InlineData("HELLO THERE.", "Hello there.")]
    [InlineData("ARE YOU THERE?", "Are you there?")]
    [InlineData("WATCH OUT!", "Watch out!")]
    public void RestoreFinal_AlreadyTerminated_KeepsExistingTerminator(string raw, string expected)
    {
        // Act
        var restored = UppercaseTranscriptRestorer.RestoreFinal(raw);

        // Assert
        Assert.Equal(expected, restored);
    }

    /// <summary>
    ///     Proves that text with no terminal punctuation has a period appended.
    /// </summary>
    [Fact]
    public void RestoreFinal_NoTerminator_AppendsPeriod()
    {
        // Act
        var restored = UppercaseTranscriptRestorer.RestoreFinal("HELLO THERE");

        // Assert
        Assert.Equal("Hello there.", restored);
    }

    /// <summary>
    ///     Proves that empty or whitespace-only input restores to the empty string rather than
    ///     throwing or producing a bare period.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RestoreFinal_EmptyOrWhitespace_ReturnsEmptyString(string raw)
    {
        // Act
        var restored = UppercaseTranscriptRestorer.RestoreFinal(raw);

        // Assert
        Assert.Equal(string.Empty, restored);
    }

    /// <summary>
    ///     Proves that <see cref="ArgumentNullException"/> is thrown for a null input rather than
    ///     silently treating it as empty.
    /// </summary>
    [Fact]
    public void RestoreFinal_NullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => UppercaseTranscriptRestorer.RestoreFinal(null!));
    }

    /// <summary>
    ///     Proves that running <see cref="UppercaseTranscriptRestorer.RestoreFinal"/> a second
    ///     time over its own output is a no-op (idempotent), since already-restored text is
    ///     already lowercase-with-contractions, capitalized, and terminated.
    /// </summary>
    [Fact]
    public void RestoreFinal_AlreadyRestoredText_IsIdempotent()
    {
        // Arrange
        var restoredOnce = UppercaseTranscriptRestorer.RestoreFinal("I DONT THINK THATS WORKING");

        // Act
        var restoredTwice = UppercaseTranscriptRestorer.RestoreFinal(restoredOnce);

        // Assert
        Assert.Equal(restoredOnce, restoredTwice);
    }

    /// <summary>
    ///     Proves that <see cref="UppercaseTranscriptRestorer.RestoreProvisional"/> does not
    ///     restore contractions - the cheap path is casing-only.
    /// </summary>
    [Fact]
    public void RestoreProvisional_ContractionForm_DoesNotRestoreApostrophe()
    {
        // Act
        var restored = UppercaseTranscriptRestorer.RestoreProvisional("IM NOT SURE");

        // Assert: casing applied, but "im" is left exactly as-is (no apostrophe)
        Assert.Equal("Im not sure", restored);
    }

    /// <summary>
    ///     Proves that <see cref="UppercaseTranscriptRestorer.RestoreProvisional"/> does not
    ///     append terminal punctuation - the cheap path never adds a period.
    /// </summary>
    [Fact]
    public void RestoreProvisional_NoTerminator_DoesNotAppendPeriod()
    {
        // Act
        var restored = UppercaseTranscriptRestorer.RestoreProvisional("HELLO THERE");

        // Assert
        Assert.Equal("Hello there", restored);
    }

    /// <summary>
    ///     Proves that <see cref="UppercaseTranscriptRestorer.RestoreProvisional"/> still
    ///     capitalizes the standalone word "I" and the first letter.
    /// </summary>
    [Fact]
    public void RestoreProvisional_StandaloneIAndFirstLetter_AreCapitalized()
    {
        // Act
        var restored = UppercaseTranscriptRestorer.RestoreProvisional("i think i know");

        // Assert
        Assert.Equal("I think I know", restored);
    }

    /// <summary>
    ///     Proves that empty or whitespace-only input restores to the empty string.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RestoreProvisional_EmptyOrWhitespace_ReturnsEmptyString(string raw)
    {
        // Act
        var restored = UppercaseTranscriptRestorer.RestoreProvisional(raw);

        // Assert
        Assert.Equal(string.Empty, restored);
    }

    /// <summary>
    ///     Proves that <see cref="ArgumentNullException"/> is thrown for a null input.
    /// </summary>
    [Fact]
    public void RestoreProvisional_NullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => UppercaseTranscriptRestorer.RestoreProvisional(null!));
    }
}
