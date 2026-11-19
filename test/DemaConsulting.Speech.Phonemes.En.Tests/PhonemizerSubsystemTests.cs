using DemaConsulting.Speech.Phonemes.En.LetterToSoundSubsystem;
using DemaConsulting.Speech.Phonemes.En.LexiconSubsystem;
using DemaConsulting.Speech.Phonemes.En.PhonemizerSubsystem;
using DemaConsulting.Speech.SynthesisSubsystem;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

namespace DemaConsulting.Speech.Phonemes.En.Tests;

/// <summary>
///     Tests of the English phonemizer subsystem. Subsystem level, driven through the public contract with synthetic
///     annotations of the kinds the English text analysis defines: default pronunciation without annotations, reading
///     selection, host overrides, replacements and spelled letters, punctuation, contractions, compounds, digits, and
///     the honest reporting of out-of-vocabulary words and unsupported tokens. Unit level: the EnglishPhonemizer unit
///     (argument and annotation handling), the WordTokenizer unit (with the spoken text it tokenizes) and the
///     WordPronouncer unit (the priority list), driven directly.
/// </summary>
/// <remarks>
///     The pronunciations asserted are well-known CMU Pronouncing Dictionary entries or the documented sound rules of
///     the morphology. The end-to-end behavior with the real text analysis is covered by the system golden corpora tests.
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
    public void EnglishPhonemizer_Phonemize_EmptyAnnotations_EqualsNoAnnotations()
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

    /// <summary>Proves the headword annotations (reading and pronunciation override) apply to a possessive or contraction with a straight and with a typographic apostrophe alike.</summary>
    [Theory]
    [InlineData("'")]
    [InlineData("\u2019")]
    public void PhonemizerSubsystem_Phonemize_HeadwordAnnotationsWithClitic_ApplyForBothApostrophes(string apostrophe)
    {
        // Act: a forced reading on the headword of a possessive, a host override on the headword of a possessive and of a
        // contraction; with the typographic apostrophe the text analysis also annotates the normalization of the apostrophe
        PhonemizedText Run(string word, string ending, params SegmentAnnotation[] annotations)
        {
            var normalized = apostrophe == "\u2019" ? [.. annotations, Norm(word.Length, 1, "'")] : annotations;
            return Phonemizer.Phonemize(word + apostrophe + ending, normalized);
        }

        var reading = Run("bass", "s", Reading(0, 4, "bass.music"));
        var fish = Run("bass", "s", Reading(0, 4, "bass.fish"));
        var possessive = Run("record", "s", Pronunciation(0, 6, "arpabet:R IY0 K AO1 R D"));
        var contraction = Run("record", "ll", Pronunciation(0, 6, "arpabet:R IY0 K AO1 R D"));
        var ipa = Run("record", "s", Pronunciation(0, 6, "ipa:ɹɪˈkɔɹd"));
        var ipaVowel = Run("sea", "s", Pronunciation(0, 3, "ipa:sˈiː"));
        var wholeToken = Run("record", "s", Pronunciation(0, 8, "arpabet:K AE1 T"));
        // Assert: the reading of the headword selects the pronunciation and the possessive ending follows it
        Assert.Equal("B EY1 S IH0 Z", reading.Items[0].Arpabet);
        Assert.Equal("B AE1 S IH0 Z", fish.Items[0].Arpabet);
        Assert.True(fish.Items[0].FromReading);
        Assert.Equal("bass's", fish.Items[0].Text); // the spoken text has the normalized apostrophe

        // Assert: the override of the headword wins and the ending follows it
        Assert.Equal("R IY0 K AO1 R D Z", possessive.Items[0].Arpabet);
        Assert.Equal(PronunciationSource.Override, possessive.Items[0].Source);
        Assert.Equal("record's", possessive.Items[0].Text);
        Assert.Equal("R IY0 K AO1 R D AH0 L", contraction.Items[0].Arpabet);
        Assert.Equal("ɹɪˈkɔɹdz", ipa.Items[0].IpaOverride);
        Assert.Equal("sˈiːz", ipaVowel.Items[0].IpaOverride);

        // Assert: an override of the whole token still beats everything
        Assert.Equal("K AE1 T", wholeToken.Items[0].Arpabet);
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

        // Assert: the override wins; missing stress digits give the first vowel primary stress; the IPA is carried for the renderer
        Assert.Equal("R IY0 K AO1 R D", arpabet.Items[0].Arpabet);
        Assert.Equal(PronunciationSource.Override, arpabet.Items[0].Source);
        Assert.Equal("K AE1 T", unstressed.Items[0].Arpabet);
        Assert.Equal(PronunciationSource.Override, ipa.Items[0].Source);
        Assert.Equal("ɹɪˈkɔɹd", ipa.Items[0].IpaOverride);
        Assert.Equal("R AH0 K AO1 R D", ipa.Items[0].Arpabet); // the normal pronunciation travels with the IPA, for a voice that cannot represent it
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

        // Assert: apostrophes and hyphens stay inside the word; each part of a compound keeps its own stress
        Assert.Equal(["don't", "dog's", "dogs'", "well-known", "unfollowed", "'tis"], result.Items.Select(i => i.Text));
        Assert.Equal("D OW1 N T", result.Items[0].Arpabet);
        Assert.Equal("D AO1 G Z", result.Items[1].Arpabet);
        Assert.Equal("D AO1 G Z", result.Items[2].Arpabet);
        Assert.Equal("W EH1 L N OW1 N", result.Items[3].Arpabet);
        Assert.Equal("AH0 N F AA1 L OW0 D", result.Items[4].Arpabet);
        Assert.Equal(PronunciationSource.Morphology, result.Items[4].Source);
        Assert.Empty(result.OovWords);
    }

    /// <summary>Proves the stress policy of hyphenated compounds: every part keeps its stress, a small function word inside a compound is unstressed, and a closed dictionary spelling is used only when a part is not a word.</summary>
    [Fact]
    public void PhonemizerSubsystem_Phonemize_HyphenatedCompounds_StressEveryPartAndReduceFunctionWords()
    {
        // Act
        var result = Phonemizer.Phonemize("a full-time job in a state-of-the-art day-to-day wi-fi");

        // Assert: full-time has a closed dictionary entry with a secondary stress on time, but both parts are words, so each is stressed
        Assert.Equal("F UH1 L T AY1 M", result.Items.Single(i => i.Text == "full-time").Arpabet);

        // Assert: of keeps its full vowel without stress and to is reduced inside a compound
        Assert.Equal("S T EY1 T AH2 V DH AH0 AA1 R T", result.Items.Single(i => i.Text == "state-of-the-art").Arpabet);
        Assert.Equal("D EY1 T AH0 D EY1", result.Items.Single(i => i.Text == "day-to-day").Arpabet);

        // Assert: wi and fi are not words of their own, so the dictionary's closed spelling wifi is used as listed
        Assert.Equal("W AY1 F AY2", result.Items.Single(i => i.Text == "wi-fi").Arpabet);
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
    public void EnglishPhonemizer_Phonemize_NullText_Throws()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => Phonemizer.Phonemize(null!));
        Assert.Throws<ArgumentNullException>(() => Phonemizer.Phonemize(null!, []));
    }

    /// <summary>Proves the words, punctuation, digits and unsupported runs of a text are classified and the audio-tag placeholder is skipped.</summary>
    [Fact]
    public void WordTokenizer_Tokenize_WordsPunctuationDigitsAndUnsupportedRuns_AreClassified()
    {
        // Arrange
        var spoken = SpokenText.Build("Wait... don't well-known 42 \U0001F600 \uFFFC go!", null);

        // Act
        var tokens = WordTokenizer.Tokenize(spoken, CmuLexicon.Default);

        // Assert: apostrophes and hyphens stay inside a word, a run of one mark is one token, the placeholder is skipped
        Assert.Equal(
            ["Wait", "...", "don't", "well-known", "42", "\U0001F600", "go", "!"],
            tokens.Select(t => spoken.Text.Substring(t.Start, t.Length)));
        Assert.Equal(
            [TokenKind.Word, TokenKind.Punctuation, TokenKind.Word, TokenKind.Word, TokenKind.Digits, TokenKind.Unsupported, TokenKind.Word, TokenKind.Punctuation],
            tokens.Select(t => t.Kind));
    }

    /// <summary>Proves a hyphen whose neighbors are both replacement text (the groups of a phone number, each replaced on its own) ends the word and is a punctuation token, while the hyphen of a compound, or one with an original neighbor, stays inside the word.</summary>
    [Fact]
    public void WordTokenizer_Tokenize_HyphenBetweenTwoReplacements_IsPunctuationNotACompoundJoint()
    {
        // Arrange: "555-0147" with both groups replaced; "COVID-19" with the number replaced; "twelve-year-old" untouched
        var phone = SpokenText.Build("555-0147", [Norm(0, 3, "five five five"), Norm(4, 4, "zero one four seven")]);
        var covid = SpokenText.Build("COVID-19", [Norm(6, 2, "nineteen")]);
        var compound = SpokenText.Build("twelve-year-old", null);

        // Act
        var phoneTokens = WordTokenizer.Tokenize(phone, CmuLexicon.Default);
        var covidTokens = WordTokenizer.Tokenize(covid, CmuLexicon.Default);
        var compoundTokens = WordTokenizer.Tokenize(compound, CmuLexicon.Default);

        // Assert: the hyphen is a token of its own between the groups; the other two stay one word each
        Assert.Equal(["five", "five", "five", "-", "zero", "one", "four", "seven"], phoneTokens.Select(t => phone.Text.Substring(t.Start, t.Length)));
        Assert.Equal(TokenKind.Punctuation, phoneTokens[3].Kind);
        Assert.Equal([TokenKind.Word], covidTokens.Select(t => t.Kind));
        Assert.Equal([TokenKind.Word], compoundTokens.Select(t => t.Kind));
    }

    /// <summary>Proves a leading apostrophe belongs to a word only when the dictionary lists it that way, and otherwise is a quote mark.</summary>
    [Fact]
    public void WordTokenizer_Tokenize_LeadingApostrophe_IsPartOfAWordOnlyWhenTheDictionaryListsIt()
    {
        // Act
        var tis = WordTokenizer.Tokenize(SpokenText.Build("'tis", null), CmuLexicon.Default);
        var quoted = WordTokenizer.Tokenize(SpokenText.Build("'hello'", null), CmuLexicon.Default);

        // Assert
        Assert.Equal([TokenKind.Word], tis.Select(t => t.Kind));
        Assert.Equal([TokenKind.Punctuation, TokenKind.Word, TokenKind.Punctuation], quoted.Select(t => t.Kind));
    }

    /// <summary>Proves the spoken text applies a replacement to its span, speaks a spelled replacement letter by letter, and ignores an annotation that does not fit the text.</summary>
    [Fact]
    public void WordTokenizer_Tokenize_ReplacementsAndSpelledLetters_AreAppliedAndBadAnnotationsIgnored()
    {
        // Act
        var replaced = SpokenText.Build("Dr. Smith", [Norm(0, 3, "Doctor")]);
        var outside = SpokenText.Build("cat", [Norm(0, 50, "dog")]);
        var spelled = SpokenText.Build("FBI's", [Spell(0, 3, "F B I")]);
        var letters = WordTokenizer.Tokenize(spelled, CmuLexicon.Default);

        // Assert: the replacement is spoken in place of the span; an out-of-range annotation changes nothing
        Assert.Equal("Doctor Smith", replaced.Text);
        Assert.Equal("cat", outside.Text);

        // Assert: one letter token per spelled letter, then the clitic that the replacement boundary cut off
        Assert.Equal([TokenKind.Letter, TokenKind.Letter, TokenKind.Letter, TokenKind.Clitic], letters.Select(t => t.Kind));
    }

    /// <summary>Proves the dictionary's first variant is the default except that the comparative of an adjective in <c>ng</c> takes the variant with the velar stop (<i>stronger</i> is listed with and without it), while an agentive does not.</summary>
    [Fact]
    public void WordPronouncer_Pronounce_ComparativeInNg_TakesTheVariantWithTheVelarStop()
    {
        // Arrange
        var pronouncer = new WordPronouncer(CmuLexicon.Default, ReadingPronunciations.Default, LetterToSound.Default);

        // Act
        var stronger = pronouncer.Pronounce("stronger", null);
        var singer = pronouncer.Pronounce("singer", null);

        // Assert: both come from the dictionary; the adjective has the stop, the agentive does not
        Assert.Equal((PronunciationSource.Lexicon, "S T R AO1 NG G ER0"), (stronger!.Source, stronger.Pronunciation.ToString()));
        Assert.Equal((PronunciationSource.Lexicon, "S IH1 NG ER0"), (singer!.Source, singer.Pronunciation.ToString()));
    }

    /// <summary>Proves each source of the priority list is used in order and reported: the reading, the dictionary's first variant, the morphology rules, a productive prefix, and the letter-to-sound model last.</summary>
    [Fact]
    public void WordPronouncer_Pronounce_ByPriority_ReportsTheSourceUsed()
    {
        // Arrange
        var pronouncer = new WordPronouncer(CmuLexicon.Default, ReadingPronunciations.Default, LetterToSound.Default);

        // Act
        var reading = pronouncer.Pronounce("record", "record.noun");
        var listed = pronouncer.Pronounce("record", null);
        var derived = pronouncer.Pronounce("unfollowed", null);
        var guessed = pronouncer.Pronounce("Zorblax", null);

        // Assert: the reading beats the dictionary, which beats the rules, which beat the model
        Assert.Equal((PronunciationSource.Reading, "R EH1 K ER0 D"), (reading!.Source, reading.Pronunciation.ToString()));
        Assert.Equal((PronunciationSource.Lexicon, "R AH0 K AO1 R D"), (listed!.Source, listed.Pronunciation.ToString()));
        Assert.Equal((PronunciationSource.Morphology, "AH0 N F AA1 L OW0 D"), (derived!.Source, derived.Pronunciation.ToString()));
        Assert.Equal(PronunciationSource.LetterToSound, guessed!.Source);
        Assert.True(guessed.Pronunciation.Count > 0);
    }

    /// <summary>Proves a productive prefix on a known word is pronounced as prefix plus word, and a word that merely begins like a prefix is left to the dictionary.</summary>
    [Fact]
    public void WordPronouncer_Pronounce_ProductivePrefix_IsPrefixPlusKnownWord()
    {
        // Arrange
        var pronouncer = new WordPronouncer(CmuLexicon.Default, ReadingPronunciations.Default, LetterToSound.Default);

        // Act
        var prefixed = pronouncer.Pronounce("unfollow", null);
        var real = pronouncer.Pronounce("under", null);

        // Assert
        Assert.NotNull(prefixed);
        Assert.Equal(PronunciationSource.Morphology, prefixed.Source);
        Assert.StartsWith("AH0 N F AA1 L", prefixed.Pronunciation.ToString(), StringComparison.Ordinal);
        Assert.Equal(PronunciationSource.Lexicon, real!.Source);
    }

    /// <summary>Proves diacritics are folded for lookup, a hyphenated compound pronounces every part with its own stress, a small function word inside it is unstressed, and text that is no word has no pronunciation.</summary>
    [Fact]
    public void WordPronouncer_Pronounce_DiacriticsCompoundsAndNonWords_FollowTheDocumentedRules()
    {
        // Arrange
        var pronouncer = new WordPronouncer(CmuLexicon.Default, ReadingPronunciations.Default, LetterToSound.Default);

        // Act
        var accented = pronouncer.Pronounce("café", null);
        var plain = pronouncer.Pronounce("cafe", null);
        var fullTime = pronouncer.Pronounce("full-time", null);
        var stateOfTheArt = pronouncer.Pronounce("state-of-the-art", null);
        var wifi = pronouncer.Pronounce("wi-fi", null);
        var notAWord = pronouncer.Pronounce("日本", null);

        // Assert
        Assert.Equal(plain!.Pronunciation.ToString(), accented!.Pronunciation.ToString());
        Assert.Equal("F UH1 L T AY1 M", fullTime!.Pronunciation.ToString());
        Assert.Equal("S T EY1 T AH2 V DH AH0 AA1 R T", stateOfTheArt!.Pronunciation.ToString());
        Assert.Equal("W AY1 F AY2", wifi!.Pronunciation.ToString());
        Assert.Null(notAWord);
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
