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

namespace DemaConsulting.Speech.Cli.Tests.Commands.RecognitionCommandSubsystem;

/// <summary>
///     Unit tests for <see cref="RecognizeCommand"/>, using <see cref="FakeCliModelCatalog"/>,
///     <see cref="FakeSpeechRecognizer"/>, and fake audio device probes so every scenario runs
///     deterministically with no real catalog, network access, native engine, or audio hardware.
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
    private static string WriteMinimalWavFile(int sampleFrameCount = 0)
    {
        var path = Path.Combine(Path.GetTempPath(), $"recognize-test-{Guid.NewGuid():N}.wav");
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
            writer.Write((short)1); // mono
            writer.Write(16000);
            writer.Write(32000);
            writer.Write((short)2);
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

    // --- ParseArguments ---

    /// <summary>Test that --model is required.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_MissingModel_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(["--input", "in.wav"]));
    }

    /// <summary>Test that --input parses into the options.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_InputFlag_ParsesInputPath()
    {
        var options = RecognizeCommand.ParseArguments(["--model", "model-1", "--input", "in.wav"]);

        Assert.Equal("model-1", options.ModelId);
        Assert.Equal("in.wav", options.InputPath);
        Assert.False(options.Mic);
    }

    /// <summary>Test that --mic parses as a flag.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_MicFlag_ParsesTrue()
    {
        var options = RecognizeCommand.ParseArguments(["--model", "model-1", "--mic"]);

        Assert.True(options.Mic);
    }

    /// <summary>Test that repeatable --param tokens accumulate in order.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_RepeatedParamFlags_AccumulatesInOrder()
    {
        var options = RecognizeCommand.ParseArguments(
            ["--model", "model-1", "--mic", "--param", "beam=2", "--param", "lang=en"]);

        Assert.Equal([("beam", "2"), ("lang", "en")], options.RawParameters);
    }

    /// <summary>Test that --silence-timeout parses a positive number of seconds.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_SilenceTimeoutFlag_ParsesSeconds()
    {
        var options = RecognizeCommand.ParseArguments(
            ["--model", "model-1", "--mic", "--silence-timeout", "2.5"]);

        Assert.Equal(2.5, options.SilenceTimeoutSeconds);
    }

    /// <summary>Test that a non-positive --silence-timeout throws.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_NonPositiveSilenceTimeout_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(
            ["--model", "model-1", "--mic", "--silence-timeout", "0"]));
    }

    /// <summary>Test that a malformed --silence-timeout throws.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_MalformedSilenceTimeout_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(
            ["--model", "model-1", "--mic", "--silence-timeout", "not-a-number"]));
    }

    /// <summary>Test that --start-timeout parses a positive number of seconds.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_StartTimeoutFlag_ParsesSeconds()
    {
        var options = RecognizeCommand.ParseArguments(
            ["--model", "model-1", "--mic", "--silence-timeout", "5", "--start-timeout", "2.5"]);

        Assert.Equal(2.5, options.StartTimeoutSeconds);
    }

    /// <summary>Test that omitting --start-timeout defaults it to null.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_StartTimeoutOmitted_DefaultsToNull()
    {
        var options = RecognizeCommand.ParseArguments(
            ["--model", "model-1", "--mic", "--silence-timeout", "5"]);

        Assert.Null(options.StartTimeoutSeconds);
    }

    /// <summary>Test that a non-positive --start-timeout throws.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_NonPositiveStartTimeout_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(
            ["--model", "model-1", "--mic", "--start-timeout", "0"]));
    }

    /// <summary>Test that a malformed --start-timeout throws.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_MalformedStartTimeout_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(
            ["--model", "model-1", "--mic", "--start-timeout", "not-a-number"]));
    }

    /// <summary>Test that --interim and --final-only parse as flags.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_InterimAndFinalOnlyFlags_ParseTrue()
    {
        var interimOptions = RecognizeCommand.ParseArguments(["--model", "model-1", "--mic", "--interim"]);
        var finalOnlyOptions = RecognizeCommand.ParseArguments(["--model", "model-1", "--mic", "--final-only"]);

        Assert.True(interimOptions.InterimOnly);
        Assert.False(interimOptions.FinalOnly);
        Assert.True(finalOnlyOptions.FinalOnly);
        Assert.False(finalOnlyOptions.InterimOnly);
    }

    /// <summary>Test that --output parses into the options.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_OutputFlag_ParsesOutputPath()
    {
        var options = RecognizeCommand.ParseArguments(["--model", "model-1", "--mic", "--output", "out.txt"]);

        Assert.Equal("out.txt", options.OutputPath);
    }

    /// <summary>Test that an unsupported argument throws.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_UnsupportedArgument_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(["--model", "model-1", "--bogus"]));
    }

    /// <summary>Test that a flag missing its value throws.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_FlagMissingValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => RecognizeCommand.ParseArguments(["--model"]));
    }

    // --- Input-source mutual exclusion ---

    /// <summary>Test that supplying both --input and --mic throws.</summary>
    [Fact]
    public void RecognizeCommand_Run_InputAndMicBothGiven_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(["recognize", "--model", "model-1", "--input", "in.wav", "--mic"]);

        Assert.Throws<ArgumentException>(() => RecognizeCommand.Run(context, catalog, factory));
    }

    /// <summary>Test that neither --input nor --mic given throws.</summary>
    [Fact]
    public void RecognizeCommand_Run_NoInputSource_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(["recognize", "--model", "model-1"]);

        Assert.Throws<ArgumentException>(() => RecognizeCommand.Run(context, catalog, factory));
    }

    // --- --interim/--final-only mutual exclusion ---

    /// <summary>Test that supplying both --interim and --final-only throws.</summary>
    [Fact]
    public void RecognizeCommand_Run_InterimAndFinalOnlyBothGiven_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(
            ["recognize", "--model", "model-1", "--mic", "--interim", "--final-only"]);

        Assert.Throws<ArgumentException>(() => RecognizeCommand.Run(context, catalog, factory));
    }

    // --- Model resolution error paths ---

    /// <summary>Test that an unknown model id throws.</summary>
    [Fact]
    public void RecognizeCommand_Run_UnknownModelId_ThrowsArgumentException()
    {
        var catalog = new FakeCliModelCatalog();
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(["recognize", "--model", "does-not-exist", "--mic"]);

        var exception = Assert.Throws<ArgumentException>(() => RecognizeCommand.Run(context, catalog, factory));
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>Test that a wrong-role (synthesis) model throws.</summary>
    [Fact]
    public void RecognizeCommand_Run_WrongRoleModel_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModel(role: SpeechModelRole.Synthesis);
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(["recognize", "--model", "model-1", "--mic"]);

        var exception = Assert.Throws<ArgumentException>(() => RecognizeCommand.Run(context, catalog, factory));
        Assert.Contains("model-1", exception.Message);
    }

    /// <summary>Test that a not-downloaded model throws an actionable "download" message.</summary>
    [Fact]
    public void RecognizeCommand_Run_NotDownloadedModel_ThrowsArgumentExceptionWithDownloadHint()
    {
        var catalog = CreateCatalogWithModel(state: SpeechModelState.NotDownloaded);
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(["recognize", "--model", "model-1", "--mic"]);

        var exception = Assert.Throws<ArgumentException>(() => RecognizeCommand.Run(context, catalog, factory));
        Assert.Contains("download model-1", exception.Message);
    }

    // --- --param validation wiring ---

    /// <summary>Test that a valid --param is forwarded to CreateRecognizer's parameterValues argument.</summary>
    [Fact]
    public void RecognizeCommand_Run_ValidParam_ForwardsToCreateRecognizer()
    {
        var beamParameter = new NumericParameter(
            "beam", "Beam", "Beam width", new NumericParameterBounds(1.0, 10.0, 1.0, 4.0));
        var catalog = CreateCatalogWithModel(parameters: [beamParameter]);
        var recognizer = new FakeSpeechRecognizer();
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;
        var wavPath = WriteMinimalWavFile();
        try
        {
            using var context = Context.Create(
                ["recognize", "--model", "model-1", "--input", wavPath, "--param", "beam=6"]);

            RecognizeCommand.Run(context, catalog, new AudioDeviceFactory());

            var parameterValues = Assert.Single(catalog.CreateRecognizerParameterValueCalls);
            Assert.NotNull(parameterValues);
            Assert.Equal(6.0, Assert.IsType<double>(parameterValues["beam"]));
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    /// <summary>Test that an invalid --param value throws before any recognizer is created.</summary>
    [Fact]
    public void RecognizeCommand_Run_InvalidParam_ThrowsArgumentException()
    {
        var beamParameter = new NumericParameter(
            "beam", "Beam", "Beam width", new NumericParameterBounds(1.0, 10.0, 1.0, 4.0));
        var catalog = CreateCatalogWithModel(parameters: [beamParameter]);
        var wavPath = WriteMinimalWavFile();
        try
        {
            using var context = Context.Create(
                ["recognize", "--model", "model-1", "--input", wavPath, "--param", "beam=999"]);

            Assert.Throws<ArgumentException>(() => RecognizeCommand.Run(context, catalog, new AudioDeviceFactory()));
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    // --- File-input EOF-driven stop flow ---

    /// <summary>
    ///     Test that file-input mode drives a real <see cref="WavFileAudioCaptureDevice"/> to
    ///     completion, the recognizer is started and stopped exactly once (via the device's own
    ///     <c>EndOfFileReached</c>), and disposed exactly once, with no explicit wait needed.
    /// </summary>
    [Fact]
    public void RecognizeCommand_Run_FileInput_StartsAndStopsRecognizerViaEndOfFile()
    {
        var catalog = CreateCatalogWithModel();
        var recognizer = new FakeSpeechRecognizer();
        // The real recognizer's Start() drives the supplied capture device's own Start() to
        // completion before returning (see RecognizeCommand's remarks); simulate that here so
        // the device's own EndOfFileReached event - which RecognizeCommand subscribes
        // recognizer.Stop() to reentrantly - actually fires within this call, exactly as it
        // would with a real SherpaOnnxSpeechRecognizer and WavFileAudioCaptureDevice pair.
        catalog.CreateRecognizerOverride = (_, device, _) =>
        {
            recognizer.OnStart = _ => device.Start();
            return recognizer;
        };
        var wavPath = WriteMinimalWavFile(sampleFrameCount: 160);
        try
        {
            using var context = Context.Create(["recognize", "--model", "model-1", "--input", wavPath]);

            RecognizeCommand.Run(context, catalog, new AudioDeviceFactory());

            Assert.Equal(1, recognizer.StartCallCount);
            Assert.Equal(1, recognizer.StopCallCount);
            Assert.Equal(1, recognizer.DisposeCallCount);
            Assert.Equal(0, context.ExitCode);
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    /// <summary>
    ///     Test that file-input mode passes a real <see cref="WavFileAudioCaptureDevice"/> over
    ///     the given path into <see cref="ICliModelCatalog.CreateRecognizer"/>.
    /// </summary>
    [Fact]
    public void RecognizeCommand_Run_FileInput_PassesWavFileCaptureDeviceToCreateRecognizer()
    {
        var catalog = CreateCatalogWithModel();
        var recognizer = new FakeSpeechRecognizer();
        IAudioCaptureDevice? capturedDevice = null;
        catalog.CreateRecognizerOverride = (_, device, _) =>
        {
            capturedDevice = device;
            return recognizer;
        };
        var wavPath = WriteMinimalWavFile();
        try
        {
            using var context = Context.Create(["recognize", "--model", "model-1", "--input", wavPath]);

            RecognizeCommand.Run(context, catalog, new AudioDeviceFactory());

            Assert.IsType<WavFileAudioCaptureDevice>(capturedDevice);
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    // --- --interim/--final-only/--output filtering and writing ---

    /// <summary>Test that, by default, both interim and final results print (interim overwritten, final settled).</summary>
    [Fact]
    public void RecognizeCommand_Run_DefaultVerbosity_PrintsBothInterimAndFinal()
    {
        var catalog = CreateCatalogWithModel();
        var recognizer = new FakeSpeechRecognizer
        {
            OnStart = self =>
            {
                self.RaiseResult("hel", isFinal: false);
                self.RaiseResult("hello", isFinal: true);
            }
        };
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;
        var wavPath = WriteMinimalWavFile();
        var originalOut = Console.Out;
        var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(["recognize", "--model", "model-1", "--input", wavPath]);

            RecognizeCommand.Run(context, catalog, new AudioDeviceFactory());
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(wavPath);
        }

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
    public void RecognizeCommand_Run_ShrinkingInterimSequence_DoesNotLeaveStaleCharacters()
    {
        var catalog = CreateCatalogWithModel();
        var recognizer = new FakeSpeechRecognizer
        {
            OnStart = self =>
            {
                self.RaiseResult("hello world", isFinal: false);
                self.RaiseResult("hello", isFinal: false);
                self.RaiseResult("hello", isFinal: true);
            }
        };
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;
        var wavPath = WriteMinimalWavFile();
        var originalOut = Console.Out;
        var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(["recognize", "--model", "model-1", "--input", wavPath]);

            RecognizeCommand.Run(context, catalog, new AudioDeviceFactory());
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(wavPath);
        }

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
    public void RecognizeCommand_Run_FinalOnly_SuppressesInterimConsoleOutput()
    {
        var catalog = CreateCatalogWithModel();
        var recognizer = new FakeSpeechRecognizer
        {
            OnStart = self =>
            {
                self.RaiseResult("hel", isFinal: false);
                self.RaiseResult("hello", isFinal: true);
            }
        };
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;
        var wavPath = WriteMinimalWavFile();
        var originalOut = Console.Out;
        var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(
                ["recognize", "--model", "model-1", "--input", wavPath, "--final-only"]);

            RecognizeCommand.Run(context, catalog, new AudioDeviceFactory());
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(wavPath);
        }

        var printed = writer.ToString();
        var physicalLines = printed.Split('\n');
        Assert.Equal("hello", physicalLines[0]);
        Assert.Equal("Recognition finished.", physicalLines[1]);
        Assert.DoesNotContain('\r', printed);
    }

    /// <summary>Test that --interim suppresses final console output (settle line).</summary>
    [Fact]
    public void RecognizeCommand_Run_Interim_SuppressesFinalSettleConsoleOutput()
    {
        var catalog = CreateCatalogWithModel();
        var recognizer = new FakeSpeechRecognizer
        {
            OnStart = self =>
            {
                self.RaiseResult("hel", isFinal: false);
                self.RaiseResult("hello", isFinal: true);
            }
        };
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;
        var wavPath = WriteMinimalWavFile();
        var originalOut = Console.Out;
        var writer = new StringWriter { NewLine = "\n" };
        Console.SetOut(writer);
        try
        {
            using var context = Context.Create(
                ["recognize", "--model", "model-1", "--input", wavPath, "--interim"]);

            RecognizeCommand.Run(context, catalog, new AudioDeviceFactory());
        }
        finally
        {
            Console.SetOut(originalOut);
            File.Delete(wavPath);
        }

        var printed = writer.ToString();
        var physicalLines = printed.Split('\n');
        var firstLineSegments = physicalLines[0].Split('\r');
        Assert.Contains("hel", firstLineSegments);
        Assert.Equal("Recognition finished.", firstLineSegments[^1]);
        Assert.DoesNotContain("hello", printed);
    }

    /// <summary>Test that --output writes only final results, one per line, overwriting any prior file content.</summary>
    [Fact]
    public void RecognizeCommand_Run_Output_WritesOnlyFinalResultsOverwritingPriorContent()
    {
        var catalog = CreateCatalogWithModel();
        var recognizer = new FakeSpeechRecognizer
        {
            OnStart = self =>
            {
                self.RaiseResult("hel", isFinal: false);
                self.RaiseResult("hello", isFinal: true);
                self.RaiseResult("wor", isFinal: false);
                self.RaiseResult("world", isFinal: true);
            }
        };
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;
        var wavPath = WriteMinimalWavFile();
        var outputPath = Path.Combine(Path.GetTempPath(), $"recognize-output-{Guid.NewGuid():N}.txt");
        File.WriteAllText(outputPath, "stale content that must be overwritten");
        try
        {
            using var context = Context.Create(
                ["recognize", "--model", "model-1", "--input", wavPath, "--output", outputPath]);

            RecognizeCommand.Run(context, catalog, new AudioDeviceFactory());

            var lines = File.ReadAllLines(outputPath);
            Assert.Equal(["hello", "world"], lines);
        }
        finally
        {
            File.Delete(wavPath);
            File.Delete(outputPath);
        }
    }

    // --- --device error path ---

    /// <summary>Test that an unknown --device throws before any recognizer is created.</summary>
    [Fact]
    public void RecognizeCommand_Run_UnknownDevice_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(
            ["recognize", "--model", "model-1", "--mic", "--device", "does-not-exist"]);

        var exception = Assert.Throws<ArgumentException>(() => RecognizeCommand.Run(context, catalog, factory));
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>Test that no available capture device (mic mode, no --output/--input) throws InvalidOperationException.</summary>
    [Fact]
    public void RecognizeCommand_Run_NoCaptureDeviceAvailable_ThrowsInvalidOperationException()
    {
        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(captureProbe: new FakeAudioCaptureDeviceProbe());
        using var context = Context.Create(["recognize", "--model", "model-1", "--mic"]);

        Assert.Throws<InvalidOperationException>(() => RecognizeCommand.Run(context, catalog, factory));
    }

    // --- Disposal ordering ---

    /// <summary>Test that the recognizer is disposed exactly once after a successful file-input run.</summary>
    [Fact]
    public void RecognizeCommand_Run_Success_DisposesRecognizerOnce()
    {
        var catalog = CreateCatalogWithModel();
        var recognizer = new FakeSpeechRecognizer();
        catalog.CreateRecognizerOverride = (_, _, _) => recognizer;
        var wavPath = WriteMinimalWavFile();
        try
        {
            using var context = Context.Create(["recognize", "--model", "model-1", "--input", wavPath]);

            RecognizeCommand.Run(context, catalog, new AudioDeviceFactory());

            Assert.Equal(1, recognizer.DisposeCallCount);
        }
        finally
        {
            File.Delete(wavPath);
        }
    }

    // --- Null argument guards ---

    /// <summary>Test that a null context is rejected.</summary>
    [Fact]
    public void RecognizeCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => RecognizeCommand.Run(null!, new FakeCliModelCatalog(), new AudioDeviceFactory()));
    }

    /// <summary>Test that a null catalog is rejected.</summary>
    [Fact]
    public void RecognizeCommand_Run_NullCatalog_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["recognize", "--model", "model-1", "--mic"]);
        Assert.Throws<ArgumentNullException>(
            () => RecognizeCommand.Run(context, null!, new AudioDeviceFactory()));
    }

    /// <summary>Test that a null factory is rejected.</summary>
    [Fact]
    public void RecognizeCommand_Run_NullFactory_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["recognize", "--model", "model-1", "--mic"]);
        Assert.Throws<ArgumentNullException>(
            () => RecognizeCommand.Run(context, new FakeCliModelCatalog(), null!));
    }

    /// <summary>Test that ParseArguments rejects a null args list.</summary>
    [Fact]
    public void RecognizeCommand_ParseArguments_NullArgs_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RecognizeCommand.ParseArguments(null!));
    }
}
