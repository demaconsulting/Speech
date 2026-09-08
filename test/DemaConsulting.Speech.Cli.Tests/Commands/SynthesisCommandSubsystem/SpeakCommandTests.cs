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
using DemaConsulting.Speech.Cli.Commands.SynthesisCommandSubsystem;
using DemaConsulting.Speech.Cli.Tests.Commands.DeviceCommandsSubsystem;
using DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.SynthesisCommandSubsystem;

/// <summary>
///     Unit tests for <see cref="SpeakCommand"/>, using <see cref="FakeCliModelCatalog"/>,
///     <see cref="FakeSpeechSynthesizer"/>, and fake audio device probes so every scenario runs
///     deterministically with no real catalog, network access, native engine, or audio hardware.
/// </summary>
[Collection("Sequential")]
public sealed class SpeakCommandTests
{
    private static readonly AudioDeviceDescription OutputDevice =
        new("Fake Speakers", AudioDeviceDirection.Playback, 2, 48000);

    private static FakeCliModelCatalog CreateCatalogWithModel(
        string modelId = "model-1",
        SpeechModelState state = SpeechModelState.Downloaded,
        SpeechModelRole role = SpeechModelRole.Synthesis,
        IReadOnlyList<ISpeechModelParameter>? parameters = null)
    {
        var catalog = new FakeCliModelCatalog();
        catalog.WithModel(new FakeSpeechModel(modelId, role: role, parameters: parameters), state);
        return catalog;
    }

    // --- ParseArguments ---

