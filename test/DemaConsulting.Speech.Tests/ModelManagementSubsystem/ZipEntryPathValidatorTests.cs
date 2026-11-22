using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Tests.ModelManagementSubsystem;

/// <summary>
///     Unit tests for the internal <c>ZipEntryPathValidator</c>, the shared zip-slip and name-portability guard.
/// </summary>
public sealed class ZipEntryPathValidatorTests
{
    /// <summary>
    ///     Proves that every name that could escape the extraction directory is rejected: parent and current-directory
    ///     segments, a rooted name, a backslash, a drive or stream colon and a NUL.
    /// </summary>
    /// <param name="entryName">The unsafe entry name.</param>
    [Theory]
    [InlineData("../x")]
    [InlineData("a/../../x")]
    [InlineData("./x")]
    [InlineData("a/./x")]
    [InlineData("/abs")]
    [InlineData("a\\b")]
    [InlineData("C:x")]
    [InlineData("a/b:stream")]
    [InlineData("a\0b")]
    public void ZipEntryPathValidator_SplitSafely_UnsafeName_ThrowsInvalidDataException(string entryName)
    {
        // Act
        var exception = Assert.Throws<InvalidDataException>(() => ZipEntryPathValidator.SplitSafely(entryName));

        // Assert: the message names the offending entry so a failed install can be diagnosed
        Assert.Contains("unsafe path", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a safe name splits into its non-empty segments, dropping the empty segment a directory entry's
    ///     trailing slash produces.
    /// </summary>
    [Fact]
    public void ZipEntryPathValidator_SplitSafely_SafeName_ReturnsSegments()
    {
        // Act
        var file = ZipEntryPathValidator.SplitSafely("onnx/model.onnx");
        var directory = ZipEntryPathValidator.SplitSafely("onnx/");

        // Assert
        Assert.Equal(["onnx", "model.onnx"], file);
        Assert.Equal(["onnx"], directory);
    }

    /// <summary>
    ///     Proves that a segment that cannot be created on every platform is rejected: invalid characters, a control
    ///     character, a trailing dot or space, and Windows reserved device names with or without an extension.
    /// </summary>
    /// <param name="entryName">The non-portable entry name.</param>
    [Theory]
    [InlineData("a/bad*name")]
    [InlineData("what?")]
    [InlineData("a<b>")]
    [InlineData("pipe|d")]
    [InlineData("quo\"te")]
    [InlineData("ctl\u0001x")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    [InlineData("CON")]
    [InlineData("voices/nul.bin")]
    [InlineData("lpt1.txt")]
    [InlineData("com0")]
    public void ZipEntryPathValidator_Validate_NonPortableSegment_ThrowsInvalidDataException(string entryName)
    {
        // Act
        var exception = Assert.Throws<InvalidDataException>(() => ZipEntryPathValidator.Validate(entryName));

        // Assert
        Assert.Contains("not valid on every platform", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that a path inside the root resolves to its canonical full path beneath the root.
    /// </summary>
    [Fact]
    public void ZipEntryPathValidator_ResolveUnderRoot_PathInsideRoot_ReturnsFullPath()
    {
        // Arrange
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Join(Path.GetTempPath(), "zip-root")));

        // Act
        var destination = ZipEntryPathValidator.ResolveUnderRoot(root, Path.Combine("onnx", "model.onnx"), "onnx/model.onnx");

        // Assert
        Assert.Equal(Path.Combine(root, "onnx", "model.onnx"), destination);
    }

    /// <summary>
    ///     Proves that a destination outside the root, a sibling directory sharing the root's name as a prefix, or the
    ///     root itself is refused, independently of the name checks.
    /// </summary>
    /// <param name="relativePath">The relative destination, using <c>/</c> as the separator.</param>
    [Theory]
    [InlineData("../outside.bin")]
    [InlineData("../zip-root-evil/x.bin")]
    [InlineData(".")]
    public void ZipEntryPathValidator_ResolveUnderRoot_PathOutsideRoot_ThrowsInvalidDataException(string relativePath)
    {
        // Arrange
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Join(Path.GetTempPath(), "zip-root")));

        // Act
        var exception = Assert.Throws<InvalidDataException>(() =>
            ZipEntryPathValidator.ResolveUnderRoot(root, relativePath.Replace('/', Path.DirectorySeparatorChar), relativePath));

        // Assert
        Assert.Contains("outside the install directory", exception.Message, StringComparison.Ordinal);
    }
}
