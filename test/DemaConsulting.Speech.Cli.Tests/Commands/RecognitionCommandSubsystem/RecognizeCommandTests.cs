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

using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Cli.Cli;
using DemaConsulting.Speech.Cli.Commands.DeviceCommandsSubsystem;
using DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.Cli.Commands.RecognitionCommandSubsystem;
using DemaConsulting.Speech.Cli.Tests.Commands.DeviceCommandsSubsystem;
using DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using DemaConsulting.Speech.RecognitionSubsystem;

// xUnit1051 is suppressed for this file: these tests deliberately call the three-argument
// RecognizeCommand.RunAsync(Context, ICliModelCatalog, AudioDeviceFactory) seam, which owns the
// Ctrl+C cancellation wiring, rather than the CancellationToken overload that bypasses it.
#pragma warning disable xUnit1051

namespace DemaConsulting.Speech.Cli.Tests.Commands.RecognitionCommandSubsystem;

/// <summary>
///     Unit tests for <see cref="RecognizeCommand"/>, using <see cref="FakeCliModelCatalog"/>,
///     <see cref="FakeRecognitionSession"/>/<see cref="FakeSpeechRecognizerEngine"/>, and fake
///     audio device probes so every scenario runs deterministically with no real catalog, network
///     access, native engine, or audio hardware.
/// </summary>
[Collection("Sequential")]
public sealed class RecognizeCommandTests
{
    private static FakeCliModelCatalog CreateCatalogWithModel(
        string modelId = "model-1",
        SpeechModelState state = SpeechModelState.Downloaded,
        SpeechModelRole role = SpeechModelRole.Recognition,
        IReadOnlyList<ISpeechModelParameter>? parameters = null)
    {
        var catalog = new FakeCliModelCatalog();
        catalog.WithModel(new FakeSpeechModel(modelId, role: role, parameters: parameters), state);
        return catalog;
    }

    /// <summary>
    ///     Writes a minimal valid mono, 16-bit PCM RIFF/WAVE file with the given number of
    ///     all-zero sample frames, so a test can construct a real
    ///     <see cref="WavFileAudioCaptureDevice"/> without needing a shared binary test fixture.
    /// </summary>
    private static string WriteMinimalWavFile(int sampleFrameCount = 0, short channelCount = 1)
    {
        var path = Path.Join(Path.GetTempPath(), $"recognize-test-{Guid.NewGuid():N}.wav");
        using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
        {
            var dataByteCount = sampleFrameCount * 2;
            writer.Write("RIFF"u8);
            writer.Write(36 + dataByteCount);
            writer.Write("WAVE"u8);
            writer.Write("fmt "u8);
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write(channelCount);
            writer.Write(16000);
            writer.Write(32000 * channelCount);
            writer.Write((short)(2 * channelCount));
            writer.Write((short)16);
            writer.Write("data"u8);
            writer.Write(dataByteCount);
            for (var i = 0; i < sampleFrameCount; i++)
            {
                writer.Write((short)0);
            }
        }

        return path;
    }

    /// <summary>
    ///     Creates a <see cref="FakeRecognitionSession"/> wrapped in a <see cref="FakeSpeechRecognizerEngine"/>,
    ///     and wires the catalog's <see cref="FakeCliModelCatalog.CreateRecognizerEngineOverride"/>
    ///     to return the engine.
    /// </summary>
    private static (FakeRecognitionSession Session, FakeSpeechRecognizerEngine Engine) WireRecognizer(
        FakeCliModelCatalog catalog)
    {
        var session = new FakeRecognitionSession();
        var engine = new FakeSpeechRecognizerEngine(session);
        catalog.CreateRecognizerEngineOverride = (_, _, _) => Task.FromResult<ISpeechRecognizerEngine>(engine);
        return (session, engine);
    }

    // --- ParseArguments ---

