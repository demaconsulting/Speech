using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Tests.SynthesisSubsystem;

/// <summary>
///     Unit tests for <see cref="SegmentAnnotation"/>.
/// </summary>
public class SegmentAnnotationTests
{
    /// <summary>
    ///     Proves a valid annotation exposes its values and an exclusive end index.
    /// </summary>
    [Fact]
    public void SegmentAnnotation_Constructor_ValidValues_ExposesValuesAndEnd()
    {
        // Act
        var annotation = new SegmentAnnotation(3, 4, "reading", "r-e-d");

        // Assert
        Assert.Equal(3, annotation.Start);
        Assert.Equal(4, annotation.Length);
        Assert.Equal("reading", annotation.Kind);
        Assert.Equal("r-e-d", annotation.Value);
        Assert.Equal(7, annotation.End);
    }

    /// <summary>
    ///     Proves a negative start is rejected.
    /// </summary>
    [Fact]
    public void SegmentAnnotation_Constructor_NegativeStart_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentAnnotation(-1, 1, "k", "v"));
    }

    /// <summary>
    ///     Proves a zero or negative length is rejected.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void SegmentAnnotation_Constructor_NonPositiveLength_ThrowsArgumentOutOfRangeException(int length)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentAnnotation(0, length, "k", "v"));
    }

    /// <summary>
    ///     Proves a range whose end would overflow an <see cref="int"/> is rejected.
    /// </summary>
    [Fact]
    public void SegmentAnnotation_Constructor_RangeEndOverflow_ThrowsArgumentOutOfRangeException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentAnnotation(int.MaxValue, 1, "k", "v"));
    }

    /// <summary>
    ///     Proves a null, empty or whitespace kind is rejected.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SegmentAnnotation_Constructor_BlankKind_Throws(string? kind)
    {
        // Act & Assert
        Assert.ThrowsAny<ArgumentException>(() => new SegmentAnnotation(0, 1, kind!, "v"));
    }

    /// <summary>
    ///     Proves a null value is rejected.
    /// </summary>
    [Fact]
    public void SegmentAnnotation_Constructor_NullValue_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SegmentAnnotation(0, 1, "k", null!));
    }

    /// <summary>
    ///     Proves an empty value is accepted, because the payload is opaque and may be empty.
    /// </summary>
    [Fact]
    public void SegmentAnnotation_Constructor_EmptyValue_IsAccepted()
    {
        // Act
        var annotation = new SegmentAnnotation(0, 1, "k", string.Empty);

        // Assert
        Assert.Equal(string.Empty, annotation.Value);
    }

    /// <summary>
    ///     Proves validation also runs for <c>with</c> expressions, not only the constructor.
    /// </summary>
    [Fact]
    public void SegmentAnnotation_With_InvalidStart_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var annotation = new SegmentAnnotation(2, 3, "k", "v");

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => annotation with { Start = -1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => annotation with { Length = 0 });
        Assert.Throws<ArgumentNullException>(() => annotation with { Value = null! });
        Assert.ThrowsAny<ArgumentException>(() => annotation with { Kind = " " });
        Assert.Equal(6, (annotation with { Start = 3 }).End);
    }
}
