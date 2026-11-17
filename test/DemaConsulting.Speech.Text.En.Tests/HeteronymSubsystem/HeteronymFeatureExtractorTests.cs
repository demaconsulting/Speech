using System.Globalization;
using System.Text;
using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.HeteronymSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.HeteronymSubsystem;

/// <summary>
///     Unit tests for <see cref="HeteronymFeatureExtractor"/>, <see cref="HeteronymContext"/> and
///     <see cref="FeatureHashing"/>: deterministic, culture-independent feature extraction whose hashes are
///     locked by golden vectors, because a trained model is only valid while the features are unchanged.
/// </summary>
public sealed class HeteronymFeatureExtractorTests
{
    /// <summary>The name of the golden vectors file in the <c>TestData</c> folder.</summary>
    private const string GoldenFileName = "heteronym-feature-vectors.tsv";

    /// <summary>Proves the golden feature vectors are reproduced exactly, under cultures whose rules differ (so no extraction step depends on the current culture).</summary>
    /// <param name="cultureName">The culture to run under; empty for the invariant culture.</param>
    [Theory]
    [InlineData("")]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    [InlineData("ar-SA")]
    public void HeteronymFeatureExtractor_Extract_GoldenVectors_MatchFileUnderEveryCulture(string cultureName)
    {
        // Arrange
        var rows = ReadGolden();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = cultureName.Length == 0 ? CultureInfo.InvariantCulture : new CultureInfo(cultureName);

            // Act / Assert: every row's hashes equal the file's
            Assert.NotEmpty(rows);
            foreach (var row in rows)
            {
                var (conjoined, shared) = ExtractHex(row.Tokens, row.Target);
                Assert.True(row.Conjoined == conjoined, $"{row.Id}: conjoined hashes differ");
                Assert.True(row.Shared == shared, $"{row.Id}: shared hashes differ");
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Proves the golden file covers the documented edge cases (sentence edges, clitics, curly apostrophes, numbers, capitals, supplementary letters, punctuation, untagged tokens, a long sentence).</summary>
    [Fact]
    public void HeteronymFeatureExtractor_Extract_GoldenVectors_CoverTheDocumentedCases()
    {
        // Act
        var ids = ReadGolden().Select(row => row.Id).ToList();

        // Assert
        Assert.Equal(
            ["basic-verb", "basic-noun", "sentence-initial", "sentence-final-untagged", "clitic-split", "curly-apostrophe", "numbers", "capitalized", "all-caps", "non-ascii-neighbor", "supplementary-letter", "punctuation-neighbors", "untagged", "long-bag", "closed-classes"],
            ids);
    }

    /// <summary>Proves extraction is a pure function: the same input gives the same hashes in the same order.</summary>
    [Fact]
    public void HeteronymFeatureExtractor_Extract_SameInputTwice_GivesIdenticalHashes()
    {
        // Arrange
        var context = Describe("I:PRON:0.99 want:VERB:0.95 to:PART:0.99 record:VERB:0.97 a:DET:0.99 record:NOUN:0.98 .:PUNCT:1.0");

        // Act
        var first = ExtractHex(context, 3);
        var second = ExtractHex(context, 3);

        // Assert
        Assert.Equal(first, second);
    }

    /// <summary>Proves the hashes do not depend on where the sentence sits in the text (the context is the sentence only).</summary>
    [Fact]
    public void HeteronymFeatureExtractor_Extract_SentenceOffsetsInText_DoNotChangeFeatures()
    {
        // Arrange: the same tokens at two different offsets
        const string sentence = "I:PRON:0.99 want:VERB:0.95 to:PART:0.99 record:VERB:0.97 a:DET:0.99 record:NOUN:0.98 .:PUNCT:1.0";
        var plain = Describe(sentence, 0);
        var shifted = Describe(sentence, 5_000);

        // Act / Assert
        Assert.Equal(ExtractHex(plain, 3), ExtractHex(shifted, 3));
    }

    /// <summary>Proves a target at the sentence edge sees the start and end markers instead of tokens outside the sentence.</summary>
    [Fact]
    public void HeteronymFeatureExtractor_Extract_SentenceEdges_UseMarkerWords()
    {
        // Arrange
        var context = Describe("Read:VERB:0.9 it:PRON:0.99");
        Span<uint> conjoined = stackalloc uint[HeteronymFeatureExtractor.MaxConjoined];
        Span<uint> shared = stackalloc uint[HeteronymFeatureExtractor.MaxShared];

        // Act
        HeteronymFeatureExtractor.Extract(context, 0, conjoined, shared, out var conjoinedCount, out _);
        var hashes = conjoined[..conjoinedCount].ToArray();

        // Assert: the word feature at offset -1 (template 4, argument 7) names the sentence-start marker
        Assert.Contains(FeatureHashing.Feature(4, 7, FeatureHashing.Fnv1a("<S>")), hashes);
        Assert.Contains(FeatureHashing.Feature(4, 11, FeatureHashing.Fnv1a("</S>")), hashes);
    }

    /// <summary>Proves the number of features stays inside the buffers for a maximal context, and reports sizes within the limits.</summary>
    [Fact]
    public void HeteronymFeatureExtractor_Extract_MaximalContext_StaysWithinBuffers()
    {
        // Arrange: 21 long content words around the target, so the bag, the neighbor suffixes and every window feature are present
        var tokens = string.Join(' ', Enumerable.Range(0, 21).Select(i => i == 10 ? "record:VERB:0.95" : "running:VERB:0.95"));
        var context = Describe(tokens);
        Span<uint> conjoined = stackalloc uint[HeteronymFeatureExtractor.MaxConjoined];
        Span<uint> shared = stackalloc uint[HeteronymFeatureExtractor.MaxShared];

        // Act
        HeteronymFeatureExtractor.Extract(context, 10, conjoined, shared, out var conjoinedCount, out var sharedCount);

        // Assert: the documented maxima are 78 and 37, well inside the 96 and 48 buffers
        Assert.InRange(conjoinedCount, 70, HeteronymFeatureExtractor.MaxConjoined);
        Assert.InRange(sharedCount, 30, HeteronymFeatureExtractor.MaxShared);
    }

    /// <summary>Proves a buffer that is too small is reported as a defect, never overrun.</summary>
    [Fact]
    public void HeteronymFeatureExtractor_Extract_BufferTooSmall_ThrowsInvalidOperationException()
    {
        // Arrange
        var context = Describe("I:PRON:0.99 record:VERB:0.9 it:PRON:0.99");

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => HeteronymFeatureExtractor.Extract(context, 1, new uint[4], new uint[HeteronymFeatureExtractor.MaxShared], out _, out _));
        Assert.Throws<InvalidOperationException>(() => HeteronymFeatureExtractor.Extract(context, 1, new uint[HeteronymFeatureExtractor.MaxConjoined], new uint[2], out _, out _));
    }

    /// <summary>Proves a target outside the sentence is rejected.</summary>
    /// <param name="target">The invalid target index.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void HeteronymFeatureExtractor_Extract_TargetOutsideSentence_ThrowsArgumentOutOfRangeException(int target)
    {
        // Arrange
        var context = Describe("I:PRON:0.99 record:VERB:0.9");

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => HeteronymFeatureExtractor.Extract(context, target, new uint[HeteronymFeatureExtractor.MaxConjoined], new uint[HeteronymFeatureExtractor.MaxShared], out _, out _));
    }

