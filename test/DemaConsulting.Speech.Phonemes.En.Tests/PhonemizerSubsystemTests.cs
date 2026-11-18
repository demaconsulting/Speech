using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tests;

/// <summary>
///     Subsystem tests for the English phonemizer, driven through its public contract with synthetic annotations of
///     the kinds the English text analysis defines: default pronunciation without annotations, reading selection,
///     host overrides, replacements and spelled letters, punctuation, contractions, compounds, digits, and the honest
///     reporting of out-of-vocabulary words and unsupported tokens.
/// </summary>
/// <remarks>
///     The pronunciations asserted are well-known CMU Pronouncing Dictionary entries or the documented sound rules of
///     the morphology. The end-to-end behavior with the real text analysis is covered by the golden corpora tests.
/// </remarks>
public sealed class PhonemizerSubsystemTests
{
    /// <summary>The phonemizer under test (immutable and thread-safe).</summary>
    private static readonly EnglishPhonemizer Phonemizer = new();

    /// <summary>Proves that without annotations a word gets its default pronunciation (the dictionary's first variant) and nothing is normalized.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_NoAnnotations_UsesDefaultReadingsWithoutNormalization()
    {
        // Act: a heteronym, a bare digit, and a word that is only a stem plus an ending in the dictionary
        var result = Phonemizer.Phonemize("record 5 cats");

        // Assert: the first dictionary variant of record, the digit spelled out as a safe fallback, no replacements
        Assert.Equal(["record", "5", "cats"], result.Items.Select(i => i.Text));
        Assert.Equal("R AH0 K AO1 R D", result.Items[0].Arpabet);
        Assert.Equal(PronunciationSource.Lexicon, result.Items[0].Source);
        Assert.Equal("F AY1 V", result.Items[1].Arpabet);
        Assert.Equal(PronunciationSource.Digits, result.Items[1].Source);
        Assert.Equal("K AE1 T S", result.Items[2].Arpabet);
        Assert.Empty(result.OovWords);
        Assert.Empty(result.UnsupportedTokens);
    }

    /// <summary>Proves an empty annotation list behaves exactly like no annotations.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_EmptyAnnotations_EqualsNoAnnotations()
    {
        // Act
        var none = Phonemizer.Phonemize("The record was 5.");
        var empty = Phonemizer.Phonemize("The record was 5.", []);

        // Assert
        Assert.Equal(none.Items.Select(i => i.ToString()), empty.Items.Select(i => i.ToString()));
    }

    /// <summary>Proves a reading annotation selects the pronunciation of that reading, for the lemma, an inflected form and a possessive.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_ReadingAnnotation_SelectsThatReading()
    {
        // Act
        var noun = Phonemizer.Phonemize("record", [Reading(0, 6, "record.noun")]);
        var verb = Phonemizer.Phonemize("record", [Reading(0, 6, "record.verb")]);
        var plural = Phonemizer.Phonemize("records", [Reading(0, 7, "record.noun")]);
        var possessive = Phonemizer.Phonemize("record's", [Reading(0, 6, "record.noun")]);

        // Assert: noun stress on the first syllable, verb on the second; the ending follows the reading
        Assert.Equal("R EH1 K ER0 D", noun.Items[0].Arpabet);
        Assert.True(noun.Items[0].FromReading);
        Assert.Equal("record.noun", noun.Items[0].ReadingId);
        Assert.Equal("R AH0 K AO1 R D", verb.Items[0].Arpabet);
        Assert.Equal("R EH1 K ER0 D Z", plural.Items[0].Arpabet);
        Assert.Equal("R EH1 K ER0 D Z", possessive.Items[0].Arpabet);
        Assert.True(possessive.Items[0].FromReading);
        Assert.Equal("record's", possessive.Items[0].Text);
    }

    /// <summary>Proves a reading id that cannot be applied to the word is visible: the word keeps its default and the request is reported.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_ReadingThatDoesNotApply_FallsBackVisibly()
    {
        // Act: the id belongs to another word
        var result = Phonemizer.Phonemize("cat", [Reading(0, 3, "record.noun")]);

        // Assert
        var item = Assert.Single(result.Items);
        Assert.Equal("K AE1 T", item.Arpabet);
        Assert.False(item.FromReading);
        Assert.Equal("record.noun", item.ReadingId);
        Assert.Equal(PronunciationSource.Lexicon, item.Source);
    }

