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
    ///     Test that <c>list-voices</c> and <c>voices</c> are registered in the dispatch table.
    /// </summary>
    [Fact]
    public void CommandDispatch_Find_VoiceCommands_AreRegistered()
    {
        Assert.NotNull(CommandDispatch.Find("list-voices"));
        Assert.NotNull(CommandDispatch.Find("voices"));
    }

    /// <summary>
    ///     Test that every registered command descriptor exposes a handler that returns a
    ///     non-null <see cref="Task"/>, so the dispatcher can always await it.
    /// </summary>
    [Fact]
    public async Task CommandDispatch_Handlers_ReturnNonNullTasks()
    {
        // Arrange: an isolated models directory and the argument-less forms of every command;
        // handlers that reject the missing arguments do so by faulting the returned task.
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
                Assert.NotNull(command.Handler);
                using var context = Context.Create(["--models-dir", modelsDir, command.Name]);

                // Act: invoke the handler. Argument problems may surface either as a synchronous
                // throw (commands that are still synchronous) or as a faulted Task (async commands);
                // what must never happen is a handler returning null.
                Task? task;
                try
                {
                    task = command.Handler(context);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                // Assert: non-null task; outcome (success or fault) is irrelevant here
                Assert.NotNull(task);
                await Record.ExceptionAsync(() => task);
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
}
