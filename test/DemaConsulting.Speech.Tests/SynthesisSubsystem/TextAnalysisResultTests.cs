using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="TextAnalysisResult"/>.
/// </summary>
public class TextAnalysisResultTests
{
    /// <summary>
    ///     Proves the shared empty result carries no annotations and no sentence opinion.
    /// </summary>
    [Fact]
    public void TextAnalysisResult_Empty_HasNoAnnotationsAndNoBreaks()
    {
        // Act
        var result = TextAnalysisResult.Empty;

        // Assert
        Assert.Empty(result.Annotations);
        Assert.Null(result.SentenceBreaks);
    }

    /// <summary>
    ///     Proves a null annotation list is rejected.
    /// </summary>
    [Fact]
    public void TextAnalysisResult_Constructor_NullAnnotations_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TextAnalysisResult(null!, null));
    }

    /// <summary>
    ///     Proves a null element inside the annotation list is rejected.
    /// </summary>
    [Fact]
    public void TextAnalysisResult_Constructor_NullAnnotationElement_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new TextAnalysisResult([null!], null));
    }

    /// <summary>
    ///     Proves a negative sentence break is rejected.
    /// </summary>
    [Fact]
    public void TextAnalysisResult_Constructor_NegativeBreak_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextAnalysisResult([], [3, -1]));
    }

    /// <summary>
    ///     Proves the result keeps private copies, so changing the caller's lists later does not
    ///     change the result.
    /// </summary>
    [Fact]
    public void TextAnalysisResult_Constructor_CallerListsChangedLater_ResultIsUnaffected()
    {
        // Arrange
        var annotations = new List<SegmentAnnotation> { new(0, 2, "k", "v") };
        var breaks = new List<int> { 5 };

        // Act
        var result = new TextAnalysisResult(annotations, breaks);
        annotations.Clear();
        breaks.Add(9);

        // Assert
        Assert.Single(result.Annotations);
        Assert.Equal([5], result.SentenceBreaks);
    }

    /// <summary>
    ///     Proves sentence breaks are stored sorted and de-duplicated.
    /// </summary>
    [Fact]
    public void TextAnalysisResult_Constructor_UnsortedDuplicateBreaks_AreSortedAndDistinct()
    {
        // Act
        var result = new TextAnalysisResult([], [9, 3, 9, 0, 3]);

        // Assert
        Assert.Equal([0, 3, 9], result.SentenceBreaks);
    }

    /// <summary>
    ///     Proves a null break list stays null (no sentence opinion) while an empty list stays
    ///     non-null (authoritative "no splits").
    /// </summary>
    [Fact]
    public void TextAnalysisResult_Constructor_NullVersusEmptyBreaks_AreDistinguished()
    {
        // Act
        var none = new TextAnalysisResult([], null);
        var empty = new TextAnalysisResult([], []);

        // Assert
        Assert.Null(none.SentenceBreaks);
        Assert.NotNull(empty.SentenceBreaks);
        Assert.Empty(empty.SentenceBreaks);
    }

    /// <summary>
    ///     Proves validation also runs for <c>with</c> expressions.
    /// </summary>
    [Fact]
    public void TextAnalysisResult_With_InvalidValues_Throws()
    {
        // Arrange
        var result = TextAnalysisResult.Empty;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => result with { Annotations = null! });
        Assert.Throws<ArgumentOutOfRangeException>(() => result with { SentenceBreaks = [-2] });
    }
}
