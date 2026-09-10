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

namespace DemaConsulting.Speech.Cli.Tests;

/// <summary>
///     Unit tests for the Program class.
/// </summary>
[Collection("Sequential")]
public class ProgramTests
{
    /// <summary>
    ///     Test that Run with version flag displays version only.
    /// </summary>
    [Fact]
    public void Program_Run_WithVersionFlag_DisplaysVersionOnly()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--version"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains(Program.Version, output);
            Assert.DoesNotContain("Copyright", output);
            Assert.DoesNotContain("Speech CLI version", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with help flag displays usage information listing every subcommand.
    /// </summary>
    [Fact]
    public void Program_Run_WithHelpFlag_DisplaysUsageInformation()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--help"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Commands:", output);
            Assert.Contains("--version", output);
            Assert.Contains("--help", output);
            Assert.Contains("--models-dir", output);
            foreach (var command in CommandDispatch.Commands)
            {
                Assert.Contains(command.Name, output);
            }

            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with validate flag runs validation.
    /// </summary>
    [Fact]
    public void Program_Run_WithValidateFlag_RunsValidation()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--validate"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Total Tests:", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with no arguments displays the banner and the same usage information as
    ///     <c>--help</c>, since no subcommand was given.
    /// </summary>
    [Fact]
    public void Program_Run_NoArguments_DisplaysBannerAndUsage()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Speech CLI version", output);
            Assert.Contains("Copyright", output);
            Assert.Contains("Usage:", output);
            Assert.Null(context.Command);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that version property returns non-empty version string.
    /// </summary>
    [Fact]
    public void Program_Version_ReturnsNonEmptyString()
    {
        // Act: execute the operation being tested
        var version = Program.Version;

        // Assert: verify expected behavior
        Assert.False(string.IsNullOrWhiteSpace(version));
    }

