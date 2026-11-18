using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tests;

/// <summary>
///     Subsystem tests for the lexicon of the English pronunciation package: the embedded CMU Pronouncing
///     Dictionary lexicon read through its public lookup contract (all variants, case folding, apostrophes),
///     the size budget of the shipped resource, and the phone text form renderers build on.
/// </summary>
/// <remarks>
///     The pronunciations asserted here are a handful of well-known CMUdict entries used as golden values. The
///     full-content evidence (every dictionary entry reads back identical) comes from the tools project
///     (<c>build-lexicon --check</c>), which needs the pinned CMUdict download, and from the enumeration/lookup
///     consistency test below, which needs nothing external.
/// </remarks>
public sealed class LexiconSubsystemTests
{
    /// <summary>The size budget of the embedded resource in bytes.</summary>
    private const long SizeBudgetBytes = 1024 * 1024;

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

    /// <summary>Proves the shipped resource stays within its size budget and the default lexicon is one shared instance under concurrent first use.</summary>
    [Fact]
    public void LexiconSubsystem_EmbeddedResource_WithinBudgetAndSharedAcrossThreads()
    {
        // Arrange
        using var stream = typeof(CmuLexicon).Assembly.GetManifestResourceStream(CmuLexicon.ResourceName);

        // Act: touch the default lexicon from many threads at once
        var instances = new CmuLexicon[16];
        Parallel.For(0, instances.Length, i => instances[i] = CmuLexicon.Default);

        // Assert
        Assert.NotNull(stream);
        Assert.InRange(stream.Length, 1, SizeBudgetBytes);
        Assert.All(instances, instance => Assert.Same(instances[0], instance));
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
