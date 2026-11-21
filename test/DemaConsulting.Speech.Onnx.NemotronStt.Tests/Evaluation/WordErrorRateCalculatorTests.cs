using DemaConsulting.Speech.SttEvaluation;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.Evaluation;

/// <summary>
///     Unit tests for <see cref="WordErrorRateCalculator"/>, proving the normalization,
///     edit-distance and alignment math is correct against simple, hand-computed cases -
///     independent of any recognition model, so a WER discrepancy in an accuracy test or the
///     evaluation tool can always be attributed to the model rather than to this calculation.
/// </summary>
public class WordErrorRateCalculatorTests
{
    /// <summary>
    ///     Proves that an exact transcript match scores zero error, the baseline every other case
    ///     is measured against.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Compute_IdenticalStrings_ReturnsZero()
    {
        // Arrange
        const string reference = "the quick brown fox jumps over the lazy dog";

        // Act
        var wer = WordErrorRateCalculator.Compute(reference, reference);

        // Assert: a perfect match has no edit-distance errors
        Assert.Equal(0.0, wer);
    }

    /// <summary>
    ///     Proves that one substituted word in a ten-word reference scores exactly 10% WER
    ///     (1 error / 10 reference words), the textbook single-substitution case.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Compute_OneSubstitutionInTenWords_ReturnsTenPercent()
    {
        // Arrange: "cat" replaces "dog" - one substitution, nine words unchanged
        const string reference = "one two three four five six seven eight nine dog";
        const string hypothesis = "one two three four five six seven eight nine cat";

        // Act
        var wer = WordErrorRateCalculator.Compute(reference, hypothesis);

        // Assert: 1 substitution / 10 reference words = 0.10
        Assert.Equal(0.10, wer, precision: 6);
    }

    /// <summary>
    ///     Proves that a missing (deleted) word is counted as exactly one error, distinct from a
    ///     substitution.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Compute_OneDeletedWord_CountsOneError()
    {
        // Arrange: the hypothesis is missing "brown"
        const string reference = "the quick brown fox";
        const string hypothesis = "the quick fox";

        // Act
        var wer = WordErrorRateCalculator.Compute(reference, hypothesis);

        // Assert: 1 deletion / 4 reference words = 0.25
        Assert.Equal(0.25, wer, precision: 6);
    }

    /// <summary>
    ///     Proves that an extra (inserted) word not present in the reference is counted as
    ///     exactly one error.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Compute_OneInsertedWord_CountsOneError()
    {
        // Arrange: the hypothesis adds an extra "very"
        const string reference = "the quick fox";
        const string hypothesis = "the very quick fox";

        // Act
        var wer = WordErrorRateCalculator.Compute(reference, hypothesis);

        // Assert: 1 insertion / 3 reference words = 0.3333...
        Assert.Equal(1.0 / 3.0, wer, precision: 6);
    }

    /// <summary>
    ///     Proves that casing and punctuation differences alone never count as errors, since
    ///     normalization must strip both before comparison.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Compute_DifferingCaseAndPunctuation_ReturnsZero()
    {
        // Arrange: identical words, but the hypothesis differs only in casing/punctuation
        const string reference = "Sunset and evening star, and one clear call for me!";
        const string hypothesis = "sunset AND evening star and one clear call for me";

        // Act
        var wer = WordErrorRateCalculator.Compute(reference, hypothesis);

        // Assert: no genuine word errors once casing/punctuation are normalized away
        Assert.Equal(0.0, wer);
    }

    /// <summary>
    ///     Proves that a reference which normalizes to zero words is treated as a perfect match
    ///     only when the hypothesis also normalizes to zero words, avoiding an undefined
    ///     division-by-zero WER.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Compute_EmptyReferenceAndEmptyHypothesis_ReturnsZero()
    {
        // Arrange & Act
        var wer = WordErrorRateCalculator.Compute(string.Empty, string.Empty);

        // Assert
        Assert.Equal(0.0, wer);
    }

    /// <summary>
    ///     Proves that a reference which normalizes to zero words but a non-empty hypothesis
    ///     scores the maximum error (1.0), rather than throwing or dividing by zero.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Compute_EmptyReferenceNonEmptyHypothesis_ReturnsOne()
    {
        // Arrange & Act
        var wer = WordErrorRateCalculator.Compute(string.Empty, "unexpected words");

        // Assert
        Assert.Equal(1.0, wer);
    }

    /// <summary>
    ///     Proves that completely disjoint transcripts of equal length score 100% WER - every
    ///     reference word must be substituted.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Compute_CompletelyDifferentWordsSameLength_ReturnsOneHundredPercent()
    {
        // Arrange
        const string reference = "alpha beta gamma";
        const string hypothesis = "delta epsilon zeta";

        // Act
        var wer = WordErrorRateCalculator.Compute(reference, hypothesis);

        // Assert: 3 substitutions / 3 reference words = 1.0
        Assert.Equal(1.0, wer, precision: 6);
    }

    /// <summary>
    ///     Proves that <see cref="WordErrorRateCalculator.NormalizeAndSplit"/> lower-cases,
    ///     strips punctuation, and collapses whitespace into discrete word tokens.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_NormalizeAndSplit_MixedCasePunctuationAndWhitespace_ProducesCleanWords()
    {
        // Arrange
        const string text = "Crossing the Bar,\n\nWhen I have crost the bar.";

        // Act
        var words = WordErrorRateCalculator.NormalizeAndSplit(text);

        // Assert
        Assert.Equal(["crossing", "the", "bar", "when", "i", "have", "crost", "the", "bar"], words);
    }