    /// <summary>Proves a host pronunciation override beats the reading and the dictionary, in ARPAbet and in IPA.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_PronunciationOverride_BeatsReadingAndLexicon()
    {
        // Act
        var arpabet = Phonemizer.Phonemize("record", [Reading(0, 6, "record.noun"), Pronunciation(0, 6, "arpabet:R IY0 K AO1 R D")]);
        var unstressed = Phonemizer.Phonemize("record", [Pronunciation(0, 6, "arpabet:K AE T")]);
        var ipa = Phonemizer.Phonemize("record", [Pronunciation(0, 6, "ipa:ɹɪˈkɔɹd")]);

        // Assert: the override wins; missing stress digits give the first vowel primary stress; IPA is carried for the renderer
        Assert.Equal("R IY0 K AO1 R D", arpabet.Items[0].Arpabet);
        Assert.Equal(PronunciationSource.Override, arpabet.Items[0].Source);
        Assert.Equal("K AE1 T", unstressed.Items[0].Arpabet);
        Assert.Equal(PronunciationSource.Override, ipa.Items[0].Source);
        Assert.Equal("ɹɪˈkɔɹd", ipa.Items[0].IpaOverride);
        Assert.Empty(ipa.Items[0].Phones);
    }

    /// <summary>Proves normalization and spell-out replacements are applied and the replacement text is what is pronounced.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_Replacements_AreSpoken()
    {
        // Act: an abbreviation replaced by a word, an initialism spelled, an empty replacement, and a clitic after the spelled letters
        var doctor = Phonemizer.Phonemize("Dr. Smith", [Norm(0, 3, "Doctor")]);
        var spelled = Phonemizer.Phonemize("FBI", [Spell(0, 3, "F B I")]);
        var possessive = Phonemizer.Phonemize("FBI's", [Spell(0, 3, "F B I")]);
        var silent = Phonemizer.Phonemize("a [x] b", [Norm(2, 3, string.Empty)]);

        // Assert
        Assert.Equal(["Doctor", "Smith"], doctor.Items.Select(i => i.Text));
        Assert.Equal("D AA1 K T ER0", doctor.Items[0].Arpabet);
        Assert.Equal(["EH1 F", "B IY1", "AY1"], spelled.Items.Select(i => i.Arpabet));
        Assert.All(spelled.Items, i => Assert.Equal(PronunciationSource.Spelled, i.Source));
        Assert.Equal(["EH1 F", "B IY1", "AY1 Z"], possessive.Items.Select(i => i.Arpabet));
        Assert.Equal(["a", "b"], silent.Items.Select(i => i.Text));
    }

    /// <summary>Proves a spelled letter is its letter name, never the article or the pronoun.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_SpelledLetterAOrI_IsTheLetterName()
    {
        // Act
        var result = Phonemizer.Phonemize("A I", [Spell(0, 3, "A I")]);

        // Assert
        Assert.Equal(["EY1", "AY1"], result.Items.Select(i => i.Arpabet));
    }

    /// <summary>Proves clause punctuation passes through as items, a run of one mark is one item, and the audio-tag placeholder is not an item.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_Punctuation_PassesThrough()
    {
        // Act
        var result = Phonemizer.Phonemize("Wait... what, then? \uFFFC Go!");

        // Assert
        Assert.Equal(["Wait", "...", "what", ",", "then", "?", "Go", "!"], result.Items.Select(i => i.Text));
        Assert.Equal(
            [PhonemizedItemKind.Word, PhonemizedItemKind.Punctuation, PhonemizedItemKind.Word, PhonemizedItemKind.Punctuation, PhonemizedItemKind.Word, PhonemizedItemKind.Punctuation, PhonemizedItemKind.Word, PhonemizedItemKind.Punctuation],
            result.Items.Select(i => i.Kind));
        Assert.All(result.Items.Where(i => i.Kind == PhonemizedItemKind.Punctuation), i => Assert.Empty(i.Phones));
    }

    /// <summary>Proves contractions, possessives, hyphenated compounds and derived forms are single words with the expected sounds.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_ContractionsPossessivesAndCompounds_AreOneWord()
    {
        // Act
        var result = Phonemizer.Phonemize("don't dog's dogs' well-known unfollowed 'tis");

        // Assert: apostrophes and hyphens stay inside the word; the second part of a compound takes secondary stress
        Assert.Equal(["don't", "dog's", "dogs'", "well-known", "unfollowed", "'tis"], result.Items.Select(i => i.Text));
        Assert.Equal("D OW1 N T", result.Items[0].Arpabet);
        Assert.Equal("D AO1 G Z", result.Items[1].Arpabet);
        Assert.Equal("D AO1 G Z", result.Items[2].Arpabet);
        Assert.Equal("W EH1 L N OW2 N", result.Items[3].Arpabet);
        Assert.Equal("AH0 N F AA1 L OW0 D", result.Items[4].Arpabet);
        Assert.Equal(PronunciationSource.Morphology, result.Items[4].Source);
        Assert.Empty(result.OovWords);
    }

