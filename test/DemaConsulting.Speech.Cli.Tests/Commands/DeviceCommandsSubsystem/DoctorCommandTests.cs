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
using DemaConsulting.Speech.Cli.Tests.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Tests.Commands.DeviceCommandsSubsystem;

/// <summary>
///     Unit tests for <see cref="DoctorCommand"/>.
/// </summary>
[Collection("Sequential")]
public sealed class DoctorCommandTests : IDisposable
{
    private readonly string _tempDir = Path.Join(Path.GetTempPath(), "DemaConsulting.Speech.Cli.Tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    ///     Test that a writable model store root reports overall health and exits cleanly
    ///     (no error raised via <see cref="Context.WriteError"/>).
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_WritableModelStore_ReportsHealthy()
    {
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog()
            .WithModel(new FakeSpeechModel("model-a"), SpeechModelState.Downloaded)
            .WithModel(new FakeSpeechModel("model-b"), SpeechModelState.NotDownloaded);

        var output = RunCapturingOutput(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

        Assert.Contains("Overall: HEALTHY", output);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that the model store root path is reported in the output.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_ReportsModelStoreRootPath()
    {
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog();

        var output = RunCapturingOutput(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

        Assert.Contains(_tempDir, output);
    }

    /// <summary>
    ///     Test that installed-versus-known model counts are reported.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_ReportsInstalledVersusKnownModelCounts()
    {
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog()
            .WithModel(new FakeSpeechModel("model-a"), SpeechModelState.Downloaded)
            .WithModel(new FakeSpeechModel("model-b"), SpeechModelState.NotDownloaded)
            .WithModel(new FakeSpeechModel("model-c"), SpeechModelState.NotDownloaded);

        var output = RunCapturingOutput(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

        Assert.Contains("Installed: 1 / 3 known", output);
    }

    /// <summary>
    ///     Test that detected input/output audio device counts are reported.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_ReportsAudioDeviceCounts()
    {
        var context = Context.Create(["doctor"]);
        var captureProbe = new FakeAudioCaptureDeviceProbe([new AudioDeviceDescription("Mic", AudioDeviceDirection.Capture, 1, 16000)]);
        var playbackProbe = new FakeAudioPlaybackDeviceProbe();
        var catalog = new FakeCliModelCatalog();

        var output = RunCapturingOutput(context, captureProbe, playbackProbe, catalog, _tempDir);

        Assert.Contains("Input devices detected: 1", output);
        Assert.Contains("Output devices detected: 0", output);
    }

    /// <summary>
    ///     Test that an unavailable PortAudio probe (the library's honest fallback singleton) is
    ///     reported as informational, not a hard failure.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_UnavailablePortAudio_ReportsInformationalNoteNotFailure()
    {
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog();

        var output = RunCapturingOutput(
            context,
            UnavailableAudioCaptureDeviceProbe.Instance,
            UnavailableAudioPlaybackDeviceProbe.Instance,
            catalog,
            _tempDir);

        Assert.Contains("[INFO] PortAudio", output);
        Assert.Contains("Overall: HEALTHY", output);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that a model store root that cannot be written to (a file occupying the path) is
    ///     reported as the only hard failure, producing a non-zero exit code.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_UnwritableModelStore_ReportsUnhealthyAndNonZeroExitCode()
    {
        // Arrange: create a plain file where the model store root directory should be, so
        // Directory.CreateDirectory fails.
        Directory.CreateDirectory(Path.GetDirectoryName(_tempDir)!);
        File.WriteAllText(_tempDir, "not a directory");

        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog();

        try
        {
            var output = RunCapturingOutput(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

            Assert.Contains("[FAIL]", output);
            Assert.NotEqual(0, context.ExitCode);
        }
        finally
        {
            File.Delete(_tempDir);
        }
    }

    /// <summary>
    ///     Test that an unsupported extra argument throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_ExtraArgument_ThrowsArgumentException()
    {
        var context = Context.Create(["doctor", "bogus"]);
        var catalog = new FakeCliModelCatalog();

        Assert.Throws<ArgumentException>(() =>
            DoctorCommand.Run(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir));
    }

    /// <summary>
    ///     Test that a <see langword="null"/> context is rejected.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            DoctorCommand.Run(null!, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), new FakeCliModelCatalog(), _tempDir));
    }

    /// <summary>
    ///     Test that a <see langword="null"/> catalog is rejected.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_NullCatalog_ThrowsArgumentNullException()
    {
        var context = Context.Create(["doctor"]);

        Assert.Throws<ArgumentNullException>(() =>
            DoctorCommand.Run(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), null!, _tempDir));
    }

    /// <summary>
    ///     Test that a null or empty model store root path is rejected.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_EmptyModelStoreRootPath_ThrowsArgumentException()
    {
        var context = Context.Create(["doctor"]);

        Assert.Throws<ArgumentException>(() =>
            DoctorCommand.Run(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), new FakeCliModelCatalog(), string.Empty));
    }

    /// <summary>
    ///     Runs <see cref="DoctorCommand"/> while capturing everything written to stdout and
    ///     stderr (combined), since <see cref="Context.WriteError"/> writes to stderr.
    /// </summary>
    private static string RunCapturingOutput(
        Context context,
        IAudioCaptureDeviceProbe captureProbe,
        IAudioPlaybackDeviceProbe playbackProbe,
        ICliModelCatalog catalog,
        string modelStoreRootPath)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        try
        {
            using var writer = new StringWriter();
            Console.SetOut(writer);
            Console.SetError(writer);
            DoctorCommand.Run(context, captureProbe, playbackProbe, catalog, modelStoreRootPath);
            return writer.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>
    ///     Deletes the temporary directory used as this test class's isolated model store root.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }
}