    /// <summary>Proves the feature-set version is 1 and the buffer maxima are the documented ones.</summary>
    [Fact]
    public void HeteronymFeatureExtractor_Constants_AreTheDocumentedOnes()
    {
        // Assert
        Assert.Equal(1, HeteronymFeatureExtractor.FeatureSetVersion);
        Assert.Equal(96, HeteronymFeatureExtractor.MaxConjoined);
        Assert.Equal(48, HeteronymFeatureExtractor.MaxShared);
    }

    /// <summary>Proves FNV-1a over code units gives the published known answers.</summary>
    [Fact]
    public void FeatureHashing_Fnv1a_KnownAnswers()
    {
        // Assert: the reference values of the FNV-1a 32-bit hash
        Assert.Equal(0x811C9DC5u, FeatureHashing.Fnv1a(""));
        Assert.Equal(0xE40C292Cu, FeatureHashing.Fnv1a("a"));
        Assert.Equal(0xBF9CF968u, FeatureHashing.Fnv1a("foobar"));
        Assert.Equal(0x8CBF6432u, FeatureHashing.Fnv1a("<S>"));
    }

    /// <summary>Proves the folded hash equals the plain hash of the ASCII-lowered text and leaves non-ASCII letters alone.</summary>
    [Fact]
    public void FeatureHashing_Fnv1aFolded_FoldsAsciiOnly()
    {
        // Assert
        Assert.Equal(FeatureHashing.Fnv1a("foobar"), FeatureHashing.Fnv1aFolded("FooBAR"));
        Assert.NotEqual(FeatureHashing.Fnv1a("i"), FeatureHashing.Fnv1aFolded("\u0130"));
        Assert.Equal(FeatureHashing.Fnv1a("\u00C9"), FeatureHashing.Fnv1aFolded("\u00C9"));
        Assert.Equal('a', FeatureHashing.FoldAscii('A'));
        Assert.Equal('\u212A', FeatureHashing.FoldAscii('\u212A'));
    }

