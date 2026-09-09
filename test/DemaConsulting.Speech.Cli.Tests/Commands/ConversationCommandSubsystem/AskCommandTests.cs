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
using DemaConsulting.Speech.Cli.Commands.ConversationCommandSubsystem;
using DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.Cli.Tests.Commands.DeviceCommandsSubsystem;
using DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.Cli.Tests.Commands.RecognitionCommandSubsystem;
using DemaConsulting.Speech.Cli.Tests.Commands.SynthesisCommandSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.ConversationCommandSubsystem;

/// <summary>
///     Unit tests for <see cref="AskCommand"/>, using <see cref="FakeCliModelCatalog"/>,
///     <see cref="FakeSpeechSynthesizer"/>, <see cref="FakeSpeechRecognizer"/>, and fake audio
///     device probes/sources so every scenario runs deterministically with no real catalog,
///     network access, native engine, or PortAudio hardware. Both the playback-side and
///     capture-side device resolution reuse dedicated CLI-owned seams -
///     <see cref="FakePlaybackDeviceSource"/> (mirroring
///     <see cref="SynthesisCommandSubsystem.SpeakCommandTests"/>'s own precedent) and
///     <see cref="FakeCaptureDeviceSource"/> respectively - rather than a real
///     <see cref="AudioDeviceFactory"/>, so no test ever depends on
///     <c>PortAudioEnvironment.Shared.IsInitialized</c> being true (which it never is on headless
///     CI runners).
/// </summary>
[Collection("Sequential")]
public sealed class AskCommandTests
{
    private static readonly AudioDeviceDescription OutputDevice =
        new("Fake Speakers", AudioDeviceDirection.Playback, 2, 48000);

    private static readonly AudioDeviceDescription CaptureDevice =
        new("Fake Microphone", AudioDeviceDirection.Capture, 1, 16000);

    private static FakeCliModelCatalog CreateCatalogWithModels(
        string ttsModelId = "tts-model-1",
        SpeechModelState ttsState = SpeechModelState.Downloaded,
        SpeechModelRole ttsRole = SpeechModelRole.Synthesis,
        IReadOnlyList<ISpeechModelParameter>? ttsParameters = null,
        string sttModelId = "stt-model-1",
        SpeechModelState sttState = SpeechModelState.Downloaded,
        SpeechModelRole sttRole = SpeechModelRole.Recognition,
        IReadOnlyList<ISpeechModelParameter>? sttParameters = null)
    {
        var catalog = new FakeCliModelCatalog();
        catalog.WithModel(new FakeSpeechModel(ttsModelId, role: ttsRole, parameters: ttsParameters), ttsState);
        catalog.WithModel(new FakeSpeechModel(sttModelId, role: sttRole, parameters: sttParameters), sttState);
        return catalog;
    }

    private static FakePlaybackDeviceSource CreatePlaybackSource() =>
        new(new FakeAudioPlaybackDeviceProbe([OutputDevice]));

    private static FakeCaptureDeviceSource CreateCaptureSource() =>
        new(new FakeAudioCaptureDeviceProbe([CaptureDevice]));

    // --- ParseArguments ---

