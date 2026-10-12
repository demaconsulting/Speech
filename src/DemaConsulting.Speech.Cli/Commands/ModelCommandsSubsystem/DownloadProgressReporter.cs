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

using DemaConsulting.Speech.Cli.Cli;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;

/// <summary>
///     Creates the throttled live progress reporter shared by the <c>download</c> and
///     <c>voices install</c> subcommands.
/// </summary>
internal static class DownloadProgressReporter
{
    /// <summary>
    ///     Creates a progress reporter that prints one line per whole percent (or per whole
    ///     kilobyte when the total size is unknown) to the context's output, suppressing repeats.
    /// </summary>
    /// <param name="context">The invocation context whose output receives the lines. Must not be null.</param>
    /// <param name="describe">
    ///     Produces the line's label (for example the item id) from a progress report. Must not be null.
    /// </param>
    /// <returns>A progress reporter bound to <paramref name="context"/>.</returns>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="context"/> or <paramref name="describe"/> is <see langword="null"/>.
    /// </exception>
    public static Progress<SpeechModelDownloadProgress> Create(
        Context context,
        Func<SpeechModelDownloadProgress, string> describe)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(describe);

        var lastReportedPercent = -1;
        var lastReportedKilobytes = -1L;
        return new Progress<SpeechModelDownloadProgress>(progress =>
        {
            if (progress.FractionComplete is { } fraction)
            {
                var percent = (int)(fraction * 100);
                if (percent == lastReportedPercent)
                {
                    return;
                }

                lastReportedPercent = percent;
                context.WriteLine($"  {describe(progress)}: {percent}%");
            }
            else
            {
                // TotalBytes is unknown (no Content-Length reported); report by whole kilobytes
                // transferred instead, throttled the same way.
                var kilobytes = progress.BytesTransferred / 1024;
                if (kilobytes == lastReportedKilobytes)
                {
                    return;
                }

                lastReportedKilobytes = kilobytes;
                context.WriteLine($"  {describe(progress)}: {kilobytes:N0} KB");
            }
        });
    }
}
