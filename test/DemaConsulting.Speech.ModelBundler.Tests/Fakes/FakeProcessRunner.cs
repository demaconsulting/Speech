namespace DemaConsulting.Speech.ModelBundler.Tests.Fakes;

/// <summary>
///     A scripted stand-in for <c>gh</c>: records every invocation and answers <c>release view</c> with a configured
///     asset list and every other command with success, so upload tests never run a real process.
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    /// <summary>Gets or sets the JSON returned for <c>gh release view</c>.</summary>
    public string AssetsJson { get; set; } = """{"assets":[]}""";

    /// <summary>Gets or sets the exit code returned for <c>gh release view</c>.</summary>
    public int ViewExitCode { get; set; }

    /// <summary>Gets the argument lists of every invocation, in order.</summary>
    public List<IReadOnlyList<string>> Calls { get; } = [];

    /// <summary>Gets the argument lists of the <c>gh release upload</c> invocations.</summary>
    public IEnumerable<IReadOnlyList<string>> Uploads => Calls.Where(call => call.Count > 1 && call[1] == "upload");

    /// <inheritdoc/>
    public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        Calls.Add([.. arguments]);
        if (arguments.Count < 2 || arguments[1] != "view")
        {
            return Task.FromResult(new ProcessResult(0, string.Empty, string.Empty));
        }

        return Task.FromResult(ViewExitCode == 0
            ? new ProcessResult(0, AssetsJson, string.Empty)
            : new ProcessResult(ViewExitCode, string.Empty, "release not found"));
    }
}
