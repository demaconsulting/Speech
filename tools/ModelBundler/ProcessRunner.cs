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
            // Stop the child, then surface the caller's cancellation rather than any failure of the kill itself
            KillAfterCancellation(process);
            throw;
        }

        return new ProcessResult(process.ExitCode, await output.ConfigureAwait(false), await error.ConfigureAwait(false));
    }

    /// <summary>Kills a canceled run's process tree, tolerating a process that has already exited.</summary>
    /// <remarks>
    ///     Cancellation can race with normal completion: the wait may observe the canceled token just after the child
    ///     exits, and killing an exited process (or a tree whose members have exited) throws. That failure is
    ///     swallowed so it never masks the caller's <see cref="OperationCanceledException"/>; a process that has
    ///     exited needs no kill, and there is nothing more a canceled run can do about one that cannot be killed.
    /// </remarks>
    /// <param name="process">The started process. Must not be <see langword="null"/>.</param>
    internal static void KillAfterCancellation(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The process exited between the check and the kill, or could not be killed; cancellation still wins
        }
    }
}