    /// <summary>Test that --stt-model is required.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_MissingModel_ThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(["--input", "in.wav"]));
    }

    /// <summary>Test that --input parses into the options.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_InputFlag_ParsesInputPath()
    {
        // Act
        var options = RecognizeCommand.ParseArguments(["--stt-model", "model-1", "--input", "in.wav"]);

        // Assert
        Assert.Equal("model-1", options.ModelId);
        Assert.Equal("in.wav", options.InputPath);
        Assert.False(options.Mic);
    }

    /// <summary>Test that --mic parses as a flag.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_MicFlag_ParsesTrue()
    {
        // Act
        var options = RecognizeCommand.ParseArguments(["--stt-model", "model-1", "--mic"]);

        // Assert
        Assert.True(options.Mic);
    }

    /// <summary>Test that repeatable --stt-param tokens accumulate in order.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_RepeatedParamFlags_AccumulatesInOrder()
    {
        // Act
        var options = RecognizeCommand.ParseArguments(
            ["--stt-model", "model-1", "--mic", "--stt-param", "beam=2", "--stt-param", "lang=en"]);

        // Assert
        Assert.Equal([("beam", "2"), ("lang", "en")], options.RawParameters);
    }

    /// <summary>Test that --silence-timeout parses a positive number of seconds.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_SilenceTimeoutFlag_ParsesSeconds()
    {
        // Act
        var options = RecognizeCommand.ParseArguments(
            ["--stt-model", "model-1", "--mic", "--silence-timeout", "2.5"]);

        // Assert
        Assert.Equal(2.5, options.SilenceTimeoutSeconds);
    }

    /// <summary>Test that a non-positive --silence-timeout throws.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_NonPositiveSilenceTimeout_ThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(
            ["--stt-model", "model-1", "--mic", "--silence-timeout", "0"]));
    }

    /// <summary>Test that a malformed --silence-timeout throws.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_MalformedSilenceTimeout_ThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(
            ["--stt-model", "model-1", "--mic", "--silence-timeout", "not-a-number"]));
    }

    /// <summary>Test that --start-timeout parses a positive number of seconds.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_StartTimeoutFlag_ParsesSeconds()
    {
        // Act
        var options = RecognizeCommand.ParseArguments(
            ["--stt-model", "model-1", "--mic", "--silence-timeout", "5", "--start-timeout", "2.5"]);

        // Assert
        Assert.Equal(2.5, options.StartTimeoutSeconds);
    }

    /// <summary>Test that omitting --start-timeout defaults it to null.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_StartTimeoutOmitted_DefaultsToNull()
    {
        // Act
        var options = RecognizeCommand.ParseArguments(
            ["--stt-model", "model-1", "--mic", "--silence-timeout", "5"]);

        // Assert
        Assert.Null(options.StartTimeoutSeconds);
    }

    /// <summary>Test that a non-positive --start-timeout throws.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_NonPositiveStartTimeout_ThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(
            ["--stt-model", "model-1", "--mic", "--start-timeout", "0"]));
    }

    /// <summary>Test that a malformed --start-timeout throws.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_MalformedStartTimeout_ThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(
            ["--stt-model", "model-1", "--mic", "--start-timeout", "not-a-number"]));
    }

    /// <summary>
    ///     Test that <c>RecognizeCommand.RunAsync</c>'s mic-mode start-timeout default
    ///     resolves to a fixed 8 seconds when <c>--start-timeout</c> is omitted, independent of
    ///     whatever value <c>--silence-timeout</c> is given - it never borrows
    ///     <c>--silence-timeout</c>'s own value, proving the requirement's corrected wording.
    /// </summary>
    [Fact]
    public void RecognizeCommand_Run_StartTimeoutOmitted_ResolvesToFixedEightSecondDefault()
    {
        // Arrange
        var options = RecognizeCommand.ParseArguments(
            ["--stt-model", "model-1", "--mic", "--silence-timeout", "2.5"]);

        // Act
        var startTimeout = RecognizeCommand.ResolveStartTimeout(options);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(8), startTimeout);
        Assert.NotEqual(RecognizeCommand.ResolveSilenceTimeout(options), startTimeout);
    }

    /// <summary>
    ///     Test that <c>RecognizeCommand.RunAsync</c>'s mic-mode silence-timeout default
    ///     resolves to a fixed 5 seconds when <c>--silence-timeout</c> is omitted.
    /// </summary>
    [Fact]
    public void RecognizeCommand_Run_SilenceTimeoutOmitted_ResolvesToFixedFiveSecondDefault()
    {
        // Arrange
        var options = RecognizeCommand.ParseArguments(["--stt-model", "model-1", "--mic"]);

        // Act
        var silenceTimeout = RecognizeCommand.ResolveSilenceTimeout(options);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(5), silenceTimeout);
    }

    /// <summary>
    ///     Test that an explicit <c>--start-timeout</c> is resolved verbatim, overriding the fixed
    ///     default.
    /// </summary>
    [Fact]
    public void RecognizeCommand_Run_StartTimeoutGiven_ResolvesToGivenValue()
    {
        // Arrange
        var options = RecognizeCommand.ParseArguments(
            ["--stt-model", "model-1", "--mic", "--start-timeout", "2.5"]);

        // Act
        var startTimeout = RecognizeCommand.ResolveStartTimeout(options);

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(2.5), startTimeout);
    }

    /// <summary>Test that --interim and --final-only parse as flags.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_InterimAndFinalOnlyFlags_ParseTrue()
    {
        // Act
        var interimOptions = RecognizeCommand.ParseArguments(["--stt-model", "model-1", "--mic", "--interim"]);
        var finalOnlyOptions = RecognizeCommand.ParseArguments(["--stt-model", "model-1", "--mic", "--final-only"]);

        // Assert
        Assert.True(interimOptions.InterimOnly);
        Assert.False(interimOptions.FinalOnly);
        Assert.True(finalOnlyOptions.FinalOnly);
        Assert.False(finalOnlyOptions.InterimOnly);
    }

    /// <summary>Test that --output-text parses into the options.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_OutputFlag_ParsesOutputPath()
    {
        // Act
        var options = RecognizeCommand.ParseArguments(["--stt-model", "model-1", "--mic", "--output-text", "out.txt"]);

        // Assert
        Assert.Equal("out.txt", options.OutputPath);
    }

    /// <summary>Test that an unsupported argument throws.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_UnsupportedArgument_ThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(["--stt-model", "model-1", "--bogus"]));
    }

    /// <summary>Test that a flag missing its value throws.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_FlagMissingValue_ThrowsArgumentException()
    {
        // Arrange
        string[] args = ["--stt-model"];

        // Act
        var exception = Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(args));

        // Assert
        Assert.Contains("--stt-model requires a value", exception.Message);
    }

    /// <summary>
    ///     Regression test: a value-taking flag directly followed by another recognized option must
    ///     be reported as missing its value instead of swallowing that option as its value.
    /// </summary>
    [Theory]
    [InlineData("--stt-model")]
    [InlineData("--input")]
    [InlineData("--capture-device")]
    [InlineData("--silence-timeout")]
    [InlineData("--start-timeout")]
    [InlineData("--stt-param")]
    [InlineData("--output-text")]
    public void RecognizeCommand_ParseArguments_ValueFlagFollowedByRecognizedOption_ThrowsArgumentException(string flag)
    {
        // Arrange
        string[] args = [flag, "--mic"];

        // Act
        var exception = Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(args));

        // Assert
        Assert.Contains($"{flag} requires a value", exception.Message);
    }

    /// <summary>Regression test: a value-taking flag at the end of the arguments is reported as missing its value.</summary>
    [Theory]
    [InlineData("--input")]
    [InlineData("--capture-device")]
    [InlineData("--silence-timeout")]
    [InlineData("--start-timeout")]
    [InlineData("--stt-param")]
    [InlineData("--output-text")]
    public void RecognizeCommand_ParseArguments_ValueFlagAtEndOfArgs_ThrowsArgumentException(string flag)
    {
        // Arrange
        string[] args = ["--stt-model", "model-1", flag];

        // Act
        var exception = Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(args));

        // Assert
        Assert.Contains($"{flag} requires a value", exception.Message);
    }

    /// <summary>
    ///     Regression test: non-finite timeouts (NaN, Infinity) must be rejected like any other
    ///     non-positive/malformed number, since NaN compares false against every bound.
    /// </summary>
    [Theory]
    [InlineData("--silence-timeout", "NaN")]
    [InlineData("--silence-timeout", "Infinity")]
    [InlineData("--silence-timeout", "-Infinity")]
    [InlineData("--start-timeout", "NaN")]
    [InlineData("--start-timeout", "Infinity")]
    [InlineData("--start-timeout", "-Infinity")]
    public void RecognizeCommand_ParseArguments_NonFiniteTimeout_ThrowsArgumentException(string flag, string value)
    {
        // Arrange
        string[] args = ["--stt-model", "model-1", "--mic", flag, value];

        // Act
        var exception = Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(args));

        // Assert
        Assert.Contains(flag, exception.Message);
        Assert.Contains("positive number", exception.Message);
    }

    /// <summary>
    ///     Regression test: when cancellation lands during file-mode processing and the drain
    ///     <c>StopAsync</c> honors the canceled token, the command still completes gracefully
    ///     (session stopped via an uncancelable stop, no exception, disposal performed).
    /// </summary>
    [Fact]
    public async Task RecognizeCommand_Run_FileInputCanceledDuringStart_CompletesGracefully()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, engine) = WireRecognizer(catalog);
        session.ObserveStopCancellation = true;
        using var cts = new CancellationTokenSource();
        session.OnStart = _ => cts.Cancel();
        var wavPath = WriteMinimalWavFile(sampleFrameCount: 160);
        try
        {
            using var context = Context.Create(["recognize", "--stt-model", "model-1", "--input", wavPath]);

            // Act
            await RecognizeCommand.RunAsync(
                context, catalog, new AudioDeviceFactory(), cts.Token);

            // Assert
            Assert.Equal(2, session.StopCallCount);
            Assert.Equal(RecognitionSessionState.Stopped, session.State);
            Assert.Equal(1, session.DisposeCallCount);
            Assert.Equal(1, engine.DisposeCallCount);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    // --- Input-source mutual exclusion ---

    /// <summary>Test that supplying both --input and --mic throws.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_InputAndMicBothGiven_ThrowsArgumentException()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(["recognize", "--stt-model", "model-1", "--input", "in.wav", "--mic"]);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => RecognizeCommand.RunAsync(context, catalog, factory));
    }

    /// <summary>Test that neither --input nor --mic given throws.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_NoInputSource_ThrowsArgumentException()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(["recognize", "--stt-model", "model-1"]);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => RecognizeCommand.RunAsync(context, catalog, factory));
    }

    // --- --interim/--final-only mutual exclusion ---

    /// <summary>Test that supplying both --interim and --final-only throws.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_InterimAndFinalOnlyBothGiven_ThrowsArgumentException()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(
            ["recognize", "--stt-model", "model-1", "--mic", "--interim", "--final-only"]);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => RecognizeCommand.RunAsync(context, catalog, factory));
    }

    // --- Model resolution error paths ---

    /// <summary>Test that an unknown model id throws.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_UnknownModelId_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(["recognize", "--stt-model", "does-not-exist", "--mic"]);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => RecognizeCommand.RunAsync(context, catalog, factory));

        // Assert
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>Test that a wrong-role (synthesis) model throws.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_WrongRoleModel_ThrowsArgumentException()
    {
        // Arrange
        var catalog = CreateCatalogWithModel(role: SpeechModelRole.Synthesis);
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(["recognize", "--stt-model", "model-1", "--mic"]);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => RecognizeCommand.RunAsync(context, catalog, factory));

        // Assert
        Assert.Contains("model-1", exception.Message);
        Assert.Contains("list-models --role stt", exception.Message);
    }

    /// <summary>Test that a not-downloaded model throws an actionable "download" message.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_NotDownloadedModel_ThrowsArgumentExceptionWithDownloadHint()
    {
        // Arrange
        var catalog = CreateCatalogWithModel(state: SpeechModelState.NotDownloaded);
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(["recognize", "--stt-model", "model-1", "--mic"]);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => RecognizeCommand.RunAsync(context, catalog, factory));

        // Assert
        Assert.Contains("download model-1", exception.Message);
    }

    // --- --stt-param validation wiring ---

    /// <summary>Test that a valid --stt-param is forwarded to CreateRecognizerEngineAsync's parameterValues argument.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_ValidParam_ForwardsToCreateRecognizerEngine()
    {
        // Arrange
        var beamParameter = new NumericParameter(
            "beam", "Beam", "Beam width", new NumericParameterBounds(1.0, 10.0, 1.0, 4.0));
        var catalog = CreateCatalogWithModel(parameters: [beamParameter]);
        WireRecognizer(catalog);
        var wavPath = WriteMinimalWavFile();
        try
        {
            using var context = Context.Create(
                ["recognize", "--stt-model", "model-1", "--input", wavPath, "--stt-param", "beam=6"]);

            // Act
            await RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory());

            // Assert
            var parameterValues = Assert.Single(catalog.CreateRecognizerParameterValueCalls);
            Assert.NotNull(parameterValues);
            Assert.Equal(6.0, Assert.IsType<double>(parameterValues["beam"]));
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    /// <summary>Test that an invalid --stt-param value throws before any recognizer engine is created.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_InvalidParam_ThrowsArgumentException()
    {
        // Arrange
        var beamParameter = new NumericParameter(
            "beam", "Beam", "Beam width", new NumericParameterBounds(1.0, 10.0, 1.0, 4.0));
        var catalog = CreateCatalogWithModel(parameters: [beamParameter]);
        var wavPath = WriteMinimalWavFile();
        try
        {
            using var context = Context.Create(
                ["recognize", "--stt-model", "model-1", "--input", wavPath, "--stt-param", "beam=999"]);

            // Act / Assert
            await Assert.ThrowsAsync<ArgumentException>(() => RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory()));
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    // --- File-input explicit-drain stop flow ---

    /// <summary>Test that a key the recognition model does not declare is rejected before any engine is created.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_UnknownParamKey_ThrowsArgumentException()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var wavPath = WriteMinimalWavFile();
        try
        {
            using var context = Context.Create(
                ["recognize", "--stt-model", "model-1", "--input", wavPath, "--stt-param", "nope=1"]);

            // Act / Assert
            await Assert.ThrowsAsync<ArgumentException>(() => RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory()));
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    /// <summary>Test that a malformed --stt-param token (no key=value separator) is rejected.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_MalformedParamToken_ThrowsArgumentException()
    {
        // Act / Assert
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(
            ["--stt-model", "model-1", "--mic", "--stt-param", "missing-equals-sign"]));
    }

    /// <summary>
    ///     Test that file-input mode starts the session once and then explicitly drains it with a
    ///     single <c>StopAsync</c> call - no longer relying on a separate end-of-file event, since
    ///     <see cref="IRecognitionSession.StartAsync"/> itself now delivers the whole file before
    ///     returning - and disposes both the engine and the session exactly once.
    /// </summary>
    [Fact]
    public async Task RecognizeCommand_Run_FileInput_StartsAndStopsRecognizerViaExplicitDrain()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, engine) = WireRecognizer(catalog);
        var wavPath = WriteMinimalWavFile(sampleFrameCount: 160);
        try
        {
            using var context = Context.Create(["recognize", "--stt-model", "model-1", "--input", wavPath]);

            // Act
            await RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory());

            // Assert
            Assert.Equal(1, session.StartCallCount);
            Assert.Equal(1, session.StopCallCount);
            Assert.Equal(1, session.DisposeCallCount);
            Assert.Equal(1, engine.DisposeCallCount);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    /// <summary>
    ///     Test that file-input mode passes a real <see cref="WavFileAudioCaptureDevice"/> over
    ///     the given path into <see cref="ISpeechRecognizerEngine.CreateSessionAsync"/>.
    /// </summary>
    [Fact]
    public async Task RecognizeCommand_Run_FileInput_PassesWavFileCaptureDeviceToCreateSession()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (_, engine) = WireRecognizer(catalog);
        var wavPath = WriteMinimalWavFile();
        try
        {
            using var context = Context.Create(["recognize", "--stt-model", "model-1", "--input", wavPath]);

            // Act
            await RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory());

            // Assert
            Assert.IsType<WavFileAudioCaptureDevice>(engine.LastDevice);
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    // --- Input WAV file validation ---

    /// <summary>
    ///     Test that <c>--start-timeout</c> and <c>--silence-timeout</c> given together with <c>--input</c> are
    ///     accepted and have no effect: file mode never constructs the idle-timeout wrapper, so the recognizer is
    ///     started and drained through the same explicit path as without them and the run succeeds.
    /// </summary>
    [Fact]
    public async Task RecognizeCommand_Run_FileInputWithTimeouts_TimeoutsIgnoredAndRunSucceeds()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, _) = WireRecognizer(catalog);
        var wavPath = WriteMinimalWavFile(sampleFrameCount: 160);
        try
        {
            using var context = Context.Create(
                ["recognize", "--stt-model", "model-1", "--input", wavPath, "--start-timeout", "0.001", "--silence-timeout", "0.001"]);

            // Act
            await RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory());

            // Assert
            Assert.Equal(1, session.StartCallCount);
            Assert.Equal(1, session.StopCallCount);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    /// <summary>Test that <c>--input</c> naming a file that is not a WAV file is rejected with a clear error.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_InputFileIsNotWav_ThrowsInvalidOperationExceptionNamingFile()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, engine) = WireRecognizer(catalog);
        session.OnStart = _ => engine.LastDevice!.Start();
        var path = Path.Join(Path.GetTempPath(), $"recognize-test-{Guid.NewGuid():N}.wav");
        await File.WriteAllTextAsync(path, "this is not a RIFF file", TestContext.Current.CancellationToken);
        try
        {
            using var context = Context.Create(["recognize", "--stt-model", "model-1", "--input", path]);

            // Act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory()));

            // Assert
            Assert.Contains(path, ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Test that <c>--input</c> naming a stereo WAV file is rejected because only mono is supported.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_InputFileIsStereoWav_ThrowsInvalidOperationExceptionAboutMono()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, engine) = WireRecognizer(catalog);
        session.OnStart = _ => engine.LastDevice!.Start();
        var path = WriteMinimalWavFile(channelCount: 2);
        try
        {
            using var context = Context.Create(["recognize", "--stt-model", "model-1", "--input", path]);

            // Act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory()));

            // Assert
            Assert.Contains("mono", ex.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // --- --interim/--final-only/--output-text filtering and writing ---

    /// <summary>Test that, by default, both interim and final results print (interim overwritten, final settled).</summary>
    [Fact]
    public async Task RecognizeCommand_Run_DefaultVerbosity_PrintsBothInterimAndFinal()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, _) = WireRecognizer(catalog);
        session.OnStart = self =>
        {
            self.RaiseResult("hel", isFinal: false);
            self.RaiseResult("hello", isFinal: true);
        };
        var wavPath = WriteMinimalWavFile();
        var originalOut = Console.Out;
        using var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(["recognize", "--stt-model", "model-1", "--input", wavPath]);

            // Act
            await RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory());
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(wavPath);
        }

        // Assert
        var printed = writer.ToString();
        var physicalLines = printed.Split('\n');
        Assert.True(physicalLines.Length >= 2, $"Expected at least 2 physical lines, got: {printed}");

        var firstLineSegments = physicalLines[0].Split('\r');
        Assert.Contains("hel", firstLineSegments);
        Assert.Equal("hello", firstLineSegments[^1]);
        Assert.Equal("Recognition finished.", physicalLines[1]);
    }

    /// <summary>
    ///     Test that a shrinking interim sequence ("hello world" then "hello") does not leave
    ///     stale trailing characters from the longer prior write visible: the shorter interim
    ///     write is itself padded over the longest length seen so far, and the final settle pads
    ///     over that same maximum length rather than only the most recent (shorter) write.
    /// </summary>
    [Fact]
    public async Task RecognizeCommand_Run_ShrinkingInterimSequence_DoesNotLeaveStaleCharacters()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, _) = WireRecognizer(catalog);
        session.OnStart = self =>
        {
            self.RaiseResult("hello world", isFinal: false);
            self.RaiseResult("hello", isFinal: false);
            self.RaiseResult("hello", isFinal: true);
        };
        var wavPath = WriteMinimalWavFile();
        var originalOut = Console.Out;
        using var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(["recognize", "--stt-model", "model-1", "--input", wavPath]);

            // Act
            await RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory());
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(wavPath);
        }

        // Assert
        var printed = writer.ToString();
        var physicalLines = printed.Split('\n');
        Assert.True(physicalLines.Length >= 2, $"Expected at least 2 physical lines, got: {printed}");

        var firstLineSegments = physicalLines[0].Split('\r');

        // The longer interim write appears verbatim.
        Assert.Contains("hello world", firstLineSegments);

        // The shorter interim write ("hello") must have been padded to the longest length seen
        // so far ("hello world".Length == 11) before the cursor is repositioned, proving the fix
        // tracks the maximum pending length rather than only the most recent write's length.
        Assert.Contains("hello".PadRight("hello world".Length), firstLineSegments);

        // The final visible state of the line is exactly "hello" - no stale "world" characters
        // remain anywhere after the last write.
        Assert.Equal("hello", firstLineSegments[^1]);
        Assert.Equal("Recognition finished.", physicalLines[1]);
    }

    /// <summary>Test that --final-only suppresses interim console output.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_FinalOnly_SuppressesInterimConsoleOutput()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, _) = WireRecognizer(catalog);
        session.OnStart = self =>
        {
            self.RaiseResult("hel", isFinal: false);
            self.RaiseResult("hello", isFinal: true);
        };
        var wavPath = WriteMinimalWavFile();
        var originalOut = Console.Out;
        using var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(
                ["recognize", "--stt-model", "model-1", "--input", wavPath, "--final-only"]);

            // Act
            await RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory());
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(wavPath);
        }

        // Assert
        var printed = writer.ToString();
        var physicalLines = printed.Split('\n');
        Assert.Equal("hello", physicalLines[0]);
        Assert.Equal("Recognition finished.", physicalLines[1]);
        Assert.DoesNotContain('\r', printed);
    }

    /// <summary>Test that --interim suppresses final console output (settle line).</summary>
    [Fact]
    public async Task RecognizeCommand_Run_Interim_SuppressesFinalSettleConsoleOutput()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, _) = WireRecognizer(catalog);
        session.OnStart = self =>
        {
            self.RaiseResult("hel", isFinal: false);
            self.RaiseResult("hello", isFinal: true);
        };
        var wavPath = WriteMinimalWavFile();
        var originalOut = Console.Out;
        using var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(
                ["recognize", "--stt-model", "model-1", "--input", wavPath, "--interim"]);

            // Act
            await RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory());
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(wavPath);
        }

        // Assert
        var printed = writer.ToString();
        var physicalLines = printed.Split('\n');
        var firstLineSegments = physicalLines[0].Split('\r');
        Assert.Contains("hel", firstLineSegments);
        Assert.Equal("Recognition finished.", firstLineSegments[^1]);
        Assert.DoesNotContain("hello", printed);
    }

    /// <summary>Test that --output-text writes only final results, one per line, overwriting any prior file content.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_Output_WritesOnlyFinalResultsOverwritingPriorContent()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, _) = WireRecognizer(catalog);
        session.OnStart = self =>
        {
            self.RaiseResult("hel", isFinal: false);
            self.RaiseResult("hello", isFinal: true);
            self.RaiseResult("wor", isFinal: false);
            self.RaiseResult("world", isFinal: true);
        };
        var wavPath = WriteMinimalWavFile();
        var outputPath = Path.Join(Path.GetTempPath(), $"recognize-output-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(outputPath, "stale content that must be overwritten", TestContext.Current.CancellationToken);
        try
        {
            using var context = Context.Create(
                ["recognize", "--stt-model", "model-1", "--input", wavPath, "--output-text", outputPath]);

            // Act
            await RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory());

            // Assert
            var lines = await File.ReadAllLinesAsync(outputPath, TestContext.Current.CancellationToken);
            Assert.Equal(["hello", "world"], lines);
        }
        finally
        {
            File.Delete(wavPath);
            File.Delete(outputPath);
        }
    }

    // --- --capture-device error path ---

    /// <summary>Test that an unknown --capture-device throws before any recognizer engine is created.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_UnknownDevice_ThrowsArgumentException()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(
            ["recognize", "--stt-model", "model-1", "--mic", "--capture-device", "does-not-exist"]);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => RecognizeCommand.RunAsync(context, catalog, factory));

        // Assert
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>Test that no available capture device (mic mode, no --output-text/--input) throws InvalidOperationException.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_NoCaptureDeviceAvailable_ThrowsInvalidOperationException()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(["recognize", "--stt-model", "model-1", "--mic"]);

        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => RecognizeCommand.RunAsync(context, catalog, factory));
    }

    /// <summary>
    ///     Test that mic mode, given an available capture device, binds the session to that device,
    ///     starts it, streams the results to the console and ends on the silence timeout.
    /// </summary>
    [Fact]
    public async Task RecognizeCommand_Run_MicMode_StreamsResultsFromCaptureDevice()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, engine) = WireRecognizer(catalog);
        session.OnStart = self => self.RaiseResult("hello mic", isFinal: true);
        var probe = new FakeAudioCaptureDeviceProbe([new AudioDeviceDescription("Fake Mic", AudioDeviceDirection.Capture, 1, 16000)]);
        var source = new Commands.ConversationCommandSubsystem.FakeCaptureDeviceSource(probe);
        var originalOut = Console.Out;
        using var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(
                ["recognize", "--stt-model", "model-1", "--mic", "--silence-timeout", "0.2", "--start-timeout", "0.2"]);

            // Act
            await RecognizeCommand.RunAsync(context, catalog, source, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(1, session.StartCallCount);
        Assert.Equal(1, session.StopCallCount);
        Assert.NotNull(engine.LastDevice);
        Assert.IsNotType<WavFileAudioCaptureDevice>(engine.LastDevice);
        Assert.Contains("hello mic", writer.ToString());
    }

    // --- Disposal ordering ---

    /// <summary>Test that both the engine and the session are disposed exactly once after a successful file-input run.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_Success_DisposesEngineAndSessionOnce()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, engine) = WireRecognizer(catalog);
        var wavPath = WriteMinimalWavFile();
        try
        {
            using var context = Context.Create(["recognize", "--stt-model", "model-1", "--input", wavPath]);

            // Act
            await RecognizeCommand.RunAsync(context, catalog, new AudioDeviceFactory());

            // Assert
            Assert.Equal(1, session.DisposeCallCount);
            Assert.Equal(1, engine.DisposeCallCount);
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    // --- Null argument guards ---

    /// <summary>Test that a null context is rejected.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => RecognizeCommand.RunAsync(null!, new FakeCliModelCatalog(), new AudioDeviceFactory()));
    }

    /// <summary>Test that a null catalog is rejected.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_NullCatalog_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = Context.Create(["recognize", "--stt-model", "model-1", "--mic"]);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => RecognizeCommand.RunAsync(context, null!, new AudioDeviceFactory()));
    }

    /// <summary>Test that a null factory is rejected.</summary>
    [Fact]
    public async Task RecognizeCommand_Run_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = Context.Create(["recognize", "--stt-model", "model-1", "--mic"]);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => RecognizeCommand.RunAsync(context, new FakeCliModelCatalog(), null!));
    }

    /// <summary>Test that ParseArguments rejects a null args list.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_NullArgs_ThrowsArgumentNullException()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => RecognizeCommand.ParseArguments(null!));
    }
}
