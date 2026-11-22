using DemaConsulting.Speech.SttEvaluation;

namespace DemaConsulting.Speech.Onnx.NemotronStt.Tests.Evaluation;

/// <summary>
///     Unit tests for <see cref="ToolPaths"/>, proving that the evaluation tool writes its reports
///     only inside the repository's git-ignored <c>artifacts</c> folder, so no transcript or report
///     can be committed by accident.
/// </summary>
public sealed class ToolPathsTests
{
    /// <summary>A repository root that need not exist (output resolution never touches the disk).</summary>
    private static readonly string RepositoryRoot = Path.Join(Path.GetTempPath(), "tool-paths-tests-repo");

    /// <summary>
    ///     Proves that a folder inside <c>artifacts</c> is accepted and returned as an absolute path.
    /// </summary>
    [Fact]
    public void ToolPaths_ResolveOutputDirectory_InsideArtifacts_ReturnsFullPath()
    {
        // Arrange: a folder below the artifacts folder
        var requested = Path.Join(RepositoryRoot, "artifacts", "stt-evaluation", "run-1");

        // Act
        var resolved = ToolPaths.ResolveOutputDirectory(RepositoryRoot, requested);

        // Assert: the same folder, fully qualified
        Assert.Equal(Path.GetFullPath(requested), resolved);
    }

    /// <summary>
    ///     Proves that a folder outside <c>artifacts</c>, including one whose name only starts with
    ///     "artifacts", is refused.
    /// </summary>
    [Fact]
    public void ToolPaths_ResolveOutputDirectory_OutsideArtifacts_ThrowsArgumentException()
    {
        // Arrange: the data folder and a sibling folder that shares the artifacts prefix
        var dataFolder = Path.Join(RepositoryRoot, "data", "stt-en");
        var prefixSibling = Path.Join(RepositoryRoot, "artifacts-extra");

        // Act / Assert: both are refused
        Assert.Throws<ArgumentException>(() => ToolPaths.ResolveOutputDirectory(RepositoryRoot, dataFolder));
        Assert.Throws<ArgumentException>(() => ToolPaths.ResolveOutputDirectory(RepositoryRoot, prefixSibling));
    }

    /// <summary>
    ///     Proves that a case variant of the artifacts folder is refused where file names are
    ///     case-sensitive (it is a different folder that git does not ignore) and accepted on
    ///     Windows, where it is the same folder.
    /// </summary>
    [Fact]
    public void ToolPaths_ResolveOutputDirectory_CaseVariantOfArtifacts_AcceptedOnlyOnWindows()
    {
        // Arrange: the artifacts folder spelled with a capital letter
        var requested = Path.Join(RepositoryRoot, "Artifacts", "stt-evaluation");

        // Act / Assert: the same folder on Windows, a refused different folder elsewhere
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal(Path.GetFullPath(requested), ToolPaths.ResolveOutputDirectory(RepositoryRoot, requested));
        }
        else
        {
            Assert.Throws<ArgumentException>(() => ToolPaths.ResolveOutputDirectory(RepositoryRoot, requested));
        }
    }
}
