using System.Diagnostics;

namespace DemaConsulting.Speech.Text.En.Trainer.Tests.Fakes;

/// <summary>
///     Creates a directory link for tests, where the platform and the account allow it.
/// </summary>
internal static class DirectoryLink
{
    /// <summary>The reason shown when a test is skipped because no link could be created.</summary>
    public const string SkipReason = "Directory links cannot be created here (a symbolic link needs a privilege such as Windows developer mode, and the junction fallback failed).";

    /// <summary>
    ///     Tries to create a directory symbolic link, falling back to a junction on Windows.
    /// </summary>
    /// <param name="link">The link to create.</param>
    /// <param name="target">The existing folder the link points to.</param>
    /// <returns><see langword="true"/> when the link exists afterwards.</returns>
    public static bool TryCreate(string link, string target)
    {
        // A symbolic link first; creating one can require a privilege
        try
        {
            Directory.CreateSymbolicLink(link, target);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // Windows only: a junction needs no privilege and is a reparse point, as the guard must handle
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }
        }

        try
        {
            var start = new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            start.ArgumentList.Add("/c");
            start.ArgumentList.Add("mklink");
            start.ArgumentList.Add("/J");
            start.ArgumentList.Add(link);
            start.ArgumentList.Add(target);
            using var process = Process.Start(start);
            process?.WaitForExit();
            return process?.ExitCode == 0 && Directory.Exists(link);
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
