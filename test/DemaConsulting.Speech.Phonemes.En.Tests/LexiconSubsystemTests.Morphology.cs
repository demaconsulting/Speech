using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using DemaConsulting.Speech.Phonemes.En.Tools.MorphologySubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tests;

/// <summary>
///     Lexicon subsystem tests, morphology part: the tests of the Morphology unit (golden derivations of words
///     from their stems, the sound rules for endings and clitics) and the subsystem test of the measured agreement
///     of the rules with the lexicon itself (the evidence behind the rules).
/// </summary>
public sealed partial class LexiconSubsystemTests
{
    /// <summary>Proves regular inflections, possessives and contractions are derived from the lexicon's stem pronunciation with the ending that stem requires.</summary>
    [Theory]
    [InlineData("cats", "K AE1 T S")]
    [InlineData("dogs", "D AO1 G Z")]
    [InlineData("boxes", "B AA1 K S IH0 Z")]
    [InlineData("cries", "K R AY1 Z")]
    [InlineData("walked", "W AO1 K T")]
    [InlineData("wanted", "W AA1 N T IH0 D")]
    [InlineData("stopped", "S T AA1 P T")]
    [InlineData("cried", "K R AY1 D")]
    [InlineData("making", "M EY1 K IH0 NG")]
    [InlineData("running", "R AH1 N IH0 NG")]
    [InlineData("slowly", "S L OW1 L IY0")]
    [InlineData("accidentally", "AE2 K S AH0 D EH1 N T AH0 L IY0")]
    [InlineData("cat's", "K AE1 T S")]
    [InlineData("cats'", "K AE1 T S")]
    [InlineData("they'll", "DH EY1 L")]
    [InlineData("it'll", "IH1 T AH0 L")]
    [InlineData("isn't", "IH1 Z AH0 N T")]
    public void Morphology_TryDerive_RegularForm_DerivesFromStem(string word, string expected)
    {
        // Act
        var found = Morphology.TryDerive(word, CmuLexicon.Default, out var derivation);

        // Assert: the preferred derived pronunciation is the expected one
        Assert.True(found);
        Assert.Equal(expected, derivation!.Pronunciations[0].ToString());
    }

    /// <summary>Proves a word that is no known stem plus a known ending yields no derivation, so the caller falls back to letter-to-sound.</summary>
    [Theory]
    [InlineData("xyzzy")]
    [InlineData("qwxzs")]
    [InlineData("")]
    [InlineData("s")]
    public void Morphology_TryDerive_NoKnownStem_ReturnsFalse(string word)
    {
        // Act
        var found = Morphology.TryDerive(word, CmuLexicon.Default, out var derivation);

        // Assert
        Assert.False(found);
        Assert.Null(derivation);
    }

    /// <summary>Proves the ending of a lemma's pronunciation is chosen by the lemma's last phone, and a clitic by whether its host ends in a vowel (the sound rules reading forms reuse).</summary>
    [Theory]
    [InlineData("K AE1 T", "PluralS", "K AE1 T S")]
    [InlineData("D AO1 G", "PluralS", "D AO1 G Z")]
    [InlineData("B AA1 K S", "PluralS", "B AA1 K S IH0 Z")]
    [InlineData("W AO1 K", "PastEd", "W AO1 K T")]
    [InlineData("P L EY1", "PastEd", "P L EY1 D")]
    [InlineData("W AA1 N T", "PastEd", "W AA1 N T IH0 D")]
    [InlineData("W AO1 K", "Ing", "W AO1 K IH0 NG")]
    public void Morphology_Apply_EndingByLastPhone(string stem, string kind, string expected)
    {
        // Act
        var result = Morphology.Apply(Pronunciation.Parse(stem), Enum.Parse<SuffixKind>(kind));

        // Assert
        Assert.Equal(expected, result.ToString());
    }

    /// <summary>Proves clitics attach to the previous word's pronunciation by its last phone.</summary>
    [Theory]
    [InlineData("AY1", "ContractionLl", "AY1 L")]
    [InlineData("IH1 T", "ContractionLl", "IH1 T AH0 L")]
    [InlineData("D AA1 G", "PossessiveS", "D AA1 G Z")]
    [InlineData("AY1", "ContractionM", "AY1 M")]
    [InlineData("K UH1 D", "ContractionVe", "K UH1 D AH0 V")]
    public void Morphology_AppendClitic_ByHostLastPhone(string host, string clitic, string expected)
    {
        // Act
        var result = Morphology.AppendClitic(Pronunciation.Parse(host), Enum.Parse<SuffixKind>(clitic));

        // Assert
        Assert.Equal(expected, result.ToString());
    }

    /// <summary>
    ///     Proves the rules reproduce the dictionary's own choices: for every lexicon entry that is a listed stem plus
    ///     an ending, deriving it from the stem agrees with the entry in at least 98% of the plural, past,
    ///     <c>-ing</c> and possessive cases (the s/z/iz and t/d/id decision, ignoring the IH0/AH0 spelling of the
    ///     syllabic ending).
    /// </summary>
    [Fact]
    public void LexiconSubsystem_Morphology_Audit_RegularEndings_AgreeWithLexiconAtLeast98Percent()
    {
        // Act
        var reports = MorphologyAudit.Run(CmuLexicon.Default).ToDictionary(r => r.Kind);

        // Assert: large populations, and the class agreement of each regular ending meets the gate
        Assert.All(
            [SuffixKind.PluralS, SuffixKind.PastEd, SuffixKind.Ing, SuffixKind.PossessiveS],
            kind =>
            {
                Assert.True(reports[kind].Compared > 1000, kind + " has too few comparable entries");
                Assert.True(reports[kind].ClassRate >= 0.98, kind + " class agreement is " + reports[kind].ClassRate.ToString("P2", System.Globalization.CultureInfo.InvariantCulture));
            });
    }
}