    /// <summary>Proves a bare digit string is spoken digit by digit, one item per digit.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_BareDigits_AreSpokenOneByOne()
    {
        // Act
        var result = Phonemizer.Phonemize("42");

        // Assert
        Assert.Equal(["F AO1 R", "T UW1"], result.Items.Select(i => i.Arpabet));
        Assert.All(result.Items, i => Assert.Equal(PronunciationSource.Digits, i.Source));
    }

    /// <summary>Proves an unknown word is never silent: it is pronounced, flagged out of vocabulary and reported once.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_UnknownWord_IsFlaggedAndReported()
    {
        // Act
        var result = Phonemizer.Phonemize("Zorblax met Zorblax");

        // Assert
        Assert.True(result.Items[0].Oov);
        Assert.NotEmpty(result.Items[0].Phones);
        Assert.False(result.Items[1].Oov);
        Assert.Equal(["Zorblax"], result.OovWords);
    }

    /// <summary>Proves a token that cannot be pronounced is kept as an unsupported item and reported, never dropped.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_UnsupportedTokens_AreKeptAndReported()
    {
        // Act
        var result = Phonemizer.Phonemize("Hi 😀 there 日本 and #");

        // Assert
        Assert.Equal(["Hi", "😀", "there", "日本", "and", "#"], result.Items.Select(i => i.Text));
        Assert.Equal(["😀", "日本", "#"], result.UnsupportedTokens);
        Assert.All(result.Items.Where(i => i.Kind == PhonemizedItemKind.Unsupported), i =>
        {
            Assert.Empty(i.Phones);
            Assert.Equal(PronunciationSource.None, i.Source);
        });
    }

    /// <summary>Proves a word with a diacritic is looked up without it.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_Diacritics_AreFoldedForLookup()
    {
        // Act
        var accented = Phonemizer.Phonemize("café");
        var plain = Phonemizer.Phonemize("cafe");

        // Assert
        Assert.Equal(plain.Items[0].Arpabet, accented.Items[0].Arpabet);
        Assert.Equal("café", accented.Items[0].Text);
    }

    /// <summary>Proves malformed or out-of-range annotations are ignored rather than corrupting the result.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_AnnotationOutsideTheText_IsIgnored()
    {
        // Act: a replacement that runs past the end of the text, and a reading id of the wrong shape
        var result = Phonemizer.Phonemize("cat", [Norm(0, 50, "dog"), Reading(0, 3, "NOT A READING")]);

        // Assert
        Assert.Equal("K AE1 T", Assert.Single(result.Items).Arpabet);
    }

    /// <summary>Proves null text is rejected.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_NullText_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => Phonemizer.Phonemize(null!));
    }

    /// <summary>Creates a reading annotation.</summary>
    /// <param name="start">The span start.</param>
    /// <param name="length">The span length.</param>
    /// <param name="id">The reading id.</param>
    /// <returns>The annotation.</returns>
    private static SegmentAnnotation Reading(int start, int length, string id) => new(start, length, TextEnAnnotations.Reading, id);

    /// <summary>Creates a pronunciation annotation.</summary>
    /// <param name="start">The span start.</param>
    /// <param name="length">The span length.</param>
    /// <param name="value">The <c>scheme:phonemes</c> value.</param>
    /// <returns>The annotation.</returns>
    private static SegmentAnnotation Pronunciation(int start, int length, string value) => new(start, length, TextEnAnnotations.Pronunciation, value);

    /// <summary>Creates a normalization annotation.</summary>
    /// <param name="start">The span start.</param>
    /// <param name="length">The span length.</param>
    /// <param name="value">The spoken replacement.</param>
    /// <returns>The annotation.</returns>
    private static SegmentAnnotation Norm(int start, int length, string value) => new(start, length, TextEnAnnotations.Normalized, value);

    /// <summary>Creates a spell-out annotation.</summary>
    /// <param name="start">The span start.</param>
    /// <param name="length">The span length.</param>
    /// <param name="value">The letters, separated by single spaces.</param>
    /// <returns>The annotation.</returns>
    private static SegmentAnnotation Spell(int start, int length, string value) => new(start, length, TextEnAnnotations.SpellOut, value);
}
