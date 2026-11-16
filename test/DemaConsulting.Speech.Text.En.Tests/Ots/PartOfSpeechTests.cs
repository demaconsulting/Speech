using DemaConsulting.PartOfSpeech;

namespace DemaConsulting.Speech.Text.En.Tests.Ots;

/// <summary>
///     OTS integration tests for the <c>DemaConsulting.PartOfSpeech</c> package: they exercise the
///     real package, through exactly the members the English text analysis uses, so the evidence
///     does not depend on the analysis code built on top of it. They also act as an API-presence
///     check: if a future version of the package removes or reshapes a member used here, this file
///     stops compiling.
/// </summary>
public sealed class PartOfSpeechTests
{
    /// <summary>Text with two sentences, a line break inside the second, a contraction and an abbreviation.</summary>
    private const string MultiSentenceText = "Dr. Smith doesn't smoke. She's\r\nwell-known; he left home.";

    /// <summary>Proves the shared default tagger tags a sentence with the expected Universal POS tag per token.</summary>
    [Fact]
    public void PartOfSpeech_DefaultTagger_TagsSentence_ReturnsUniversalTagPerToken()
    {
        // Arrange: a sentence whose tags the embedded model assigns with full confidence
        const string text = "The cat sat on the mat.";

        // Act: tag with the embedded default model
        var sentences = Tagger.Default.Tag(text);

        // Assert: one sentence, one tag per token, in the expected order
        var sentence = Assert.Single(sentences);
        Assert.Equal(["The", "cat", "sat", "on", "the", "mat", "."], sentence.Tokens.Select(token => token.Text));
        Assert.Equal(
            ["DET", "NOUN", "VERB", "ADP", "DET", "NOUN", "PUNCT"],
            sentence.Tokens.Select(token => token.Tag.ToTagString()));
    }

    /// <summary>Proves every read of the default tagger returns the same shared instance.</summary>
    [Fact]
    public void PartOfSpeech_DefaultTagger_SharedInstance_ReturnsSameTagger()
    {
        // Arrange / Act: read the shared tagger twice
        var first = Tagger.Default;
        var second = Tagger.Default;

        // Assert: the very same instance
        Assert.Same(first, second);
    }

    /// <summary>
    ///     Proves every member of the package the text analysis uses is present with the expected
    ///     shape, and that the tag set is the 17 Universal POS tags.
    /// </summary>
    [Fact]
    public void PartOfSpeech_PublicApi_MembersUsedByTextEn_ArePresent()
    {
        // Arrange: bind each member the analysis uses to a delegate of the expected shape, which
        // fails to compile when a member is removed or its signature changes
        Func<Tagger> defaultTagger = () => Tagger.Default;
        Func<string, IReadOnlyList<Sentence>> tokenize = Tokenizer.Tokenize;
        Func<Tagger, Sentence, TaggedSentence> tagSentence = (tagger, sentence) => tagger.Tag(sentence);
        Func<Tagger, string, IReadOnlyList<TaggedSentence>> tagText = (tagger, text) => tagger.Tag(text);
        Func<Upos, string> toTagString = tag => tag.ToTagString();

        // Act: use them on a tiny text, reading every property the analysis reads
        var sentence = tokenize("Go.")[0];
        var token = sentence.Tokens[0];
        var tagged = tagSentence(defaultTagger(), sentence).Tokens[0];
        var taggedByText = tagText(defaultTagger(), "Go.")[0].Tokens[0];

        // Assert: the properties are readable and the tag set has the 17 documented values
        Assert.Equal(("Go", 0, 2), (token.Text, token.Start, token.Length));
        Assert.Equal(("Go", 0, 2), (tagged.Text, tagged.Start, tagged.Length));
        Assert.Equal(tagged.Tag, taggedByText.Tag);
        Assert.InRange(tagged.Confidence, 0.0, 1.0);
        Assert.True(tagged.IsKnownWord);
        Assert.Equal(17, Enum.GetValues<Upos>().Length);
        Assert.Equal("NOUN", toTagString(Upos.Noun));
        Assert.Equal("PUNCT", toTagString(Upos.Punct));
        Assert.Equal("SYM", toTagString(Upos.Sym));
        Assert.Equal("NUM", toTagString(Upos.Num));
    }

