using System.Diagnostics;

namespace DemaConsulting.Speech.ModelBundler;

/// <summary>The outcome of an external process run.</summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">Everything the process wrote to standard output.</param>
/// <param name="StandardError">Everything the process wrote to standard error.</param>
internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
///     Runs an external program, so the release uploader's use of <c>gh</c> can be replaced by a fake in tests.
/// </summary>
internal interface IProcessRunner
{
    /// <summary>Runs a program to completion and captures its output.</summary>
    /// <param name="fileName">The program, resolved through <c>PATH</c>.</param>
    /// <param name="arguments">The arguments, each passed as one argument with no shell interpretation.</param>
    /// <param name="cancellationToken">A token that stops waiting (and kills the process).</param>
    /// <returns>The exit code and captured output.</returns>
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

/// <summary>
///     Runs external programs with <see cref="Process"/>, passing each argument verbatim (no shell).
/// </summary>
/// <remarks>Stateless and thread-safe.</remarks>
internal sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc/>
    /// <exception cref="System.ComponentModel.Win32Exception">Thrown when the program cannot be started.</exception>
    public async Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        // ArgumentList passes each argument as-is, so a path with spaces needs no quoting
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"'{fileName}' could not be started.");

        // Read both streams concurrently so a full pipe never blocks the process
        var output = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw;
        }

        return new ProcessResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }
}
