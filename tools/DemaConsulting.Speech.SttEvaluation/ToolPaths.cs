namespace DemaConsulting.Speech.SttEvaluation;

/// <summary>
///     Locates the repository folders the evaluation tool reads from and writes to.
/// </summary>
/// <remarks>
///     The tool runs from its build output folder, so the repository root is found by walking up
///     to the folder holding <c>Speech.slnx</c> (the convention the repository's tests use as
///     well). Output is confined to the git-ignored <c>artifacts/</c> folder so that no engine
///     transcript or report can be committed by accident. Stateless and thread-safe.
/// </remarks>
internal static class ToolPaths
{
    /// <summary>The file that marks the repository root.</summary>
    public const string RootMarker = "Speech.slnx";

    /// <summary>
    ///     Finds the repository root by walking up from the tool's own folder, then from the
    ///     current directory.
    /// </summary>
    /// <returns>The absolute path of the repository root.</returns>
    /// <exception cref="DirectoryNotFoundException">
    ///     Thrown when neither folder lies inside a checkout of the repository (the output must stay
    ///     under the repository's <c>artifacts/</c> folder, so a root is always required).
    /// </exception>
    public static string FindRepositoryRoot() =>
        FindRoot(AppContext.BaseDirectory)
        ?? FindRoot(Environment.CurrentDirectory)
        ?? throw new DirectoryNotFoundException(
            $"No folder holding '{RootMarker}' was found above '{AppContext.BaseDirectory}' or " +
            $"'{Environment.CurrentDirectory}'. Run the tool from a checkout of the repository.");

    /// <summary>Gets the default data folder, <c>data/stt-en</c>, of a repository root.</summary>
    /// <param name="repositoryRoot">The repository root.</param>
    /// <returns>The absolute path of the data folder.</returns>
    public static string DefaultDataDirectory(string repositoryRoot) =>
        Path.Join(repositoryRoot, "data", "stt-en");

    /// <summary>Gets the git-ignored <c>artifacts</c> folder of a repository root.</summary>
    /// <param name="repositoryRoot">The repository root.</param>
    /// <returns>The absolute path of the artifacts folder.</returns>
    public static string ArtifactsDirectory(string repositoryRoot) =>
        Path.Join(repositoryRoot, "artifacts");

    /// <summary>Gets the default output folder, <c>artifacts/stt-evaluation</c>, of a repository root.</summary>
    /// <param name="repositoryRoot">The repository root.</param>
    /// <returns>The absolute path of the output folder.</returns>
    public static string DefaultOutputDirectory(string repositoryRoot) =>
        Path.Join(ArtifactsDirectory(repositoryRoot), "stt-evaluation");

    /// <summary>
    ///     Resolves a requested output folder and checks that it lies inside the repository's
    ///     <c>artifacts</c> folder.
    /// </summary>
    /// <param name="repositoryRoot">The repository root.</param>
    /// <param name="requested">The requested folder (absolute, or relative to the current directory), or <see langword="null"/> for the default.</param>
    /// <returns>The absolute output folder.</returns>
    /// <exception cref="ArgumentException">Thrown when the folder is outside <c>artifacts/</c>.</exception>
    public static string ResolveOutputDirectory(string repositoryRoot, string? requested)
    {
        if (requested is null)
        {
            return DefaultOutputDirectory(repositoryRoot);
        }

        // Only a folder inside the git-ignored artifacts folder is accepted, so no output can be committed
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(requested));
        var artifacts = Path.TrimEndingDirectorySeparator(Path.GetFullPath(ArtifactsDirectory(repositoryRoot)));
        if (!full.StartsWith(artifacts + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(full, artifacts, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"The output folder '{full}' must be inside '{artifacts}'.", nameof(requested));
        }

        return full;
    }

    /// <summary>Walks up from a folder to the first one holding <see cref="RootMarker"/>.</summary>
    /// <param name="start">The folder to start from.</param>
    /// <returns>The root folder, or <see langword="null"/> when none is found.</returns>
    private static string? FindRoot(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Join(directory.FullName, RootMarker)))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
