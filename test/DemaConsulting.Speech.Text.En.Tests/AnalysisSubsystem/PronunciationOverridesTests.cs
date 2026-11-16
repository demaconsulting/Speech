using System.Globalization;
using System.Text;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.NormalizationSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.AnalysisSubsystem;

/// <summary>
///     Unit tests for <see cref="PronunciationOverrides"/> and <see cref="PronunciationOverrideEntry"/>:
///     entry validation, payload grammars, duplicate rules, limits, the tab-separated loader,
///     immutability, case rules, whole-word matching, the directives that come out of a lookup,
///     cancellation, allocation and argument validation. The collaborators are real (the shared
///     override syntax and the real tokenizer through the harness); no fake is needed.
/// </summary>
public sealed class PronunciationOverridesTests
{
    /// <summary>A word that cannot occur in any fixed message, used to prove messages never echo dictionary content.</summary>
    private const string Sentinel = "Sentinelx9";

    /// <summary>The documented sample file next to the test assembly.</summary>
    private const string SampleFileName = "overrides-dictionary.tsv";

    /// <summary>Resolves a dictionary against a text, tokenized by the real tokenizer.</summary>
    /// <param name="overrides">The dictionary.</param>
    /// <param name="text">The text; placeholders are neutralized first.</param>
    /// <returns>The directives and the tokens they index.</returns>
    private static (TokenDirective[] Directives, NormalizationToken[] Tokens, string Neutralized) Resolve(PronunciationOverrides overrides, string text)
    {
        var neutralized = NormalizationHarness.Neutralize(text);
        var tokens = NormalizationHarness.Tokenize(neutralized);
        return (overrides.Resolve(neutralized, tokens, CancellationToken.None), tokens, neutralized);
    }

    /// <summary>Lists the source words of the tokens that got a directive.</summary>
    /// <param name="overrides">The dictionary.</param>
    /// <param name="text">The text.</param>
    /// <returns>The matched tokens' text, in order.</returns>
    private static string[] Matched(PronunciationOverrides overrides, string text)
    {
        var (directives, tokens, neutralized) = Resolve(overrides, text);
        return [.. directives.Select(directive => tokens[directive.TokenIndex].In(neutralized).ToString())];
    }

    /// <summary>Resolves a single-token text and returns the directive's value.</summary>
    /// <param name="overrides">The dictionary.</param>
    /// <param name="text">The one-word text.</param>
    /// <returns>The value of the one directive, or null when nothing matched or the directive has no value.</returns>
    private static string? ValueOf(PronunciationOverrides overrides, string text)
    {
        var (directives, _, _) = Resolve(overrides, text);
        return directives.Length == 1 ? directives[0].Value : null;
    }

    /// <summary>Reads the documented sample file.</summary>
    /// <returns>The file's text.</returns>
    private static string ReadSample() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", SampleFileName));

    /// <summary>Turns an index into a four-letter lowercase word, so thousands of distinct valid words are easy to make.</summary>
    /// <param name="index">The index, below 26 to the fourth.</param>
    /// <returns>The word.</returns>
    private static string Word(int index) =>
        new(new[] { (char)('a' + (index / 17576 % 26)), (char)('a' + (index / 676 % 26)), (char)('a' + (index / 26 % 26)), (char)('a' + (index % 26)) });

    // ---------------------------------------------------------------- entry validation

    /// <summary>Proves every invalid word is rejected with the entry's index and a reason, and the message never contains the word.</summary>
    /// <param name="word">The invalid word.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("ab cd")]
    [InlineData(" ab")]
    [InlineData("-ab")]
    [InlineData("'ab")]
    [InlineData("\u2019ab")]
    [InlineData(".ab")]
    [InlineData("1ab")]
    [InlineData("ab-")]
    [InlineData("ab.")]
    [InlineData("ab'")]
    [InlineData("ab\u2019")]
    [InlineData("Dr.")]
    [InlineData("U.S.A.")]
    [InlineData("ab,cd")]
    [InlineData("ab;cd")]
    [InlineData("ab:cd")]
    [InlineData("ab\tcd")]
    [InlineData("ab\ncd")]
    [InlineData("ab\0cd")]
    [InlineData("ab\u200Bcd")]
    [InlineData("ab\uFFFCcd")]
    [InlineData("ab\uFFFDcd")]
    [InlineData("ab\uD800cd")]
    [InlineData("ab\uDC00cd")]
    [InlineData("ab\uD800")]
    [InlineData("ab\uE000cd")]
    [InlineData("ab\u0378cd")]
    [InlineData("ab\u00A0cd")]
    [InlineData("ab\u2028cd")]
    [InlineData("ab*cd")]
    [InlineData("ab(cd")]
    [InlineData("ab\U0001F600cd")]
    [InlineData("d'o\u2019x")]
    [InlineData("\u0301ab")]
    [InlineData("e\u0301")]
    public void PronunciationOverrides_Create_InvalidWord_ThrowsWithEntryIndexAndNoContent(string word)
    {
        // Arrange: a valid first entry, so the bad one is at index 1
        PronunciationOverrideEntry[] entries = [PronunciationOverrideEntry.Skip("good"), new PronunciationOverrideEntry(word, false, OverrideAction.Skip)];

        // Act
        var exception = Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create(entries));

        // Assert
        Assert.Contains("Entry 1:", exception.Message, StringComparison.Ordinal);
        if (word.Length > 1)
        {
            Assert.DoesNotContain(word, exception.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>Proves a word that is too long is rejected, and one of exactly the maximum length is accepted.</summary>
    [Fact]
    public void PronunciationOverrides_Create_WordLengthBoundary_RejectsOnlyTooLong()
    {
        // Act
        var accepted = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip(new string('a', PronunciationOverrides.MaxWordLength))]);
        var exception = Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create([PronunciationOverrideEntry.Skip(new string('a', PronunciationOverrides.MaxWordLength + 1))]));

        // Assert
        Assert.Equal(1, accepted.Count);
        Assert.StartsWith("Entry 0:", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves the documented word shapes are accepted: letters of any script, supplementary letters, digits at the end, marks inside, and the allowed punctuation.</summary>
    /// <param name="word">The valid word.</param>
    [Theory]
    [InlineData("a")]
    [InlineData("NASA")]
    [InlineData("Wi-Fi")]
    [InlineData("O'Brien")]
    [InlineData("O\u2019Brien")]
    [InlineData("AT&T")]
    [InlineData("and/or")]
    [InlineData("foo_bar")]
    [InlineData("Ph.D")]
    [InlineData("x86")]
    [InlineData("ab1")]
    [InlineData("\U00010400\U00010401x")]
    [InlineData("caf\u00E9")]
    [InlineData("cafe\u0301s")]
    [InlineData("\u0130stanbul")]
    [InlineData("\u0645\u0631\u062D\u0628\u0627")]
    [InlineData("\u4F60\u597D")]
    [InlineData("a\u0661")]
    public void PronunciationOverrides_Create_ValidWord_IsAccepted(string word)
    {
        // Act
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip(word)]);

