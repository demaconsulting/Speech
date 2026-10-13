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

namespace DemaConsulting.Speech.Cli.Tests.Cli;

/// <summary>
///     Unit tests for <see cref="CommandDispatch"/>: the subcommand registration table.
/// </summary>
[Collection("Sequential")]
public class CommandDispatchTests
{
    /// <summary>
    ///     Test that <c>list-voices</c> and <c>voices</c> are registered in the dispatch table, and
    ///     that near-miss names (different case, empty) are not resolved.
    /// </summary>
    [Fact]
    public void CommandDispatch_Find_VoiceCommands_AreRegistered()
    {
        // Arrange: the two reference-voice command names plus near-miss names
        const string listVoices = "list-voices";
        const string voices = "voices";

        // Act
        var listVoicesDescriptor = CommandDispatch.Find(listVoices);
        var voicesDescriptor = CommandDispatch.Find(voices);
        var wrongCase = CommandDispatch.Find("VOICES");
        var empty = CommandDispatch.Find(string.Empty);

        // Assert: both resolve to their own descriptor; the boundary names do not resolve
        Assert.NotNull(listVoicesDescriptor);
        Assert.Equal(listVoices, listVoicesDescriptor.Name);
        Assert.NotNull(voicesDescriptor);
        Assert.Equal(voices, voicesDescriptor.Name);
        Assert.Null(wrongCase);
        Assert.Null(empty);
    }

    /// <summary>
    ///     Test that every registered descriptor has a non-empty, unique name and a non-empty usage summary.
    /// </summary>
    [Fact]
    public void CommandDispatch_Commands_EveryDescriptorHasNameAndUsageSummary()
    {
        // Arrange
        var commands = CommandDispatch.Commands;

        // Act
        var names = commands.Select(c => c.Name).ToList();

        // Assert
        Assert.NotEmpty(commands);
        Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());
        foreach (var command in commands)
        {
            Assert.False(string.IsNullOrWhiteSpace(command.Name));
            Assert.False(string.IsNullOrWhiteSpace(command.UsageSummary), $"'{command.Name}' has no usage summary");
        }
    }

    /// <summary>
    ///     Test that every registered command's handler runs when invoked with its argument-less
    ///     form and either completes or reports a documented operator error
    ///     (<see cref="ArgumentException"/> or <see cref="InvalidOperationException"/>); any other
    ///     failure, and any handler that cannot be awaited, fails the test.
    /// </summary>
    [Fact]
    public async Task CommandDispatch_Handlers_RunAndReportOnlyOperatorErrors()
    {
        // Arrange: an isolated models directory and captured console output
        var modelsDir = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(modelsDir);
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var outWriter = new StringWriter();
            using var errWriter = new StringWriter();
            Console.SetOut(outWriter);
            Console.SetError(errWriter);

            foreach (var command in CommandDispatch.Commands)
            {
                using var context = Context.Create(["--models-dir", modelsDir, command.Name]);

                // Act: await the handler; a synchronous throw and a faulted task are both
                // observed by the awaiting caller as the same exception
                var exception = await Record.ExceptionAsync(() => command.Handler(context));

                // Assert: every path is checked - success, or a documented operator error
                if (exception != null)
                {
                    Assert.True(
                        exception is ArgumentException or InvalidOperationException,
                        $"Command '{command.Name}' failed with unexpected {exception.GetType().Name}: {exception.Message}");
                }
            }
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
            if (Directory.Exists(modelsDir))
            {
                Directory.Delete(modelsDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Test that a handler fault surfaces to the awaiting caller identically whether the command
    ///     is synchronous (<c>model-info</c> throws before returning a task) or asynchronous
    ///     (<c>download</c> faults the returned task): neither is masked, and each reports its
    ///     <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public async Task CommandDispatch_Handler_MissingArgument_SurfacesFaultForSyncAndAsyncCommands()
    {
        // Arrange: one synchronous and one asynchronous command, each missing its required model id
        var modelsDir = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(modelsDir);
        try
        {
            var syncCommand = CommandDispatch.Find("model-info");
            var asyncCommand = CommandDispatch.Find("download");
            Assert.NotNull(syncCommand);
            Assert.NotNull(asyncCommand);
            using var syncContext = Context.Create(["--models-dir", modelsDir, "model-info"]);
            using var asyncContext = Context.Create(["--models-dir", modelsDir, "download"]);

            // Act
            var syncException = await Record.ExceptionAsync(() => syncCommand.Handler(syncContext));
            var asyncException = await Record.ExceptionAsync(() => asyncCommand.Handler(asyncContext));

            // Assert
            Assert.IsAssignableFrom<ArgumentException>(syncException);
            Assert.IsAssignableFrom<ArgumentException>(asyncException);
        }
        finally
        {
            if (Directory.Exists(modelsDir))
            {
                Directory.Delete(modelsDir, recursive: true);
            }
        }
    }
}