    /// <summary>Test that --tts-model is required.</summary>
    [Fact]
    public void AskCommand_ParseArguments_MissingTtsModel_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => AskCommand.ParseArguments(["--stt-model", "stt-1", "--text", "hi"]));
        Assert.Contains("--tts-model", exception.Message);
    }

    /// <summary>Test that --stt-model is required.</summary>
    [Fact]
    public void AskCommand_ParseArguments_MissingSttModel_ThrowsArgumentException()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => AskCommand.ParseArguments(["--tts-model", "tts-1", "--text", "hi"]));
        Assert.Contains("--stt-model", exception.Message);
    }

    /// <summary>Test that --text parses into the options.</summary>
    [Fact]
    public void AskCommand_ParseArguments_TextFlag_ParsesText()
    {
        var options = AskCommand.ParseArguments(
            ["--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hello world"]);

        Assert.Equal("tts-1", options.TtsModelId);
        Assert.Equal("stt-1", options.SttModelId);
        Assert.Equal("hello world", options.Text);
    }

    /// <summary>Test that repeatable --tts-param and --stt-param tokens each accumulate in order, independently.</summary>
    [Fact]
    public void AskCommand_ParseArguments_RepeatedParamFlags_AccumulateInOrderIndependently()
    {
        var options = AskCommand.ParseArguments(
            [
                "--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi",
                "--tts-param", "rate=1.2", "--stt-param", "beam=2",
                "--tts-param", "voice=bob", "--stt-param", "lang=en"
            ]);

        Assert.Equal([("rate", "1.2"), ("voice", "bob")], options.RawTtsParameters);
        Assert.Equal([("beam", "2"), ("lang", "en")], options.RawSttParameters);
    }

    /// <summary>Test that --silence-timeout and --start-timeout parse as positive doubles.</summary>
    [Fact]
    public void AskCommand_ParseArguments_TimeoutFlags_ParseAsPositiveDoubles()
    {
        var options = AskCommand.ParseArguments(
            [
                "--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi",
                "--silence-timeout", "1.5", "--start-timeout", "20"
            ]);

        Assert.Equal(1.5, options.SilenceTimeoutSeconds);
        Assert.Equal(20, options.StartTimeoutSeconds);
    }

    /// <summary>Test that a non-positive --silence-timeout throws.</summary>
    [Fact]
    public void AskCommand_ParseArguments_NonPositiveSilenceTimeout_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AskCommand.ParseArguments(
            ["--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi", "--silence-timeout", "0"]));
    }

    /// <summary>Test that --output-text parses into the options.</summary>
    [Fact]
    public void AskCommand_ParseArguments_OutputTextFlag_ParsesPath()
    {
        var options = AskCommand.ParseArguments(
            ["--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi", "--output-text", "out.txt"]);

        Assert.Equal("out.txt", options.OutputPath);
    }

    /// <summary>Test that --interim is rejected (not supported by ask).</summary>
    [Fact]
    public void AskCommand_ParseArguments_Interim_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AskCommand.ParseArguments(
            ["--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi", "--interim"]));
    }

    /// <summary>Test that --final-only is rejected (not supported by ask).</summary>
    [Fact]
    public void AskCommand_ParseArguments_FinalOnly_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AskCommand.ParseArguments(
            ["--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi", "--final-only"]));
    }

    /// <summary>Test that --no-tags is rejected (not supported by ask).</summary>
    [Fact]
    public void AskCommand_ParseArguments_NoTags_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AskCommand.ParseArguments(
            ["--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi", "--no-tags"]));
    }

    /// <summary>Test that --input is rejected (not supported by ask).</summary>
    [Fact]
    public void AskCommand_ParseArguments_Input_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AskCommand.ParseArguments(
            ["--tts-model", "tts-1", "--stt-model", "stt-1", "--input", "in.wav"]));
    }

    /// <summary>Test that --output-audio is rejected (not supported by ask).</summary>
    [Fact]
    public void AskCommand_ParseArguments_OutputAudio_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AskCommand.ParseArguments(
            ["--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi", "--output-audio", "out.wav"]));
    }

    /// <summary>Test that an unsupported argument throws.</summary>
    [Fact]
    public void AskCommand_ParseArguments_UnsupportedArgument_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AskCommand.ParseArguments(
            ["--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi", "--bogus"]));
    }

    /// <summary>Test that a flag missing its value throws.</summary>
    [Fact]
    public void AskCommand_ParseArguments_FlagMissingValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AskCommand.ParseArguments(["--tts-model"]));
    }

    // --- Text-source mutual exclusion ---

    /// <summary>Test that supplying both --text and --file throws.</summary>
    [Fact]
    public void AskCommand_Run_TextAndFileBothGiven_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModels();
        using var context = Context.Create(
            ["ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi", "--file", "in.txt"]);

        Assert.Throws<ArgumentException>(
            () => AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource()));
    }

    // --- Model validation ---

    /// <summary>Test that an unknown --tts-model id throws before any --stt-model resolution.</summary>
    [Fact]
    public void AskCommand_Run_UnknownTtsModelId_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModels();
        using var context = Context.Create(
            ["ask", "--tts-model", "does-not-exist", "--stt-model", "stt-model-1", "--text", "hi"]);

        var exception = Assert.Throws<ArgumentException>(
            () => AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource()));
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>Test that an unknown --stt-model id throws.</summary>
    [Fact]
    public void AskCommand_Run_UnknownSttModelId_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModels();
        using var context = Context.Create(
            ["ask", "--tts-model", "tts-model-1", "--stt-model", "does-not-exist", "--text", "hi"]);

        var exception = Assert.Throws<ArgumentException>(
            () => AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource()));
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>Test that a wrong-role --tts-model (a recognition model) throws.</summary>
    [Fact]
    public void AskCommand_Run_WrongRoleTtsModel_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModels(ttsRole: SpeechModelRole.Recognition);
        using var context = Context.Create(
            ["ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi"]);

        var exception = Assert.Throws<ArgumentException>(
            () => AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource()));
        Assert.Contains("synthesis model", exception.Message);
    }

    /// <summary>Test that a wrong-role --stt-model (a synthesis model) throws.</summary>
    [Fact]
    public void AskCommand_Run_WrongRoleSttModel_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModels(sttRole: SpeechModelRole.Synthesis);
        using var context = Context.Create(
            ["ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi"]);

        var exception = Assert.Throws<ArgumentException>(
            () => AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource()));
        Assert.Contains("recognition model", exception.Message);
    }

    /// <summary>Test that a not-downloaded --tts-model throws with a download hint.</summary>
    [Fact]
    public void AskCommand_Run_NotDownloadedTtsModel_ThrowsArgumentExceptionWithDownloadHint()
    {
        var catalog = CreateCatalogWithModels(ttsState: SpeechModelState.NotDownloaded);
        using var context = Context.Create(
            ["ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi"]);

        var exception = Assert.Throws<ArgumentException>(
            () => AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource()));
        Assert.Contains("download tts-model-1", exception.Message);
    }

    /// <summary>Test that a not-downloaded --stt-model throws with a download hint.</summary>
    [Fact]
    public void AskCommand_Run_NotDownloadedSttModel_ThrowsArgumentExceptionWithDownloadHint()
    {
        var catalog = CreateCatalogWithModels(sttState: SpeechModelState.NotDownloaded);
        using var context = Context.Create(
            ["ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi"]);

        var exception = Assert.Throws<ArgumentException>(
            () => AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource()));
        Assert.Contains("download stt-model-1", exception.Message);
    }

    // --- Device errors ---

    /// <summary>Test that an unknown --playback-device throws before any synthesizer is created.</summary>
    [Fact]
    public void AskCommand_Run_UnknownPlaybackDevice_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModels();
        using var context = Context.Create(
            [
                "ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi",
                "--playback-device", "does-not-exist"
            ]);

        var exception = Assert.Throws<ArgumentException>(
            () => AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource()));
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>Test that no available playback device throws InvalidOperationException.</summary>
    [Fact]
    public void AskCommand_Run_NoPlaybackDeviceAvailable_ThrowsInvalidOperationException()
    {
        var catalog = CreateCatalogWithModels();
        var deviceSource = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe());
        using var context = Context.Create(
            ["ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi"]);

        Assert.Throws<InvalidOperationException>(
            () => AskCommand.Run(context, catalog, deviceSource, CreateCaptureSource()));
    }

    /// <summary>Test that an unknown --capture-device throws before any recognizer is created.</summary>
    [Fact]
    public void AskCommand_Run_UnknownCaptureDevice_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModels();
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;
        var captureSource = new FakeCaptureDeviceSource(new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(
            [
                "ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi",
                "--capture-device", "does-not-exist"
            ]);

        var exception = Assert.Throws<ArgumentException>(
            () => AskCommand.Run(context, catalog, CreatePlaybackSource(), captureSource));
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>Test that no available capture device throws InvalidOperationException.</summary>
    [Fact]
    public void AskCommand_Run_NoCaptureDeviceAvailable_ThrowsInvalidOperationException()
    {
        var catalog = CreateCatalogWithModels();
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;
        var captureSource = new FakeCaptureDeviceSource(new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(
            ["ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi"]);

        Assert.Throws<InvalidOperationException>(
            () => AskCommand.Run(context, catalog, CreatePlaybackSource(), captureSource));
    }

    // --- Success path ---

    /// <summary>
    ///     Test that a successful run speaks the prompt, then listens and stops on the first final
    ///     result, printing the recognized text and running Start/Stop/Dispose exactly once for
    ///     both the synthesizer and the recognizer.
    /// </summary>
    [Fact]
    public void AskCommand_Run_Success_SpeaksThenListensAndPrintsFinalResult()
    {
        var catalog = CreateCatalogWithModels();
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;
        var recognizer = new FakeSpeechRecognizer
        {
            OnStart = self => self.RaiseResult("hello there", isFinal: true)
        };
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;

        var originalOut = Console.Out;
        var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(
                ["ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "How are you?"]);

            AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource());
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(["How are you?"], synthesizer.SpeakAsyncCalls);
        Assert.Equal(1, synthesizer.DisposeCallCount);
        Assert.Equal(1, recognizer.StartCallCount);
        Assert.Equal(1, recognizer.StopCallCount);
        Assert.Equal(1, recognizer.DisposeCallCount);

        var printed = writer.ToString();
        Assert.Contains("hello there", printed);
    }

    /// <summary>Test that --output-text writes the recognized text to a file instead of stdout.</summary>
    [Fact]
    public void AskCommand_Run_OutputText_WritesRecognizedTextToFile()
    {
        var catalog = CreateCatalogWithModels();
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;
        var recognizer = new FakeSpeechRecognizer
        {
            OnStart = self => self.RaiseResult("the reply", isFinal: true)
        };
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;

        var outputPath = Path.Combine(Path.GetTempPath(), $"ask-test-{Guid.NewGuid():N}.txt");
        try
        {
            using var context = Context.Create(
                [
                    "ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi",
                    "--output-text", outputPath
                ]);

            AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource());

            Assert.True(File.Exists(outputPath));
            Assert.Equal("the reply", File.ReadAllText(outputPath).TrimEnd());
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    /// <summary>Test that --tts-param and --stt-param each forward to their own model's CreateSynthesizer/CreateRecognizer call.</summary>
    [Fact]
    public void AskCommand_Run_ValidParams_ForwardToRespectiveCreateCalls()
    {
        var rateParameter = new NumericParameter(
            "rate", "Rate", "Speaking rate", new NumericParameterBounds(0.5, 2.0, 0.1, 1.0));
        var beamParameter = new NumericParameter(
            "beam", "Beam", "Beam width", new NumericParameterBounds(1.0, 10.0, 1.0, 4.0));
        var catalog = CreateCatalogWithModels(ttsParameters: [rateParameter], sttParameters: [beamParameter]);
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;
        var recognizer = new FakeSpeechRecognizer
        {
            OnStart = self => self.RaiseResult("ok", isFinal: true)
        };
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;

        using var context = Context.Create(
            [
                "ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi",
                "--tts-param", "rate=2.0", "--stt-param", "beam=4"
            ]);

        AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource());

        var ttsParameterValues = Assert.Single(catalog.CreateSynthesizerParameterValueCalls);
        Assert.NotNull(ttsParameterValues);
        Assert.Equal(2.0, Assert.IsType<double>(ttsParameterValues["rate"]));

        var sttParameterValues = Assert.Single(catalog.CreateRecognizerParameterValueCalls);
        Assert.NotNull(sttParameterValues);
        Assert.Equal(4.0, Assert.IsType<double>(sttParameterValues["beam"]));
    }

    /// <summary>Test that an invalid --tts-param value throws before any recognizer is touched.</summary>
    [Fact]
    public void AskCommand_Run_InvalidTtsParam_ThrowsArgumentException()
    {
        var rateParameter = new NumericParameter(
            "rate", "Rate", "Speaking rate", new NumericParameterBounds(0.5, 2.0, 0.1, 1.0));
        var catalog = CreateCatalogWithModels(ttsParameters: [rateParameter]);
        using var context = Context.Create(
            [
                "ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi",
                "--tts-param", "rate=100"
            ]);

        Assert.Throws<ArgumentException>(
            () => AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource()));
    }

    /// <summary>
    ///     Test that --silence-timeout ends the listen phase with an empty recognized text when
    ///     no final result ever arrives, using a real (small) wall-clock idle window so the
    ///     production <see cref="DemaConsulting.Speech.Cli.Commands.RecognitionCommandSubsystem.SilenceTimeoutRecognizerSession"/>
    ///     genuinely fires and calls <c>Stop()</c>, exactly as it would for a real reply that
    ///     never finishes.
    /// </summary>
    [Fact]
    public void AskCommand_Run_SilenceTimeoutWithNoFinalResult_EndsTurnWithEmptyText()
    {
        var catalog = CreateCatalogWithModels();
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;

        // OnStart synchronously raises only an interim (non-final) result, then never a final
        // one: the idle timer re-arms once on that event and then fires for real (a small but
        // real wall-clock delay), calling Stop() and ending the turn with no recognized text.
        var recognizer = new FakeSpeechRecognizer
        {
            OnStart = self => self.RaiseResult("still talking", isFinal: false)
        };
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;

        var outputPath = Path.Combine(Path.GetTempPath(), $"ask-test-{Guid.NewGuid():N}.txt");
        try
        {
            using var context = Context.Create(
                [
                    "ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi",
                    "--silence-timeout", "0.05", "--output-text", outputPath
                ]);

            AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource());

            Assert.Equal(1, recognizer.StopCallCount);
            Assert.True(File.Exists(outputPath));
            Assert.Equal(string.Empty, File.ReadAllText(outputPath));
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    // --- Cancellation ---

    /// <summary>Test that a canceled Phase-1 speak session is reported cleanly, and Phase 2 (listen) never runs.</summary>
    [Fact]
    public void AskCommand_Run_CanceledDuringSpeak_SkipsListenPhase()
    {
        var catalog = CreateCatalogWithModels();
        var synthesizer = new FakeSpeechSynthesizer { SpeakAsyncException = new OperationCanceledException() };
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;
        var recognizer = new FakeSpeechRecognizer();
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;

        using var context = Context.Create(
            ["ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi"]);

        AskCommand.Run(context, catalog, CreatePlaybackSource(), CreateCaptureSource());

        Assert.Equal(1, context.ExitCode);
        Assert.Equal(1, synthesizer.DisposeCallCount);
        Assert.Equal(0, recognizer.StartCallCount);
    }

    /// <summary>
    ///     Test that a genuine <c>Ctrl+C</c> landing mid-listen (simulated by canceling the same
    ///     <see cref="CancellationTokenSource"/> and setting the same <see cref="ManualResetEventSlim"/>
    ///     the real <see cref="Console.CancelKeyPress"/> handler uses, from within the fake
    ///     recognizer's <c>Start()</c> callback) is reported as a cancellation - via
    ///     <see cref="Context.WriteError"/> and a non-zero exit code - rather than silently
    ///     printed as an empty, successful result. Drives <see cref="AskCommand.RunAsync"/>
    ///     directly (rather than the public <c>Run</c> entry point) because there is no
    ///     supported way to raise a real <see cref="Console.CancelKeyPress"/> event from a test.
    /// </summary>
    [Fact]
    public async Task AskCommand_RunAsync_CtrlCDuringListen_ReportsCanceledAndDoesNotPrintText()
    {
        var catalog = CreateCatalogWithModels();
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;

        using var cancellationSource = new CancellationTokenSource();
        using var stopSignal = new ManualResetEventSlim(initialState: false);

        var recognizer = new FakeSpeechRecognizer
        {
            // Simulates the exact interleaving AskCommand's own onCancelKeyPress handler
            // produces when Ctrl+C lands during Phase 2: cancel the shared token, stop the
            // recognizer, then signal stopSignal - all without ever raising a final result.
            OnStart = self =>
            {
                cancellationSource.Cancel();
                self.Stop();
                stopSignal.Set();
            }
        };
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;

        var originalOut = Console.Out;
        var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(
                ["ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi"]);

            await AskCommand.RunAsync(
                context,
                catalog,
                CreatePlaybackSource(),
                CreateCaptureSource(),
                stopSignal,
                _ => { },
                cancellationSource.Token);

            Assert.Equal(1, context.ExitCode);
            Assert.Equal(string.Empty, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that the edge case where <c>Ctrl+C</c> lands so early that <c>stopSignal</c> is
    ///     already set before <c>Listen</c> even starts a recognizer is also reported as a
    ///     cancellation, not a silent empty success - covering the early-return path distinct
    ///     from the mid-listen path exercised by
    ///     <see cref="AskCommand_RunAsync_CtrlCDuringListen_ReportsCanceledAndDoesNotPrintText"/>.
    /// </summary>
    [Fact]
    public async Task AskCommand_RunAsync_CtrlCBeforeListenStarts_ReportsCanceledAndDoesNotPrintText()
    {
        var catalog = CreateCatalogWithModels();
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;
        var recognizer = new FakeSpeechRecognizer();
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;

        using var cancellationSource = new CancellationTokenSource();
        using var stopSignal = new ManualResetEventSlim(initialState: false);

        // Ctrl+C already landed (token canceled, stopSignal set) in the narrow window between
        // Phase 1 finishing successfully and Phase 2 starting, before RunAsync is even invoked.
        await cancellationSource.CancelAsync();
        stopSignal.Set();

        var originalOut = Console.Out;
        var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(
                ["ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi"]);

            await AskCommand.RunAsync(
                context,
                catalog,
                CreatePlaybackSource(),
                CreateCaptureSource(),
                stopSignal,
                _ => { },
                cancellationSource.Token);

            Assert.Equal(1, context.ExitCode);
            Assert.Equal(0, recognizer.StartCallCount);
            Assert.Equal(string.Empty, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that a legitimate empty result from a silence/start timeout (no <c>Ctrl+C</c>
    ///     involved) is still reported as success, not cancellation, distinguishing it from
    ///     <see cref="AskCommand_RunAsync_CtrlCDuringListen_ReportsCanceledAndDoesNotPrintText"/>.
    /// </summary>
    [Fact]
    public async Task AskCommand_RunAsync_SilenceTimeoutWithNoCtrlC_ReportsSuccessNotCanceled()
    {
        var catalog = CreateCatalogWithModels();
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;

        using var cancellationSource = new CancellationTokenSource();
        using var stopSignal = new ManualResetEventSlim(initialState: false);

        var recognizer = new FakeSpeechRecognizer
        {
            // No Ctrl+C involved: the timeout session (armed by --silence-timeout) is what sets
            // stopSignal here, exactly as the production TimedOut handler does.
            OnStart = _ => stopSignal.Set()
        };
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;

        var originalOut = Console.Out;
        var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(
                ["ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi"]);

            await AskCommand.RunAsync(
                context,
                catalog,
                CreatePlaybackSource(),
                CreateCaptureSource(),
                stopSignal,
                _ => { },
                cancellationSource.Token);

            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    /// <summary>
    ///     Test that a genuine <c>Ctrl+C</c> landing in the narrow window <em>after</em>
    ///     <c>Listen</c> has already unblocked with a legitimate final result
    ///     (<c>listenWasCanceled == false</c>) but <em>before</em> <c>RunAsync</c> writes the
    ///     recognized text is still reported as a cancellation - via <see cref="Context.WriteError"/>
    ///     and a non-zero exit code, with nothing printed - rather than silently succeeding.
    ///     Simulated by canceling the shared token from within the
    ///     <c>onRecognizerCreated(null)</c> callback: that call is the very last thing <c>Listen</c>
    ///     does (in its outermost <c>finally</c>) before returning control to <c>RunAsync</c>, so
    ///     canceling there reproduces the exact interleaving a real <see cref="Console.CancelKeyPress"/>
    ///     could produce in that gap, distinct from the mid-listen race exercised by
    ///     <see cref="AskCommand_RunAsync_CtrlCDuringListen_ReportsCanceledAndDoesNotPrintText"/>.
    /// </summary>
    [Fact]
    public async Task AskCommand_RunAsync_CtrlCImmediatelyAfterListenReturns_ReportsCanceledAndDoesNotPrintText()
    {
        var catalog = CreateCatalogWithModels();
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;

        using var cancellationSource = new CancellationTokenSource();
        using var stopSignal = new ManualResetEventSlim(initialState: false);

        var recognizer = new FakeSpeechRecognizer
        {
            // A legitimate final result, with no Ctrl+C involved yet: Listen() will observe
            // cancellationToken.IsCancellationRequested == false and return (text, false).
            OnStart = self =>
            {
                self.RaiseResult("hello", isFinal: true);
                stopSignal.Set();
            }
        };
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;

        var originalOut = Console.Out;
        var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(
                ["ask", "--tts-model", "tts-model-1", "--stt-model", "stt-model-1", "--text", "hi"]);

            await AskCommand.RunAsync(
                context,
                catalog,
                CreatePlaybackSource(),
                CreateCaptureSource(),
                stopSignal,
                createdRecognizer =>
                {
                    // Listen() calls onRecognizerCreated(null) as the very last step in its
                    // outermost finally block, immediately before returning control to
                    // RunAsync: canceling here simulates Ctrl+C landing in that exact gap.
                    if (createdRecognizer is null)
                    {
                        cancellationSource.Cancel();
                    }
                },
                cancellationSource.Token);

            Assert.Equal(1, context.ExitCode);
            Assert.Equal(string.Empty, writer.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    // --- Null argument guards ---

    /// <summary>Test that a null context is rejected.</summary>
    [Fact]
    public void AskCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => AskCommand.Run(null!, new FakeCliModelCatalog(), CreatePlaybackSource(), CreateCaptureSource()));
    }

    /// <summary>Test that ParseArguments rejects a null args list.</summary>
    [Fact]
    public void AskCommand_ParseArguments_NullArgs_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => AskCommand.ParseArguments(null!));
    }

    /// <summary>Test that a null catalog is rejected.</summary>
    [Fact]
    public void AskCommand_Run_NullCatalog_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["ask", "--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi"]);
        Assert.Throws<ArgumentNullException>(
            () => AskCommand.Run(context, null!, CreatePlaybackSource(), CreateCaptureSource()));
    }

    /// <summary>Test that a null device source is rejected.</summary>
    [Fact]
    public void AskCommand_Run_NullDeviceSource_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["ask", "--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi"]);
        Assert.Throws<ArgumentNullException>(
            () => AskCommand.Run(context, new FakeCliModelCatalog(), null!, CreateCaptureSource()));
    }

    /// <summary>Test that a null capture-device source is rejected.</summary>
    [Fact]
    public void AskCommand_Run_NullCaptureSource_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["ask", "--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi"]);
        Assert.Throws<ArgumentNullException>(
            () => AskCommand.Run(context, new FakeCliModelCatalog(), CreatePlaybackSource(), null!));
    }

    // --- RunAsync null argument guards ---
    //
    // RunAsync is internal (rather than private) specifically so unit tests can drive Phase 2's
    // Ctrl+C-during-listen cancellation race directly (see its XML doc remarks). Because it can
    // be invoked directly - bypassing Run(Context)'s own guards - it must independently reject
    // null dependencies with ArgumentNullException rather than a NullReferenceException.

    /// <summary>Test that a null context is rejected by RunAsync directly.</summary>
    [Fact]
    public async Task AskCommand_RunAsync_NullContext_ThrowsArgumentNullException()
    {
        using var cancellationSource = new CancellationTokenSource();
        using var stopSignal = new ManualResetEventSlim(initialState: false);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => AskCommand.RunAsync(
                null!,
                new FakeCliModelCatalog(),
                CreatePlaybackSource(),
                CreateCaptureSource(),
                stopSignal,
                _ => { },
                cancellationSource.Token));
    }

    /// <summary>Test that a null catalog is rejected by RunAsync directly.</summary>
    [Fact]
    public async Task AskCommand_RunAsync_NullCatalog_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["ask", "--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi"]);
        using var cancellationSource = new CancellationTokenSource();
        using var stopSignal = new ManualResetEventSlim(initialState: false);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => AskCommand.RunAsync(
                context,
                null!,
                CreatePlaybackSource(),
                CreateCaptureSource(),
                stopSignal,
                _ => { },
                cancellationSource.Token));
    }

    /// <summary>Test that a null device source is rejected by RunAsync directly.</summary>
    [Fact]
    public async Task AskCommand_RunAsync_NullDeviceSource_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["ask", "--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi"]);
        using var cancellationSource = new CancellationTokenSource();
        using var stopSignal = new ManualResetEventSlim(initialState: false);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => AskCommand.RunAsync(
                context,
                new FakeCliModelCatalog(),
                null!,
                CreateCaptureSource(),
                stopSignal,
                _ => { },
                cancellationSource.Token));
    }

    /// <summary>Test that a null capture-device source is rejected by RunAsync directly.</summary>
    [Fact]
    public async Task AskCommand_RunAsync_NullCaptureSource_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["ask", "--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi"]);
        using var cancellationSource = new CancellationTokenSource();
        using var stopSignal = new ManualResetEventSlim(initialState: false);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => AskCommand.RunAsync(
                context,
                new FakeCliModelCatalog(),
                CreatePlaybackSource(),
                null!,
                stopSignal,
                _ => { },
                cancellationSource.Token));
    }

    /// <summary>Test that a null stop signal is rejected by RunAsync directly.</summary>
    [Fact]
    public async Task AskCommand_RunAsync_NullStopSignal_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["ask", "--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi"]);
        using var cancellationSource = new CancellationTokenSource();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => AskCommand.RunAsync(
                context,
                new FakeCliModelCatalog(),
                CreatePlaybackSource(),
                CreateCaptureSource(),
                null!,
                _ => { },
                cancellationSource.Token));
    }

    /// <summary>Test that a null recognizer-created callback is rejected by RunAsync directly.</summary>
    [Fact]
    public async Task AskCommand_RunAsync_NullOnRecognizerCreated_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["ask", "--tts-model", "tts-1", "--stt-model", "stt-1", "--text", "hi"]);
        using var cancellationSource = new CancellationTokenSource();
        using var stopSignal = new ManualResetEventSlim(initialState: false);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => AskCommand.RunAsync(
                context,
                new FakeCliModelCatalog(),
                CreatePlaybackSource(),
                CreateCaptureSource(),
                stopSignal,
                null!,
                cancellationSource.Token));
    }
}
