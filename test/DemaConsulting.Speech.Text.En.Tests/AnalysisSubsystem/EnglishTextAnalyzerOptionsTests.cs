using DemaConsulting.Speech.Text.En.AnalysisSubsystem;

namespace DemaConsulting.Speech.Text.En.Tests.AnalysisSubsystem;

/// <summary>
///     Unit tests for <see cref="EnglishTextAnalyzerOptions"/> and
///     <see cref="TextEnAnnotationSelection"/>.
/// </summary>
public sealed class EnglishTextAnalyzerOptionsTests
{
    /// <summary>Proves the defaults select part-of-speech, normalization and reading annotations, no diagnostics sink, no overrides and a 0.60 low-confidence threshold.</summary>
    [Fact]
    public void EnglishTextAnalyzerOptions_Defaults_SelectAllFamiliesAndNoSink()
    {
        // Arrange / Act
        var options = new EnglishTextAnalyzerOptions();

        // Assert
        Assert.Equal(TextEnAnnotationSelection.PartOfSpeech | TextEnAnnotationSelection.Normalization | TextEnAnnotationSelection.Reading, options.Annotations);
        Assert.Null(options.Diagnostics);
        Assert.Null(options.Overrides);
        Assert.Equal(0.60, options.LowConfidenceThreshold);
    }

    /// <summary>Proves the defined flag combinations at the extremes (nothing, a family pair) are accepted.</summary>
    /// <param name="selection">The selection.</param>
    [Theory]
    [InlineData(TextEnAnnotationSelection.None)]
    [InlineData(TextEnAnnotationSelection.Normalization | TextEnAnnotationSelection.Reading)]
    public void EnglishTextAnalyzerOptions_Annotations_DefinedFlagCombinations_AreAccepted(TextEnAnnotationSelection selection)
    {
        // Act
        var options = new EnglishTextAnalyzerOptions { Annotations = selection };

        // Assert
        Assert.Equal(selection, options.Annotations);
    }

    /// <summary>Proves a selection containing an undefined flag is rejected, at initialization and in a with-expression.</summary>
    [Fact]
    public void EnglishTextAnalyzerOptions_Annotations_UndefinedFlag_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new EnglishTextAnalyzerOptions();

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnglishTextAnalyzerOptions { Annotations = (TextEnAnnotationSelection)0x80 });
        Assert.Throws<ArgumentOutOfRangeException>(() => options with { Annotations = TextEnAnnotationSelection.PartOfSpeech | (TextEnAnnotationSelection)8 });
    }

    /// <summary>Proves the low-confidence threshold accepts the boundary values 0 and 1, keeps them through a with-expression and re-validates the value there.</summary>
    /// <param name="threshold">The threshold.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void EnglishTextAnalyzerOptions_LowConfidenceThreshold_ValuesFromZeroToOne_AreAcceptedAndCopied(double threshold)
    {
        // Act
        var options = new EnglishTextAnalyzerOptions { LowConfidenceThreshold = threshold };
        var copy = options with { Annotations = TextEnAnnotationSelection.Reading };

        // Assert
        Assert.Equal(threshold, options.LowConfidenceThreshold);
        Assert.Equal(threshold, copy.LowConfidenceThreshold);
    }

    /// <summary>Proves a threshold that is negative, above 1 or NaN is rejected, at initialization and in a with-expression.</summary>
    /// <param name="threshold">The invalid threshold.</param>
    [Theory]
    [InlineData(-0.001)]
    [InlineData(1.001)]
    [InlineData(double.NaN)]
    public void EnglishTextAnalyzerOptions_LowConfidenceThreshold_OutOfRange_ThrowsArgumentOutOfRangeException(double threshold)
    {
        // Arrange
        var options = new EnglishTextAnalyzerOptions();

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnglishTextAnalyzerOptions { LowConfidenceThreshold = threshold });
        Assert.Throws<ArgumentOutOfRangeException>(() => options with { LowConfidenceThreshold = threshold });
    }
}
