namespace DemaConsulting.Speech.Phonemes.En.Tools;

/// <summary>
///     Locates the repository folders the tool reads and writes.
/// </summary>
/// <remarks>
///     The tool is run from the repository (or from its own build folder), so every path is derived from the
///     repository root, the folder that holds <c>Speech.slnx</c>.
/// </remarks>
internal static class ToolPaths
{
    /// <summary>
    ///     Searches a folder and its parents for the repository root.
    /// </summary>
    /// <param name="start">The folder to start at.</param>
    /// <returns>The root folder, or <see langword="null"/> when no parent holds <c>Speech.slnx</c>.</returns>
    public static string? FindRepositoryRoot(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Speech.slnx")))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    /// <summary>
    ///     Gets the Phonemes.En data folder.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The path of <c>data/phonemes-en</c>.</returns>
    public static string DataDirectory(string root) => Path.Combine(root, "data", "phonemes-en");

    /// <summary>
    ///     Gets the path of the embedded lexicon resource.
    /// </summary>
    /// <param name="root">The repository root.</param>
    /// <returns>The path of <c>cmudict-en.bin.gz</c> in the library's <c>Resources</c> folder.</returns>
    public static string LexiconResourcePath(string root) =>
        Path.Combine(root, "src", "DemaConsulting.Speech.Phonemes.En", "Resources", "cmudict-en.bin.gz");
}
