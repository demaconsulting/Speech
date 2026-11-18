using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;
using DemaConsulting.Speech.Phonemes.En.Tools.PhonemizerSubsystem;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tests;

/// <summary>
///     Lexicon subsystem tests, reading pronunciations part: the contract that every reading id and every (spelling,
///     reading) pair of the English text analysis inventory resolves to a pronunciation, with every gap explicit; the
///     golden corpus of one reviewed sentence per reading id run through the real text analysis and the phonemizer; and
///     the default reading of a heteronym when no analysis is available.
/// </summary>
/// <remarks>
///     The reading golden has two kinds of rows. An <c>e2e</c> row runs the whole pipeline (the trained classifier
///     picks the reading, the phonemizer applies it). A <c>forced</c> row is one the classifier labels differently; the
///     golden is not bent to the classifier, so the phonemizer is tested with a synthetic reading annotation instead.
///     The share of <c>e2e</c> rows is the classifier's hit rate on these own-written sentences and is gated below.
/// </remarks>
public sealed partial class LexiconSubsystemTests
{
    /// <summary>The share of reading sentences the real classifier must label correctly (measured 97.5%).</summary>
    private const double MinimumClassifierHitRate = 0.95;

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

    /// <summary>Proves the readings golden: every reading id has one sentence and its reviewed pronunciation comes out of the pipeline (end to end, or with the reading forced where the classifier labels the sentence differently).</summary>
    [Fact]
    public void LexiconSubsystem_Readings_Golden_EveryReadingIsPronouncedAsReviewed()
    {
        // Arrange
        var cases = GoldenCorpus.ReadReadings(TestDataPath("readings-golden.tsv"));
        var failures = new List<string>();

        // Act
        foreach (var c in cases)
        {
            var forced = c.Mode == "forced";
            var result = forced ? GoldenCorpus.RunForced(c.Sentence, c.Target, c.ReadingId) : GoldenCorpus.RunEndToEnd(c.Sentence);
            var item = GoldenCorpus.FindWord(result, c.Target);
            if (item is null || !item.FromReading || item.ReadingId != c.ReadingId || item.Arpabet != c.Expected)
            {
                failures.Add($"{c.ReadingId} ({c.Mode}): expected '{c.Expected}' with reading applied, got '{item?.Arpabet}' reading '{item?.ReadingId}' applied {item?.FromReading}");
            }
        }

        // Assert: one case per reading id of the inventory, all as reviewed
        Assert.Equal(ReadingInventory.Default.ReadingIds.Order(StringComparer.Ordinal), cases.Select(c => c.ReadingId).Order(StringComparer.Ordinal));
        Assert.All(cases, c => Assert.Contains(c.Mode, new[] { "e2e", "forced" }));
        Assert.Empty(failures);
    }

    /// <summary>Proves the real classifier labels at least the gated share of the readings golden sentences correctly (its hit rate on own-written sentences).</summary>
    [Fact]
    public void LexiconSubsystem_Readings_Golden_ClassifierHitRate_MeetsTheGate()
    {
        // Arrange
        var cases = GoldenCorpus.ReadReadings(TestDataPath("readings-golden.tsv"));

        // Act: count the rows the committed mode says the classifier gets right, and re-check each against the classifier now
        var endToEnd = cases.Where(c => c.Mode == "e2e").ToList();
        var stale = endToEnd.Where(c => GoldenCorpus.ClassifiedReading(c.Sentence, c.Target) != c.ReadingId).Select(c => c.ReadingId).ToList();

        // Assert
        Assert.Empty(stale);
        Assert.True((double)endToEnd.Count / cases.Count >= MinimumClassifierHitRate, $"hit rate {endToEnd.Count}/{cases.Count}");
    }

    /// <summary>Proves that without an analysis every spelling of the inventory is pronounced as its default reading: the first variant of the dictionary.</summary>
    [Fact]
    public void LexiconSubsystem_Readings_NoAnnotations_EveryInventoryFormGetsItsDefaultReading()
    {
        // Arrange
        var phonemizer = new EnglishPhonemizer();
        var forms = ReadTsv("forms.tsv").Select(f => f[0]).Distinct(StringComparer.Ordinal).Where(f => CmuLexicon.Default.Contains(f)).ToList();

        // Act
        var wrong = forms.Where(f =>
        {
            var item = phonemizer.Phonemize(f).Items[0];
            return item.FromReading || item.Source != PronunciationSource.Lexicon || item.Arpabet != CmuLexicon.Default.Lookup(f)[0].ToString();
        }).ToList();

        // Assert: nearly all 501 forms are in the dictionary
        Assert.True(forms.Count >= 480);
        Assert.Empty(wrong);
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
