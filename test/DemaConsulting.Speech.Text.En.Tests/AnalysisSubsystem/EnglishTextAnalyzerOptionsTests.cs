using DemaConsulting.Speech.Text.En.AnalysisSubsystem;
using DemaConsulting.Speech.Text.En.Tests.Fakes;

namespace DemaConsulting.Speech.Text.En.Tests.AnalysisSubsystem;

/// <summary>
///     Unit tests for <see cref="EnglishTextAnalyzerOptions"/> and
///     <see cref="TextEnAnnotationSelection"/>.
/// </summary>
public sealed class EnglishTextAnalyzerOptionsTests
{
    /// <summary>Proves the defaults select part-of-speech annotations and no diagnostics sink.</summary>
    [Fact]
    public void EnglishTextAnalyzerOptions_Defaults_SelectPartOfSpeechAndNoSink()
    {
        // Arrange / Act
        var options = new EnglishTextAnalyzerOptions();

        // Assert
        Assert.Equal(TextEnAnnotationSelection.PartOfSpeech, options.Annotations);
        Assert.Null(options.Diagnostics);
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
        Assert.Equal(TextEnAnnotationSelection.PartOfSpeech, original.Annotations);
    }

    /// <summary>Proves a selection containing an undefined flag is rejected, at initialization and in a with-expression.</summary>
    [Fact]
    public void EnglishTextAnalyzerOptions_Annotations_UndefinedFlag_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var options = new EnglishTextAnalyzerOptions();

        // Act / Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new EnglishTextAnalyzerOptions { Annotations = (TextEnAnnotationSelection)0x80 });
        Assert.Throws<ArgumentOutOfRangeException>(() => options with { Annotations = TextEnAnnotationSelection.PartOfSpeech | (TextEnAnnotationSelection)2 });
    }

    /// <summary>Proves the selection enumeration is a flags enumeration with None as zero.</summary>
    [Fact]
    public void TextEnAnnotationSelection_Members_AreFlagsWithNoneZero()
    {
        // Assert
        Assert.Equal(0, (int)TextEnAnnotationSelection.None);
        Assert.Equal(1, (int)TextEnAnnotationSelection.PartOfSpeech);
        Assert.True(typeof(TextEnAnnotationSelection).IsDefined(typeof(FlagsAttribute), false));
    }
}
