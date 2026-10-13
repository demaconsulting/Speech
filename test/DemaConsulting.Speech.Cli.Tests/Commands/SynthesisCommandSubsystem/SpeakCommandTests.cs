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
using DemaConsulting.Speech.SynthesisSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.SynthesisCommandSubsystem;

/// <summary>
///     Unit tests for <see cref="SpeakCommand"/>, using <see cref="FakeCliModelCatalog"/>,
///     <see cref="FakeSynthesisSession"/>/<see cref="FakeSpeechSynthesizerEngine"/>, and fake
///     audio device probes so every scenario runs deterministically with no real catalog, network
///     access, native engine, or audio hardware.
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

    /// <summary>
    ///     Creates a <see cref="FakeSynthesisSession"/> wrapped in a <see cref="FakeSpeechSynthesizerEngine"/>,
    ///     and wires the catalog's <see cref="FakeCliModelCatalog.CreateSynthesizerEngineOverride"/>
    ///     to return the engine.
    /// </summary>
    private static (FakeSynthesisSession Session, FakeSpeechSynthesizerEngine Engine) WireSynthesizer(
        FakeCliModelCatalog catalog)
    {
        var session = new FakeSynthesisSession();
        var engine = new FakeSpeechSynthesizerEngine(session);
        catalog.CreateSynthesizerEngineOverride = (_, _, _) => Task.FromResult<ISpeechSynthesizerEngine>(engine);
        return (session, engine);
    }

    // --- ParseArguments ---

    /// <summary>Test that --tts-model is required.</summary>
    [Fact]
    public void SpeakCommand_ParseArguments_MissingModel_ThrowsArgumentException()
    {
        // Arrange
        string[] args = ["--text", "hello"];

        // Act / Assert
        Assert.Throws<ArgumentException>(() => SpeakCommand.ParseArguments(args));
    }

    /// <summary>Test that --text parses into the options.</summary>
    [Fact]
    public void SpeakCommand_ParseArguments_TextFlag_ParsesText()
    {
        // Arrange
        string[] args = ["--tts-model", "model-1", "--text", "hello world"];

        // Act
        var options = SpeakCommand.ParseArguments(args);

        // Assert
        Assert.Equal("model-1", options.ModelId);
        Assert.Equal("hello world", options.Text);
    }

    /// <summary>Test that repeatable --tts-param tokens accumulate in order.</summary>
    [Fact]
    public void SpeakCommand_ParseArguments_RepeatedParamFlags_AccumulatesInOrder()
    {
        // Arrange
        string[] args =
            ["--tts-model", "model-1", "--text", "hi", "--tts-param", "rate=1.2", "--tts-param", "voice=bob"];

        // Act
        var options = SpeakCommand.ParseArguments(args);

        // Assert
        Assert.Equal([("rate", "1.2"), ("voice", "bob")], options.RawParameters);
    }

    /// <summary>Test that --no-tags parses as a flag.</summary>
    [Fact]
    public void SpeakCommand_ParseArguments_NoTagsFlag_ParsesTrue()
    {
        // Arrange
        string[] args = ["--tts-model", "model-1", "--text", "hi", "--no-tags"];

        // Act
        var options = SpeakCommand.ParseArguments(args);

        // Assert
        Assert.True(options.NoTags);
    }

    /// <summary>Test that an unsupported argument throws.</summary>
    [Fact]
    public void SpeakCommand_ParseArguments_UnsupportedArgument_ThrowsArgumentException()
    {
        // Arrange
        string[] args = ["--tts-model", "model-1", "--bogus"];

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeakCommand.ParseArguments(args));

        // Assert
        Assert.Contains("--bogus", exception.Message);
    }

    /// <summary>Test that a flag missing its value throws.</summary>
    [Fact]
    public void SpeakCommand_ParseArguments_FlagMissingValue_ThrowsArgumentException()
    {
        // Arrange
        string[] args = ["--tts-model"];

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeakCommand.ParseArguments(args));

        // Assert
        Assert.Contains("--tts-model requires a value", exception.Message);
    }

    /// <summary>
    ///     Regression test: a value-taking flag directly followed by another recognized option must
    ///     be reported as missing its value instead of swallowing that option as its value.
    /// </summary>
    [Theory]
    [InlineData("--tts-model")]
    [InlineData("--text")]
    [InlineData("--file")]
    [InlineData("--output-audio")]
    [InlineData("--playback-device")]
    [InlineData("--tts-param")]
    public void SpeakCommand_ParseArguments_ValueFlagFollowedByRecognizedOption_ThrowsArgumentException(string flag)
    {
        // Arrange
        string[] args = [flag, "--no-tags"];

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeakCommand.ParseArguments(args));

        // Assert
        Assert.Contains($"{flag} requires a value", exception.Message);
    }

    /// <summary>Regression test: a value-taking flag at the end of the arguments is reported as missing its value.</summary>
    [Theory]
    [InlineData("--text")]
    [InlineData("--file")]
    [InlineData("--output-audio")]
    [InlineData("--playback-device")]
    [InlineData("--tts-param")]
    public void SpeakCommand_ParseArguments_ValueFlagAtEndOfArgs_ThrowsArgumentException(string flag)
    {
        // Arrange
        string[] args = ["--tts-model", "model-1", flag];

        // Act
        var exception = Assert.Throws<ArgumentException>(() => SpeakCommand.ParseArguments(args));

        // Assert
        Assert.Contains($"{flag} requires a value", exception.Message);
    }

    /// <summary>
    ///     Test that a value which merely starts with a dash but is not a recognized option (for
    ///     example, narration text) is still accepted as the flag's value.
    /// </summary>
    [Fact]
    public void SpeakCommand_ParseArguments_DashPrefixedNonOptionValue_IsAccepted()
    {
        // Arrange
        string[] args = ["--tts-model", "model-1", "--text", "-- not an option"];

        // Act
        var options = SpeakCommand.ParseArguments(args);

        // Assert
        Assert.Equal("-- not an option", options.Text);
    }

    // --- Text-source mutual exclusion ---

    /// <summary>Test that supplying both --text and --file throws.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_TextAndFileBothGiven_ThrowsArgumentException()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--tts-model", "model-1", "--text", "hi", "--file", "in.txt"]);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));
    }

    /// <summary>Test that neither --text nor --file given, with stdin not redirected, throws.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NoTextSourceAndStdinNotRedirected_ThrowsArgumentException()
    {
        // This test specifically covers the "no source available" branch, so it cannot run when
        // stdin actually is redirected (e.g. under some CI/test-runner harnesses).
        Assert.SkipWhen(Console.IsInputRedirected, "Standard input is redirected, so the no-text-source branch is unreachable.");

        // Arrange
        var catalog = CreateCatalogWithModel();
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--tts-model", "model-1"]);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));
    }

    // --- Text file and model resolution error paths ---

    /// <summary>Test that an unreadable --file is reported as a clean InvalidOperationException naming the file.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_MissingTextFile_ThrowsInvalidOperationException()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.txt");
        using var context = Context.Create(["speak", "--tts-model", "model-1", "--file", missing]);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));

        // Assert
        Assert.Contains("missing.txt", exception.Message);
        Assert.IsAssignableFrom<IOException>(exception.InnerException);
    }

    /// <summary>Test that an unknown model id throws.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_UnknownModelId_ThrowsArgumentException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--tts-model", "does-not-exist", "--text", "hi"]);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));

        // Assert
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>Test that a wrong-role (recognition) model throws.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_WrongRoleModel_ThrowsArgumentException()
    {
        // Arrange
        var catalog = CreateCatalogWithModel(role: SpeechModelRole.Recognition);
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--tts-model", "model-1", "--text", "hi"]);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));

        // Assert
        Assert.Contains("model-1", exception.Message);
        Assert.Contains("list-models --role tts", exception.Message);
    }

    /// <summary>Test that a not-downloaded model throws an actionable "download" message.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NotDownloadedModel_ThrowsArgumentExceptionWithDownloadHint()
    {
        // Arrange
        var catalog = CreateCatalogWithModel(state: SpeechModelState.NotDownloaded);
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--tts-model", "model-1", "--text", "hi"]);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));

        // Assert
        Assert.Contains("download model-1", exception.Message);
    }

    // --- --tts-param validation wiring ---

    /// <summary>Test that a valid --tts-param is forwarded to CreateSynthesizerEngineAsync's parameterValues argument.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_ValidParam_ForwardsToCreateSynthesizerEngine()
    {
        // Arrange
        var rateParameter = new NumericParameter(
            "rate", "Rate", "Speaking rate", new NumericParameterBounds(0.5, 2.0, 0.1, 1.0));
        var catalog = CreateCatalogWithModel(parameters: [rateParameter]);
        WireSynthesizer(catalog);
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--tts-model", "model-1", "--text", "hi", "--tts-param", "rate=1.5"]);

        // Act
        await SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None);

        // Assert
        var parameterValues = Assert.Single(catalog.CreateSynthesizerParameterValueCalls);
        Assert.NotNull(parameterValues);
        Assert.Equal(1.5, Assert.IsType<double>(parameterValues["rate"]));
    }

    /// <summary>Test that an invalid --tts-param value throws before any synthesizer engine is created.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_InvalidParam_ThrowsArgumentException()
    {
        // Arrange
        var rateParameter = new NumericParameter(
            "rate", "Rate", "Speaking rate", new NumericParameterBounds(0.5, 2.0, 0.1, 1.0));
        var catalog = CreateCatalogWithModel(parameters: [rateParameter]);
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--tts-model", "model-1", "--text", "hi", "--tts-param", "rate=100"]);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));
        Assert.Empty(catalog.CreateSynthesizerParameterValueCalls);
    }

    // --- --no-tags exact-string assertion ---

    /// <summary>Test that --no-tags strips a recognized tag, keeping only literal narration text.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NoTags_StripsRecognizedTagExactly()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, _) = WireSynthesizer(catalog);
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(
            ["speak", "--tts-model", "model-1", "--text", "Hello [laughs] there", "--no-tags"]);

        // Act
        await SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None);

        // Assert
        var spokenText = Assert.Single(session.SpeakAsyncCalls);
        Assert.Equal("Hello  there", spokenText);
    }

    /// <summary>Test that without --no-tags the original text (including tags) is passed unchanged.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_WithoutNoTags_PassesOriginalTextUnchanged()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, _) = WireSynthesizer(catalog);
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--tts-model", "model-1", "--text", "Hello [laughs] there"]);

        // Act
        await SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None);

        // Assert
        var spokenText = Assert.Single(session.SpeakAsyncCalls);
        Assert.Equal("Hello [laughs] there", spokenText);
    }

    // --- --output-audio vs. device dispatch ---

    /// <summary>
    ///     Test that --output-audio constructs a WavFileAudioPlaybackDevice sized from the model's
    ///     preferred format and never touches the playback-device source's probe.
    /// </summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_Output_ConstructsWavFileDeviceFromPreferredFormat()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        catalog.GetPreferredAudioFormatOverride = _ => AudioFormat.Mono(22050);
        var (_, engine) = WireSynthesizer(catalog);
        // A probe that throws if enumerated, proving --output-audio never touches real device probes.
        var factory = new FakePlaybackDeviceSource(new ThrowingAudioPlaybackDeviceProbe());
        var outputPath = Path.Join(Path.GetTempPath(), $"speak-test-{Guid.NewGuid():N}.wav");

        try
        {
            using var context = Context.Create(["speak", "--tts-model", "model-1", "--text", "hi", "--output-audio", outputPath]);

            // Act
            await SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None);

            // Assert
            Assert.NotNull(engine.LastDevice);
            Assert.Equal(22050, engine.LastDevice.SampleRate);
            Assert.Equal(1, engine.LastDevice.ChannelCount);
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

    /// <summary>Test that an unknown --playback-device throws before any synthesizer engine is created.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_UnknownDevice_ThrowsArgumentException()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(
            ["speak", "--tts-model", "model-1", "--text", "hi", "--playback-device", "does-not-exist"]);

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));

        // Assert
        Assert.Contains("does-not-exist", exception.Message);
    }

    /// <summary>
    ///     Test that no available playback device (and no --output-audio) throws a clean
    ///     InvalidOperationException and that synthesis never started (no engine created).
    /// </summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NoPlaybackDeviceAvailable_ThrowsInvalidOperationException()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, _) = WireSynthesizer(catalog);
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe());
        using var context = Context.Create(["speak", "--tts-model", "model-1", "--text", "hi"]);

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None));

        // Assert
        Assert.Contains("--output-audio", exception.Message);
        Assert.Empty(catalog.CreateSynthesizerParameterValueCalls);
        Assert.Empty(session.SpeakAsyncCalls);
    }

    // --- Cancellation ---

    /// <summary>Test that a canceled speak session is reported cleanly, not rethrown.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_Canceled_ReportsErrorCleanly()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, engine) = WireSynthesizer(catalog);
        session.SpeakAsyncException = new OperationCanceledException();
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--tts-model", "model-1", "--text", "hi"]);

        // Act
        await SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None);

        // Assert
        Assert.Equal(1, context.ExitCode);
        Assert.Equal(1, session.DisposeCallCount);
        Assert.Equal(1, engine.DisposeCallCount);
    }

    // --- Disposal ordering ---

    /// <summary>Test that both the engine and the session are disposed exactly once after a successful speak.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_Success_DisposesEngineAndSessionOnce()
    {
        // Arrange
        var catalog = CreateCatalogWithModel();
        var (session, engine) = WireSynthesizer(catalog);
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe([OutputDevice]));
        using var context = Context.Create(["speak", "--tts-model", "model-1", "--text", "hi"]);

        // Act
        await SpeakCommand.RunAsync(context, catalog, factory, CancellationToken.None);

        // Assert
        Assert.Equal(1, session.DisposeCallCount);
        Assert.Equal(1, engine.DisposeCallCount);
        Assert.Equal(0, context.ExitCode);
    }

    // --- Null argument guards ---

    /// <summary>Test that a null context is rejected.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe());

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeakCommand.RunAsync(null!, catalog, factory, CancellationToken.None));
    }

    /// <summary>Test that a null catalog is rejected.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NullCatalog_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = Context.Create(["speak", "--tts-model", "model-1", "--text", "hi"]);
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe());

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeakCommand.RunAsync(context, null!, factory, CancellationToken.None));
    }

    /// <summary>Test that a null factory is rejected.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NullFactory_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = Context.Create(["speak", "--tts-model", "model-1", "--text", "hi"]);

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeakCommand.RunAsync(context, new FakeCliModelCatalog(), null!, CancellationToken.None));
    }

    /// <summary>Test that RunAsync(Context, ICliModelCatalog, ICliPlaybackDeviceSource) rejects a null context.</summary>
    [Fact]
    public async Task SpeakCommand_RunAsync_NullContextWithDeviceSource_ThrowsArgumentNullException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();
        var factory = new FakePlaybackDeviceSource(new FakeAudioPlaybackDeviceProbe());

        // Act / Assert
        // xUnit1051 is suppressed: this deliberately targets the three-argument seam that owns the
        // Ctrl+C cancellation wiring rather than the CancellationToken overload.
#pragma warning disable xUnit1051
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => SpeakCommand.RunAsync(null!, catalog, factory));
#pragma warning restore xUnit1051
    }

    /// <summary>
    ///     A fake playback device probe that throws if <see cref="Enumerate"/> is called, used
    ///     to prove <c>--output-audio</c> never touches the real audio device probes.
    /// </summary>
    private sealed class ThrowingAudioPlaybackDeviceProbe : IAudioPlaybackDeviceProbe
    {
        /// <inheritdoc/>
        public IReadOnlyList<AudioDeviceDescription> Enumerate() =>
            throw new InvalidOperationException("--output-audio must never enumerate real playback devices.");
    }
}
