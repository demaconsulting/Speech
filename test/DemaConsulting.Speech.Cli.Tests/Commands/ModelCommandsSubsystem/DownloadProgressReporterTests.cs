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
using DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;

/// <summary>
///     Unit tests for <see cref="DownloadProgressReporter"/>.
/// </summary>
[Collection("Sequential")]
public sealed class DownloadProgressReporterTests
{
    /// <summary>
    ///     Test that repeated reports within the same whole percent print a single line, and that
    ///     the label comes from the describe callback.
    /// </summary>
    [Fact]
    public void DownloadProgressReporter_Create_KnownTotal_PrintsOneLinePerWholePercent()
    {
        // Arrange
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["list-devices"]);
            var reporter = CreateInlineReporter(context);

            // Act: three reports, two of them inside 50%
            ((IProgress<SpeechModelDownloadProgress>)reporter).Report(new SpeechModelDownloadProgress(0, 1, 500, 1000));
            ((IProgress<SpeechModelDownloadProgress>)reporter).Report(new SpeechModelDownloadProgress(0, 1, 505, 1000));
            ((IProgress<SpeechModelDownloadProgress>)reporter).Report(new SpeechModelDownloadProgress(0, 1, 1000, 1000));
            var output = writer.ToString();

            // Assert
            Assert.Contains("  'm1': 50%", output);
            Assert.Contains("  'm1': 100%", output);
            Assert.Equal(2, output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>Test that an unknown total reports whole kilobytes instead of a percentage.</summary>
    [Fact]
    public void DownloadProgressReporter_Create_UnknownTotal_ReportsKilobytes()
    {
        // Arrange
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["list-devices"]);
            var reporter = CreateInlineReporter(context);

            // Act
            ((IProgress<SpeechModelDownloadProgress>)reporter).Report(new SpeechModelDownloadProgress(0, 1, 2048, null));
            var output = writer.ToString();

            // Assert
            Assert.Contains("  'm1': 2 KB", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>Test that null arguments are rejected.</summary>
    [Fact]
    public void DownloadProgressReporter_Create_NullArguments_ThrowArgumentNullException()
    {
        using var context = Context.Create(["list-devices"]);

        Assert.Throws<ArgumentNullException>(() => DownloadProgressReporter.Create(null!, _ => "x"));
        Assert.Throws<ArgumentNullException>(() => DownloadProgressReporter.Create(context, null!));
    }

    /// <summary>
    ///     Creates the reporter while an inline <see cref="SynchronizationContext"/> is current.
    ///     <see cref="Progress{T}"/> captures the context at construction and posts every callback to it, so
    ///     the callbacks run synchronously inside <c>Report</c> and no waiting is needed.
    /// </summary>
    private static Progress<SpeechModelDownloadProgress> CreateInlineReporter(Context context)
    {
        var original = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new InlineSynchronizationContext());
        try
        {
            return DownloadProgressReporter.Create(context, _ => "'m1'");
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(original);
        }
    }

    /// <summary>A synchronization context that runs posted callbacks immediately on the calling thread.</summary>
    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        /// <inheritdoc />
        public override void Post(SendOrPostCallback d, object? state) => d(state);
    }
}