    /// <summary>Proves tagging a text reports token offsets that index the source text, across sentences and a line break.</summary>
    [Fact]
    public void PartOfSpeech_Tag_MultiSentenceText_TokenOffsetsIndexSourceText()
    {
        // Arrange / Act
        var sentences = Tagger.Default.Tag(MultiSentenceText);

        // Assert: more than one sentence, and every token is exactly the source slice at its offset
        Assert.True(sentences.Count >= 2);
        foreach (var token in sentences.SelectMany(sentence => sentence.Tokens))
        {
            Assert.Equal(token.Text, MultiSentenceText.Substring(token.Start, token.Length));
        }
    }

    /// <summary>Proves the tokenizer alone reports token offsets that index the source text, including after a line break.</summary>
    [Fact]
    public void PartOfSpeech_Tokenizer_Tokenize_MultiSentenceText_TokenOffsetsIndexSourceText()
    {
        // Arrange / Act
        var sentences = Tokenizer.Tokenize(MultiSentenceText);

        // Assert: sentences are in text order and every token is the source slice at its offset
        Assert.True(sentences.Count >= 2);
        Assert.True(sentences[0].Tokens[0].Start < sentences[1].Tokens[0].Start);
        foreach (var token in sentences.SelectMany(sentence => sentence.Tokens))
        {
            Assert.Equal(token.Text, MultiSentenceText.Substring(token.Start, token.Length));
        }

        Assert.Contains(sentences.SelectMany(sentence => sentence.Tokens), token => token.Text == "n't");
    }

    /// <summary>Proves tagging sentence by sentence yields exactly the tags and offsets of tagging the whole text.</summary>
    [Fact]
    public void PartOfSpeech_TagSentence_PerSentence_MatchesWholeTextTagging()
    {
        // Arrange
        var whole = Tagger.Default.Tag(MultiSentenceText)
            .SelectMany(sentence => sentence.Tokens)
            .Select(token => (token.Start, token.Length, Tag: token.Tag.ToTagString(), token.Confidence))
            .ToList();

        // Act: tag each tokenized sentence on its own
        var perSentence = Tokenizer.Tokenize(MultiSentenceText)
            .Select(sentence => Tagger.Default.Tag(sentence))
            .SelectMany(sentence => sentence.Tokens)
            .Select(token => (token.Start, token.Length, Tag: token.Tag.ToTagString(), token.Confidence))
            .ToList();

        // Assert
        Assert.Equal(whole, perSentence);
    }

    /// <summary>Proves every token's confidence is a probability, and a token with a single allowed tag is fully confident.</summary>
    [Fact]
    public void PartOfSpeech_Tag_Tokens_ConfidenceIsProbabilityInRange()
    {
        // Arrange / Act
        var tokens = Tagger.Default.Tag("The cat sat on the mat. Time flies like an arrow.")
            .SelectMany(sentence => sentence.Tokens)
            .ToList();

        // Assert: every confidence is within 0..1 and the most clear-cut tokens are at the top end
        Assert.All(tokens, token => Assert.InRange(token.Confidence, 0.0, 1.0));
        Assert.Contains(tokens, token => token.Confidence >= 0.99);
    }

    /// <summary>Proves the known-word flag is true for an ordinary English word and false for a nonsense spelling.</summary>
    [Fact]
    public void PartOfSpeech_Tag_Tokens_KnownWordFlagReflectsLexicon()
    {
        // Arrange / Act
        var tokens = Tagger.Default.Tag("The qzxvbnk sat.")[0].Tokens;

        // Assert: "The" and "sat" are in the lexicon, the invented spelling is not
        Assert.True(tokens[0].IsKnownWord);
        Assert.False(tokens[1].IsKnownWord);
        Assert.True(tokens[2].IsKnownWord);
    }

    /// <summary>Proves many threads tagging at once through the shared tagger get exactly the results of tagging sequentially.</summary>
    [Fact]
    public async Task PartOfSpeech_DefaultTagger_ConcurrentTagging_ReturnsSameResultsAsSequential()
    {
        // Arrange: a few distinct texts and their sequential reference signatures
        string[] texts =
        [
            "The cat sat on the mat.",
            MultiSentenceText,
            "Please read the record. I will record it later.",
            "Wow, that costs $5.50 on Jan. 5th!",
        ];
        var expected = texts.Select(Signature).ToArray();

        // Act: tag every text from many tasks at once, several times over
        var tasks = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(
                () =>
                {
                    var results = new List<string>();
                    for (var round = 0; round < 10; round++)
                    {
                        results.AddRange(texts.Select(Signature));
                    }

                    return results;
                },
                TestContext.Current.CancellationToken))
            .ToArray();
        var all = await Task.WhenAll(tasks);