    /// <summary>Proves the combiner, the feature hash and the salts equal values computed by an independent implementation.</summary>
    [Fact]
    public void FeatureHashing_MixAndFeature_ReferenceValues()
    {
        // Assert: expected values come from an independent reference implementation (Python) of Murmur3 mixing
        Assert.Equal(0x7F8EA7CCu, FeatureHashing.Mix(0x12345678u, 0x9ABCDEF0u));
        Assert.Equal(0xDC472F75u, FeatureHashing.Feature(0, 0, 0));
        Assert.Equal(0x70860BBFu, FeatureHashing.Feature(1, 5, 9));
        Assert.Equal(0xB83C48B6u, FeatureHashing.Feature(4, 7, FeatureHashing.Fnv1a("<S>")));
        Assert.Equal(0xC523ACF4u, FeatureHashing.LabelSalt("record.noun"));
        Assert.Equal(0x38AF8491u, FeatureHashing.RoleSalt("noun"));
    }

    /// <summary>Proves the lower-case hash folds ASCII only, so the Turkish dotted capital I is not the letter i, under any culture.</summary>
    [Fact]
    public void HeteronymContext_Create_TurkishDottedI_IsNotFoldedToAscii()
    {
        // Arrange
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            // Act
            var capital = HeteronymContext.Create("I", 0, 1, null, 0);
            var lower = HeteronymContext.Create("i", 0, 1, null, 0);
            var dotted = HeteronymContext.Create("\u0130", 0, 1, null, 0);

            // Assert
            Assert.Equal(lower.LowerHash, capital.LowerHash);
            Assert.NotEqual(lower.LowerHash, dotted.LowerHash);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    /// <summary>Proves the curly single quotes normalize to the plain apostrophe in the lower-case form, its suffixes and its class.</summary>
    [Fact]
    public void HeteronymContext_Create_CurlyApostrophes_NormalizeToPlain()
    {
        // Act
        var plain = HeteronymContext.Create("n't", 0, 3, null, 0);
        var curly = HeteronymContext.Create("n\u2019t", 0, 3, null, 0);
        var opening = HeteronymContext.Create("n\u2018t", 0, 3, null, 0);

        // Assert
        Assert.Equal(plain, curly);
        Assert.Equal(plain, opening);
        Assert.Equal(HeteronymContext.ClassNeg, curly.Class);
    }

    /// <summary>Proves tokens of digits and no letters share one normalized form, while a token with a letter does not.</summary>
    [Fact]
    public void HeteronymContext_Create_DigitOnlyTokens_ShareTheNumberForm()
    {
        // Act
        var one = HeteronymContext.Create("1999", 0, 4, null, 0);
        var two = HeteronymContext.Create("42", 0, 2, null, 0);
        var money = HeteronymContext.Create("$5", 0, 2, null, 0);
        var ordinal = HeteronymContext.Create("3rd", 0, 3, null, 0);
        var arabicIndic = HeteronymContext.Create("\u0663", 0, 1, null, 0);

        // Assert
        Assert.Equal(FeatureHashing.Fnv1a("<num>"), one.LowerHash);
        Assert.Equal(one.LowerHash, two.LowerHash);
        Assert.Equal(one.LowerHash, money.LowerHash);
        Assert.Equal(one.LowerHash, arabicIndic.LowerHash);
        Assert.NotEqual(one.LowerHash, ordinal.LowerHash);
        Assert.Equal(HeteronymContext.ClassNum, one.Class);
        Assert.Equal(5, one.LowerLength);
        Assert.Equal(HeteronymContext.ClassNone, ordinal.Class);
    }

    /// <summary>Proves the shape of a token is decided per Unicode scalar, including supplementary-plane letters and a lone surrogate.</summary>
    /// <param name="text">The token text.</param>
    /// <param name="shape">The expected shape code.</param>
    /// <param name="isWord">Whether the token is a word.</param>
    [Theory]
    [InlineData("record", 3, true)]
    [InlineData("Record", 6, true)]
    [InlineData("RECORD", 5, true)]
    [InlineData("rEcord", 7, true)]
    [InlineData("A", 4, true)]
    [InlineData("1999", 2, true)]
    [InlineData("...", 1, false)]
    [InlineData("\U0001F600", 1, false)]
    [InlineData("\U0001D400", 4, true)]
    [InlineData("\U0001D400\U0001D401", 5, true)]
    [InlineData("\uD800", 1, false)]
    [InlineData("\u00C9cole", 6, true)]
    public void HeteronymContext_Create_Shape_IsDecidedPerScalar(string text, int shape, bool isWord)
    {
        // Act
        var token = HeteronymContext.Create(text, 0, text.Length, null, 0);

        // Assert
        Assert.Equal(shape, token.Shape);
        Assert.Equal(isWord, token.IsWord);
        Assert.Equal(isWord, token.Class != HeteronymContext.ClassPunct);
    }

    /// <summary>Proves the closed word lists classify words case-insensitively and leave long and ordinary words unclassified.</summary>
    /// <param name="text">The token text.</param>
    /// <param name="expected">The expected class code.</param>
    [Theory]
    [InlineData("the", HeteronymContext.ClassDet)]
    [InlineData("THE", HeteronymContext.ClassDet)]
    [InlineData("she", HeteronymContext.ClassPron)]
    [InlineData("would", HeteronymContext.ClassModal)]
    [InlineData("'ll", HeteronymContext.ClassModal)]
    [InlineData("was", HeteronymContext.ClassAux)]
    [InlineData("'s", HeteronymContext.ClassAux)]
    [InlineData("to", HeteronymContext.ClassTo)]
    [InlineData("n't", HeteronymContext.ClassNeg)]
    [InlineData("under", HeteronymContext.ClassPrep)]
    [InlineData("because", HeteronymContext.ClassConj)]
    [InlineData("record", HeteronymContext.ClassNone)]
    [InlineData("underneath", HeteronymContext.ClassPrep)]
    [InlineData("understandingly", HeteronymContext.ClassNone)]
    [InlineData(",", HeteronymContext.ClassPunct)]
    [InlineData("42", HeteronymContext.ClassNum)]
    public void HeteronymContext_Create_Class_UsesTheClosedWordLists(string text, byte expected)
    {
        // Act
        var token = HeteronymContext.Create(text, 0, text.Length, null, 0);

        // Assert
        Assert.Equal(expected, token.Class);
    }

    /// <summary>Proves the tag names map to the alphabetical codes 1 to 17 and anything else to 0.</summary>
    [Fact]
    public void HeteronymContext_Create_TagNames_MapToAlphabeticalCodes()
    {
        // Arrange
        string[] names = ["ADJ", "ADP", "ADV", "AUX", "CCONJ", "DET", "INTJ", "NOUN", "NUM", "PART", "PRON", "PROPN", "PUNCT", "SCONJ", "SYM", "VERB", "X"];

        // Act
        var codes = names.Select(name => HeteronymContext.Create("x", 0, 1, name, 0).Upos).ToArray();

        // Assert
        Assert.Equal(Enumerable.Range(1, 17).Select(i => (byte)i), codes);
        Assert.Equal(0, HeteronymContext.Create("x", 0, 1, null, 0).Upos);
        Assert.Equal(0, HeteronymContext.Create("x", 0, 1, "noun", 0).Upos);
        Assert.Equal(0, HeteronymContext.Create("x", 0, 1, "WAT", 0).Upos);
    }

    /// <summary>Proves the confidence buckets are 3, 2, 1 and 0 at the documented cut points, and NaN is 0.</summary>
    /// <param name="confidence">The confidence.</param>
    /// <param name="bucket">The expected bucket.</param>
    [Theory]
    [InlineData(1.0, 3)]
    [InlineData(0.99, 3)]
    [InlineData(0.9899, 2)]
    [InlineData(0.9, 2)]
    [InlineData(0.89, 1)]
    [InlineData(0.7, 1)]
    [InlineData(0.69, 0)]
    [InlineData(0.0, 0)]
    [InlineData(double.NaN, 0)]
    public void HeteronymContext_Create_Confidence_FallsInDocumentedBucket(double confidence, int bucket)
    {
        // Act
        var token = HeteronymContext.Create("x", 0, 1, "NOUN", confidence);

        // Assert
        Assert.Equal(bucket, token.ConfidenceBucket);
    }

    /// <summary>Proves the token range is validated without overflow: negative, empty, past-the-end and near-int.MaxValue ranges are rejected.</summary>
    /// <param name="start">The start.</param>
    /// <param name="length">The length.</param>
    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 7)]
    [InlineData(3, 4)]
    [InlineData(6, 1)]
    [InlineData(int.MaxValue, 1)]
    [InlineData(1, int.MaxValue)]
    [InlineData(int.MaxValue, int.MaxValue)]
    public void HeteronymContext_Create_InvalidRange_ThrowsArgumentOutOfRangeException(int start, int length)
    {
        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => HeteronymContext.Create("hello!", start, length, null, 0));
    }

    /// <summary>Proves a null text is rejected and a range that ends exactly at the end of the text is accepted.</summary>
    [Fact]
    public void HeteronymContext_Create_NullTextAndExactEnd_AreHandled()
    {
        // Act
        var token = HeteronymContext.Create("hello", 2, 3, null, 0);

        // Assert
        Assert.Throws<ArgumentNullException>(() => HeteronymContext.Create(null!, 0, 1, null, 0));
        Assert.Equal(2, token.Start);
        Assert.Equal(3, token.Length);
    }

    /// <summary>Proves tokens the tokenizer yields for equivalent spellings (clitics, quotes, brackets, abbreviations, compounds) are the same texts and give the same target features, so the trainer and the analyzer see one context.</summary>
    /// <param name="first">One spelling.</param>
    /// <param name="second">The equivalent spelling.</param>
    /// <param name="target">The target word to compare features of.</param>
    [Theory]
    [InlineData("I can't read it.", "I ca n't read it.", "read")]
    [InlineData("They don't record it.", "They do n't record it.", "record")]
    [InlineData("John's record is good.", "John 's record is good.", "record")]
    [InlineData("He said \"read it\" twice.", "He said \" read it \" twice.", "read")]
    [InlineData("(Read it) now.", "( Read it ) now.", "Read")]
    [InlineData("The U.S. record stands.", "The U.S. record stands .", "record")]
    [InlineData("A well-known record.", "A well-known record .", "record")]
    public void HeteronymContext_Create_TokenizationParity_EquivalentSpellingsGiveTheSameFeatures(string first, string second, string target)
    {
        // Act
        var a = DescribeWithTokenizer(first, target);
        var b = DescribeWithTokenizer(second, target);

        // Assert: same token texts, same target and same hashes
        Assert.Equal(a.Words, b.Words);
        Assert.Equal(a.Hex, b.Hex);
    }

    /// <summary>
    ///     Reads the golden vectors file.
    /// </summary>
    /// <returns>The rows in file order.</returns>
    private static List<GoldenRow> ReadGolden()
    {
        var lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "TestData", GoldenFileName));
        Assert.StartsWith("id\ttokens\ttarget\tconjoined\tshared", lines[0], StringComparison.Ordinal);
        return [.. lines.Skip(1).Where(line => line.Length > 0).Select(line =>
        {
            var columns = line.Split('\t');
            Assert.Equal(5, columns.Length);
            return new GoldenRow(columns[0], Describe(columns[1]), int.Parse(columns[2], CultureInfo.InvariantCulture), columns[3], columns[4]);
        })];
    }

    /// <summary>
    ///     Builds a context from a token description: space-separated <c>text:TAG:confidence</c> items, <c>-</c> for no tag.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <param name="offset">The offset of the first token in the (virtual) text.</param>
    /// <returns>The context tokens.</returns>
    private static ContextToken[] Describe(string description, int offset = 0)
    {
        var tokens = description.Split(' ');
        var text = new StringBuilder(new string(' ', offset));
        var starts = new int[tokens.Length];
        var parts = new (string Text, string? Tag, double Confidence)[tokens.Length];
        for (var i = 0; i < tokens.Length; i++)
        {
            // The text may itself contain colons only as the first character, so split from the right.
            var last = tokens[i].LastIndexOf(':');
            var middle = tokens[i].LastIndexOf(':', last - 1);
            var word = tokens[i][..middle];
            var tag = tokens[i][(middle + 1)..last];
            var confidence = double.Parse(tokens[i][(last + 1)..], CultureInfo.InvariantCulture);
            parts[i] = (word, tag == "-" ? null : tag, confidence);
            starts[i] = text.Length;
            text.Append(word).Append(' ');
        }

        var built = text.ToString();
        return [.. parts.Select((part, i) => HeteronymContext.Create(built, starts[i], part.Text.Length, part.Tag, part.Confidence))];
    }

    /// <summary>
    ///     Extracts the features of a target and renders them as comma-separated hexadecimal hashes.
    /// </summary>
    /// <param name="context">The sentence.</param>
    /// <param name="target">The target index.</param>
    /// <returns>The conjoined and shared hash lists.</returns>
    private static (string Conjoined, string Shared) ExtractHex(ContextToken[] context, int target)
    {
        var conjoined = new uint[HeteronymFeatureExtractor.MaxConjoined];
        var shared = new uint[HeteronymFeatureExtractor.MaxShared];
        HeteronymFeatureExtractor.Extract(context, target, conjoined, shared, out var conjoinedCount, out var sharedCount);
        return (Render(conjoined, conjoinedCount), Render(shared, sharedCount));
    }

    /// <summary>
    ///     Renders hashes as comma-separated eight-digit hexadecimal numbers.
    /// </summary>
    /// <param name="hashes">The hash buffer.</param>
    /// <param name="count">The number of valid hashes.</param>
    /// <returns>The text.</returns>
    private static string Render(uint[] hashes, int count) =>
        string.Join(',', hashes.Take(count).Select(hash => hash.ToString("x8", CultureInfo.InvariantCulture)));

    /// <summary>
    ///     Tokenizes a text with the real tokenizer (untagged), finds the first token equal to a word and extracts its features.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="target">The target word.</param>
    /// <returns>The token texts and the hashes of the target.</returns>
    private static (List<string> Words, (string Conjoined, string Shared) Hex) DescribeWithTokenizer(string text, string target)
    {
        var sentences = PartOfSpeechAdapter.Shared.Analyze(text, tag: false, TestContext.Current.CancellationToken);
        var words = new List<string>();
        var context = new List<ContextToken>();
        var targetIndex = -1;
        foreach (var token in sentences.SelectMany(sentence => sentence))
        {
            var word = text.Substring(token.Start, token.Length);
            if (targetIndex < 0 && word == target)
            {
                targetIndex = context.Count;
            }

            words.Add(word);
            context.Add(HeteronymContext.Create(text, token.Start, token.Length, null, 0));
        }

        Assert.True(targetIndex >= 0, "The target word was not found.");
        return (words, ExtractHex([.. context], targetIndex));
    }

    /// <summary>
    ///     One row of the golden vectors file.
    /// </summary>
    /// <param name="Id">The case name.</param>
    /// <param name="Tokens">The context tokens.</param>
    /// <param name="Target">The target index.</param>
    /// <param name="Conjoined">The expected conjoined hashes.</param>
    /// <param name="Shared">The expected shared hashes.</param>
    private sealed record GoldenRow(string Id, ContextToken[] Tokens, int Target, string Conjoined, string Shared);
}
