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
        var expected = Path.GetFullPath(Path.Join(basePath, relativePath));
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
        var basePath = Path.GetFullPath(Path.Join(Path.GetTempPath(), "safe-path-combine-tests"));
        const string relativePath = "child.txt";

        // Act: combine the paths
        var result = PathHelpers.SafePathCombine(basePath, relativePath);

        // Assert: the returned path is the fully-qualified absolute equivalent
        var expected = Path.GetFullPath(Path.Join(basePath, relativePath));
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

    /// <summary>
    ///     Validates that a rooted relative path is rejected, because it would replace the base
    ///     directory entirely.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_RootedRelativePath_ThrowsArgumentException()
    {
        // Arrange: a base path and a rooted "relative" path outside it
        var basePath = Path.GetFullPath(Path.Join(Path.GetTempPath(), "safe-path-combine-tests"));
        var rootedPath = Path.GetFullPath(Path.Join(Path.GetTempPath(), "elsewhere", "outside.txt"));

        // Act / Assert: the rooted path is rejected
        Assert.Throws<ArgumentException>(() => PathHelpers.SafePathCombine(basePath, rootedPath));
    }

    /// <summary>
    ///     Validates that a rooted path that lies inside the base directory is also rejected
    ///     (regression: it was previously accepted because it passed the containment check).
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_RootedPathInsideBase_ThrowsArgumentException()
    {
        // Arrange: a rooted path that is inside the base directory
        var basePath = Path.GetFullPath(Path.Join(Path.GetTempPath(), "safe-path-combine-tests"));
        var rootedInside = Path.Join(basePath, "child.txt");

        // Act
        var exception = Assert.Throws<ArgumentException>(() => PathHelpers.SafePathCombine(basePath, rootedInside));

        // Assert: the offending parameter is identified
        Assert.Equal("relativePath", exception.ParamName);
    }

    /// <summary>
    ///     Validates that a path passing through an existing directory symbolic link is rejected,
    ///     even though its lexical form lies inside the base directory.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_DirectorySymbolicLinkComponent_ThrowsArgumentException()
    {
        // Arrange: a base directory containing a symbolic link that points outside the base
        var root = Path.Join(Path.GetTempPath(), "safe-path-link-" + Guid.NewGuid().ToString("N"));
        var basePath = Path.Join(root, "base");
        var outside = Path.Join(root, "outside");
        Directory.CreateDirectory(basePath);
        Directory.CreateDirectory(outside);
        try
        {
            var linkPath = Path.Join(basePath, "link");
            if (!TryCreateDirectoryLink(linkPath, outside))
            {
                Assert.Skip("Symbolic links cannot be created in this environment.");
            }

            // Act / Assert: the contained-looking path through the link is rejected, with the
            // offending parameter identified
            var exception = Assert.Throws<ArgumentException>(
                () => PathHelpers.SafePathCombine(basePath, Path.Join("link", "file.txt")));
            Assert.Equal("relativePath", exception.ParamName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that a Windows directory junction (creatable without elevated privilege) is rejected.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_JunctionComponent_ThrowsArgumentException()
    {
        // Arrange: only Windows has junctions
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Junctions exist only on Windows.");
        var root = Path.Join(Path.GetTempPath(), "safe-path-junction-" + Guid.NewGuid().ToString("N"));
        var basePath = Path.Join(root, "base");
        var outside = Path.Join(root, "outside");
        Directory.CreateDirectory(basePath);
        Directory.CreateDirectory(outside);
        try
        {
            var junction = Path.Join(basePath, "junction");
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                "cmd.exe", $"/c mklink /J \"{junction}\" \"{outside}\"")
            {
                CreateNoWindow = true,
                RedirectStandardOutput = true,
            })!;
            process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            Assert.SkipUnless(Directory.Exists(junction), "The junction could not be created.");

            // Act / Assert: the path through the junction is rejected
            Assert.Throws<ArgumentException>(() => PathHelpers.SafePathCombine(basePath, Path.Join("junction", "file.txt")));
        }
        finally
        {
            // Remove the junction itself first so the recursive delete never follows it
            var junctionPath = Path.Join(basePath, "junction");
            if (Directory.Exists(junctionPath))
            {
                Directory.Delete(junctionPath);
            }

            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that an existing file symbolic link component is rejected.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_FileSymbolicLinkComponent_ThrowsArgumentException()
    {
        // Arrange: a base directory containing a file symbolic link to a file outside the base
        var root = Path.Join(Path.GetTempPath(), "safe-path-flink-" + Guid.NewGuid().ToString("N"));
        var basePath = Path.Join(root, "base");
        Directory.CreateDirectory(basePath);
        try
        {
            var target = Path.Join(root, "outside.txt");
            File.WriteAllText(target, "x");
            var linkPath = Path.Join(basePath, "link.txt");
            try
            {
                File.CreateSymbolicLink(linkPath, target);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                Assert.Skip("Symbolic links cannot be created in this environment.");
            }

            // Act / Assert: the link component is rejected
            Assert.Throws<ArgumentException>(() => PathHelpers.SafePathCombine(basePath, "link.txt"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that ordinary existing and non-existing components below the base are still accepted.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_PlainExistingDirectory_ReturnsCombinedPath()
    {
        // Arrange: a base directory with a plain subdirectory
        var basePath = Path.Join(Path.GetTempPath(), "safe-path-plain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(basePath, "sub"));
        try
        {
            // Act: combine through the existing directory to a file that does not exist
            var result = PathHelpers.SafePathCombine(basePath, Path.Join("sub", "missing.txt"));

            // Assert: the fully qualified combination is returned
            Assert.Equal(Path.GetFullPath(Path.Join(basePath, "sub", "missing.txt")), result);
        }
        finally
        {
            Directory.Delete(basePath, recursive: true);
        }
    }

    /// <summary>
    ///     Creates a directory symbolic link, reporting whether the environment allowed it.
    /// </summary>
    /// <param name="linkPath">The link to create.</param>
    /// <param name="target">The directory the link points to.</param>
    /// <returns><see langword="true"/> when the link was created.</returns>
    private static bool TryCreateDirectoryLink(string linkPath, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, target);
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Validates that null arguments are rejected.
    /// </summary>
    [Fact]
    public void PathHelpers_SafePathCombine_NullArguments_ThrowArgumentNullException()
    {
        // Act / Assert: each null argument is rejected
        Assert.Throws<ArgumentNullException>(() => PathHelpers.SafePathCombine(null!, "child.txt"));
        Assert.Throws<ArgumentNullException>(() => PathHelpers.SafePathCombine("base", null!));
    }
}