        // Assert: every result of every task equals the sequential reference
        foreach (var results in all)
        {
            for (var i = 0; i < results.Count; i++)
            {
                Assert.Equal(expected[i % texts.Length], results[i]);
            }
        }
    }

    /// <summary>
    ///     Proves the tokenizer conventions the normalization relies on hold for the pinned package version: which
    ///     abbreviations keep their period in the token and which split it off, how clitics, symbols, marks, slash and
    ///     ampersand forms and invisible characters are tokenized, and that titles never end a sentence. A version bump
    ///     that changes any of them fails here instead of silently changing the normalization.
    /// </summary>
    [Fact]
    public void PartOfSpeech_Tokenizer_NormalizationRelevantConventions_HoldForPinnedVersion()
    {
        // Act / Assert: abbreviations that keep their period inside one token
        foreach (var abbreviation in new[] { "Dr.", "St.", "Mr.", "Prof.", "Capt.", "Sgt.", "Mt.", "Ft.", "Co.", "Ltd.", "Corp.", "Sr.", "Jr.", "e.g.", "i.e.", "etc.", "vs.", "J.", "J.R.R.", "U.S.", "U.S.A.", "p.m.", "a.m." })
        {
            Assert.Contains(abbreviation, Tokens($"Hello {abbreviation} world"));
        }

        // abbreviations the tokenizer splits into a word and an adjacent period token
        foreach (var word in new[] { "Ave", "Blvd", "Rd", "Ln", "Apt", "Ste", "Dept", "Bldg" })
        {
            var tokens = Tokens($"Hello {word}. world");
            var index = tokens.IndexOf(word);
            Assert.True(index >= 0 && index + 1 < tokens.Count && tokens[index + 1] == ".", $"'{word}.' is not split into a word and a period");
        }

        // clitics split off their host, and a typographic apostrophe is part of the clitic
        Assert.Equal(["I", "do", "n\u2019t", "think", "it", "\u2019s", "ready"], Tokens("I don\u2019t think it\u2019s ready"));
        Assert.Contains("'d", Tokens("I'd say"));
        Assert.Equal(["wait", "\u2019", "til", "dawn"], Tokens("wait \u2019til dawn"));

        // standalone symbols are single tokens, and these joined forms stay one token
        Assert.Equal(["a", "&", "b", "@", "c", "+", "d", "=", "e", "<", "f", ">", "g", "/", "h"], Tokens("a & b @ c + d = e < f > g / h"));
        foreach (var joined in new[] { "he/she", "and/or", "w/o", "N/A", "24/7", "km/h", "R&D", "Q&A", "AT&T", "Johnson&Johnson", "Brand\u2122", "a\u200Bb", "co\u00ADop" })
        {
            Assert.Contains(joined, Tokens($"Hello {joined} world"));
        }

        // titles never end a sentence, a business suffix can
        Assert.Single(Tokenizer.Tokenize("Dr. Smith lives on Maple Dr. He likes it."));
        Assert.Single(Tokenizer.Tokenize("I live on Main St. It is nice."));
        Assert.Single(Tokenizer.Tokenize("No. I don't know."));
        Assert.Equal(2, Tokenizer.Tokenize("Acme Inc. Smith left.").Count);
    }

    /// <summary>Tokenizes a text and returns the token strings of all its sentences in order.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The tokens.</returns>
    private static List<string> Tokens(string text) =>
        Tokenizer.Tokenize(text).SelectMany(sentence => sentence.Tokens).Select(token => text.Substring(token.Start, token.Length)).ToList();

    /// <summary>Builds a comparable signature of the tags, offsets and confidences of a tagged text.</summary>
    /// <param name="text">The text to tag with the shared tagger.</param>
    /// <returns>A string that is equal for equal tagging results.</returns>
    private static string Signature(string text) =>
        string.Join(
            ';',
            Tagger.Default.Tag(text)
                .SelectMany(sentence => sentence.Tokens)
                .Select(token => $"{token.Start}:{token.Length}:{token.Tag.ToTagString()}:{token.Confidence:R}:{token.IsKnownWord}"));
}
