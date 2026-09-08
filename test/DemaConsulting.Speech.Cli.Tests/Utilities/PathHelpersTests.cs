// Copyright (c) DEMA Consulting
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using DemaConsulting.Speech.Cli.Utilities;

namespace DemaConsulting.Speech.Cli.Tests.Utilities;

/// <summary>
///     Unit tests for <see cref="PathHelpers.SafePathCombine"/>, proving the containment boundary
///     check and the returned path stay consistent with each other.
/// </summary>
public sealed class PathHelpersTests
{
    /// <summary>
    ///     Validates that a relative base path yields a fully-qualified absolute result, so a
    ///     caller cannot be handed back a path that resolves against a different working
    ///     directory than the one the containment check just validated.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_RelativeBasePath_ReturnsAbsolutePath()
    {
        // Arrange: a relative base path and a relative child path
        const string basePath = "relative-base";
        const string relativePath = "child.txt";

        // Act: combine the paths
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: the returned path is the fully-qualified absolute equivalent, matching what
        // the containment check itself validated, not the unnormalized relative combination
        var expected = Path.GetFullPath(Path.Combine(basePath, relativePath));
        Assert.Equal(expected, result);
        Assert.True(Path.IsPathRooted(result));
    }

    /// <summary>
    ///     Validates that an absolute base path also yields a fully-qualified absolute result.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_AbsoluteBasePath_ReturnsAbsolutePath()
    {
        // Arrange: an absolute base path and a relative child path
        var basePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "safe-path-combine-tests"));
        const string relativePath = "child.txt";

        // Act: combine the paths
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: the returned path is the fully-qualified absolute equivalent
        var expected = Path.GetFullPath(Path.Combine(basePath, relativePath));
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Validates that a relative path attempting to escape the base directory still throws,
    ///     proving the returned-path fix did not weaken the containment check itself.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_EscapingRelativePath_ThrowsArgumentException()
    {
        // Arrange: a base path and a relative path that escapes it
        const string basePath = "relative-base";
        const string relativePath = "../outside.txt";

        // Act / Assert: the escape attempt is rejected
        Assert.Throws<ArgumentException>(() => PathHelpers.SafePathCombine(basePath, relativePath));
    }
}
