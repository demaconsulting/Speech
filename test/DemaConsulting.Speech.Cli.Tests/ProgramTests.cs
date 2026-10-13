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

namespace DemaConsulting.Speech.Cli.Tests;

/// <summary>
///     Unit tests for the Program class (entry point, dispatch priority, exit codes, help text).
/// </summary>
[Collection("Sequential")]
public class ProgramTests
{
    /// <summary>
    ///     Test that Run with version flag displays version only.
    /// </summary>
    [Fact]
    public async Task Program_Run_WithVersionFlag_DisplaysVersionOnly()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--version"]);

            // Act: execute the operation being tested
            await Program.RunAsync(context);

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
    public async Task Program_Run_WithHelpFlag_DisplaysUsageInformation()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--help"]);

            // Act: execute the operation being tested
            await Program.RunAsync(context);

            // Assert: verify expected behavior
            var output = outWriter.ToString();
            Assert.Contains("Usage:", output);
            Assert.Contains("Commands:", output);
            Assert.Contains("--version", output);
            Assert.Contains("--help", output);
            Assert.Contains("--models-dir", output);
            Assert.Contains("--mirror-bearer-token <t>  HTTP Bearer token for --mirror-url", output);
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
    public async Task Program_Run_WithValidateFlag_RunsValidation()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--validate"]);

            // Act: execute the operation being tested
            await Program.RunAsync(context);

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
    public async Task Program_Run_NoArguments_DisplaysBannerAndUsage()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create([]);

