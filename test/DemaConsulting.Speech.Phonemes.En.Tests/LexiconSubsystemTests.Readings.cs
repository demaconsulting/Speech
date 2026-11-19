using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tests;

/// <summary>
///     Lexicon subsystem tests, reading pronunciations part: the subsystem contract that every reading id and every
///     (spelling, reading) pair of the English text analysis inventory resolves to a pronunciation, with every gap
///     explicit, and the tests of the ReadingPronunciations unit (headword, inflection, form override, gap and table
///     parsing). The reviewed sentence per reading id is the system test <c>SpeechPhonemesEn_ReadingGolden_*</c>.
/// </summary>
public sealed partial class LexiconSubsystemTests
{
    /// <summary>Proves the table has a row for exactly every reading id of the inventory and that every <c>cmu#N</c> row is that variant of the lexicon.</summary>
    [Fact]
    public void LexiconSubsystem_Readings_Table_CoversTheInventoryAndMatchesTheLexicon()
    {
        // Arrange
        var inventoryIds = ReadingInventory.Default.ReadingIds.ToHashSet(StringComparer.Ordinal);
        var tableIds = ReadingPronunciations.Default.Entries.Select(e => e.ReadingId).ToHashSet(StringComparer.Ordinal);

        // Act: every provenance-tagged row of the lexicon kind must be the named variant
        var wrongVariants = ReadingPronunciations.Default.Entries
            .Where(e => e.Source.StartsWith("cmu#", StringComparison.Ordinal))
            .Where(e =>
            {
                var variants = CmuLexicon.Default.Lookup(ReadingPronunciations.LemmaOf(e.ReadingId));
                var number = int.Parse(e.Source.AsSpan(4), System.Globalization.CultureInfo.InvariantCulture);
                return number > variants.Count || !variants[number - 1].Equals(e.Pronunciation);
            })
            .Select(e => e.ReadingId)
            .ToList();

        // Assert
        Assert.Equal(395, inventoryIds.Count);
        Assert.True(inventoryIds.SetEquals(tableIds));
        Assert.Empty(wrongVariants);
    }

    /// <summary>Proves every (form, reading id) pair of the inventory resolves to a non-empty pronunciation, and that the gap list is explicit and empty.</summary>
    [Fact]
    public void LexiconSubsystem_Readings_Resolve_EveryInventoryPair_ResolvesWithNoSilentGap()
    {
        // Arrange: the committed copy of the inventory's form table, checked against the embedded inventory first
        var pairs = ReadTsv("forms.tsv").Select(f => (Form: f[0], ReadingId: f[1])).ToList();
        var embeddedMatches = pairs.GroupBy(p => p.Form, StringComparer.Ordinal)
            .All(g => g.Select(p => p.ReadingId).Order(StringComparer.Ordinal)
                .SequenceEqual(ReadingInventory.Default.CandidatesFor(g.Key).Order(StringComparer.Ordinal)));
        var listedGaps = ReadTsv("reading-gaps.tsv").Select(f => (Form: f[0], ReadingId: f[1])).ToHashSet();

        // Act
        var unresolved = pairs
            .Where(p => !ReadingPronunciations.Default.TryResolve(p.Form, p.ReadingId, out var pronunciation) || pronunciation is null || pronunciation.Count == 0)
            .ToList();

        // Assert: 853 pairs, the same as the embedded inventory; nothing unresolved that is not listed; the target is zero gaps
        var unlisted = unresolved.Where(p => !listedGaps.Contains(p)).ToList();
        var staleGaps = listedGaps.Where(g => !unresolved.Contains(g)).ToList();
        Assert.Equal(853, pairs.Count);
        Assert.True(embeddedMatches);
        Assert.Empty(unlisted);
        Assert.Empty(staleGaps);
        Assert.Empty(unresolved);
    }

    /// <summary>Proves one spelling under one reading resolves from the table: the headword as listed, a regular inflection by the morphology rules, and an explicit form override that beats them.</summary>
    [Fact]
    public void ReadingPronunciations_TryResolve_HeadwordInflectionAndOverride_ResolveFromTheTables()
    {
        // Arrange
        var tables = ReadingPronunciations.Default;

        // Act
        var headword = tables.TryResolve("record", "record.noun", out var noun);
        var verb = tables.TryResolve("record", "record.verb", out var verbPronunciation);
        var inflected = tables.TryResolve("records", "record.noun", out var plural);
        var overridden = tables.TryResolve("does", "do.verb", out var does);

        // Assert: the headword as listed (noun stress first, verb stress second), the ending by the rules, the override as listed
        Assert.True(headword);
        Assert.Equal("R EH1 K ER0 D", noun!.ToString());
        Assert.True(verb);
        Assert.Equal("R AH0 K AO1 R D", verbPronunciation!.ToString());
        Assert.True(inflected);
        Assert.Equal("R EH1 K ER0 D Z", plural!.ToString());
        Assert.True(overridden);
        Assert.Equal("D AH1 Z", does!.ToString());
    }

    /// <summary>Proves a pair that has no row, no regular inflection and no override is a gap that is reported, never a default.</summary>
    [Fact]
    public void ReadingPronunciations_TryResolve_UnknownReadingOrUnrelatedForm_IsAGap()
    {
        // Act
        var unknownReading = ReadingPronunciations.Default.TryResolve("record", "nonsense.reading", out var none1);
        var unrelatedForm = ReadingPronunciations.Default.TryResolve("cat", "record.noun", out var none2);

        // Assert
        Assert.False(unknownReading);
        Assert.Null(none1);
        Assert.False(unrelatedForm);
        Assert.Null(none2);
        Assert.Throws<ArgumentNullException>(() => ReadingPronunciations.Default.TryResolve(null!, "record.noun", out _));
    }

    /// <summary>Proves malformed tables are rejected with a format error naming the file and line, and a repeated reading id is rejected.</summary>
    [Fact]
    public void ReadingPronunciations_Parse_MalformedTables_AreRejected()
    {
        // Arrange
        const string header = "reading_id\tarpabet\tsource\n";
        const string overridesHeader = "form\treading_id\tarpabet\tnote\n";

        // Act and assert: a good table parses; a wrong source, a non-ARPAbet phone and a repeated id do not
        var good = ReadingPronunciations.Parse(new StringReader(header + "record.noun\tR EH1 K ER0 D\tauthored\n"), new StringReader(overridesHeader));
        Assert.Single(good.Entries);
        Assert.Throws<FormatException>(() => ReadingPronunciations.Parse(new StringReader(header + "record.noun\tR EH1 K ER0 D\tguessed\n"), new StringReader(overridesHeader)));
        Assert.Throws<FormatException>(() => ReadingPronunciations.Parse(new StringReader(header + "record.noun\tR QQ1 K\tauthored\n"), new StringReader(overridesHeader)));
        Assert.Throws<FormatException>(() => ReadingPronunciations.Parse(new StringReader(header + "a.b\tAH0\tauthored\na.b\tAH0\tauthored\n"), new StringReader(overridesHeader)));
    }

    /// <summary>Gets the path of a file in the test data folder beside the test assembly.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The full path.</returns>
    private static string TestDataPath(string name) => Path.Combine(AppContext.BaseDirectory, "TestData", name);

    /// <summary>Reads the data rows of a tab-separated test data file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The fields of each row after the header.</returns>
    private static IEnumerable<string[]> ReadTsv(string name) =>
        File.ReadLines(TestDataPath(name)).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t'));
}
