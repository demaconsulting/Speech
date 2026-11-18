using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tests;

/// <summary>
///     Subsystem tests for the lexicon of the English pronunciation package: the embedded CMU Pronouncing
///     Dictionary lexicon read through its lookup contract (all variants, case folding, apostrophes) and the phone
///     text form the rest of the package builds on. The morphology and the reading pronunciations of the subsystem
///     are tested in the other parts of this class.
/// </summary>
/// <remarks>
///     The pronunciations asserted here are a handful of well-known CMUdict entries used as golden values. The
///     full-content evidence (every dictionary entry reads back identical, and the resource stays within its size
///     budget) comes from the tools project (<c>build-lexicon --check</c>), which needs the pinned CMUdict download,
///     and from the enumeration/lookup consistency test below, which needs nothing external.
/// </remarks>
public sealed partial class LexiconSubsystemTests
{
    /// <summary>Proves a word with one pronunciation and a heteronym with three come back with every variant in dictionary order.</summary>
    [Fact]
    public void LexiconSubsystem_Lookup_KnownWords_ReturnsAllVariantsInOrder()
    {
        // Arrange
        var lexicon = CmuLexicon.Default;

        // Act
        var cat = lexicon.Lookup("cat");
        var record = lexicon.Lookup("record");

        // Assert: a single variant, and three variants in dictionary order
        Assert.Equal(["K AE1 T"], cat.Select(p => p.ToString()));
        Assert.Equal(["R AH0 K AO1 R D", "R EH1 K ER0 D", "R IH0 K AO1 R D"], record.Select(p => p.ToString()));
    }

    /// <summary>Proves lookup ignores case, accepts the typographic apostrophe and finds apostrophe words.</summary>
    [Fact]
    public void LexiconSubsystem_Lookup_CaseAndApostrophe_AreFolded()
    {
        // Arrange
        var lexicon = CmuLexicon.Default;

        // Act
        var plain = lexicon.Lookup("don't");
        var shouted = lexicon.Lookup("DON'T");
        var typographic = lexicon.Lookup("don\u2019t");

        // Assert: the same, non-empty answer for every spelling
        Assert.NotEmpty(plain);
        Assert.Equal(plain, shouted);
        Assert.Equal(plain, typographic);
    }

    /// <summary>Proves words that are absent or cannot be headwords give an empty answer, never an exception.</summary>
    [Theory]
    [InlineData("zzxqvk")]
    [InlineData("")]
    [InlineData("route66")]
    [InlineData("well-known")]
    public void LexiconSubsystem_Lookup_UnknownOrInvalidWord_ReturnsEmpty(string word)
    {
        // Act
        var result = CmuLexicon.Default.Lookup(word);

        // Assert
        Assert.Empty(result);
        Assert.False(CmuLexicon.Default.Contains(word));
    }

    /// <summary>Proves every entry the lexicon enumerates is found again by lookup with identical variants (the binary-search path agrees with the stored order).</summary>
    [Fact]
    public void LexiconSubsystem_Entries_EveryEntryIsFoundByLookup()
    {
        // Arrange
        var lexicon = CmuLexicon.Default;
        var count = 0;
        var previous = string.Empty;

        // Act / Assert: sorted, unique, and each word's lookup equals the enumerated variants
        foreach (var entry in lexicon.Entries())
        {
            Assert.True(string.CompareOrdinal(previous, entry.Word) < 0, "Entries are not strictly sorted at " + entry.Word);
            Assert.Equal(entry.Pronunciations, lexicon.Lookup(entry.Word));
            previous = entry.Word;
            count++;
        }

        Assert.Equal(lexicon.WordCount, count);
        Assert.True(count > 120_000);
    }

    /// <summary>Proves the shared default lexicon answers the same from many threads at once, including the first use.</summary>
    [Fact]
    public void LexiconSubsystem_Lookup_ConcurrentUse_GivesTheSingleThreadedAnswers()
    {
        // Arrange
        string[] words = ["cat", "record", "don't", "zzxqvk", "photograph"];
        var expected = words.Select(w => string.Join('|', CmuLexicon.Default.Lookup(w))).ToArray();
        var failures = new System.Collections.Concurrent.ConcurrentBag<string>();

        // Act
        Parallel.For(0, 64, i =>
        {
            var word = words[i % words.Length];
            var actual = string.Join('|', CmuLexicon.Default.Lookup(word));
            if (actual != expected[i % words.Length])
            {
                failures.Add(word);
            }
        });

        // Assert
        Assert.Empty(failures);
    }

    /// <summary>Proves the CMUdict text form of a pronunciation parses and prints back unchanged and exposes stress.</summary>
    [Fact]
    public void LexiconSubsystem_Pronunciation_TextForm_RoundTrips()
    {
        // Arrange
        const string text = "R EH1 K ER0 D";

        // Act
        var pronunciation = Pronunciation.Parse(text);

        // Assert
        Assert.Equal(text, pronunciation.ToString());
        Assert.Equal(5, pronunciation.Count);
        Assert.Equal(1, pronunciation[1].Stress);
        Assert.Equal(-1, pronunciation[0].Stress);
        Assert.Equal("EH", pronunciation[1].BaseSymbol);
        Assert.Throws<FormatException>(() => Pronunciation.Parse("K QQ1 T"));
    }
}