            // Act: execute the operation being tested
            await Program.RunAsync(context);

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
    public async Task Program_Main_WithInvalidArgs_ReturnsNonZeroExitCode()
    {
        // Arrange: redirect stderr to suppress error output during test
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: invoke Main with an unrecognized argument (not a known subcommand or option)
            var result = await Program.Main(["--invalid-argument"]);

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
    public async Task Program_Main_WithUnknownCommand_ReturnsNonZeroExitCodeAndReportsCommand()
    {
        // Arrange: redirect stderr to capture error output
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: invoke Main with a command name that is not in the dispatch table
            var result = await Program.Main(["not-a-real-command"]);

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
    ///     <c>--silent</c> even though <see cref="Context.Create(string[])"/> itself threw before a
    ///     <see cref="Context"/> could be constructed to expose its own
    ///     <see cref="Context.Silent"/> flag.
    /// </summary>
    [Fact]
    public async Task Program_Main_WithInvalidArgsAndSilentFlag_SuppressesErrorOutput()
    {
        // Arrange: redirect stderr to capture (expected empty) error output
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: invoke Main with an unrecognized argument plus --silent
            var result = await Program.Main(["--invalid-argument", "--silent"]);

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
    public async Task Program_Main_WithUnknownCommandAndSilentFlag_SuppressesErrorOutput()
    {
        // Arrange: redirect stderr to capture (expected empty) error output
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);

            // Act: invoke Main with an unknown command name plus --silent
            var result = await Program.Main(["not-a-real-command", "--silent"]);

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
    ///     Test that dispatching to <c>recognize</c> with no arguments reaches the real
    ///     implementation, which rejects the missing model with an
    ///     <see cref="ArgumentException"/>, proving the dispatch table wiring for the
    ///     <c>recognize</c> command. Full <c>recognize</c> coverage lives in
    ///     <c>RecognizeCommandTests</c>.
    /// </summary>
    [Fact]
    public async Task Program_Run_WithRecognizeCommandAndNoModel_ThrowsArgumentException()
    {
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create(["recognize"]);

            await Assert.ThrowsAnyAsync<ArgumentException>(() => Program.RunAsync(context));
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that dispatching to <c>list-models</c> with no arguments reaches the real
    ///     implementation and completes with a success exit code and a model listing, proving
    ///     the dispatch table wiring for the model-management commands.
    /// </summary>
    [Fact]
    public async Task Program_Run_WithListModelsCommand_ListsModelsAndSucceeds()
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

            // Act
            await Program.RunAsync(context);

            // Assert
            Assert.Equal(0, context.ExitCode);
            Assert.NotEqual(string.Empty, outWriter.ToString());
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
    ///     Test that dispatching to <c>list-devices</c> with no arguments reaches the real
    ///     implementation and completes with a success exit code, proving the dispatch table
    ///     wiring for the device-related commands.
    /// </summary>
    [Fact]
    public async Task Program_Run_WithListDevicesCommand_CompletesWithSuccessExitCode()
    {
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["list-devices"]);

            await Program.RunAsync(context);

            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that dispatching to <c>doctor</c> with no arguments reaches the real
    ///     implementation and produces a report without throwing, against an isolated, empty
    ///     models directory so no real network access or per-user store is touched.
    /// </summary>
    [Fact]
    public async Task Program_Run_WithDoctorCommand_ProducesReportWithoutThrowing()
    {
        var modelsDir = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(modelsDir);
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["--models-dir", modelsDir, "doctor"]);

            var exception = await Record.ExceptionAsync(() => Program.RunAsync(context));

            Assert.Null(exception);
            Assert.NotEqual(string.Empty, outWriter.ToString());
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
    ///     Test that dispatching to <c>speak</c> without <c>--model</c> reaches the real
    ///     implementation, which rejects the missing model with an
    ///     <see cref="ArgumentException"/>, proving the dispatch table wiring for the
    ///     <c>speak</c> command. Full <c>speak</c> coverage lives in <c>SpeakCommandTests</c>.
    /// </summary>
    [Fact]
    public async Task Program_Run_WithSpeakCommandAndNoModel_ThrowsArgumentException()
    {
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create(["speak"]);

            await Assert.ThrowsAnyAsync<ArgumentException>(() => Program.RunAsync(context));
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Test that dispatching to <c>ask</c> without <c>--tts-model</c> reaches the real
    ///     implementation, which rejects the missing model with an
    ///     <see cref="ArgumentException"/>, proving the dispatch table wiring for the
    ///     <c>ask</c> command. Full <c>ask</c> coverage lives in <c>AskCommandTests</c>.
    /// </summary>
    [Fact]
    public async Task Program_Run_WithAskCommandAndNoModel_ThrowsArgumentException()
    {
        var originalError = Console.Error;
        try
        {
            using var errWriter = new StringWriter();
            Console.SetError(errWriter);
            using var context = Context.Create(["ask"]);

            await Assert.ThrowsAnyAsync<ArgumentException>(() => Program.RunAsync(context));
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
    public async Task Program_Run_WithShortVersionFlag_DisplaysVersion()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["-v"]);

            // Act: execute the operation being tested
            await Program.RunAsync(context);

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
    public async Task Program_Run_WithShortHelpFlag_DisplaysUsage()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["-h"]);

            // Act: execute the operation being tested
            await Program.RunAsync(context);

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
    public async Task Program_Run_WithQuestionMarkFlag_DisplaysUsage()
    {
        // Arrange: setup test conditions
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);
            using var context = Context.Create(["-?"]);

            // Act: execute the operation being tested
            await Program.RunAsync(context);

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
    ///     Test that help documents <c>--voices-dir</c>, the voice commands, voice selection, and the consent note.
    /// </summary>
    [Fact]
    public async Task Program_Run_Help_DocumentsVoicesOptionsAndConsentNote()
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["--help"]);

            await Program.RunAsync(context);

            var output = writer.ToString();
            Assert.Contains("--voices-dir", output);
            Assert.Contains("list-voices", output);
            Assert.Contains("voices (packs | install <packId> | uninstall <packId>)", output);
            Assert.Contains("--tts-param voice=<voiceId>", output);
            Assert.Contains("Only clone voices you have permission to use", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that <see cref="Program.RunAsync"/> given a null context faults with
    ///     <see cref="ArgumentNullException"/> (surfaced through the returned task, since the
    ///     method is asynchronous).
    /// </summary>
    [Fact]
    public async Task Program_Run_WithNullContext_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => Program.RunAsync(null!));
    }

    /// <summary>
    ///     Builds an environment reader over a fixed set of variables, so tests never mutate the process environment.
    /// </summary>
    private static Func<string, string?> FakeEnvironment(params (string Name, string Value)[] variables) =>
        name => variables.Where(v => v.Name == name).Select(v => v.Value).FirstOrDefault();

    /// <summary>
    ///     Test that help names the mirror environment variables.
    /// </summary>
    [Fact]
    public async Task Program_Run_Help_DocumentsMirrorEnvironmentVariables()
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(["--help"], FakeEnvironment());

            await Program.RunAsync(context);

            var output = writer.ToString();
            Assert.Contains("DEMASPEECH_MIRROR_URL", output);
            Assert.Contains("DEMASPEECH_MIRROR_USER", output);
            Assert.Contains("DEMASPEECH_MIRROR_PASSWORD", output);
            Assert.Contains("DEMASPEECH_MIRROR_BEARER_TOKEN", output);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that help never prints an environment secret's value.
    /// </summary>
    [Fact]
    public async Task Program_Run_Help_DoesNotPrintEnvironmentSecretValues()
    {
        var originalOut = Console.Out;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            using var context = Context.Create(
                ["--help"],
                FakeEnvironment(
                    ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
                    ("DEMASPEECH_MIRROR_BEARER_TOKEN", "super-secret-value")));

            await Program.RunAsync(context);

            Assert.DoesNotContain("super-secret-value", writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that <c>--validate</c> with a <c>--results</c> file of an unsupported extension
    ///     reports the error and makes <c>Main</c> return a non-zero exit code.
    /// </summary>
    [Fact]
    public async Task Program_Main_ValidateWithUnsupportedResultsExtension_ReturnsNonZeroExitCode()
    {
        // Arrange
        var resultsFile = Path.Join(Path.GetTempPath(), $"speech_cli_results_{Guid.NewGuid():N}.txt");
        var originalError = Console.Error;
        var originalOut = Console.Out;
        try
        {
            using var errWriter = new StringWriter();
            using var outWriter = new StringWriter();
            Console.SetError(errWriter);
            Console.SetOut(outWriter);

            // Act
            var result = await Program.Main(["--validate", "--results", resultsFile]);

            // Assert
            Assert.Equal(1, result);
            Assert.Contains("Unsupported results file format", errWriter.ToString());
            Assert.False(File.Exists(resultsFile));
        }
        finally
        {
            Console.SetError(originalError);
            Console.SetOut(originalOut);
            File.Delete(resultsFile);
        }
    }

    /// <summary>
    ///     Test that <c>--validate</c> with a <c>--results</c> path that cannot be written reports
    ///     the error and makes <c>Main</c> return a non-zero exit code.
    /// </summary>
    [Fact]
    public async Task Program_Main_ValidateWithUnwritableResultsPath_ReturnsNonZeroExitCode()
    {
        // Arrange: the parent directory does not exist, so the write fails
        var resultsFile = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "results.trx");
        var originalError = Console.Error;
        var originalOut = Console.Out;
        try
        {
            using var errWriter = new StringWriter();
            using var outWriter = new StringWriter();
            Console.SetError(errWriter);
            Console.SetOut(outWriter);

            // Act
            var result = await Program.Main(["--validate", "--results", resultsFile]);

            // Assert
            Assert.Equal(1, result);
            Assert.NotEqual(string.Empty, errWriter.ToString());
        }
        finally
        {
            Console.SetError(originalError);
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that <c>--validate</c> with a <c>.xml</c> results file writes a JUnit report and exits successfully.
    /// </summary>
    [Fact]
    public async Task Program_Main_ValidateWithXmlResults_WritesJUnitFile()
    {
        // Arrange
        var resultsFile = Path.Join(Path.GetTempPath(), $"speech_cli_results_{Guid.NewGuid():N}.xml");
        var originalOut = Console.Out;
        try
        {
            using var outWriter = new StringWriter();
            Console.SetOut(outWriter);

            // Act
            var result = await Program.Main(["--validate", "--results", resultsFile]);

            // Assert
            Assert.Equal(0, result);
            Assert.True(File.Exists(resultsFile), "Results file was not created");
            Assert.Contains("<testsuite", await File.ReadAllTextAsync(resultsFile, TestContext.Current.CancellationToken));
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(resultsFile);
        }
    }
}
