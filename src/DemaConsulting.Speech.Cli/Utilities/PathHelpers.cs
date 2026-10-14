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

namespace DemaConsulting.Speech.Cli.Utilities;

/// <summary>
///     Helper utilities for safe path operations.
/// </summary>
internal static class PathHelpers
{
    /// <summary>
    ///     Safely combines two paths, ensuring the resolved combined path stays within the base directory.
    /// </summary>
    /// <param name="basePath">The base path.</param>
    /// <param name="relativePath">The relative path to combine.</param>
    /// <returns>The combined, fully-qualified absolute path.</returns>
    /// <remarks>
    ///     Provides a security boundary for caller-supplied path components. Stateless and thread-safe.
    ///     Besides string-level normalization, it queries the attributes of existing components below the
    ///     base directory so that symbolic links and reparse points are rejected.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="basePath"/> or <paramref name="relativePath"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="relativePath"/> is rooted (even if it lies inside the base
    ///     directory), when the resolved combined path escapes the base directory, when an existing
    ///     component below the base is a symbolic link or reparse point, or when a supplied path is invalid.
    /// </exception>
    /// <exception cref="NotSupportedException">Thrown when a supplied path contains an unsupported format.</exception>
    /// <exception cref="PathTooLongException">Thrown when the combined or resolved path exceeds the system-defined maximum length.</exception>
    internal static string SafePathCombine(string basePath, string relativePath)
    {
        // Validate inputs
        ArgumentNullException.ThrowIfNull(basePath);
        ArgumentNullException.ThrowIfNull(relativePath);

        // Reject every rooted component outright, including one that happens to lie inside the
        // base directory: a component is documented as relative, and accepting an absolute one
        // would let the caller bypass the relative-component contract.
        if (Path.IsPathRooted(relativePath))
        {
            throw new ArgumentException($"Invalid path component: {relativePath}", nameof(relativePath));
        }

        // Combine the paths using Path.Combine; the rooted case is already rejected above.
        var combinedPath = Path.Combine(basePath, relativePath);

        // Security check: resolve both paths to absolute form and verify the combined
        // path is still inside the base directory. Path.GetRelativePath handles root
        // paths, platform case-sensitivity, and directory-separator normalization natively.
        var absoluteBase = Path.GetFullPath(basePath);
        var absoluteCombined = Path.GetFullPath(combinedPath);
        var checkRelative = Path.GetRelativePath(absoluteBase, absoluteCombined);

        if (string.Equals(checkRelative, "..", StringComparison.Ordinal)
            || checkRelative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || checkRelative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(checkRelative))
        {
            throw new ArgumentException($"Invalid path component: {relativePath}", nameof(relativePath));
        }

        // Lexical containment is not enough: an existing symbolic link, junction or other reparse
        // point below the base can redirect a contained path to a location outside it.
        RejectLinkedComponents(absoluteBase, checkRelative, relativePath);

        // Return the already-validated absolute path rather than the unnormalized combined
        // path: when basePath is relative, combinedPath could later resolve against a
        // different working directory than the one this containment check just validated,
        // silently weakening the security boundary this method exists to enforce.
        return absoluteCombined;
    }

    /// <summary>
    ///     Walks each component of the contained relative path from the base downward and rejects the
    ///     path when an existing component is a symbolic link or reparse point.
    /// </summary>
    /// <param name="absoluteBase">The fully qualified base directory (itself not inspected).</param>
    /// <param name="containedRelative">The lexically contained path relative to the base.</param>
    /// <param name="relativePath">The original component, used for the exception.</param>
    /// <exception cref="ArgumentException">Thrown when an existing component is a link or reparse point.</exception>
    private static void RejectLinkedComponents(string absoluteBase, string containedRelative, string relativePath)
    {
        var current = absoluteBase;
        var separators = new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
        foreach (var part in containedRelative.Split(separators, StringSplitOptions.RemoveEmptyEntries))
        {
            // The base itself is the trusted root; "." contributes nothing.
            if (part == ".")
            {
                continue;
            }

            current = Path.Combine(current, part);

            FileAttributes attributes;
            try
            {
                attributes = File.GetAttributes(current);
            }
            catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
            {
                // Nothing exists here, so nothing deeper can be a link either.
                return;
            }

            if (attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new ArgumentException($"Invalid path component: {relativePath}", nameof(relativePath));
            }
        }
    }
}
