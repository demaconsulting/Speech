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
        // Arrange
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog()
            .WithModel(new FakeSpeechModel("model-a"), SpeechModelState.Downloaded)
            .WithModel(new FakeSpeechModel("model-b"), SpeechModelState.NotDownloaded);

        // Act
        var output = RunCapturingOutput(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

        // Assert
        Assert.Contains("Overall: HEALTHY", output);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that the model store root path is reported in the output.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_ReportsModelStoreRootPath()
    {
        // Arrange
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog();

        // Act
        var output = RunCapturingOutput(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

        // Assert
        Assert.Contains(_tempDir, output);
    }

    /// <summary>
    ///     Test that the available disk space at the model store root is reported in the output.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_ReportsAvailableDiskSpace()
    {
        // Arrange
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog();

        // Act
        var output = RunCapturingOutput(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

        // Assert
        Assert.Contains("Available disk space:", output);
    }

    /// <summary>
    ///     Test that installed-versus-known model counts are reported.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_ReportsInstalledVersusKnownModelCounts()
    {
        // Arrange
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog()
            .WithModel(new FakeSpeechModel("model-a"), SpeechModelState.Downloaded)
            .WithModel(new FakeSpeechModel("model-b"), SpeechModelState.NotDownloaded)
            .WithModel(new FakeSpeechModel("model-c"), SpeechModelState.NotDownloaded);

        // Act
        var output = RunCapturingOutput(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

        // Assert
        Assert.Contains("Installed: 1 / 3 known", output);
    }

    /// <summary>
    ///     Test that detected input/output audio device counts are reported.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_ReportsAudioDeviceCounts()
    {
        // Arrange
        var context = Context.Create(["doctor"]);
        var captureProbe = new FakeAudioCaptureDeviceProbe([new AudioDeviceDescription("Mic", AudioDeviceDirection.Capture, 1, 16000)]);
        var playbackProbe = new FakeAudioPlaybackDeviceProbe();
        var catalog = new FakeCliModelCatalog();

        // Act
        var output = RunCapturingOutput(context, captureProbe, playbackProbe, catalog, _tempDir);

        // Assert
        Assert.Contains("Input devices detected: 1", output);
        Assert.Contains("Output devices detected: 0", output);
    }

    /// <summary>
    ///     Test that the SherpaOnnx native inference library is reported resolvable end to end
    ///     through <see cref="DoctorCommand.Run(Context, IAudioCaptureDeviceProbe, IAudioPlaybackDeviceProbe, ICliModelCatalog, string)"/> -
    ///     exercising the real
    ///     <c>TryProbeSherpaOnnxNativeLibrary</c>/<c>CheckNativeRuntimes</c> load path, not just
    ///     path resolution. Deterministic on every CI runner (win-x64/linux-x64/osx-arm64)
    ///     because <c>org.k2fsa.sherpa.onnx</c>'s own nuspec unconditionally depends on every
    ///     RID's runtime package, so the real native asset always flows transitively into this
    ///     test project's own output via its reference to <c>DemaConsulting.Speech.Cli</c> - see
    ///     this project's csproj remarks.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_ReportsSherpaOnnxNativeLibraryResolvable()
    {
        // Arrange: this test deliberately exercises the real native load path, which depends on the
        // platform's runtime identifier being supported; on an unsupported platform it is skipped.
        Assert.SkipUnless(
            DoctorCommand.ResolveRuntimeIdentifier() is not null,
            "The current platform has no supported SherpaOnnx runtime identifier.");
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog();

        // Act
        var output = RunCapturingOutput(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

        // Assert
        Assert.Contains("[ OK ] SherpaOnnx native inference library is resolvable.", output);
    }

    /// <summary>
    ///     Test that <see cref="DoctorCommand.ResolveSherpaOnnxNativeLibraryPath"/> resolves the
    ///     RID-specific native asset path against a fixture directory laid out to match NuGet's
    ///     <c>runtimes/&lt;rid&gt;/native/</c> convention, deterministically and independent of
    ///     whether this test project happens to carry a real SherpaOnnx native asset transitively.
    /// </summary>
    [Fact]
    public void DoctorCommand_ResolveSherpaOnnxNativeLibraryPath_AssetPresent_ReturnsPath()
    {
        // Arrange
        var runtimeIdentifier = DoctorCommand.ResolveRuntimeIdentifier();
        Assert.SkipUnless(
            runtimeIdentifier is not null,
            "The current platform has no supported SherpaOnnx runtime identifier.");

        var fileName = DoctorCommand.ResolveSherpaOnnxNativeLibraryFileName();
        var nativeDir = Path.Join(_tempDir, "runtimes", runtimeIdentifier, "native");
        Directory.CreateDirectory(nativeDir);
        var assetPath = Path.Join(nativeDir, fileName);
        File.WriteAllText(assetPath, string.Empty);

        // Act
        var resolved = DoctorCommand.ResolveSherpaOnnxNativeLibraryPath(_tempDir);

        // Assert
        Assert.Equal(assetPath, resolved);
    }

    /// <summary>
    ///     Test that <see cref="DoctorCommand.ResolveSherpaOnnxNativeLibraryPath"/> returns
    ///     <see langword="null"/> when the expected RID-specific asset is absent, rather than
    ///     resolving to a path that does not exist.
    /// </summary>
    [Fact]
    public void DoctorCommand_ResolveSherpaOnnxNativeLibraryPath_AssetAbsent_ReturnsNull()
    {
        // Arrange: _tempDir contains no runtimes directory.

        // Act
        var resolved = DoctorCommand.ResolveSherpaOnnxNativeLibraryPath(_tempDir);

        // Assert
        Assert.Null(resolved);
    }

    /// <summary>
    ///     Test that an unavailable PortAudio probe (the library's honest fallback singleton) is
    ///     reported as informational, not a hard failure.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_UnavailablePortAudio_ReportsInformationalNoteNotFailure()
    {
        // Arrange
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog();

        // Act
        var output = RunCapturingOutput(
            context,
            UnavailableAudioCaptureDeviceProbe.Instance,
            UnavailableAudioPlaybackDeviceProbe.Instance,
            catalog,
            _tempDir);

        // Assert
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
            // Act
            var output = RunCapturingOutput(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

            // Assert
            Assert.Contains("[FAIL]", output);
            Assert.NotEqual(0, context.ExitCode);
        }
        finally
        {
            File.Delete(_tempDir);
        }
    }

    /// <summary>
    ///     Test that audio probes that throw while enumerating degrade the informational audio section
    ///     instead of crashing the health check.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_ThrowingAudioProbes_DegradesGracefullyAndStaysHealthy()
    {
        // Arrange
        var context = Context.Create(["doctor"]);

        // Act
        var output = RunCapturingOutput(context, new ThrowingCaptureProbe(), new ThrowingPlaybackProbe(), new FakeCliModelCatalog(), _tempDir);

        // Assert
        Assert.Contains("Input devices detected: (could not be determined)", output);
        Assert.Contains("Output devices detected: (could not be determined)", output);
        Assert.Contains("Overall: HEALTHY", output);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that a model catalog that throws while enumerating degrades the informational models
    ///     section instead of crashing the health check.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_ThrowingCatalog_DegradesGracefullyAndStaysHealthy()
    {
        // Arrange
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog { EnumerateException = new InvalidOperationException("catalog broke") };

        // Act
        var output = RunCapturingOutput(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

        // Assert
        Assert.Contains("Installed: (could not be determined)", output);
        Assert.Contains("Overall: HEALTHY", output);
        Assert.Equal(0, context.ExitCode);
    }

    /// <summary>
    ///     Test that an unsupported extra argument throws <see cref="ArgumentException"/>.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_ExtraArgument_ThrowsArgumentException()
    {
        // Arrange
        var context = Context.Create(["doctor", "bogus"]);
        var catalog = new FakeCliModelCatalog();

        // Act
        void Act() => DoctorCommand.Run(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

        // Assert
        Assert.Throws<ArgumentException>(Act);
    }

    /// <summary>
    ///     Test that a <see langword="null"/> context is rejected.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_NullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var catalog = new FakeCliModelCatalog();

        // Act
        void Act() => DoctorCommand.Run(null!, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>
    ///     Test that a <see langword="null"/> capture probe is rejected.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_NullCaptureProbe_ThrowsArgumentNullException()
    {
        // Arrange
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog();

        // Act
        void Act() => DoctorCommand.Run(context, null!, new FakeAudioPlaybackDeviceProbe(), catalog, _tempDir);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>
    ///     Test that a <see langword="null"/> playback probe is rejected.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_NullPlaybackProbe_ThrowsArgumentNullException()
    {
        // Arrange
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog();

        // Act
        void Act() => DoctorCommand.Run(context, new FakeAudioCaptureDeviceProbe(), null!, catalog, _tempDir);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>
    ///     Test that a <see langword="null"/> catalog is rejected.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_NullCatalog_ThrowsArgumentNullException()
    {
        // Arrange
        var context = Context.Create(["doctor"]);

        // Act
        void Act() => DoctorCommand.Run(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), null!, _tempDir);

        // Assert
        Assert.Throws<ArgumentNullException>(Act);
    }

    /// <summary>
    ///     Test that a null or empty model store root path is rejected.
    /// </summary>
    [Fact]
    public void DoctorCommand_Run_EmptyModelStoreRootPath_ThrowsArgumentException()
    {
        // Arrange
        var context = Context.Create(["doctor"]);
        var catalog = new FakeCliModelCatalog();

        // Act
        void Act() => DoctorCommand.Run(context, new FakeAudioCaptureDeviceProbe(), new FakeAudioPlaybackDeviceProbe(), catalog, string.Empty);

        // Assert
        Assert.Throws<ArgumentException>(Act);
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

    /// <summary>A capture probe whose enumeration always fails.</summary>
    private sealed class ThrowingCaptureProbe : IAudioCaptureDeviceProbe
    {
        /// <inheritdoc/>
        public IReadOnlyList<AudioDeviceDescription> Enumerate() => throw new InvalidOperationException("probe broke");
    }

    /// <summary>A playback probe whose enumeration always fails.</summary>
    private sealed class ThrowingPlaybackProbe : IAudioPlaybackDeviceProbe
    {
        /// <inheritdoc/>
        public IReadOnlyList<AudioDeviceDescription> Enumerate() => throw new InvalidOperationException("probe broke");
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
