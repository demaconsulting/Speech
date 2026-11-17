using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.AnalysisSubsystem;

/// <summary>
///     Unit tests for <see cref="EnglishTextAnalyzerOptions"/> and
///     <see cref="TextEnAnnotationSelection"/>.
/// </summary>
public sealed class EnglishTextAnalyzerOptionsTests
{
    /// <summary>Proves the defaults select part-of-speech, normalization and reading annotations, no diagnostics sink and a 0.60 low-confidence threshold.</summary>
    [Fact]
    public void EnglishTextAnalyzerOptions_Defaults_SelectAllFamiliesAndNoSink()
    {
        // Arrange / Act
        var options = new EnglishTextAnalyzerOptions();

        // Assert
        Assert.Equal(TextEnAnnotationSelection.PartOfSpeech | TextEnAnnotationSelection.Normalization | TextEnAnnotationSelection.Reading, options.Annotations);
        Assert.Null(options.Diagnostics);
        Assert.Equal(0.60, options.LowConfidenceThreshold);
    }

    /// <summary>Proves the selection and the sink can be set at initialization, including by a with-expression, and the original is unchanged.</summary>
    [Fact]
    public void EnglishTextAnalyzerOptions_Initialization_SetsValuesAndWithExpressionCopies()
    {
        // Arrange
        var sink = new RecordingDiagnostics();
        var original = new EnglishTextAnalyzerOptions { Diagnostics = sink };

        // Act
        var changed = original with { Annotations = TextEnAnnotationSelection.None };

        // Assert
        Assert.Same(sink, changed.Diagnostics);
        Assert.Equal(TextEnAnnotationSelection.None, changed.Annotations);
        Assert.Equal(TextEnAnnotationSelection.PartOfSpeech | TextEnAnnotationSelection.Normalization | TextEnAnnotationSelection.Reading, original.Annotations);
    }

    /// <summary>Proves every combination of the defined flags is accepted, including each family alone.</summary>
    /// <param name="selection">The selection.</param>
    [Theory]
    [InlineData(TextEnAnnotationSelection.None)]
    [InlineData(TextEnAnnotationSelection.PartOfSpeech)]
    [InlineData(TextEnAnnotationSelection.Normalization)]
    [InlineData(TextEnAnnotationSelection.Reading)]
    [InlineData(TextEnAnnotationSelection.PartOfSpeech | TextEnAnnotationSelection.Normalization)]
    [InlineData(TextEnAnnotationSelection.PartOfSpeech | TextEnAnnotationSelection.Reading)]
    [InlineData(TextEnAnnotationSelection.Normalization | TextEnAnnotationSelection.Reading)]
    [InlineData(TextEnAnnotationSelection.PartOfSpeech | TextEnAnnotationSelection.Normalization | TextEnAnnotationSelection.Reading)]
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

    /// <summary>Proves the selection enumeration is a flags enumeration with None as zero.</summary>
    [Fact]
    public void TextEnAnnotationSelection_Members_AreFlagsWithNoneZero()
    {
        // Assert
        Assert.Equal(0, (int)TextEnAnnotationSelection.None);
        Assert.Equal(1, (int)TextEnAnnotationSelection.PartOfSpeech);
        Assert.Equal(2, (int)TextEnAnnotationSelection.Normalization);
        Assert.Equal(4, (int)TextEnAnnotationSelection.Reading);
        Assert.True(typeof(TextEnAnnotationSelection).IsDefined(typeof(FlagsAttribute), false));
    }

    /// <summary>Proves the low-confidence threshold accepts every finite value from 0 to 1, keeps it through a with-expression and re-validates it there.</summary>
    /// <param name="threshold">The threshold.</param>
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.6)]
    [InlineData(0.999)]
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

    /// <summary>Proves a threshold that is negative, above 1, NaN or infinite is rejected, at initialization and in a with-expression.</summary>
    /// <param name="threshold">The invalid threshold.</param>
    [Theory]
    [InlineData(-0.001)]
    [InlineData(1.001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void EnglishTextAnalyzerOptions_LowConfidenceThreshold_OutOfRange_ThrowsArgumentOutOfRangeException(double threshold)
    {
        // Arrange
        var options = new EnglishTextAnalyzerOptions();

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnglishTextAnalyzerOptions { LowConfidenceThreshold = threshold });
        Assert.Throws<ArgumentOutOfRangeException>(() => options with { LowConfidenceThreshold = threshold });
    }

    /// <summary>Proves the options default to no overrides, keep the dictionary reference through a with-expression and a copy, and let a later copy replace it without changing the original.</summary>
    [Fact]
    public void EnglishTextAnalyzerOptions_Overrides_DefaultsToNullAndCopiesWithWith()
    {
        // Arrange
        var overrides = PronunciationOverrides.Create([PronunciationOverrideEntry.Skip("word")]);

        // Act
        var defaults = new EnglishTextAnalyzerOptions();
        var original = new EnglishTextAnalyzerOptions { Overrides = overrides };
        var copy = original with { Annotations = TextEnAnnotationSelection.Normalization };
        var replaced = original with { Overrides = null };

        // Assert
        Assert.Null(defaults.Overrides);
        Assert.Same(overrides, original.Overrides);
        Assert.Same(overrides, copy.Overrides);
        Assert.Null(replaced.Overrides);
        Assert.Same(overrides, original.Overrides);
    }
}