    /// <summary>Test that --model is required.</summary>
    [Fact]
    public void SpeakCommand_ParseArguments_MissingModel_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => SpeakCommand.ParseArguments(["--text", "hello"]));
    }

    /// <summary>Test that --text parses into the options.</summary>
    [Fact]
    public void SpeakCommand_ParseArguments_TextFlag_ParsesText()
    {
        var options = SpeakCommand.ParseArguments(["--model", "model-1", "--text", "hello world"]);

        Assert.Equal("model-1", options.ModelId);
        Assert.Equal("hello world", options.Text);
    }

    /// <summary>Test that repeatable --param tokens accumulate in order.</summary>
    [Fact]
    public void SpeakCommand_ParseArguments_RepeatedParamFlags_AccumulatesInOrder()
    {
        var options = SpeakCommand.ParseArguments(
            ["--model", "model-1", "--text", "hi", "--param", "rate=1.2", "--param", "voice=bob"]);

        Assert.Equal([("rate", "1.2"), ("voice", "bob")], options.RawParameters);
    }

    /// <summary>Test that --no-tags parses as a flag.</summary>
    [Fact]
    public void SpeakCommand_ParseArguments_NoTagsFlag_ParsesTrue()
    {
        var options = SpeakCommand.ParseArguments(["--model", "model-1", "--text", "hi", "--no-tags"]);

        Assert.True(options.NoTags);
    }

    /// <summary>Test that an unsupported argument throws.</summary>
    [Fact]
    public void SpeakCommand_ParseArguments_UnsupportedArgument_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => SpeakCommand.ParseArguments(["--model", "model-1", "--bogus"]));
    }

    /// <summary>Test that a flag missing its value throws.</summary>
    [Fact]
    public void SpeakCommand_ParseArguments_FlagMissingValue_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => SpeakCommand.ParseArguments(["--model"]));
    }

    // --- Text-source mutual exclusion ---

    /// <summary>Test that supplying both --text and --file throws.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_TextAndFileBothGiven_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(playbackProbe: new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--model", "model-1", "--text", "hi", "--file", "in.txt"]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));
    }

    /// <summary>Test that neither --text nor --file given, with stdin not redirected, throws.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NoTextSourceAndStdinNotRedirected_ThrowsArgumentException()
    {
        // Skip when stdin actually is redirected (e.g. under some CI/test-runner harnesses),
        // since this test specifically covers the "no source available" branch.
        if (Console.IsInputRedirected)
        {
            return;
        }

        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(playbackProbe: new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--model", "model-1"]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));
    }

    // --- Model resolution error paths ---

    /// <summary>Test that an unknown model id throws.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_UnknownModelId_ThrowsArgumentException()
    {
        var catalog = new FakeCliModelCatalog();
        var factory = new AudioDeviceFactory(playbackProbe: new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--model", "does-not-exist", "--text", "hi"]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>Test that a wrong-role (recognition) model throws.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_WrongRoleModel_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModel(role: SpeechModelRole.Recognition);
        var factory = new AudioDeviceFactory(playbackProbe: new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--model", "model-1", "--text", "hi"]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));
        Assert.Contains("model-1", exception.Message);
    }

    /// <summary>Test that a not-downloaded model throws an actionable "download" message.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NotDownloadedModel_ThrowsArgumentExceptionWithDownloadHint()
    {
        var catalog = CreateCatalogWithModel(state: SpeechModelState.NotDownloaded);
        var factory = new AudioDeviceFactory(playbackProbe: new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--model", "model-1", "--text", "hi"]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));
        Assert.Contains("download model-1", exception.Message);
    }

    // --- --param validation wiring ---

    /// <summary>Test that a valid --param is forwarded to CreateSynthesizer's parameterValues argument.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_ValidParam_ForwardsToCreateSynthesizer()
    {
        var rateParameter = new NumericParameter(
            "rate", "Rate", "Speaking rate", new NumericParameterBounds(0.5, 2.0, 0.1, 1.0));
        var catalog = CreateCatalogWithModel(parameters: [rateParameter]);
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;
        var factory = new AudioDeviceFactory(playbackProbe: new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--model", "model-1", "--text", "hi", "--param", "rate=1.5"]);

        await SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None);

        var parameterValues = Assert.Single(catalog.CreateSynthesizerParameterValueCalls);
        Assert.NotNull(parameterValues);
        Assert.Equal(1.5, Assert.IsType<double>(parameterValues["rate"]));
    }

    /// <summary>Test that an invalid --param value throws before any synthesizer is created.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_InvalidParam_ThrowsArgumentException()
    {
        var rateParameter = new NumericParameter(
            "rate", "Rate", "Speaking rate", new NumericParameterBounds(0.5, 2.0, 0.1, 1.0));
        var catalog = CreateCatalogWithModel(parameters: [rateParameter]);
        var factory = new AudioDeviceFactory(playbackProbe: new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--model", "model-1", "--text", "hi", "--param", "rate=100"]);

        await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));
    }

    // --- --no-tags exact-string assertion ---

    /// <summary>Test that --no-tags strips a recognized tag, keeping only literal narration text.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NoTags_StripsRecognizedTagExactly()
    {
        var catalog = CreateCatalogWithModel();
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;
        var factory = new AudioDeviceFactory(playbackProbe: new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(
            ["speak", "--model", "model-1", "--text", "Hello [laughs] there", "--no-tags"]);

        await SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None);

        var spokenText = Assert.Single(synthesizer.SpeakAsyncCalls);
        Assert.Equal("Hello  there", spokenText);
    }

    /// <summary>Test that without --no-tags the original text (including tags) is passed unchanged.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_WithoutNoTags_PassesOriginalTextUnchanged()
    {
        var catalog = CreateCatalogWithModel();
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;
        var factory = new AudioDeviceFactory(playbackProbe: new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--model", "model-1", "--text", "Hello [laughs] there"]);

        await SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None);

        var spokenText = Assert.Single(synthesizer.SpeakAsyncCalls);
        Assert.Equal("Hello [laughs] there", spokenText);
    }

    // --- --output vs. device dispatch ---

    /// <summary>
    ///     Test that --output constructs a WavFileAudioPlaybackDevice sized from the model's
    ///     preferred format and never touches the AudioDeviceFactory's probes.
    /// </summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_Output_ConstructsWavFileDeviceFromPreferredFormat()
    {
        var catalog = CreateCatalogWithModel();
        catalog.GetPreferredAudioFormatOverride = _ => AudioFormat.Mono(22050);
        var synthesizer = new FakeSpeechSynthesizer();
        IAudioPlaybackDevice? capturedDevice = null;
        catalog.CreateSynthesizerOverride = (_, device, _) =>
        {
            capturedDevice = device;
            return synthesizer;
        };
        // A probe that throws if enumerated, proving --output never touches real device probes.
        var factory = new AudioDeviceFactory(playbackProbe: new ThrowingAudioPlaybackDeviceProbe());
        var outputPath = Path.Combine(Path.GetTempPath(), $"speak-test-{Guid.NewGuid():N}.wav");

        try
        {
            using var context = Context.Create(["speak", "--model", "model-1", "--text", "hi", "--output", outputPath]);

            await SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None);

            Assert.NotNull(capturedDevice);
            Assert.Equal(22050, capturedDevice.SampleRate);
            Assert.Equal(1, capturedDevice.ChannelCount);
            Assert.True(File.Exists(outputPath));
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    /// <summary>Test that an unknown --device throws before any synthesizer is created.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_UnknownDevice_ThrowsArgumentException()
    {
        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(playbackProbe: new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(
            ["speak", "--model", "model-1", "--text", "hi", "--device", "does-not-exist"]);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>Test that no available playback device (and no --output) throws InvalidOperationException.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NoPlaybackDeviceAvailable_ThrowsInvalidOperationException()
    {
        var catalog = CreateCatalogWithModel();
        var factory = new AudioDeviceFactory(playbackProbe: new FakeAudioPlaybackDeviceProbe());
        using var context = Context.Create(["speak", "--model", "model-1", "--text", "hi"]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));
    }

    // --- Cancellation ---

    /// <summary>Test that a canceled speak session is reported cleanly, not rethrown.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_Canceled_ReportsErrorCleanly()
    {
        var catalog = CreateCatalogWithModel();
        var synthesizer = new FakeSpeechSynthesizer { SpeakAsyncException = new OperationCanceledException() };
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;
        var factory = new AudioDeviceFactory(playbackProbe: new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--model", "model-1", "--text", "hi"]);

        await SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None);

        Assert.Equal(1, context.ExitCode);
        Assert.Equal(1, synthesizer.DisposeCallCount);
    }

    // --- Disposal ordering ---

    /// <summary>Test that the synthesizer is disposed exactly once after a successful speak.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_Success_DisposesSynthesizerOnce()
    {
        var catalog = CreateCatalogWithModel();
        var synthesizer = new FakeSpeechSynthesizer();
        catalog.CreateSynthesizerOverride = (_, _, _) => synthesizer;
        var factory = new AudioDeviceFactory(playbackProbe: new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--model", "model-1", "--text", "hi"]);

        await SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None);

        Assert.Equal(1, synthesizer.DisposeCallCount);
        Assert.Equal(0, context.ExitCode);
    }

    // --- Null argument guards ---

    /// <summary>Test that a null context is rejected.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NullContext_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeakCommand.RunAsync(null!, new FakeCliModelCatalog(), new AudioDeviceFactory(), CancellationToken.None));
    }

    /// <summary>Test that a null catalog is rejected.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NullCatalog_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["speak", "--model", "model-1", "--text", "hi"]);
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeakCommand.RunAsync(context, null!, new AudioDeviceFactory(), CancellationToken.None));
    }

    /// <summary>Test that a null factory is rejected.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NullFactory_ThrowsArgumentNullException()
    {
        using var context = Context.Create(["speak", "--model", "model-1", "--text", "hi"]);
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeakCommand.RunAsync(context, new FakeCliModelCatalog(), null!, CancellationToken.None));
    }

    /// <summary>Test that Run(Context, ICliModelCatalog, AudioDeviceFactory) rejects a null context.</summary>
    [Fact]
    public void SpeakCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => SpeakCommand.Run(null!, new FakeCliModelCatalog(), new AudioDeviceFactory()));
    }

    /// <summary>
    ///     A fake playback device probe that throws if <see cref="Enumerate"/> is called, used
    ///     to prove <c>--output</c> never touches the real audio device probes.
    /// </summary>
    private sealed class ThrowingAudioPlaybackDeviceProbe : IAudioPlaybackDeviceProbe
    {
        public IReadOnlyList<AudioDeviceDescription> Enumerate() =>
            throw new InvalidOperationException("--output must never enumerate real playback devices.");
    }
}