    /// <summary>
    ///     Test that Main with invalid arguments returns non-zero exit code.
    /// </summary>
    [Fact]
    public void Program_Main_WithInvalidArgs_ReturnsNonZeroExitCode()
    {
        // Arrange: redirect stderr to suppress error output during test
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: invoke Main with an unrecognized argument (not a known subcommand or option)
            var result = Program.Main(["--invalid-argument"]);

            // Assert: invalid arguments produce a non-zero exit code
            Assert.Equal(1, result);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Main with an unrecognized subcommand-shaped argument returns a non-zero exit
    ///     code and reports the argument in the error output.
    /// </summary>
    [Fact]
    public void Program_Main_WithUnknownCommand_ReturnsNonZeroExitCodeAndReportsCommand()
    {
        // Arrange: redirect stderr to capture error output
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: invoke Main with a command name that is not in the dispatch table
            var result = Program.Main(["not-a-real-command"]);

            // Assert: unknown command produces a clean, non-zero exit with a naming error message
            Assert.Equal(1, result);
            Assert.Contains("not-a-real-command", errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Main with an unrecognized argument and <c>--silent</c> still returns the
    ///     non-zero exit code, but suppresses the stderr message - proving the top-level
    ///     <see cref="ArgumentException"/> catch in <see cref="Program.Main"/> honors
    ///     <c>--silent</c> even though <see cref="Cli.Context.Create"/> itself threw before a
    ///     <see cref="Cli.Context"/> could be constructed to expose its own
    ///     <see cref="Cli.Context.Silent"/> flag.
    /// </summary>
    [Fact]
    public void Program_Main_WithInvalidArgsAndSilentFlag_SuppressesErrorOutput()
    {
        // Arrange: redirect stderr to capture (expected empty) error output
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: invoke Main with an unrecognized argument plus --silent
            var result = Program.Main(["--invalid-argument", "--silent"]);

            // Assert: non-zero exit code still reported, but nothing written to stderr
            Assert.Equal(1, result);
            Assert.Equal(string.Empty, errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Main with an unrecognized subcommand-shaped argument and <c>--silent</c>
    ///     still returns the non-zero exit code, but suppresses the stderr message naming the
    ///     unrecognized command.
    /// </summary>
    [Fact]
    public void Program_Main_WithUnknownCommandAndSilentFlag_SuppressesErrorOutput()
    {
        // Arrange: redirect stderr to capture (expected empty) error output
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: invoke Main with an unknown command name plus --silent
            var result = Program.Main(["not-a-real-command", "--silent"]);

            // Assert: non-zero exit code still reported, but nothing written to stderr
            Assert.Equal(1, result);
            Assert.Equal(string.Empty, errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that dispatching to <c>recognize</c> with no arguments runs the real
    ///     implementation (never throwing <see cref="NotImplementedException"/>), proving the
    ///     dispatch table wiring for this pass's <c>recognize</c> command - the last of all 10
    ///     subcommands to be implemented. Full <c>recognize</c> coverage lives in
    ///     <c>RecognizeCommandTests</c>.
    /// </summary>
    [Fact]
    public void Program_Run_WithRecognizeCommand_DoesNotThrowNotImplemented()
    {
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create(["recognize"]);

            var exception = Record.Exception(() => Program.Run(context));
            Assert.IsNotType<NotImplementedException>(exception);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that dispatching to <c>list-models</c> with no arguments runs the real
    ///     implementation (never throwing <see cref="NotImplementedException"/>), proving the
    ///     dispatch table wiring for this pass's five model-management commands.
    /// </summary>
    [Fact]
    public void Program_Run_WithListModelsCommand_DoesNotThrowNotImplemented()
    {
        // Arrange: an isolated, empty models directory so no real network access occurs
        var modelsDir = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(modelsDir);
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--models-dir", modelsDir, "list-models"]);

            // Act & Assert: does not throw NotImplementedException
            var exception = Record.Exception(() => Program.Run(context));
            Assert.Null(exception);
        }
        finally
        {
            Console.SetOut(originalOut);
            if (Directory.Exists(modelsDir))
            {
                Directory.Delete(modelsDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Test that dispatching to <c>list-devices</c> with no arguments runs the real
    ///     implementation (never throwing <see cref="NotImplementedException"/>), proving the
    ///     dispatch table wiring for this pass's device-related commands.
    /// </summary>
    [Fact]
    public void Program_Run_WithListDevicesCommand_DoesNotThrowNotImplemented()
    {
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["list-devices"]);

            var exception = Record.Exception(() => Program.Run(context));
            Assert.Null(exception);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that dispatching to <c>doctor</c> with no arguments runs the real implementation
    ///     (never throwing <see cref="NotImplementedException"/>), against an isolated, empty
    ///     models directory so no real network access or per-user store is touched.
    /// </summary>
    [Fact]
    public void Program_Run_WithDoctorCommand_DoesNotThrowNotImplemented()
    {
        var modelsDir = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(modelsDir);
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--models-dir", modelsDir, "doctor"]);

            var exception = Record.Exception(() => Program.Run(context));
            Assert.Null(exception);
        }
        finally
        {
            Console.SetOut(originalOut);
            if (Directory.Exists(modelsDir))
            {
                Directory.Delete(modelsDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Test that dispatching to <c>speak</c> without <c>--model</c> reports a clean,
    ///     missing-model <see cref="ArgumentException"/> rather than
    ///     <see cref="NotImplementedException"/>, proving the dispatch table wiring for this
    ///     pass's <c>speak</c> command. Full <c>speak</c> coverage lives in
    ///     <c>SpeakCommandTests</c>.
    /// </summary>
    [Fact]
    public void Program_Run_WithSpeakCommand_DoesNotThrowNotImplemented()
    {
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create(["speak"]);

            var exception = Record.Exception(() => Program.Run(context));
            Assert.IsNotType<NotImplementedException>(exception);
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that Run with short version flag -v displays version.
    /// </summary>
    [Fact]
    public void Program_Run_WithShortVersionFlag_DisplaysVersion()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["-v"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains(Program.Version, output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with short help flag -h displays usage.
    /// </summary>
    [Fact]
    public void Program_Run_WithShortHelpFlag_DisplaysUsage()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["-h"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Commands:", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that Run with short help flag -? displays usage.
    /// </summary>
    [Fact]
    public void Program_Run_WithQuestionMarkFlag_DisplaysUsage()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["-?"]);

            // Act: execute the operation being tested
            Program.Run(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Commands:", output);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that a global option (<c>--models-dir</c>) appearing after the subcommand name is
    ///     still recognized as a global option rather than collected into the subcommand's raw
    ///     arguments.
    /// </summary>
    [Fact]
    public void Context_Create_ModelsDirAfterCommand_IsRecognizedAsGlobalOption()
    {
        // Act: parse a command line with the subcommand first, then a global option
        using var context = Context.Create(["list-models", "--models-dir", "/tmp/models", "--format", "json"]);

        // Assert: the global option is resolved onto the context, not left in CommandArgs
        Assert.Equal("list-models", context.Command);
        Assert.Equal("/tmp/models", context.ModelsDir);
        Assert.Equal(["--format", "json"], context.CommandArgs);
    }

    /// <summary>
    ///     Test that Run passes through to Main's ArgumentException handling with a null-safe
    ///     path when <see cref="Program.Run"/> itself is given a null context.
    /// </summary>
    [Fact]
    public void Program_Run_WithNullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Program.Run(null!));
    }
}
