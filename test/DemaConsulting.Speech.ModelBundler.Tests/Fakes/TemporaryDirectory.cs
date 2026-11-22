namespace DemaConsulting.Speech.ModelBundler.Tests.Fakes;

/// <summary>
///     A uniquely named scratch directory under the system temporary folder, deleted (with its contents) on dispose.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TemporaryDirectory"/> class, creating the directory.</summary>
    public TemporaryDirectory() => Path = Directory.CreateTempSubdirectory("model-bundler-tests-").FullName;

    /// <summary>Gets the full path of the directory.</summary>
    public string Path { get; }

    /// <summary>Combines the directory with a relative path.</summary>
    /// <param name="parts">The path parts.</param>
    /// <returns>The combined path.</returns>
    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    /// <summary>Deletes the directory and everything in it.</summary>
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