        // Assert
        Assert.Equal(word, Assert.Single(overrides.Entries).Word);
    }

    /// <summary>Proves an undefined action (including the value reserved for a later reading action) is rejected.</summary>
    /// <param name="action">The undefined action value.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(9)]
    [InlineData(-1)]
    public void PronunciationOverrides_Create_UndefinedAction_Throws(int action)
    {
        // Act
        var exception = Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create([new PronunciationOverrideEntry("word", false, (OverrideAction)action)]));

        // Assert
        Assert.StartsWith("Entry 0:", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a payload that does not fit the action (a payload on skip or spell, none on say or pron) is rejected without echoing it.</summary>
    [Fact]
    public void PronunciationOverrides_Create_ActionPayloadMismatch_Throws()
    {
        // Arrange
        PronunciationOverrideEntry[] bad =
        [
            new("word", false, OverrideAction.Skip, Sentinel),
            new("word", false, OverrideAction.Skip, string.Empty),
            new("word", false, OverrideAction.SpellOut, Sentinel),
            new("word", false, OverrideAction.Say),
            new("word", false, OverrideAction.Say, string.Empty),
            new("word", false, OverrideAction.Pronunciation),
            new("word", false, OverrideAction.Pronunciation, string.Empty),
        ];

        // Act / Assert
        foreach (var entry in bad)
        {
            var exception = Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create([entry]));
            Assert.StartsWith("Entry 0:", exception.Message, StringComparison.Ordinal);
            Assert.DoesNotContain(Sentinel, exception.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>Proves a spell-out word must be 1 to 32 ASCII letters (letter names exist only for A to Z).</summary>
    /// <param name="word">The word that cannot be spelled.</param>
    [Theory]
    [InlineData("caf\u00E9")]
    [InlineData("ab1")]
    [InlineData("a-b")]
    [InlineData("O'Brien")]
    [InlineData("\u0130B")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void PronunciationOverrides_Create_SpellOutNonAsciiOrTooLong_Throws(string word)
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create([PronunciationOverrideEntry.SpellOut(word)]));
        Assert.Single(PronunciationOverrides.Create([PronunciationOverrideEntry.SpellOut("abcdefghijklmnopqrstuvwxyzABCDEF")]).Entries);
    }

    /// <summary>Proves a null entry list and a null element are rejected with an argument-null exception.</summary>
    [Fact]
    public void PronunciationOverrides_Create_NullEntriesOrElement_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => PronunciationOverrides.Create(null!));
        Assert.Throws<ArgumentNullException>(() => PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("a"), null!]));
        Assert.Throws<ArgumentNullException>(() => PronunciationOverrideEntry.Skip(null!));
        Assert.Throws<ArgumentNullException>(() => PronunciationOverrideEntry.Say("a", null!));
        Assert.Throws<ArgumentNullException>(() => PronunciationOverrideEntry.Say(null!, "b"));
        Assert.Throws<ArgumentNullException>(() => PronunciationOverrideEntry.SpellOut(null!));
        Assert.Throws<ArgumentNullException>(() => PronunciationOverrideEntry.Pronounce(null!, PronunciationScheme.Ipa, "a"));
        Assert.Throws<ArgumentNullException>(() => PronunciationOverrideEntry.Pronounce("a", PronunciationScheme.Ipa, null!));
        Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create([new PronunciationOverrideEntry(null!, false, OverrideAction.Skip)]));
    }

    // ---------------------------------------------------------------- payload grammar

    /// <summary>Proves the spoken-text grammar of a say payload.</summary>
    /// <param name="payload">The payload.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData("engine ex", true)]
    [InlineData("a", true)]
    [InlineData("o'brien", true)]
    [InlineData("well-known fact", true)]
    [InlineData("r\u00E9sum\u00E9", true)]
    [InlineData("cafe\u0301", true)]
    [InlineData("-a", true)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData(" a", false)]
    [InlineData("a ", false)]
    [InlineData("a  b", false)]
    [InlineData("a1", false)]
    [InlineData("1", false)]
    [InlineData("a,b", false)]
    [InlineData("a.b", false)]
    [InlineData("a;b", false)]
    [InlineData("a:b", false)]
    [InlineData("a!b", false)]
    [InlineData("'", false)]
    [InlineData("-", false)]
    [InlineData("a\tb", false)]
    [InlineData("a\u00A0b", false)]
    [InlineData("a\u200Bb", false)]
    [InlineData("a\uFFFCb", false)]
    [InlineData("a_b", false)]
    [InlineData("a\uD800b", false)]
    public void PronunciationOverrides_Create_SayPayload_GrammarEnforced(string payload, bool valid)
    {
        // Act
        var exception = Record.Exception(() => PronunciationOverrides.Create([new PronunciationOverrideEntry("word", false, OverrideAction.Say, payload)]));

        // Assert
        Assert.Equal(valid, exception is null);
        if (exception is not null)
        {
            Assert.IsType<ArgumentException>(exception);
        }
    }

    /// <summary>Proves the length bound of a say payload: exactly the maximum is accepted, one more is not.</summary>
    [Fact]
    public void PronunciationOverrides_Create_SayPayloadLength_BoundaryIsInclusive()
    {
        // Act / Assert
        Assert.Single(PronunciationOverrides.Create([PronunciationOverrideEntry.Say("word", new string('a', PronunciationOverrides.MaxPayloadLength))]).Entries);
        Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create([PronunciationOverrideEntry.Say("word", new string('a', PronunciationOverrides.MaxPayloadLength + 1))]));
    }

    /// <summary>Proves the scheme list is closed and each scheme's phonemes follow that scheme's syntax.</summary>
    /// <param name="payload">The full pronunciation payload.</param>
    /// <param name="valid">Whether it is accepted.</param>
    [Theory]
    [InlineData("ipa:\u02C8po\u028Al\u026A\u0283", true)]
    [InlineData("ipa:a.b", true)]
    [InlineData("ipa:p\u0251\u02D0:l", true)]
    [InlineData("ipa:\u0283", true)]
    [InlineData("arpabet:P OW1 L IH0 SH", true)]
    [InlineData("arpabet:K", true)]
    [InlineData("arpabet:AA2", true)]
    [InlineData("", false)]
    [InlineData("abc", false)]
    [InlineData("ipa", false)]
    [InlineData("ipa:", false)]
    [InlineData("IPA:abc", false)]
    [InlineData("Ipa:abc", false)]
    [InlineData("xsampa:abc", false)]
    [InlineData("sapi:abc", false)]
    [InlineData(":abc", false)]
    [InlineData(" ipa:abc", false)]
    [InlineData("ipa: abc", false)]
    [InlineData("ipa:a b", false)]
    [InlineData("ipa:a\tb", false)]
    [InlineData("ipa:a\u0007b", false)]
    [InlineData("ipa:a\u200Bb", false)]
    [InlineData("ipa:a\uE000b", false)]
    [InlineData("ipa:a\uFFFCb", false)]
    [InlineData("ipa:a\uFFFDb", false)]
    [InlineData("ipa:a\uD800b", false)]
    [InlineData("arpabet:", false)]
    [InlineData("arpabet:p ow1", false)]
    [InlineData("arpabet:P  OW1", false)]
    [InlineData("arpabet: P", false)]
    [InlineData("arpabet:P ", false)]
    [InlineData("arpabet:P OW3", false)]
    [InlineData("arpabet:PHON", false)]
    [InlineData("arpabet:P1 1", false)]
    [InlineData("arpabet:P-OW", false)]
    public void PronunciationOverrides_Create_PronunciationPayload_SchemeAndSyntaxEnforced(string payload, bool valid)
    {
        // Act
        var exception = Record.Exception(() => PronunciationOverrides.Create([new PronunciationOverrideEntry("word", false, OverrideAction.Pronunciation, payload)]));

        // Assert
        Assert.Equal(valid, exception is null);
    }

    /// <summary>Proves the phoneme length bounds: 128 IPA code units and 32 ARPAbet phones are accepted, one more is not.</summary>
    [Fact]
    public void PronunciationOverrides_Create_PhonemeLengthBounds_AreInclusive()
    {
        // Arrange
        var ipaMax = "ipa:" + new string('\u0283', OverrideSyntax.MaxIpaLength);
        var ipaOver = "ipa:" + new string('\u0283', OverrideSyntax.MaxIpaLength + 1);
        var arpaMax = "arpabet:" + string.Join(' ', Enumerable.Repeat("AA1", OverrideSyntax.MaxArpabetPhones));
        var arpaOver = "arpabet:" + string.Join(' ', Enumerable.Repeat("AA1", OverrideSyntax.MaxArpabetPhones + 1));
        var totalOver = "ipa:" + new string('\u0283', PronunciationOverrides.MaxPayloadLength);

        // Act / Assert
        Assert.Null(Record.Exception(() => PronunciationOverrides.Create([new PronunciationOverrideEntry("word", false, OverrideAction.Pronunciation, ipaMax)])));
        Assert.Null(Record.Exception(() => PronunciationOverrides.Create([new PronunciationOverrideEntry("word", false, OverrideAction.Pronunciation, arpaMax)])));
        Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create([new PronunciationOverrideEntry("word", false, OverrideAction.Pronunciation, ipaOver)]));
        Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create([new PronunciationOverrideEntry("word", false, OverrideAction.Pronunciation, arpaOver)]));
        Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create([new PronunciationOverrideEntry("word", false, OverrideAction.Pronunciation, totalOver)]));
    }

    /// <summary>Proves the four factories produce exactly the entries they document, with the lower-case scheme name in the pronunciation payload.</summary>
    [Fact]
    public void PronunciationOverrides_Factories_ProduceValidatedEntries()
    {
        // Act
        var skip = PronunciationOverrideEntry.Skip("NASA", caseSensitive: true);
        var say = PronunciationOverrideEntry.Say("Nginx", "engine ex");
        var spell = PronunciationOverrideEntry.SpellOut("GIF", true);
        var ipa = PronunciationOverrideEntry.Pronounce("Polish", PronunciationScheme.Ipa, "\u02C8po\u028Al\u026A\u0283", true);
        var arpabet = PronunciationOverrideEntry.Pronounce("cafe", PronunciationScheme.Arpabet, "K AE0 F EY1");

        // Assert
        Assert.Equal(new PronunciationOverrideEntry("NASA", true, OverrideAction.Skip), skip);
        Assert.Equal(new PronunciationOverrideEntry("Nginx", false, OverrideAction.Say, "engine ex"), say);
        Assert.Equal(new PronunciationOverrideEntry("GIF", true, OverrideAction.SpellOut), spell);
        Assert.Equal("ipa:\u02C8po\u028Al\u026A\u0283", ipa.Payload);
        Assert.Equal("arpabet:K AE0 F EY1", arpabet.Payload);
        Assert.Equal(5, PronunciationOverrides.Create([skip, say, spell, ipa, arpabet]).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => PronunciationOverrideEntry.Pronounce("a", (PronunciationScheme)7, "b"));
        Assert.Equal(1, (int)OverrideAction.Skip);
        Assert.Equal(2, (int)OverrideAction.Say);
        Assert.Equal(3, (int)OverrideAction.SpellOut);
        Assert.Equal(4, (int)OverrideAction.Pronunciation);
        Assert.Equal(1, (int)PronunciationScheme.Ipa);
        Assert.Equal(2, (int)PronunciationScheme.Arpabet);
    }

    // ---------------------------------------------------------------- duplicates

    /// <summary>Proves a duplicate is an error that names the later entry: same word with the same case flag, with insensitive duplicates and apostrophe styles unified.</summary>
    /// <param name="first">The first word.</param>
    /// <param name="second">The second word.</param>
    /// <param name="caseSensitive">The case flag of both entries.</param>
    [Theory]
    [InlineData("NASA", "NASA", true)]
    [InlineData("NASA", "NASA", false)]
    [InlineData("NASA", "nasa", false)]
    [InlineData("don't", "don\u2019t", true)]
    [InlineData("don't", "don\u2019t", false)]
    [InlineData("Don't", "DON\u2019T", false)]
    [InlineData("\u0130stanbul", "\u0130STANBUL", false)]
    public void PronunciationOverrides_Create_DuplicateKeys_Throws(string first, string second, bool caseSensitive)
    {
        // Act
        var exception = Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create(
            [PronunciationOverrideEntry.Skip(first, caseSensitive), PronunciationOverrideEntry.Say(second, "other", caseSensitive)]));

        // Assert
        Assert.StartsWith("Entry 1:", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves entries that are not duplicates are accepted: a sensitive and an insensitive entry for one word, sensitive entries that differ in case, and keys that differ only in a dotted capital I.</summary>
    [Fact]
    public void PronunciationOverrides_Create_SensitiveAndInsensitiveSameWord_Allowed()
    {
        // Act
        var overrides = PronunciationOverrides.Create(
        [
            PronunciationOverrideEntry.Skip("Polish", true),
            PronunciationOverrideEntry.Say("Polish", "polish", false),
            PronunciationOverrideEntry.Skip("polish", true),
            PronunciationOverrideEntry.Skip("istanbul", false),
            PronunciationOverrideEntry.Skip("\u0130stanbul", false),
        ]);

        // Assert
        Assert.Equal(5, overrides.Count);
    }

    // ---------------------------------------------------------------- limits

    /// <summary>Proves exactly the maximum number of entries is accepted, one more is rejected, and an endless source is cut off instead of enumerated forever.</summary>
    [Fact]
    public void PronunciationOverrides_Create_EntryCountBoundary_IsInclusive()
    {
        // Arrange
        static IEnumerable<PronunciationOverrideEntry> Endless()
        {
            for (var i = 0; i < 456_976; i++)
            {
                yield return PronunciationOverrideEntry.Skip(Word(i));
            }
        }

        // Act
        var full = PronunciationOverrides.Create(Endless().Take(PronunciationOverrides.MaxEntries));
        var exception = Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create(Endless()));

        // Assert
        Assert.Equal(PronunciationOverrides.MaxEntries, full.Count);
        Assert.StartsWith($"Entry {PronunciationOverrides.MaxEntries}:", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a TSV line of exactly the maximum length is accepted and one more character is rejected, naming the line.</summary>
    [Fact]
    public void PronunciationOverrides_FromTsv_LineLengthBoundary_IsInclusive()
    {
        // Arrange: comment lines, so only the length matters
        var exact = "#" + new string('x', PronunciationOverrides.MaxTsvLineLength - 1);
        var over = "#" + new string('x', PronunciationOverrides.MaxTsvLineLength);

        // Act
        var accepted = PronunciationOverrides.FromTsv(new StringReader(exact + "\n"));
        var exception = Assert.Throws<InvalidDataException>(() => PronunciationOverrides.FromTsv(new StringReader("a\tsensitive\tskip\n" + over + "\n")));

        // Assert
        Assert.Equal(0, accepted.Count);
        Assert.StartsWith("Line 2:", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves exactly the maximum number of lines (blank ones count) is accepted and one more is rejected.</summary>
    [Fact]
    public void PronunciationOverrides_FromTsv_LineCountBoundary_IsInclusive()
    {
        // Act
        var accepted = PronunciationOverrides.FromTsv(new StringReader(new string('\n', PronunciationOverrides.MaxTsvLines)));
        var exception = Assert.Throws<InvalidDataException>(() => PronunciationOverrides.FromTsv(new StringReader(new string('\n', PronunciationOverrides.MaxTsvLines + 1))));

        // Assert
        Assert.Equal(0, accepted.Count);
        Assert.StartsWith($"Line {PronunciationOverrides.MaxTsvLines + 1}:", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves the entry limit applies to a file too: the first entry beyond the maximum is rejected by line number.</summary>
    [Fact]
    public void PronunciationOverrides_FromTsv_EntryCountBoundary_NamesTheLine()
    {
        // Arrange
        var builder = new StringBuilder();
        for (var i = 0; i <= PronunciationOverrides.MaxEntries; i++)
        {
            builder.Append(Word(i)).Append("\tsensitive\tskip\n");
        }

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => PronunciationOverrides.FromTsv(new StringReader(builder.ToString())));

        // Assert
        Assert.StartsWith($"Line {PronunciationOverrides.MaxEntries + 1}:", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a reader that never produces a line break is abandoned after the line limit, not read to its (nonexistent) end.</summary>
    [Fact]
    public void PronunciationOverrides_FromTsv_EndlessInputWithoutNewline_StopsAtLimit()
    {
        // Arrange
        using var reader = new EndlessReader('a');

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => PronunciationOverrides.FromTsv(reader));

        // Assert
        Assert.StartsWith("Line 1:", exception.Message, StringComparison.Ordinal);
        Assert.True(reader.CharactersRead <= PronunciationOverrides.MaxTsvLineLength + 1, $"read {reader.CharactersRead} characters");
    }

    /// <summary>Proves an endless input of empty lines is abandoned at the line-count limit.</summary>
    [Fact]
    public void PronunciationOverrides_FromTsv_EndlessShortLines_StopsAtLineLimit()
    {
        // Arrange
        using var reader = new EndlessReader('\n');

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => PronunciationOverrides.FromTsv(reader));

        // Assert
        Assert.StartsWith($"Line {PronunciationOverrides.MaxTsvLines + 1}:", exception.Message, StringComparison.Ordinal);
        Assert.True(reader.CharactersRead <= PronunciationOverrides.MaxTsvLines + 1);
    }

    // ---------------------------------------------------------------- TSV loading

    /// <summary>Proves the documented sample file loads, in file order, to the entries the user guide describes.</summary>
    [Fact]
    public void PronunciationOverrides_FromTsv_SampleDictionary_ParsesEntries()
    {
        // Act
        var overrides = PronunciationOverrides.FromTsv(new StringReader(ReadSample()));

        // Assert: the sample of the user guide comes first, then the entries the golden rows use
        Assert.Equal(23, overrides.Count);
        Assert.Equal(
            [
                new PronunciationOverrideEntry("Nginx", false, OverrideAction.Say, "engine ex"),
                new PronunciationOverrideEntry("Kubernetes", true, OverrideAction.Say, "koo ber net eez"),
                new PronunciationOverrideEntry("GIF", true, OverrideAction.SpellOut),
                new PronunciationOverrideEntry("Polish", true, OverrideAction.Pronunciation, "ipa:\u02C8po\u028Al\u026A\u0283"),
                new PronunciationOverrideEntry("polish", false, OverrideAction.Pronunciation, "ipa:\u02C8p\u0251\u02D0l\u026A\u0283"),
                new PronunciationOverrideEntry("SQL", false, OverrideAction.Say, "sequel"),
                new PronunciationOverrideEntry("NASA", true, OverrideAction.Skip),
            ],
            overrides.Entries.Take(7));
    }

    /// <summary>Proves every key of the sample dictionary is one token of the real tokenizer, so a tokenizer change cannot silently make a documented key inert.</summary>
    [Fact]
    public void PronunciationOverrides_SampleDictionaryKeys_AreSingleTokensOfTheRealTokenizer()
    {
        // Arrange
        var overrides = PronunciationOverrides.FromTsv(new StringReader(ReadSample()));

        // Act / Assert: surrounded by plain words, each key is found as exactly one token
        foreach (var entry in overrides.Entries.Where(entry => entry.Word != "don't"))
        {
            var text = $"We said {entry.Word} twice.";
            var matched = Matched(overrides, text);
            Assert.True(matched.Contains(entry.Word, StringComparer.OrdinalIgnoreCase) || matched.Contains(entry.Word.Replace('\'', '\u2019'), StringComparer.OrdinalIgnoreCase), $"key '{entry.Word}' is not one token of the real tokenizer");
        }

        // the documented limitation: a contraction key never matches, because the tokenizer splits the contraction
        Assert.Empty(Matched(overrides, "I don't know"));
    }

    /// <summary>Proves LF, CRLF, mixed endings, a leading byte-order mark, a missing final newline and an empty fourth column are all accepted.</summary>
    [Fact]
    public void PronunciationOverrides_FromTsv_LineEndingsAndBom_AreAccepted()
    {
        // Arrange
        const string lf = "# c\n\na\tsensitive\tskip\nb\tinsensitive\tsay\tsome words\n";
        var crlf = lf.Replace("\n", "\r\n", StringComparison.Ordinal);
        const string mixed = "# c\r\n\na\tsensitive\tskip\nb\tinsensitive\tsay\tsome words";
        var bom = "\uFEFF" + lf;
        const string emptyFourth = "a\tsensitive\tskip\t\nb\tinsensitive\tsay\tsome words\n";
        PronunciationOverrideEntry[] expected = [PronunciationOverrideEntry.Skip("a", true), PronunciationOverrideEntry.Say("b", "some words")];

        // Act / Assert
        foreach (var text in new[] { lf, crlf, mixed, bom, emptyFourth })
        {
            Assert.Equal(expected, PronunciationOverrides.FromTsv(new StringReader(text)).Entries);
        }

        // a stream with a real byte-order mark is also read through the caller's StreamReader
        using var stream = new MemoryStream([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(lf)]);
        using var streamReader = new StreamReader(stream, Encoding.UTF8);
        Assert.Equal(expected, PronunciationOverrides.FromTsv(streamReader).Entries);
    }

    /// <summary>Proves a file and the same entries given to <see cref="PronunciationOverrides.Create"/> produce equal dictionaries.</summary>
    [Fact]
    public void PronunciationOverrides_FromTsv_EquivalentToCreate()
    {
        // Arrange
        var fromFile = PronunciationOverrides.FromTsv(new StringReader(ReadSample()));

        // Act
        var created = PronunciationOverrides.Create(fromFile.Entries);

        // Assert: same entries, same lookup results
        Assert.Equal(fromFile.Entries, created.Entries);
        const string text = "Nginx and Kubernetes, kubernetes; GIF gif Polish polish SQL sql NASA US FBI and Wi-Fi.";
        Assert.Equal(Resolve(fromFile, text).Directives, Resolve(created, text).Directives);
        Assert.NotEmpty(Resolve(created, text).Directives);
    }

    /// <summary>Proves a file that has only comments and blank lines gives the shared empty dictionary.</summary>
    [Fact]
    public void PronunciationOverrides_Empty_IsSharedAndEmpty()
    {
        // Act / Assert
        Assert.Same(PronunciationOverrides.Empty, PronunciationOverrides.Empty);
        Assert.Equal(0, PronunciationOverrides.Empty.Count);
        Assert.Empty(PronunciationOverrides.Empty.Entries);
        Assert.Same(PronunciationOverrides.Empty, PronunciationOverrides.Create([]));
        Assert.Same(PronunciationOverrides.Empty, PronunciationOverrides.FromTsv(new StringReader(string.Empty)));
        Assert.Same(PronunciationOverrides.Empty, PronunciationOverrides.FromTsv(new StringReader("# only a comment\n\n")));
        Assert.Empty(Resolve(PronunciationOverrides.Empty, "Nginx and SQL").Directives);
    }

    // ---------------------------------------------------------------- TSV validation

    /// <summary>Proves every kind of malformed row is rejected with the one-based line number and a reason, and the message never contains the row's content.</summary>
    /// <param name="content">The rows after two leading lines (a comment and a blank line), so the bad row is line 3.</param>
    [Theory]
    [InlineData("Sentinelx9\tsensitive")]
    [InlineData("Sentinelx9")]
    [InlineData("Sentinelx9\tsensitive\tskip\textra\tmore")]
    [InlineData("Sentinelx9\tsensitive\tskip\tpayload")]
    [InlineData("Sentinelx9\tsensitive\tspell\tpayload")]
    [InlineData("Sentinelx9\tmaybe\tskip")]
    [InlineData("Sentinelx9\tSensitive\tskip")]
    [InlineData("Sentinelx9\t\tskip")]
    [InlineData("Sentinelx9\tsensitive\tyell")]
    [InlineData("Sentinelx9\tsensitive\tSkip")]
    [InlineData("Sentinelx9\tsensitive\treading\tx")]
    [InlineData("Sentinelx9\tsensitive\tsay")]
    [InlineData("Sentinelx9\tsensitive\tsay\t")]
    [InlineData("Sentinelx9\tsensitive\tsay\tdigits 42")]
    [InlineData("Sentinelx9\tsensitive\tsay\t two")]
    [InlineData("Sentinelx9\tsensitive\tsay\ttwo ")]
    [InlineData("Sentinelx9\tsensitive\tpron\tSentinelx9")]
    [InlineData("Sentinelx9\tsensitive\tpron\tipa:")]
    [InlineData("Sentinelx9\tsensitive\tpron\txsampa:Sentinelx9")]
    [InlineData("Sentinelx9\tsensitive\tpron\tarpabet:sentinelx9")]
    [InlineData("word\tcase\taction")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\t\t")]
    [InlineData(" Sentinelx9\tsensitive\tskip")]
    [InlineData("Sentinelx9 \tsensitive\tskip")]
    [InlineData("Sentinelx9\t sensitive\tskip")]
    [InlineData("Sentinelx9\tsensitive\tskip ")]
    [InlineData("Sentinelx9\tsensitive\tskip\u00A0")]
    [InlineData("Sentinelx9\u0001\tsensitive\tskip")]
    [InlineData("Sentinelx9\tsensitive\tskip\u0007")]
    [InlineData("Sentinelx9\tsensitive\tskip\u0085")]
    [InlineData("Sentinelx9\tsensitive\tskip\rmore")]
    [InlineData("Sentinelx9\tsensitive\tskip\r\r")]
    [InlineData("Sentinelx9 two\tsensitive\tskip")]
    [InlineData("-Sentinelx9\tsensitive\tskip")]
    [InlineData("Sentinelx9.\tsensitive\tskip")]
    [InlineData("Sentinelx9,\tsensitive\tskip")]
    [InlineData("Sentinelx9\uD800\tsensitive\tskip")]
    [InlineData("Sentinelx9\u0130\tsensitive\tspell")]
    [InlineData("# comment\u0001")]
    public void PronunciationOverrides_FromTsv_MalformedRows_ThrowWithLineNumberAndNoContent(string content)
    {
        // Arrange: the bad row is the third line
        var text = "# header comment\n\n" + content + "\nok\tsensitive\tskip\n";

        // Act
        var exception = Assert.Throws<InvalidDataException>(() => PronunciationOverrides.FromTsv(new StringReader(text)));

        // Assert
        Assert.StartsWith("Line 3:", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Sentinel, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves a duplicate row is reported at the later line, and a null reader is rejected.</summary>
    [Fact]
    public void PronunciationOverrides_FromTsv_DuplicateAndNullReader_Throw()
    {
        // Act
        var exception = Assert.Throws<InvalidDataException>(() => PronunciationOverrides.FromTsv(new StringReader("a\tsensitive\tskip\n# gap\nA\tinsensitive\tskip\na\tsensitive\tskip\n")));

        // Assert
        Assert.StartsWith("Line 4:", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", exception.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentNullException>(() => PronunciationOverrides.FromTsv(null!));
    }

    /// <summary>Proves an exception of the reader itself propagates unchanged.</summary>
    [Fact]
    public void PronunciationOverrides_FromTsv_ReaderFailure_Propagates()
    {
        // Arrange
        using var reader = new FailingReader();

        // Act / Assert
        var exception = Assert.Throws<IOException>(() => PronunciationOverrides.FromTsv(reader));
        Assert.Equal("reader failed", exception.Message);
    }

    /// <summary>Proves the loader reads to the end but neither disposes nor retains the reader.</summary>
    [Fact]
    public void PronunciationOverrides_FromTsv_DoesNotDisposeReader()
    {
        // Arrange
        using var reader = new TrackingReader("a\tsensitive\tskip\n");

        // Act
        var overrides = PronunciationOverrides.FromTsv(reader);

        // Assert
        Assert.Equal(1, overrides.Count);
        Assert.False(reader.Disposed);
        Assert.Equal(-1, reader.Read());
    }

    // ---------------------------------------------------------------- immutability

    /// <summary>Proves later changes to the source collection never reach the dictionary.</summary>
    [Fact]
    public void PronunciationOverrides_Create_SourceMutatedAfterwards_DoesNotAffectDictionary()
    {
        // Arrange
        var source = new List<PronunciationOverrideEntry> { PronunciationOverrideEntry.Skip("alpha"), PronunciationOverrideEntry.Skip("beta") };
        var overrides = PronunciationOverrides.Create(source);

        // Act
        source[0] = PronunciationOverrideEntry.Skip("gamma");
        source.Add(PronunciationOverrideEntry.Skip("delta"));
        source.Clear();

        // Assert
        Assert.Equal(["alpha", "beta"], overrides.Entries.Select(entry => entry.Word));
        Assert.Equal(["alpha"], Matched(overrides, "alpha gamma delta"));
    }

    /// <summary>Proves the entry list is a read-only view that cannot be used to change the dictionary.</summary>
    [Fact]
    public void PronunciationOverrides_Entries_IsReadOnlyCopy()
    {
        // Arrange
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("alpha")]);

        // Act
        var entries = overrides.Entries;

        // Assert: not a list or array the caller could write to
        Assert.IsNotType<List<PronunciationOverrideEntry>>(entries);
        Assert.False(entries is PronunciationOverrideEntry[]);
        var asList = Assert.IsAssignableFrom<IList<PronunciationOverrideEntry>>(entries);
        Assert.True(asList.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => asList.Add(PronunciationOverrideEntry.Skip("beta")));
        Assert.Throws<NotSupportedException>(() => asList[0] = PronunciationOverrideEntry.Skip("beta"));
        Assert.Equal(["alpha"], Matched(overrides, "alpha beta"));
    }

    /// <summary>Proves many threads resolving through one shared dictionary all get identical results.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_ConcurrentCalls_ReturnIdenticalDirectives()
    {
        // Arrange
        var overrides = PronunciationOverrides.FromTsv(new StringReader(ReadSample()));
        const string text = "Nginx and Kubernetes, kubernetes; GIF gif Polish polish SQL sql NASA US FBI and Wi-Fi O'Brien.";
        var expected = Resolve(overrides, text).Directives;
        var neutralized = NormalizationHarness.Neutralize(text);
        var tokens = NormalizationHarness.Tokenize(neutralized);

        // Act
        var results = new TokenDirective[16][];
        Parallel.For(0, results.Length, index =>
        {
            TokenDirective[] last = [];
            for (var i = 0; i < 200; i++)
            {
                last = overrides.Resolve(neutralized, tokens, CancellationToken.None);
            }

            results[index] = last;
        });

        // Assert
        Assert.NotEmpty(expected);
        Assert.All(results, result => Assert.Equal(expected, result));
    }

    // ---------------------------------------------------------------- case rules

    /// <summary>Proves a case-sensitive entry beats a case-insensitive one for the same token, and the insensitive entry covers every other case.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_CaseSensitiveBeatsInsensitive()
    {
        // Arrange
        var overrides = PronunciationOverrides.Create(
        [
            PronunciationOverrideEntry.Say("polish", "shine", false),
            PronunciationOverrideEntry.Skip("Polish", true),
        ]);

        // Act
        var (directives, _, _) = Resolve(overrides, "Polish polish POLISH");

        // Assert
        Assert.Equal(
            [new TokenDirective(0, TokenDirectiveKind.Skip, null), new TokenDirective(1, TokenDirectiveKind.Replace, "shine"), new TokenDirective(2, TokenDirectiveKind.Replace, "shine")],
            directives);
    }

    /// <summary>Proves insensitive matching is exactly the runtime's ordinal ignore-case, with the documented consequences for the dotted and dotless I, the Kelvin sign and the sharp s.</summary>
    [Theory]
    [InlineData("istanbul", "ISTANBUL", true)]
    [InlineData("istanbul", "Istanbul", true)]
    [InlineData("ISTANBUL", "istanbul", true)]
    [InlineData("istanbul", "\u0130stanbul", false)]
    [InlineData("istanbul", "\u0131stanbul", false)]
    [InlineData("\u0130stanbul", "\u0131stanbul", false)]
    [InlineData("kelvin", "\u212Aelvin", false)]
    [InlineData("strasse", "stra\u00DFe", false)]
    [InlineData("stra\u00DFe", "STRASSE", false)]
    [InlineData("caf\u00E9", "CAF\u00C9", true)]
    [InlineData("caf\u00E9", "cafe\u0301", false)]
    public void PronunciationOverrides_Resolve_InsensitiveEntry_UsesOrdinalIgnoreCase(string key, string text, bool matches)
    {
        // Arrange
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip(key, false)]);

        // Act
        var (directives, _, _) = ResolveWords(overrides, text);

        // Assert: the expected value is also what the runtime comparison says, so the test pins both
        Assert.Equal(matches, directives.Length == 1);
        Assert.Equal(matches, string.Equals(key, text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Proves the current culture, including the Turkish and Azeri ones whose dotted and dotless I differ, never changes matching.</summary>
    /// <param name="cultureName">The culture to make current while resolving.</param>
    [Theory]
    [InlineData("tr-TR")]
    [InlineData("az-Latn-AZ")]
    [InlineData("en-US")]
    [InlineData("de-DE")]
    [InlineData("")]
    public void PronunciationOverrides_Resolve_TurkishAndAzeriCurrentCulture_DoNotChangeMatching(string cultureName)
    {
        // Arrange
        var overrides = PronunciationOverrides.Create(
        [
            PronunciationOverrideEntry.Skip("istanbul", false),
            PronunciationOverrideEntry.Skip("TITLE", true),
            PronunciationOverrideEntry.Skip("\u0130zmir", false),
        ]);
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);

            // Act
            var matched = Matched(overrides, "ISTANBUL Istanbul istanbul \u0130stanbul \u0131stanbul TITLE title \u0130ZM\u0130R \u0130zmir Izmir");

            // Assert
            Assert.Equal(["ISTANBUL", "Istanbul", "istanbul", "TITLE", "\u0130zmir"], matched);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>Proves supplementary-plane letters match exactly like the runtime's ordinal ignore-case, never split a surrogate pair, and always match in their exact case.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_SupplementaryLetters_MatchWithoutSplittingPairs()
    {
        // Arrange: Deseret capital and small letters
        const string capital = "\U00010400\U00010401x";
        const string small = "\U00010428\U00010429X";
        var insensitive = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip(capital, false)]);
        var sensitive = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip(capital, true)]);

        // Act / Assert: exact case always matches
        Assert.Single(ResolveWords(insensitive, capital).Directives);
        Assert.Single(ResolveWords(sensitive, capital).Directives);

        // a different case matches exactly when the runtime's ordinal ignore-case says so, and never for a sensitive entry
        Assert.Equal(string.Equals(capital, small, StringComparison.OrdinalIgnoreCase), ResolveWords(insensitive, small).Directives.Length == 1);
        Assert.Empty(ResolveWords(sensitive, small).Directives);

        // half a pair never matches and a lone surrogate never throws
        Assert.Empty(ResolveWords(insensitive, "\U00010400").Directives);
        Assert.Empty(ResolveWords(insensitive, "\uD801\uD801x").Directives);
        Assert.Empty(ResolveWords(insensitive, "\uDC00\uDC01x").Directives);
    }

    /// <summary>Proves no Unicode normalization form is applied: the precomposed and decomposed spellings are different keys and different tokens.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_NoNormalizationFolding()
    {
        // Arrange
        var precomposed = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("caf\u00E9s", true)]);

        // Act / Assert
        Assert.Single(ResolveWords(precomposed, "caf\u00E9s").Directives);
        Assert.Empty(ResolveWords(precomposed, "cafe\u0301s").Directives);
    }

    // ---------------------------------------------------------------- whole-word matching

    /// <summary>Resolves a dictionary over a text in which every maximal run of non-space characters is one token (no tokenizer involved).</summary>
    /// <param name="overrides">The dictionary.</param>
    /// <param name="text">The text.</param>
    /// <returns>The directives, the tokens and the text.</returns>
    private static (TokenDirective[] Directives, NormalizationToken[] Tokens, string Text) ResolveWords(PronunciationOverrides overrides, string text)
    {
        var tokens = new List<NormalizationToken>();
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var space = i == text.Length || text[i] == ' ';
            if (!space && start < 0)
            {
                start = i;
            }
            else if (space && start >= 0)
            {
                tokens.Add(new NormalizationToken(start, i - start, tokens.Count == 0));
                start = -1;
            }
        }

        return (overrides.Resolve(text, [.. tokens], CancellationToken.None), [.. tokens], text);
    }

    /// <summary>Proves only a whole token matches: never a part of a word, a longer word or a different word.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_PartialToken_DoesNotMatch()
    {
        // Arrange
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("cat")]);

        // Act / Assert
        Assert.Empty(Matched(overrides, "concatenate cats category scat"));
        Assert.Equal(["cat", "Cat", "CAT"], Matched(overrides, "the cat, Cat and CAT sat"));
    }

    /// <summary>Proves a token that is only a piece of a longer run of word characters never matches: a letter, a combining mark, a decimal digit or a supplementary-plane letter next to it blocks it, while punctuation, spaces and malformed neighbors do not.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_NeighborLetterMarkOrDigit_Blocks()
    {
        // Arrange
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("ab")]);

        // Act / Assert: the token is always the two characters "ab" inside a longer text
        static bool Matches(PronunciationOverrides dictionary, string text, int start) =>
            dictionary.Resolve(text, [new NormalizationToken(start, 2, true)], CancellationToken.None).Length == 1;

        Assert.False(Matches(overrides, "abc", 0));
        Assert.False(Matches(overrides, "cab", 1));
        Assert.False(Matches(overrides, "ab1", 0));
        Assert.False(Matches(overrides, "1ab", 1));
        Assert.False(Matches(overrides, "ab\u0301", 0));
        Assert.False(Matches(overrides, "ab\u0661", 0));
        Assert.False(Matches(overrides, "ab\U00010400", 0));
        Assert.False(Matches(overrides, "\U00010400ab", 2));
        Assert.False(Matches(overrides, "ab\u00E9", 0));
        Assert.True(Matches(overrides, "ab", 0));
        Assert.True(Matches(overrides, "(ab)", 1));
        Assert.True(Matches(overrides, "-ab-", 1));
        Assert.True(Matches(overrides, "ab ", 0));
        Assert.True(Matches(overrides, "x ab", 2));
        Assert.True(Matches(overrides, "ab\u2019s", 0));
        Assert.True(Matches(overrides, "ab\uFFFC", 0));
        Assert.True(Matches(overrides, "ab\uD800", 0));
        Assert.True(Matches(overrides, "\uDC00ab", 1));
    }

    /// <summary>Proves the stem of a clitic split matches (<c>NASA</c> in <c>NASA's</c>) while the first half of a split contraction never does.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_CliticStem_Matches()
    {
        // Arrange
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("NASA", true), PronunciationOverrideEntry.Skip("Do"), PronunciationOverrideEntry.Skip("n't")]);

        // Act / Assert
        Assert.Equal(["NASA"], Matched(overrides, "NASA's launch"));
        Assert.Empty(Matched(overrides, "Don't go"));
    }

    /// <summary>Proves a hyphen compound is one token: a key for one part never matches, a key for the whole compound does.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_HyphenCompound_OnlyWhole()
    {
        // Act / Assert
        Assert.Empty(Matched(PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("Nginx"), PronunciationOverrideEntry.Skip("based")]), "Nginx-based"));
        Assert.Equal(["Nginx-based"], Matched(PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("Nginx-based")]), "Nginx-based apps"));
    }

    /// <summary>Proves a key with an apostrophe matches both the plain and the typographic spelling, but not a mixed or another apostrophe-like spelling.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_ApostropheStyles_MatchBothSpellings()
    {
        // Arrange
        var plainKey = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("O'Brien", true)]);
        var typographicKey = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("O\u2019Brien", false)]);

        // Act / Assert
        Assert.Equal(2, Matched(plainKey, "O'Brien and O\u2019Brien").Length);
        Assert.Equal(2, Matched(typographicKey, "o'brien and O\u2019BRIEN").Length);
        Assert.Empty(Matched(plainKey, "O\u2018Brien O\u02BCBrien O`Brien"));
        Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("a'b\u2019c")]));
    }

    /// <summary>Proves the review example is not a collision: sensitive <c>O'Brien</c> and <c>O’BRIEN</c> are distinct keys, their typographic aliases are distinct too, and each token selects exactly its own entry whichever order the entries are supplied in.</summary>
    /// <param name="reversed">Whether the entries are supplied in reverse order.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PronunciationOverrides_Resolve_ApostropheCaseVariants_SelectOwnEntry(bool reversed)
    {
        // Arrange
        var entries = new[]
        {
            PronunciationOverrideEntry.Say("O'Brien", "first", true),
            PronunciationOverrideEntry.Say("O\u2019BRIEN", "second", true),
        };
        var overrides = PronunciationOverrides.Create(reversed ? entries.AsEnumerable().Reverse() : entries);

        // Act / Assert
        Assert.Equal("first", ValueOf(overrides, "O'Brien"));
        Assert.Equal("first", ValueOf(overrides, "O\u2019Brien"));
        Assert.Equal("second", ValueOf(overrides, "O'BRIEN"));
        Assert.Equal("second", ValueOf(overrides, "O\u2019BRIEN"));
        Assert.Null(ValueOf(overrides, "o'brien"));
        Assert.Null(ValueOf(overrides, "O\u2019brien"));
    }

    /// <summary>Proves a case-sensitive entry (exact or through its apostrophe alias) beats a case-insensitive entry for the same token, regardless of entry order, and that the case-insensitive entry serves every other casing.</summary>
    /// <param name="reversed">Whether the entries are supplied in reverse order.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PronunciationOverrides_Resolve_SensitiveAliasBeatsInsensitive(bool reversed)
    {
        // Arrange
        var entries = new[]
        {
            PronunciationOverrideEntry.Say("O'Brien", "sensitive", true),
            PronunciationOverrideEntry.Say("o\u2019brien", "insensitive", false),
        };
        var overrides = PronunciationOverrides.Create(reversed ? entries.AsEnumerable().Reverse() : entries);

        // Act / Assert
        Assert.Equal("sensitive", ValueOf(overrides, "O'Brien"));
        Assert.Equal("sensitive", ValueOf(overrides, "O\u2019Brien"));
        Assert.Equal("insensitive", ValueOf(overrides, "O'BRIEN"));
        Assert.Equal("insensitive", ValueOf(overrides, "O\u2019BRIEN"));
        Assert.Equal("insensitive", ValueOf(overrides, "o'brien"));
    }

    /// <summary>Proves alias-versus-alias and alias-versus-exact collisions are rejected as duplicates by Create and FromTsv (with the line number), so no two rows ever claim the same lookup key: a typographic spelling is the same key as the plain one, and case-insensitive entries collide across casings.</summary>
    /// <param name="first">The first word.</param>
    /// <param name="second">The second word.</param>
    /// <param name="caseSensitive">The case flag of both entries.</param>
    [Theory]
    [InlineData("O'Brien", "O\u2019Brien", true)]
    [InlineData("O\u2019Brien", "O'Brien", true)]
    [InlineData("O'Brien", "O\u2019Brien", false)]
    [InlineData("O'Brien", "O\u2019BRIEN", false)]
    [InlineData("O\u2019BRIEN", "o'brien", false)]
    public void PronunciationOverrides_AliasCollisions_AreRejectedByCreateAndFromTsv(string first, string second, bool caseSensitive)
    {
        // Arrange
        var flag = caseSensitive ? "sensitive" : "insensitive";
        var tsv = $"{first}\t{flag}\tskip\n{second}\t{flag}\tskip\n";

        // Act
        var createException = Assert.Throws<ArgumentException>(() => PronunciationOverrides.Create(
            [PronunciationOverrideEntry.Skip(first, caseSensitive), PronunciationOverrideEntry.Skip(second, caseSensitive)]));
        var tsvException = Assert.Throws<InvalidDataException>(() => PronunciationOverrides.FromTsv(new StringReader(tsv)));

        // Assert
        Assert.StartsWith("Entry 1:", createException.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", createException.Message, StringComparison.Ordinal);
        Assert.StartsWith("Line 2:", tsvException.Message, StringComparison.Ordinal);
        Assert.Contains("duplicate", tsvException.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves the review example also loads from TSV and selects deterministically.</summary>
    [Fact]
    public void PronunciationOverrides_FromTsv_ApostropheCaseVariants_SelectOwnEntry()
    {
        // Act
        var overrides = PronunciationOverrides.FromTsv(new StringReader("O\u2019BRIEN\tsensitive\tsay\tsecond\nO'Brien\tsensitive\tsay\tfirst\n"));

        // Assert
        Assert.Equal("first", ValueOf(overrides, "O\u2019Brien"));
        Assert.Equal("second", ValueOf(overrides, "O\u2019BRIEN"));
    }

    /// <summary>Proves a tag placeholder acts as a word boundary: overrides on both sides of it match.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_PlaceholderSeparators_AreBoundaries()
    {
        // Arrange
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("Nginx"), PronunciationOverrideEntry.Skip("SQL")]);

        // Act / Assert
        Assert.Equal(["Nginx", "SQL"], Matched(overrides, "Nginx\uFFFCSQL"));
        Assert.Equal(["Nginx", "SQL"], Matched(overrides, "\uFFFCNginx\uFFFC\uFFFCSQL\uFFFC"));
    }

    /// <summary>Proves the sorted-table lookup equals a naive linear reference built only from the runtime's string comparison, over many random dictionaries and random token boundaries (including boundaries in the middle of words and of surrogate pairs).</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_AgainstNaiveReference_RandomKeysAndTexts()
    {
        // Arrange: a pool of words that collide in every interesting way
        string[] pool =
        [
            "a", "A", "ab", "AB", "Ab", "aB", "abc", "ABC", "istanbul", "ISTANBUL", "\u0130stanbul", "\u0131stanbul", "\u0130STANBUL", "caf\u00E9", "CAF\u00C9", "cafe\u0301s",
            "stra\u00DFe", "STRASSE", "strasse", "\u212Aelvin", "kelvin", "Kelvin", "O'Brien", "O\u2019Brien", "o'brien", "O\u2019BRIEN", "Wi-Fi", "wi-fi", "WI-FI", "AT&T", "at&t",
            "and/or", "x86", "X86", "foo_bar", "Ph.D", "ph.d", "\U00010400\U00010401x", "\U00010428\U00010429X", "\U00010400", "\U00010428", "z\U00010400", "Z\U00010428",
            "k", "K", "i", "I", "\u00DF", "SS", "ss", "\u03C3", "\u03A3", "\u03C2",
        ];
        string[] actions = ["skip", "say", "spell"];
        var random = new Random(20260901);

        for (var iteration = 0; iteration < 20000; iteration++)
        {
            // a random valid dictionary: unique (canonical key, case flag) pairs only
            var entries = new List<PronunciationOverrideEntry>();
            for (var i = random.Next(0, 8); i > 0; i--)
            {
                var word = pool[random.Next(pool.Length)];
                var sensitive = random.Next(2) == 0;
                var action = actions[random.Next(actions.Length)];
                PronunciationOverrideEntry entry = action switch
                {
                    "say" => PronunciationOverrideEntry.Say(word, "spoken", sensitive),
                    "spell" when word.All(character => character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z')) => PronunciationOverrideEntry.SpellOut(word, sensitive),
                    _ => PronunciationOverrideEntry.Skip(word, sensitive),
                };

                // every pool word is a valid key by construction; only duplicates are left out
                if (!entries.Any(existing => Collides(existing, entry)))
                {
                    entries.Add(entry);
                }
            }

            var overrides = PronunciationOverrides.Create(entries);

            // a random text of pool words and random token boundaries, so boundaries land inside words and pairs too
            var text = string.Join(random.Next(2) == 0 ? " " : "-", Enumerable.Range(0, random.Next(1, 6)).Select(_ => pool[random.Next(pool.Length)]));
            var tokens = new List<NormalizationToken>();
            var position = 0;
            while (position < text.Length)
            {
                var length = Math.Min(random.Next(1, 9), text.Length - position);
                if (random.Next(3) > 0)
                {
                    tokens.Add(new NormalizationToken(position, length, tokens.Count == 0));
                }

                position += length + random.Next(0, 2);
            }

            // Act
            var actual = overrides.Resolve(text, [.. tokens], CancellationToken.None);

            // Assert
            var expected = Reference(entries, text, tokens);
            Assert.True(
                expected.SequenceEqual(actual),
                $"iteration {iteration}: expected [{string.Join(", ", expected)}] but got [{string.Join(", ", actual)}]");
        }
    }

    /// <summary>Tells whether two entries are duplicates under the dictionary's rule, computed independently of the production code.</summary>
    /// <param name="a">The first entry.</param>
    /// <param name="b">The second entry.</param>
    /// <returns><see langword="true"/> for the same canonical key with the same case flag.</returns>
    private static bool Collides(PronunciationOverrideEntry a, PronunciationOverrideEntry b) =>
        a.CaseSensitive == b.CaseSensitive
        && string.Equals(a.Word.Replace('\u2019', '\''), b.Word.Replace('\u2019', '\''), a.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    /// <summary>The naive reference: a linear scan of the entries for every token, using only the runtime's string comparison.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens.</param>
    /// <returns>The expected directives.</returns>
    private static List<TokenDirective> Reference(List<PronunciationOverrideEntry> entries, string text, List<NormalizationToken> tokens)
    {
        var result = new List<TokenDirective>();
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = text.Substring(tokens[index].Start, tokens[index].Length);
            var firstIsRune = Rune.TryGetRuneAt(token, 0, out var first);
            if (!firstIsRune || !Rune.IsLetter(first) || TouchesWord(text, tokens[index].Start, tokens[index].Length))
            {
                continue;
            }

            PronunciationOverrideEntry? hit = null;
            foreach (var sensitive in new[] { true, false })
            {
                hit = entries.FirstOrDefault(entry =>
                    entry.CaseSensitive == sensitive
                    && Equal(token, entry.Word.Replace('\u2019', '\''), sensitive, alias: false) | Equal(token, entry.Word.Replace('\u2019', '\''), sensitive, alias: true));
                if (hit is not null)
                {
                    break;
                }
            }

            if (hit is not null)
            {
                result.Add(hit.Action switch
                {
                    OverrideAction.Say => new TokenDirective(index, TokenDirectiveKind.Replace, hit.Payload),
                    OverrideAction.SpellOut => new TokenDirective(index, TokenDirectiveKind.Spell, null),
                    _ => new TokenDirective(index, TokenDirectiveKind.Skip, null),
                });
            }
        }

        return result;
    }

    /// <summary>Compares a token with a canonical key, optionally in its typographic-apostrophe spelling.</summary>
    /// <param name="token">The token.</param>
    /// <param name="key">The canonical key.</param>
    /// <param name="sensitive">Whether to compare exactly.</param>
    /// <param name="alias">Whether to compare with the key's typographic-apostrophe spelling instead.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    private static bool Equal(string token, string key, bool sensitive, bool alias) =>
        (!alias || key.Contains('\'')) && string.Equals(token, alias ? key.Replace('\'', '\u2019') : key, sensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);

    /// <summary>Tells whether the scalar before or after the range is a letter, a combining mark or a decimal digit, the reference way.</summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The range's start.</param>
    /// <param name="length">The range's length.</param>
    /// <returns><see langword="true"/> when either neighbor is a word character.</returns>
    private static bool TouchesWord(string text, int start, int length)
    {
        static bool Word(Rune rune) => Rune.IsLetter(rune) || Rune.GetUnicodeCategory(rune) is UnicodeCategory.DecimalDigitNumber or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

        // The neighbors are read from the text cut at the token's edges, so a pair cut by the token boundary is a malformed neighbor.
        var prefix = text[..start];
        var suffix = text[(start + length)..];
        var before = false;
        if (prefix.Length > 0)
        {
            var index = prefix.Length - 1;
            if (char.IsLowSurrogate(prefix[index]) && index > 0 && char.IsHighSurrogate(prefix[index - 1]))
            {
                index--;
            }

            before = Rune.TryGetRuneAt(prefix, index, out var rune) && Word(rune);
        }

        var after = suffix.Length > 0 && Rune.TryGetRuneAt(suffix, 0, out var next) && Word(next);
        return before || after;
    }

    // ---------------------------------------------------------------- directives

    /// <summary>Proves each action maps to exactly one directive kind carrying the entry's own value, and an unmatched token gets none.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_Actions_MapToDirectives()
    {
        // Arrange
        var overrides = PronunciationOverrides.Create(
        [
            PronunciationOverrideEntry.Skip("skipme"),
            PronunciationOverrideEntry.Say("sayme", "say this"),
            PronunciationOverrideEntry.SpellOut("gif"),
            PronunciationOverrideEntry.Pronounce("pronme", PronunciationScheme.Arpabet, "P R OW1"),
        ]);

        // Act
        var (directives, _, _) = ResolveWords(overrides, "skipme other sayme GIF pronme");

        // Assert
        Assert.Equal(
            [
                new TokenDirective(0, TokenDirectiveKind.Skip, null),
                new TokenDirective(2, TokenDirectiveKind.Replace, "say this"),
                new TokenDirective(3, TokenDirectiveKind.Spell, null),
                new TokenDirective(4, TokenDirectiveKind.Pronounce, "arpabet:P R OW1"),
            ],
            directives);
    }

    /// <summary>Proves a lookup yields at most one directive per token, in strictly ascending token order, for a long repetitive text.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_OneDirectivePerToken_AscendingUnique()
    {
        // Arrange: a sensitive and an insensitive entry for the same word, repeated many times
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("word", true), PronunciationOverrideEntry.Say("WORD", "spoken", false), PronunciationOverrideEntry.Skip("other", false)]);
        var text = string.Concat(Enumerable.Repeat("word Word other plain ", 500));

        // Act
        var (directives, tokens, _) = ResolveWords(overrides, text);

        // Assert
        Assert.Equal(1500, directives.Length);
        Assert.Equal(directives.Select(directive => directive.TokenIndex).Distinct().OrderBy(index => index), directives.Select(directive => directive.TokenIndex));
        Assert.All(directives, directive => Assert.InRange(directive.TokenIndex, 0, tokens.Length - 1));
    }

    // ---------------------------------------------------------------- cancellation, allocation, size

    /// <summary>Proves the lookup checks the cancellation token every 256 visited tokens counting every token (matched or not), before any work too, and stops at the first check after the cancellation.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_Cancellation_CheckedEveryIntervalCountingEveryToken()
    {
        // Arrange: 1000 tokens that never match, and an observer that cancels while token 300 is visited
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("zzzz")]);
        var text = string.Concat(Enumerable.Repeat("a ", 1000));
        var (_, tokens, _) = ResolveWords(overrides, text);
        using var cancellation = new CancellationTokenSource();
        var visited = new List<int>();

        // Act
        Assert.Throws<OperationCanceledException>(() => overrides.Resolve(text, tokens, cancellation.Token, index =>
        {
            visited.Add(index);
            if (index == 300)
            {
                cancellation.Cancel();
            }
        }));

        // Assert: every token up to the next check (the 512th visited token) was visited, in order, and none after
        Assert.Equal(Enumerable.Range(0, 511), visited);

        // an already-canceled token stops before the first token is visited
        var visitedAfterCancel = 0;
        Assert.Throws<OperationCanceledException>(() => overrides.Resolve(text, tokens, cancellation.Token, _ => visitedAfterCancel++));
        Assert.Equal(0, visitedAfterCancel);
    }

    /// <summary>Proves tokens that cannot match cost no allocation: the lookup over thousands of non-matching tokens allocates only a constant.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_NoMatches_AllocatesNothingPerToken()
    {
        // Arrange: a large dictionary and words that are in no way keys (some pass the gates, some do not)
        var overrides = PronunciationOverrides.Create(Enumerable.Range(0, 2000).Select(i => PronunciationOverrideEntry.Skip(Word(i), i % 2 == 0)));
        var text = string.Concat(Enumerable.Repeat("zzzzz zzzz 1234 (zzzz) \u00E9\u00E9\u00E9\u00E9 ", 1000));
        var (_, tokens, _) = ResolveWords(overrides, text);
        _ = overrides.Resolve(text, tokens, CancellationToken.None);

        // Act
        var before = GC.GetAllocatedBytesForCurrentThread();
        var directives = overrides.Resolve(text, tokens, CancellationToken.None);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Assert
        Assert.Empty(directives);
        Assert.True(allocated < 2048, $"allocated {allocated} bytes for {tokens.Length} tokens");
    }

    /// <summary>Proves a 1 MB text against a 10,000-entry dictionary resolves in linear time (a generous budget, far above the real cost), including one huge token.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_LargeText_CompletesWithinBudget()
    {
        // Arrange
        var overrides = PronunciationOverrides.Create(Enumerable.Range(0, PronunciationOverrides.MaxEntries).Select(i => PronunciationOverrideEntry.Skip(Word(i), i % 3 == 0)));
        var builder = new StringBuilder();
        var index = 0;
        while (builder.Length < 1_000_000)
        {
            builder.Append(Word(index++ * 7 % 456_976)).Append(' ');
        }

        builder.Append(new string('a', 100_000));
        var text = builder.ToString();
        var (_, tokens, _) = ResolveWords(overrides, text);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var directives = overrides.Resolve(text, tokens, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        Assert.NotEmpty(directives);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), $"took {stopwatch.Elapsed}");
    }

    // ---------------------------------------------------------------- argument validation

    /// <summary>Proves a null text is rejected.</summary>
    [Fact]
    public void PronunciationOverrides_Resolve_NullText_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => PronunciationOverrides.Empty.Resolve(null!, [], CancellationToken.None));
    }

    /// <summary>Proves a token that is empty, negative or outside the text is rejected without any overflowing arithmetic.</summary>
    /// <param name="start">The token's start.</param>
    /// <param name="length">The token's length.</param>
    [Theory]
    [InlineData(5, 10)]
    [InlineData(0, 9)]
    [InlineData(9, 1)]
    [InlineData(-1, 2)]
    [InlineData(0, 0)]
    [InlineData(0, -3)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(1, int.MaxValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    [InlineData(2, int.MaxValue - 1)]
    public void PronunciationOverrides_Resolve_TokenOutsideText_Throws(int start, int length)
    {
        // Arrange
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("abc")]);
        NormalizationToken[] tokens = [new(start, length, true)];

        // Act / Assert
        Assert.Throws<ArgumentException>(() => overrides.Resolve("abc def", tokens, CancellationToken.None));
    }

    // ---------------------------------------------------------------- golden rows

    /// <summary>Proves every row of the override golden file: the dictionary, the real tokenizer and the real normalizer produce the documented spoken text, pronunciation marks, spelled spans and override counts.</summary>
    [Fact]
    public void PronunciationOverrides_GoldenCases_NormalizeAsDocumented()
    {
        // Arrange
        var overrides = PronunciationOverrides.FromTsv(new StringReader(ReadSample()));
        var cases = NormalizationHarness.ReadOverrideCases("overrides-cases.tsv");
        Assert.True(cases.Count >= 45, "the golden file must keep its breadth");
        Assert.Equal(cases.Count, cases.Select(row => row.Id).Distinct(StringComparer.Ordinal).Count());

        var failures = new List<string>();
        foreach (var row in cases)
        {
            // Act
            var neutralized = NormalizationHarness.Neutralize(row.Input);
            var aligned = NormalizationHarness.NormalizeWithOverrides(row.Input, overrides);
            var spoken = aligned.ApplySpokenForms();
            var pronunciations = aligned.Pronunciations.Select(mark => neutralized.Substring(mark.Start, mark.Length) + "=" + mark.Value).ToList();
            var spelled = aligned.Spans.Where(span => span.Spelled).Select(span => neutralized.Substring(span.Start, span.Length)).ToList();

            // Assert (collected, so one run reports every mismatching row)
            if (row.Spoken != spoken)
            {
                failures.Add($"{row.Id}: spoken '{spoken}' but expected '{row.Spoken}'");
            }

            if (!row.Pronunciations.SequenceEqual(pronunciations))
            {
                failures.Add($"{row.Id}: pronunciations '{string.Join("|", pronunciations)}'");
            }

            if (!row.Spelled.SequenceEqual(spelled))
            {
                failures.Add($"{row.Id}: spelled '{string.Join("|", spelled)}'");
            }

            if (row.Applied != aligned.OverridesApplied || row.Ignored != aligned.OverridesIgnored)
            {
                failures.Add($"{row.Id}: applied {aligned.OverridesApplied} (expected {row.Applied}), ignored {aligned.OverridesIgnored} (expected {row.Ignored})");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    // ---------------------------------------------------------------- test readers

    /// <summary>A reader that returns one character forever and counts how many were taken.</summary>
    private sealed class EndlessReader : TextReader
    {
        /// <summary>The character to return.</summary>
        private readonly char _character;

        /// <summary>Initializes a new reader.</summary>
        /// <param name="character">The character to return forever.</param>
        public EndlessReader(char character) => _character = character;

        /// <summary>Gets the number of characters read so far.</summary>
        public long CharactersRead { get; private set; }

        /// <inheritdoc />
        public override int Peek() => _character;

        /// <inheritdoc />
        public override int Read()
        {
            CharactersRead++;
            return _character;
        }
    }

    /// <summary>A reader whose every read fails.</summary>
    private sealed class FailingReader : TextReader
    {
        /// <inheritdoc />
        public override int Peek() => throw new IOException("reader failed");

        /// <inheritdoc />
        public override int Read() => throw new IOException("reader failed");
    }

    /// <summary>A string reader that notes whether it was disposed.</summary>
    private sealed class TrackingReader : StringReader
    {
        /// <summary>Initializes a new reader.</summary>
        /// <param name="text">The text to read.</param>
        public TrackingReader(string text)
            : base(text)
        {
        }

        /// <summary>Gets a value indicating whether the reader was disposed.</summary>
        public bool Disposed { get; private set; }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
