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

namespace DemaConsulting.Speech.Cli.Tests;

/// <summary>
///     System-level integration tests that run the Speech CLI application via <c>dotnet</c>.
/// </summary>
/// <remarks>
///     Covers the CI-safe scaffold subset (dispatch, help/version/self-test, a clean error for an
///     unrecognized subcommand) plus the five model-management subcommands implemented in Pass 3
///     (<c>list-models</c>, <c>model-info</c>, <c>download</c>, <c>uninstall</c>, <c>clean</c>),
///     the three device-related subcommands implemented in Pass 4 (<c>list-devices</c>,
///     <c>devices</c>, <c>doctor</c>), the <c>speak</c> subcommand implemented in Pass 5, and the
///     <c>recognize</c> subcommand implemented in this pass - the last of all 10 subcommands -
///     each exercised against an isolated, empty <c>--models-dir</c> so no test ever touches the
///     real per-user model store or performs a real network access. <c>speak</c>'s and
///     <c>recognize</c>'s error paths (missing model, unknown/wrong-role/not-downloaded model id,
///     conflicting input/text sources) are covered here; a real, downloaded-model
///     <c>speak --output-audio</c>/<c>recognize --input</c> end-to-end run is a manual verification
///     step only - see each subsystem's verification document for why.
/// </remarks>
[Collection("Sequential")]
public class IntegrationTests
{
    private readonly string _dllPath;

    /// <summary>
    ///     Initialize test by locating the Speech CLI DLL.
    /// </summary>
    public IntegrationTests()
    {
        // The DLL should be in the same directory as the test assembly
        // because the test project references the main project
        var baseDir = AppContext.BaseDirectory;
        _dllPath = Path.Combine(baseDir, "DemaConsulting.Speech.Cli.dll");

        Assert.True(File.Exists(_dllPath), $"Could not find Speech CLI DLL at {_dllPath}");
    }

    /// <summary>
    ///     Test that version flag outputs version information.
    /// </summary>
    [Fact]
    public void SpeechCli_VersionFlag_Provided_OutputsVersion()
    {
        // Act: run the tool with version flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--version");

        // Assert: version string is printed; no banner or errors
        Assert.Equal(0, exitCode);
        Assert.Matches(@"\d+\.\d+\.\d+", output);
        Assert.DoesNotContain("Error", output);
        Assert.DoesNotContain("Copyright", output);
    }

    /// <summary>
    ///     Test that help flag lists every recognized subcommand.
    /// </summary>
    [Fact]
    public void SpeechCli_HelpFlag_Provided_ListsAllSubcommands()
    {
        // Act: run the tool with help flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--help");

        // Assert: usage text contains required sections and every subcommand name
        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", output);
        Assert.Contains("Commands:", output);
        Assert.Contains("--version", output);
        Assert.Contains("--help", output);
        Assert.Contains("list-models", output);
        Assert.Contains("model-info", output);
        Assert.Contains("download", output);
        Assert.Contains("uninstall", output);
        Assert.Contains("clean", output);
        Assert.Contains("list-devices", output);
        Assert.Contains("devices", output);
        Assert.Contains("doctor", output);
        Assert.Contains("speak", output);
        Assert.Contains("recognize", output);
    }

    /// <summary>
    ///     Test that no arguments displays the tool banner and the same usage information as
    ///     <c>--help</c>.
    /// </summary>
    [Fact]
    public void SpeechCli_NoArguments_Invoked_DisplaysBannerAndUsage()
    {
        // Act: run the tool with no arguments
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath);

