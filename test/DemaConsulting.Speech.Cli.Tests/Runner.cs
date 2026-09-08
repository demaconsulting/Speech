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

using System.Diagnostics;

namespace DemaConsulting.Speech.Cli.Tests;

/// <summary>
///     Program runner class for integration testing.
/// </summary>
internal static class Runner
{
    /// <summary>
    ///     The maximum time to wait for the child process to exit before treating it as hung.
    /// </summary>
    /// <remarks>
    ///     60 seconds is comfortably larger than this suite's slowest legitimate scenario - the
    ///     real-model recognition integration test, which loads an already-downloaded STT model
    ///     and runs inference against a short audio fixture (no network download occurs during
    ///     tests) - while still failing fast if a regression ever causes the CLI to hang (for
    ///     example, blocking on stdin it never receives).
    /// </remarks>
    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     Runs the specified program and captures its output.
    /// </summary>
    /// <param name="output">Program output (stdout and stderr combined).</param>
    /// <param name="program">Program name or path.</param>
    /// <param name="arguments">Program arguments.</param>
    /// <returns>Program exit code.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the process fails to start, or when it does not exit within
    ///     <see cref="ProcessExitTimeout"/> (the process tree is killed before throwing, so no
    ///     orphaned process is left behind).
    /// </exception>
    public static int Run(out string output, string program, params string[] arguments)
    {
        // Construct the start information
        var startInfo = new ProcessStartInfo(program)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Add the arguments
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Start the process
        using var process = Process.Start(startInfo) ??
                            throw new InvalidOperationException("Failed to start process");

        // Read output asynchronously to avoid buffer overflow
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        // Wait for the process to exit, bounded by a generous timeout: an unbounded wait here
        // would let a regression that hangs the CLI (e.g. blocking on stdin it never receives)
        // hang this entire test suite - and CI - indefinitely instead of failing fast.
        if (!process.WaitForExit((int)ProcessExitTimeout.TotalMilliseconds))
        {
            // Kill the whole process tree so no orphaned child process outlives the test, then
            // report a clear failure instead of hanging.
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException(
                $"Process '{program}' did not exit within {ProcessExitTimeout.TotalSeconds}s.");
        }

        // Combine stdout and stderr, save the output and return the exit code
        var stdout = outputTask.GetAwaiter().GetResult();
        var stderr = errorTask.GetAwaiter().GetResult();
        output = stdout + stderr;
        return process.ExitCode;
    }
}