    /// <summary>
    ///     Proves that an apostrophe inside a word (straight or typographic) is kept, so a
    ///     contraction or possessive stays one word as the LibriSpeech transcripts write it, while
    ///     a quotation mark at a word edge is dropped.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_NormalizeAndSplit_ApostrophesInsideAndAroundWords_KeepsOnlyWordInternalOnes()
    {
        // Arrange: a straight and a typographic contraction, and single quotation marks
        const string text = "POLLY COULDN'T speak, she said \u2018it\u2019s Luther\u2019s\u2019 'quote'";

        // Act
        var words = WordErrorRateCalculator.NormalizeAndSplit(text);

        // Assert
        Assert.Equal(["polly", "couldn't", "speak", "she", "said", "it's", "luther's", "quote"], words);
    }

    /// <summary>
    ///     Proves that the verbatim split keeps casing and punctuation, so the punctuated word
    ///     error rate counts a punctuation difference as an error.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_SplitVerbatim_PunctuatedText_KeepsCasingAndPunctuation()
    {
        // Arrange
        const string text = "Sunset and evening star,\nAnd one";

        // Act
        var words = WordErrorRateCalculator.SplitVerbatim(text);

        // Assert
        Assert.Equal(["Sunset", "and", "evening", "star,", "And", "one"], words);
    }

    /// <summary>
    ///     Proves that a null reference or hypothesis is rejected rather than producing a
    ///     misleading result, since a null transcript is a programming error, not a legitimate
    ///     "silence" case (which is instead represented by an empty string).
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Compute_NullArguments_ThrowsArgumentNullException()
    {
        // Arrange & Act / Assert
        Assert.Throws<ArgumentNullException>(() => WordErrorRateCalculator.Compute(null!, "text"));
        Assert.Throws<ArgumentNullException>(() => WordErrorRateCalculator.Compute("text", null!));
    }

    /// <summary>
    ///     Proves that words added after the end of the reference are counted as invented words
    ///     (and as insertions), the measure of a tail hallucination.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Align_TrailingExtraWords_CountsInventedWords()
    {
        // Arrange: the recognizer added three words after the last spoken word
        const string reference = "when I have crost the bar";
        const string hypothesis = "when I have crossed the bar I am she";

        // Act
        var alignment = WordErrorRateCalculator.Align(reference, hypothesis);

        // Assert: one substitution, three insertions, all three invented
        Assert.Equal(6, alignment.ReferenceWords);
        Assert.Equal(1, alignment.Substitutions);
        Assert.Equal(0, alignment.Deletions);
        Assert.Equal(3, alignment.Insertions);
        Assert.Equal(3, alignment.InventedWords);
        Assert.Equal(4.0 / 6.0, alignment.WordErrorRate, precision: 6);
    }

    /// <summary>
    ///     Proves that a word inserted before the last reference word is an insertion but not an
    ///     invented word, because speech still follows it.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Align_InsertionBeforeLastWord_IsNotInvented()
    {
        // Arrange: "very" is inserted in the middle
        const string reference = "the quick fox";
        const string hypothesis = "the very quick fox";

        // Act
        var alignment = WordErrorRateCalculator.Align(reference, hypothesis);

        // Assert
        Assert.Equal(1, alignment.Insertions);
        Assert.Equal(0, alignment.InventedWords);
    }

    /// <summary>
    ///     Proves that a repeated last word is placed at the end of the alignment and therefore
    ///     counted as invented, rather than hidden before the last reference word.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Align_RepeatedLastWord_CountsRepetitionAsInvented()
    {
        // Arrange
        const string reference = "call for me";
        const string hypothesis = "call for me me";

        // Act
        var alignment = WordErrorRateCalculator.Align(reference, hypothesis);

        // Assert
        Assert.Equal(1, alignment.Insertions);
        Assert.Equal(1, alignment.InventedWords);
    }

    /// <summary>
    ///     Proves that an empty hypothesis counts every reference word as a deletion and invents
    ///     nothing.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Align_EmptyHypothesis_CountsDeletions()
    {
        // Arrange & Act
        var alignment = WordErrorRateCalculator.Align("the quick brown fox", string.Empty);

        // Assert
        Assert.Equal(4, alignment.Deletions);
        Assert.Equal(0, alignment.Substitutions);
        Assert.Equal(0, alignment.Insertions);
        Assert.Equal(0, alignment.InventedWords);
        Assert.Equal(1.0, alignment.WordErrorRate);
    }

    /// <summary>
    ///     Proves that summing per-file alignments yields the corpus word error rate (total errors
    ///     over total reference words), not the mean of the per-file rates.
    /// </summary>
    [Fact]
    public void WordErrorRateCalculator_Align_SummedAlignments_GiveCorpusWordErrorRate()
    {
        // Arrange: one error in 2 words and no error in 8 words
        var first = WordErrorRateCalculator.Align("one two", "one too");
        var second = WordErrorRateCalculator.Align("a b c d e f g h", "a b c d e f g h");

        // Act
        var corpus = WordAlignment.Empty.Add(first).Add(second);

        // Assert: 1 / 10, not the mean (0.5 + 0) / 2
        Assert.Equal(10, corpus.ReferenceWords);
        Assert.Equal(0.1, corpus.WordErrorRate, precision: 6);
    }
}