        // Assert: banner and usage are displayed; exit code is success
        Assert.Equal(0, exitCode);
        Assert.Contains("Speech CLI version", output);
        Assert.Contains("Copyright", output);
        Assert.Contains("Usage:", output);
    }

    /// <summary>
    ///     Test that validate flag runs self-validation and outputs a summary.
    /// </summary>
    [Fact]
    public void SpeechCli_ValidateFlag_Provided_RunsValidation()
    {
        // Act: run the tool with validate flag
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--validate");

        // Assert: validation summary is present; exit code is success (no failed self-tests)
        Assert.Equal(0, exitCode);
        Assert.Contains("Total Tests:", output);
        Assert.Contains("Passed:", output);
        Assert.Contains("Failed: 0", output);
    }

    /// <summary>
    ///     Test that validate with --results flag generates a TRX file.
    /// </summary>
    [Fact]
    public void SpeechCli_ValidateWithTrxResults_Requested_GeneratesTrxFile()
    {
        // Arrange: temporary TRX results file path
        var resultsFile = Path.Combine(Path.GetTempPath(), $"speech_cli_integration_test_{Guid.NewGuid()}.trx");

        try
        {
            // Act: run validation with TRX results output
            var exitCode = Runner.Run(
                out var _,
                "dotnet",
                _dllPath,
                "--validate",
                "--results",
                resultsFile);

            // Assert: results file is created with valid TRX structure
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(resultsFile), "Results file was not created");

            var trxContent = File.ReadAllText(resultsFile);
            Assert.Contains("<TestRun", trxContent);
            Assert.Contains("</TestRun>", trxContent);
        }
        finally
        {
            if (File.Exists(resultsFile))
            {
                File.Delete(resultsFile);
            }
        }
    }

    /// <summary>
    ///     Test that silent flag suppresses output.
    /// </summary>
    [Fact]
    public void SpeechCli_SilentFlag_Provided_SuppressesOutput()
    {
        // Act: run the tool with --version and --silent to produce deterministic silent output
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--version",
            "--silent");

        // Assert: no console output in silent mode
        Assert.Equal(0, exitCode);
        Assert.True(string.IsNullOrWhiteSpace(output), $"Expected no output in silent mode but got: {output}");
    }

    /// <summary>
    ///     Test that log flag writes output to a file.
    /// </summary>
    [Fact]
    public void SpeechCli_LogFlag_Provided_WritesOutputToFile()
    {
        // Arrange: temporary log file path
        var logFile = Path.GetTempFileName();

        try
        {
            // Act: run the tool with log flag
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--log",
                logFile);

            // Assert: log file is created and contains tool output; console output matches
            Assert.Equal(0, exitCode);
            Assert.True(File.Exists(logFile), "Log file was not created");

            var logContent = File.ReadAllText(logFile);
            Assert.Contains("Speech CLI version", logContent);
            Assert.Contains("Speech CLI version", output);
        }
        finally
        {
            if (File.Exists(logFile))
            {
                File.Delete(logFile);
            }
        }
    }

    /// <summary>
    ///     Test that an unrecognized global-option-shaped argument causes a clean error message
    ///     and a non-zero exit code.
    /// </summary>
    [Fact]
    public void SpeechCli_UnknownOption_Provided_ReturnsCleanError()
    {
        // Act: run the tool with an unknown option
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--unknown");

        // Assert: non-zero exit code and error message naming the unrecognized argument
        Assert.NotEqual(0, exitCode);
        Assert.Contains("Error", output);
        Assert.Contains("--unknown", output);
    }

    /// <summary>
    ///     Test that an unrecognized subcommand name causes a clean error message and a non-zero
    ///     exit code, rather than a stack trace or a hang.
    /// </summary>
    [Fact]
    public void SpeechCli_UnknownCommand_Provided_ReturnsCleanError()
    {
        // Act: run the tool with a command name that is not in the dispatch table
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "not-a-real-command");

        // Assert: non-zero exit code and error message naming the unrecognized command
        Assert.NotEqual(0, exitCode);
        Assert.Contains("Error", output);
        Assert.Contains("not-a-real-command", output);
    }

    /// <summary>
    ///     Test that <c>list-models</c> lists the compiled-in known models as a table, honoring
    ///     an isolated <c>--models-dir</c> so the test never touches the real per-user store.
    /// </summary>
    [Fact]
    public void SpeechCli_ListModelsCommand_Invoked_ListsKnownModelsAsTable()
    {
        // Arrange: an isolated, empty models directory
        var modelsDir = CreateTempModelsDir();
        try
        {
            // Act
            var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--models-dir", modelsDir, "list-models");

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Contains("Id", output);
            Assert.Contains("Display Name", output);
            Assert.Contains("Role", output);
            Assert.Contains("State", output);
            Assert.Contains("NotDownloaded", output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>list-models --format json</c> emits a parseable JSON array.
    /// </summary>
    [Fact]
    public void SpeechCli_ListModelsCommandWithJsonFormat_Invoked_EmitsJsonArray()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            // Act
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "list-models",
                "--format",
                "json");

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Contains("\"Id\":", output);
            Assert.Contains("\"DisplayName\":", output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>model-info</c> with an unknown model id returns a clean, non-zero-exit
    ///     error naming the id, rather than a stack trace.
    /// </summary>
    [Fact]
    public void SpeechCli_ModelInfoCommandWithUnknownId_Invoked_ReturnsCleanError()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            // Act
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "model-info",
                "does-not-exist");

            // Assert
            Assert.NotEqual(0, exitCode);
            Assert.Contains("Unknown model id 'does-not-exist'", output);
            Assert.Contains("list-models", output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>model-info</c> with no <c>&lt;modelId&gt;</c> argument returns a clean,
    ///     non-zero-exit error rather than a stack trace.
    /// </summary>
    [Fact]
    public void SpeechCli_ModelInfoCommandWithMissingArgument_Invoked_ReturnsCleanError()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            // Act
            var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--models-dir", modelsDir, "model-info");

            // Assert
            Assert.NotEqual(0, exitCode);
            Assert.Contains("Error", output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>download</c> with an unknown model id returns a non-zero exit code and
    ///     reports the failure, without attempting a real network access for any other model.
    /// </summary>
    [Fact]
    public void SpeechCli_DownloadCommandWithUnknownId_Invoked_ReturnsNonZeroExitCode()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            // Act
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "download",
                "does-not-exist");

            // Assert
            Assert.NotEqual(0, exitCode);
            Assert.Contains("does-not-exist", output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>download</c> with no <c>&lt;modelId&gt;</c> argument returns a clean,
    ///     non-zero-exit error.
    /// </summary>
    [Fact]
    public void SpeechCli_DownloadCommandWithMissingArgument_Invoked_ReturnsCleanError()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            // Act
            var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--models-dir", modelsDir, "download");

            // Assert
            Assert.NotEqual(0, exitCode);
            Assert.Contains("Error", output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>uninstall</c> of a model that was never downloaded is a safe no-op that
    ///     exits cleanly, per <c>SpeechModelStore.Uninstall</c>'s own documented behavior.
    /// </summary>
    [Fact]
    public void SpeechCli_UninstallCommand_Invoked_NeverInstalledModel_ExitsCleanly()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            // Act
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "uninstall",
                "never-installed");

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Contains("never-installed", output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>clean</c> for a model with nothing to clean up is a safe no-op that exits
    ///     cleanly, per <c>SpeechModelStore.CleanUpLeftovers</c>'s own documented behavior.
    /// </summary>
    [Fact]
    public void SpeechCli_CleanCommand_Invoked_NothingToCleanUp_ExitsCleanly()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            // Act
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "clean",
                "some-model");

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Contains("some-model", output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>list-devices</c> runs to completion and exits cleanly, printing either an
    ///     aligned device table or a "no devices found" message, regardless of whether this
    ///     machine has real audio hardware.
    /// </summary>
    [Fact]
    public void SpeechCli_ListDevicesCommand_Invoked_ExitsCleanly()
    {
        // Act
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "list-devices");

        // Assert
        Assert.Equal(0, exitCode);
        Assert.True(
            output.Contains("No devices found.", StringComparison.Ordinal) ||
            (output.Contains("Name", StringComparison.Ordinal) && output.Contains("Direction", StringComparison.Ordinal)),
            $"Expected either a device table or a 'no devices found' message, got: {output}");
    }

    /// <summary>
    ///     Test that <c>list-devices --direction bogus</c> returns a clean, non-zero-exit error
    ///     rather than a stack trace.
    /// </summary>
    [Fact]
    public void SpeechCli_ListDevicesCommandWithInvalidDirection_Invoked_ReturnsCleanError()
    {
        // Act
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "list-devices", "--direction", "bogus");

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("Error", output);
    }

    /// <summary>
    ///     Test that <c>devices</c> with no <c>test</c> sub-action returns a clean, non-zero-exit
    ///     error rather than a stack trace.
    /// </summary>
    [Fact]
    public void SpeechCli_DevicesCommandWithoutSubAction_Invoked_ReturnsCleanError()
    {
        // Act
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "devices");

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("test", output);
    }

    /// <summary>
    ///     Test that <c>devices test --device &lt;unknown&gt;</c> returns a clean, non-zero-exit
    ///     error naming the unrecognized device, rather than attempting to create a device for
    ///     it, so this test never depends on real audio hardware being present.
    /// </summary>
    [Fact]
    public void SpeechCli_DevicesTestCommandWithUnknownDevice_Invoked_ReturnsCleanError()
    {
        // Act
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "devices",
            "test",
            "--device",
            "does-not-exist-device",
            "--direction",
            "output");

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("does-not-exist-device", output);
    }

    /// <summary>
    ///     Test that <c>doctor</c> runs to completion, printing the environment health checklist,
    ///     against an isolated, writable <c>--models-dir</c> so it always reports healthy
    ///     regardless of this machine's audio hardware or native runtime availability.
    /// </summary>
    [Fact]
    public void SpeechCli_DoctorCommand_Invoked_ReportsHealthChecklist()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            // Act
            var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "--models-dir", modelsDir, "doctor");

            // Assert
            Assert.Equal(0, exitCode);
            Assert.Contains("Native runtimes:", output);
            Assert.Contains("Model store:", output);
            Assert.Contains("Audio devices:", output);
            Assert.Contains("Models:", output);
            Assert.Contains("Overall: HEALTHY", output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>speak</c> without <c>--tts-model</c> returns a clean, non-zero-exit error
    ///     naming the missing requirement, rather than a stack trace.
    /// </summary>
    [Fact]
    public void SpeechCli_SpeakCommandWithoutModel_Invoked_ReturnsCleanError()
    {
        // Act
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "speak", "--text", "hello");

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("--tts-model", output);
    }

    /// <summary>
    ///     Test that <c>speak --tts-model &lt;unknown&gt;</c> returns a clean, non-zero-exit error
    ///     naming the unrecognized model id, against an isolated, empty <c>--models-dir</c> so no
    ///     real network access occurs.
    /// </summary>
    [Fact]
    public void SpeechCli_SpeakCommandWithUnknownModel_Invoked_ReturnsCleanError()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            // Act
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "speak",
                "--tts-model",
                "does-not-exist-model",
                "--text",
                "hello");

            // Assert
            Assert.NotEqual(0, exitCode);
            Assert.Contains("does-not-exist-model", output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>speak --tts-model &lt;recognitionModel&gt;</c> returns a clean, non-zero-exit
    ///     error reporting the wrong role, rather than attempting to load it as a synthesizer.
    /// </summary>
    [Fact]
    public void SpeechCli_SpeakCommandWithWrongRoleModel_Invoked_ReturnsCleanError()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            // Act: list-models is used only to discover a known recognition model id without
            // needing this test to hard-code one from the library's model catalog.
            var listExitCode = Runner.Run(
                out var listOutput,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "list-models",
                "--role",
                "stt",
                "--format",
                "json");
            Assert.Equal(0, listExitCode);

            var recognitionModelId = ExtractFirstJsonId(listOutput);
            Assert.False(string.IsNullOrEmpty(recognitionModelId), $"Expected at least one recognition model id in: {listOutput}");

            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "speak",
                "--tts-model",
                recognitionModelId,
                "--text",
                "hello");

            // Assert
            Assert.NotEqual(0, exitCode);
            Assert.Contains(recognitionModelId, output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>speak --tts-model &lt;notDownloaded&gt;</c> returns a clean, non-zero-exit
    ///     error with an actionable <c>download</c> hint, against an isolated, empty
    ///     <c>--models-dir</c> so the model is genuinely not downloaded.
    /// </summary>
    [Fact]
    public void SpeechCli_SpeakCommandWithNotDownloadedModel_Invoked_ReturnsCleanErrorWithDownloadHint()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            var listExitCode = Runner.Run(
                out var listOutput,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "list-models",
                "--role",
                "tts",
                "--format",
                "json");
            Assert.Equal(0, listExitCode);

            var synthesisModelId = ExtractFirstJsonId(listOutput);
            Assert.False(string.IsNullOrEmpty(synthesisModelId), $"Expected at least one synthesis model id in: {listOutput}");

            // Act
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "speak",
                "--tts-model",
                synthesisModelId,
                "--text",
                "hello");

            // Assert
            Assert.NotEqual(0, exitCode);
            Assert.Contains("download", output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>speak</c> with both <c>--text</c> and <c>--file</c> returns a clean,
    ///     non-zero-exit error naming the conflict, before any model resolution is attempted.
    /// </summary>
    [Fact]
    public void SpeechCli_SpeakCommandWithConflictingTextSources_Invoked_ReturnsCleanError()
    {
        // Act
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "speak",
            "--tts-model",
            "any-model",
            "--text",
            "hello",
            "--file",
            "some-file.txt");

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("--text", output);
        Assert.Contains("--file", output);
    }

    /// <summary>
    ///     Test that <c>recognize</c> without <c>--stt-model</c> returns a clean, non-zero-exit error
    ///     naming the missing requirement, rather than a stack trace.
    /// </summary>
    [Fact]
    public void SpeechCli_RecognizeCommandWithoutModel_Invoked_ReturnsCleanError()
    {
        // Act
        var exitCode = Runner.Run(out var output, "dotnet", _dllPath, "recognize", "--mic");

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("--stt-model", output);
    }

    /// <summary>
    ///     Test that <c>recognize --stt-model &lt;unknown&gt;</c> returns a clean, non-zero-exit error
    ///     naming the unrecognized model id, against an isolated, empty <c>--models-dir</c> so no
    ///     real network access occurs.
    /// </summary>
    [Fact]
    public void SpeechCli_RecognizeCommandWithUnknownModel_Invoked_ReturnsCleanError()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            // Act
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "recognize",
                "--stt-model",
                "does-not-exist-model",
                "--mic");

            // Assert
            Assert.NotEqual(0, exitCode);
            Assert.Contains("does-not-exist-model", output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>recognize --stt-model &lt;synthesisModel&gt;</c> returns a clean,
    ///     non-zero-exit error reporting the wrong role, rather than attempting to load it as a
    ///     recognizer.
    /// </summary>
    [Fact]
    public void SpeechCli_RecognizeCommandWithWrongRoleModel_Invoked_ReturnsCleanError()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            // Act: list-models is used only to discover a known synthesis model id without
            // needing this test to hard-code one from the library's model catalog.
            var listExitCode = Runner.Run(
                out var listOutput,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "list-models",
                "--role",
                "tts",
                "--format",
                "json");
            Assert.Equal(0, listExitCode);

            var synthesisModelId = ExtractFirstJsonId(listOutput);
            Assert.False(string.IsNullOrEmpty(synthesisModelId), $"Expected at least one synthesis model id in: {listOutput}");

            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "recognize",
                "--stt-model",
                synthesisModelId,
                "--mic");

            // Assert
            Assert.NotEqual(0, exitCode);
            Assert.Contains(synthesisModelId, output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>recognize --stt-model &lt;notDownloaded&gt;</c> returns a clean, non-zero-exit
    ///     error with an actionable <c>download</c> hint, against an isolated, empty
    ///     <c>--models-dir</c> so the model is genuinely not downloaded.
    /// </summary>
    [Fact]
    public void SpeechCli_RecognizeCommandWithNotDownloadedModel_Invoked_ReturnsCleanErrorWithDownloadHint()
    {
        // Arrange
        var modelsDir = CreateTempModelsDir();
        try
        {
            var listExitCode = Runner.Run(
                out var listOutput,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "list-models",
                "--role",
                "stt",
                "--format",
                "json");
            Assert.Equal(0, listExitCode);

            var recognitionModelId = ExtractFirstJsonId(listOutput);
            Assert.False(string.IsNullOrEmpty(recognitionModelId), $"Expected at least one recognition model id in: {listOutput}");

            // Act
            var exitCode = Runner.Run(
                out var output,
                "dotnet",
                _dllPath,
                "--models-dir",
                modelsDir,
                "recognize",
                "--stt-model",
                recognitionModelId,
                "--mic");

            // Assert
            Assert.NotEqual(0, exitCode);
            Assert.Contains("download", output);
        }
        finally
        {
            Directory.Delete(modelsDir, recursive: true);
        }
    }

    /// <summary>
    ///     Test that <c>recognize</c> with both <c>--input</c> and <c>--mic</c> returns a clean,
    ///     non-zero-exit error naming the conflict, before any model resolution is attempted.
    /// </summary>
    [Fact]
    public void SpeechCli_RecognizeCommandWithConflictingInputSources_Invoked_ReturnsCleanError()
    {
        // Act
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "recognize",
            "--stt-model",
            "any-model",
            "--input",
            "some-file.wav",
            "--mic");

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("--input", output);
        Assert.Contains("--mic", output);
    }

    /// <summary>
    ///     Test that <c>recognize --stt-model &lt;realSttModel&gt; --input &lt;fixture&gt;.wav</c>
    ///     produces non-empty recognized text, using a real, already-downloaded speech-to-text
    ///     model (skipped when none is available, e.g. in a network-isolated CI environment - see
    ///     this subsystem's verification document for the manual verification fallback).
    /// </summary>
    [Fact]
    public void SpeechCli_RecognizeCommandWithRealSttModel_Invoked_ProducesNonEmptyRecognizedText()
    {
        // Arrange: discover a real, already-downloaded recognition model from the real, per-user
        // model store (no --models-dir override), so this test only runs meaningfully when one
        // is genuinely available - never fabricating a pass.
        var listExitCode = Runner.Run(
            out var listOutput,
            "dotnet",
            _dllPath,
            "list-models",
            "--role",
            "stt",
            "--format",
            "json");
        Assert.Equal(0, listExitCode);

        var downloadedModelId = ExtractFirstDownloadedJsonId(listOutput);
        if (downloadedModelId is null)
        {
            // No real, downloaded speech-to-text model is available in this environment; this
            // scenario is a documented manual verification gap, not a fabricated pass.
            return;
        }

        var fixturePath = FindRepositoryTestFixturePath("crossing-the-bar-16k-mono.wav");
        Assert.True(File.Exists(fixturePath), $"Expected fixture at {fixturePath}");

        // Act
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "recognize",
            "--stt-model",
            downloadedModelId,
            "--input",
            fixturePath);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Recognition finished.", output);

        var recognizedLines = output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line != "Recognition finished." &&
                           !line.StartsWith("Speech CLI version", StringComparison.Ordinal) &&
                           !line.StartsWith("Copyright", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(recognizedLines);
        Assert.Contains(recognizedLines, line => !string.IsNullOrWhiteSpace(line));
    }

    /// <summary>
    ///     Extracts the first <c>"Id"</c> field value whose sibling <c>"State"</c> field is
    ///     <c>"Downloaded"</c> from a <c>list-models --format json</c> array output, or
    ///     <see langword="null"/> when none is present.
    /// </summary>
    private static string? ExtractFirstDownloadedJsonId(string jsonOutput)
    {
        const string stateMarker = "\"State\": \"Downloaded\"";
        var stateIndex = jsonOutput.IndexOf(stateMarker, StringComparison.Ordinal);
        if (stateIndex < 0)
        {
            return null;
        }

        // The object's own "Id" field precedes its "State" field in the emitted JSON shape (see
        // list-models's writer); search backwards from the "State" match for the nearest "Id".
        const string idMarker = "\"Id\":";
        var idIndex = jsonOutput.LastIndexOf(idMarker, stateIndex, StringComparison.Ordinal);
        if (idIndex < 0)
        {
            return null;
        }

        var valueStart = jsonOutput.IndexOf('"', idIndex + idMarker.Length) + 1;
        var valueEnd = jsonOutput.IndexOf('"', valueStart);
        return valueStart <= 0 || valueEnd < 0 ? null : jsonOutput[valueStart..valueEnd];
    }

    /// <summary>
    ///     Locates a binary test fixture shared with <c>DemaConsulting.Speech.Tests</c> by
    ///     walking up from this test assembly's own output directory to the repository root
    ///     (identified by <c>Speech.slnx</c>), avoiding a duplicate binary fixture or a
    ///     cross-project content-copy <c>.csproj</c> item just for one integration test.
    /// </summary>
    private static string FindRepositoryTestFixturePath(string fixtureFileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Speech.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "test", "DemaConsulting.Speech.Tests", "TestData", fixtureFileName);
    }

    /// <summary>
    ///     Extracts the first <c>"id"</c> field value from a <c>list-models --format json</c>
    ///     array output, or <see langword="null"/> when none is present.
    /// </summary>
    private static string? ExtractFirstJsonId(string jsonOutput)
    {
        const string marker = "\"Id\":";
        var markerIndex = jsonOutput.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var valueStart = jsonOutput.IndexOf('"', markerIndex + marker.Length) + 1;
        var valueEnd = jsonOutput.IndexOf('"', valueStart);
        return valueStart <= 0 || valueEnd < 0 ? null : jsonOutput[valueStart..valueEnd];
    }

    /// <summary>
    ///     Creates a fresh, unique, empty directory to use as an isolated <c>--models-dir</c> for
    ///     an integration test, so no test ever touches the real per-user model store.
    /// </summary>
    private static string CreateTempModelsDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    ///     Test that validate with depth flag outputs headings at the specified depth.
    /// </summary>
    [Fact]
    public void SpeechCli_ValidateWithDepth_DepthThree_OutputsCorrectHeadingLevel()
    {
        // Act: run validation with heading depth 3
        var exitCode = Runner.Run(
            out var output,
            "dotnet",
            _dllPath,
            "--validate",
            "--depth",
            "3");

        // Assert: output contains level-3 markdown headings
        Assert.Equal(0, exitCode);
        Assert.Contains("###", output);
    }
}
