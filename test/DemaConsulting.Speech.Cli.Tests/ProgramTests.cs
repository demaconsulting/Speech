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
///     Unit tests for the Program class.
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
    ///     <c>--silent</c> even though <see cref="Cli.Context.Create(string[])"/> itself threw before a
    ///     <see cref="Cli.Context"/> could be constructed to expose its own
    ///     <see cref="Cli.Context.Silent"/> flag.
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
    ///     Test that <c>--voices-dir</c> parses onto the context, anywhere relative to the subcommand,
    ///     when the directory exists.
    /// </summary>
    [Fact]
    public void Context_Create_WithExistingVoicesDir_ParsesOntoContext()
    {
        // Arrange
        var dir = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Act
            using var context = Context.Create(["list-voices", "--voices-dir", dir]);

            // Assert
            Assert.Equal(dir, context.VoicesDir);
            Assert.Empty(context.CommandArgs);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a <c>--voices-dir</c> naming a missing directory, or lacking a value, is rejected.
    /// </summary>
    [Fact]
    public void Context_Create_WithBadVoicesDir_ThrowsArgumentException()
    {
        var missing = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));

        Assert.Throws<ArgumentException>(() => Context.Create(["list-voices", "--voices-dir", missing]));
        Assert.Throws<ArgumentException>(() => Context.Create(["list-voices", "--voices-dir"]));
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
    ///     Test that <c>list-voices</c> and <c>voices</c> are registered in the dispatch table.
    /// </summary>
    [Fact]
    public void CommandDispatch_Find_VoiceCommands_AreRegistered()
    {
        Assert.NotNull(CommandDispatch.Find("list-voices"));
        Assert.NotNull(CommandDispatch.Find("voices"));
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

    /// <summary>
    ///     Test that <c>--mirror-url</c> and the other <c>--mirror-*</c> global options parse onto
    ///     the <see cref="Context"/> regardless of where they appear relative to the subcommand.
    /// </summary>
    [Fact]
    public void Context_Create_WithMirrorOptions_ParsesOntoContext()
    {
        // Act
        using var context = Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret"
        ]);

        // Assert
        Assert.Equal("https://mirror.example.com", context.MirrorUrl);
        Assert.Equal("alice", context.MirrorUser);
        Assert.Equal("secret", context.MirrorPassword);
        Assert.Null(context.MirrorBearerToken);
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> returns <see langword="null"/> when no
    ///     <c>--mirror-url</c> was supplied.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_NoMirrorUrl_ReturnsNull()
    {
        // Act
        using var context = Context.Create(["list-models"]);

        // Assert
        Assert.Null(context.CreateMirror());
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> builds a <see cref="DownloadMirror"/> whose
    ///     base URI matches a valid <c>--mirror-url</c>, with Basic credentials attached when a
    ///     matching <c>--mirror-user</c>/<c>--mirror-password</c> pair is supplied.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_ValidCredentialedMirror_ReturnsConfiguredMirror()
    {
        // Act
        using var context = Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret"
        ]);
        var mirror = context.CreateMirror();

        // Assert
        Assert.NotNull(mirror);
        Assert.Equal(new Uri("https://mirror.example.com"), mirror.BaseUri);
        Assert.Equal("alice", mirror.Credentials?.UserName);
        Assert.Equal("secret", mirror.Credentials?.Password);
        Assert.Null(mirror.BearerToken);
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> builds a bearer-token-authenticated
    ///     <see cref="DownloadMirror"/> when only <c>--mirror-bearer-token</c> is supplied.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_BearerTokenMirror_ReturnsConfiguredMirror()
    {
        // Act
        using var context = Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-bearer-token", "token-value"
        ]);
        var mirror = context.CreateMirror();

        // Assert
        Assert.NotNull(mirror);
        Assert.Null(mirror.Credentials);
        Assert.Equal("token-value", mirror.BearerToken);
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-url</c> that is not a
    ///     valid absolute URL.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_InvalidMirrorUrl_ThrowsArgumentException()
    {
        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create(["list-models", "--mirror-url", "not a url"]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-url</c> carrying a
    ///     query string, fragment, or user-info component, or using a scheme other than http/https.
    /// </summary>
    [Theory]
    [InlineData("https://mirror.example.com/base?token=abc")]
    [InlineData("https://mirror.example.com/base#fragment")]
    [InlineData("https://user:secret@mirror.example.com/base")]
    [InlineData("ftp://mirror.example.com/base")]
    public void Context_CreateMirror_UnsupportedMirrorUrlShape_ThrowsArgumentException(string mirrorUrl)
    {
        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create(["list-models", "--mirror-url", mirrorUrl]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects an empty or whitespace-only bearer token.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_WhitespaceBearerToken_ThrowsArgumentException()
    {
        // Act and Assert
        Assert.Throws<ArgumentException>(() => Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-bearer-token", "   "
        ]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects either credential form combined
    ///     with a plain <c>http</c> mirror on a non-loopback host, since the credential would be
    ///     sent in cleartext, but accepts the same combination on a loopback host.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_CredentialOverPlainHttp_RejectedUnlessLoopback()
    {
        // Act and Assert: non-loopback host, Basic credentials and bearer token
        Assert.Throws<ArgumentException>(() => Context.Create([
            "list-models",
            "--mirror-url", "http://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret"
        ]));

        Assert.Throws<ArgumentException>(() => Context.Create([
            "list-models",
            "--mirror-url", "http://mirror.example.com",
            "--mirror-bearer-token", "token-value"
        ]));

        // Loopback host is accepted
        using var loopback = Context.Create([
            "list-models",
            "--mirror-url", "http://localhost:8080",
            "--mirror-bearer-token", "token-value"
        ]);
        Assert.NotNull(loopback.CreateMirror());
    }

    /// <summary>
    ///     Test that mirror credentials supplied without <c>--mirror-url</c> are rejected even for
    ///     invocations that never build a model catalog, such as <c>--help</c>.
    /// </summary>
    [Fact]
    public void Context_Create_MirrorCredentialWithoutUrlAndHelp_ThrowsArgumentException()
    {
        // Act and Assert
        Assert.Throws<ArgumentException>(() => Context.Create(["--help", "--mirror-user", "alice"]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-user</c> supplied
    ///     without the matching <c>--mirror-password</c>.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_MirrorUserWithoutPassword_ThrowsArgumentException()
    {
        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice"
        ]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects supplying both HTTP Basic
    ///     credentials (<c>--mirror-user</c>/<c>--mirror-password</c>) and a bearer token
    ///     (<c>--mirror-bearer-token</c>) together, since the two authentication mechanisms are
    ///     mutually exclusive.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_BasicCredentialsAndBearerTokenTogether_ThrowsArgumentException()
    {
        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create([
            "list-models",
            "--mirror-url", "https://mirror.example.com",
            "--mirror-user", "alice",
            "--mirror-password", "secret",
            "--mirror-bearer-token", "token-value"
        ]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-user</c>/
    ///     <c>--mirror-password</c> pair supplied without <c>--mirror-url</c>, since the
    ///     credential would otherwise be silently ignored rather than applied anywhere.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_CredentialsWithoutMirrorUrl_ThrowsArgumentException()
    {
        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create([
            "list-models",
            "--mirror-user", "alice",
            "--mirror-password", "secret"
        ]));
    }

    /// <summary>
    ///     Test that <see cref="Context.CreateMirror"/> rejects a <c>--mirror-bearer-token</c>
    ///     supplied without <c>--mirror-url</c>, since the token would otherwise be silently
    ///     ignored rather than applied anywhere.
    /// </summary>
    [Fact]
    public void Context_CreateMirror_BearerTokenWithoutMirrorUrl_ThrowsArgumentException()
    {
        // Act and Assert (mirror options are validated while creating the context)
        Assert.Throws<ArgumentException>(() => Context.Create(["list-models", "--mirror-bearer-token", "token-value"]));
    }

    /// <summary>
    ///     Builds an environment reader over a fixed set of variables, so tests never mutate the process environment.
    /// </summary>
    private static Func<string, string?> FakeEnvironment(params (string Name, string Value)[] variables) =>
        name => variables.Where(v => v.Name == name).Select(v => v.Value).FirstOrDefault();

    /// <summary>
    ///     Test that <c>DEMASPEECH_MIRROR_URL</c> supplies the mirror URL when <c>--mirror-url</c> is absent.
    /// </summary>
    [Fact]
    public void Context_Create_EnvironmentMirrorUrl_UsedWhenOptionAbsent()
    {
        using var context = Context.Create(
            ["list-models"],
            FakeEnvironment(("DEMASPEECH_MIRROR_URL", "https://env.example.com")));

        Assert.Equal("https://env.example.com", context.MirrorUrl);
    }

    /// <summary>
    ///     Test that <c>--mirror-url</c> takes precedence over <c>DEMASPEECH_MIRROR_URL</c>.
    /// </summary>
    [Fact]
    public void Context_Create_MirrorUrlOption_OverridesEnvironment()
    {
        using var context = Context.Create(
            ["list-models", "--mirror-url", "https://cli.example.com"],
            FakeEnvironment(("DEMASPEECH_MIRROR_URL", "https://env.example.com")));

        Assert.Equal("https://cli.example.com", context.MirrorUrl);
    }

    /// <summary>
    ///     Test that the user and password variables supply the Basic credential when no credential option is given.
    /// </summary>
    [Fact]
    public void Context_Create_EnvironmentBasicCredentials_UsedWhenOptionsAbsent()
    {
        using var context = Context.Create(
            ["list-models"],
            FakeEnvironment(
                ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
                ("DEMASPEECH_MIRROR_USER", "alice"),
                ("DEMASPEECH_MIRROR_PASSWORD", "env-secret")));

        var mirror = context.CreateMirror();
        Assert.NotNull(mirror);
        Assert.Equal("alice", mirror.Credentials?.UserName);
        Assert.Equal("env-secret", mirror.Credentials?.Password);
    }

    /// <summary>
    ///     Test that <c>DEMASPEECH_MIRROR_BEARER_TOKEN</c> supplies the bearer token when no credential option is given.
    /// </summary>
    [Fact]
    public void Context_Create_EnvironmentBearerToken_UsedWhenOptionsAbsent()
    {
        using var context = Context.Create(
            ["list-models"],
            FakeEnvironment(
                ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
                ("DEMASPEECH_MIRROR_BEARER_TOKEN", "env-token")));

        var mirror = context.CreateMirror();
        Assert.NotNull(mirror);
        Assert.Equal("env-token", mirror.BearerToken);
    }

    /// <summary>
    ///     Test that any credential option disables every environment credential, so the two sources never mix.
    /// </summary>
    [Fact]
    public void Context_Create_CredentialOption_DisablesEnvironmentCredentials()
    {
        using var context = Context.Create(
            ["list-models", "--mirror-bearer-token", "cli-token"],
            FakeEnvironment(
                ("DEMASPEECH_MIRROR_URL", "https://env.example.com"),
                ("DEMASPEECH_MIRROR_USER", "alice"),
                ("DEMASPEECH_MIRROR_PASSWORD", "env-secret")));

        var mirror = context.CreateMirror();
        Assert.NotNull(mirror);
        Assert.Null(mirror.Credentials);
        Assert.Equal("cli-token", mirror.BearerToken);
    }

    /// <summary>
    ///     Test that an empty environment variable is treated as unset.
    /// </summary>
    [Fact]
    public void Context_Create_EmptyEnvironmentVariables_TreatedAsUnset()
    {
        using var context = Context.Create(
            ["list-models"],
            FakeEnvironment(
                ("DEMASPEECH_MIRROR_URL", ""),
                ("DEMASPEECH_MIRROR_USER", ""),
                ("DEMASPEECH_MIRROR_BEARER_TOKEN", "")));

        Assert.Null(context.CreateMirror());
    }

    /// <summary>
    ///     Test that environment credentials without any mirror URL are rejected, without echoing their values.
    /// </summary>
    [Fact]
    public void Context_Create_EnvironmentCredentialWithoutUrl_ThrowsWithoutEchoingSecret()
    {
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(
            ["list-models"],
            FakeEnvironment(("DEMASPEECH_MIRROR_BEARER_TOKEN", "super-secret-value"))));

        Assert.DoesNotContain("super-secret-value", exception.Message);
    }

    /// <summary>
    ///     Test that an invalid mirror URL error does not echo the rejected value.
    /// </summary>
    [Fact]
    public void Context_Create_InvalidMirrorUrl_ErrorDoesNotEchoValue()
    {
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(
            ["list-models"],
            FakeEnvironment(("DEMASPEECH_MIRROR_URL", "not a url secret-value"))));

        Assert.DoesNotContain("secret-value", exception.Message);
    }

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
    ///     Test that <c>--depth</c> accepts every value from 1 through 6 and stores it on the context.
    /// </summary>
    [Theory]
    [InlineData("1", 1)]
    [InlineData("6", 6)]
    public void Context_Create_DepthWithinRange_ParsesOntoContext(string value, int expected)
    {
        // Act
        using var context = Context.Create(["--validate", "--depth", value]);

        // Assert
        Assert.Equal(expected, context.HeadingDepth);
    }

    /// <summary>
    ///     Test that <c>--depth</c> rejects a value outside 1-6, a non-numeric value, and a missing value.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("7")]
    [InlineData("-1")]
    [InlineData("deep")]
    public void Context_Create_DepthOutOfRangeOrNonNumeric_ThrowsArgumentException(string value)
    {
        // Act and Assert
        var exception = Assert.Throws<ArgumentException>(() => Context.Create(["--validate", "--depth", value]));
        Assert.Contains("--depth", exception.Message);
    }

    /// <summary>
    ///     Test that <c>--depth</c> without a value is rejected.
    /// </summary>
    [Fact]
    public void Context_Create_DepthWithoutValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Context.Create(["--validate", "--depth"]));
    }

    /// <summary>
    ///     Test that <c>--verbose</c> and its alias <c>--diagnostics</c> enable diagnostics output,
    ///     and that diagnostics stay off by default.
    /// </summary>
    [Theory]
    [InlineData("--verbose", true)]
    [InlineData("--diagnostics", true)]
    [InlineData("", false)]
    public void Context_Create_VerboseOrDiagnosticsFlag_SetsDiagnostics(string flag, bool expected)
    {
        // Arrange
        string[] args = flag.Length == 0 ? ["list-devices"] : [flag, "list-devices"];

        // Act
        using var context = Context.Create(args);

        // Assert
        Assert.Equal(expected, context.Diagnostics);
    }

    /// <summary>
    ///     Test that <c>--verbose</c> after the subcommand is still a global option and is not
    ///     collected into the subcommand's arguments.
    /// </summary>
    [Fact]
    public void Context_Create_VerboseAfterCommand_IsRecognizedAsGlobalOption()
    {
        using var context = Context.Create(["list-devices", "--verbose"]);

        Assert.True(context.Diagnostics);
        Assert.Empty(context.CommandArgs);
    }

    /// <summary>
    ///     Test that <c>--no-emotions</c> is a global option recognized before or after any
    ///     subcommand (not only <c>voices install</c>), and that it is off by default.
    /// </summary>
    [Fact]
    public void Context_Create_NoEmotionsOnNonVoicesCommand_IsRecognizedAsGlobalOption()
    {
        // Act
        using var before = Context.Create(["--no-emotions", "list-devices"]);
        using var after = Context.Create(["list-devices", "--no-emotions"]);
        using var absent = Context.Create(["list-devices"]);

        // Assert
        Assert.True(before.NoEmotions);
        Assert.True(after.NoEmotions);
        Assert.Empty(after.CommandArgs);
        Assert.False(absent.NoEmotions);
    }

    /// <summary>
    ///     Test that <c>--results</c> and its alias <c>--result</c> store the results file path,
    ///     and that a missing value is rejected.
    /// </summary>
    [Fact]
    public void Context_Create_ResultsOption_ParsesPathAndRejectsMissingValue()
    {
        // Act
        using var results = Context.Create(["--validate", "--results", "out.trx"]);
        using var alias = Context.Create(["--validate", "--result", "out.xml"]);

        // Assert
        Assert.Equal("out.trx", results.ResultsFile);
        Assert.Equal("out.xml", alias.ResultsFile);
        Assert.Throws<ArgumentException>(() => Context.Create(["--validate", "--results"]));
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
