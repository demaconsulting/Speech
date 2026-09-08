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

using System.Globalization;
using System.Runtime.InteropServices;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.Cli.Cli;
using DemaConsulting.Speech.Cli.Commands.ModelCommandsSubsystem;
using DemaConsulting.Speech.Cli.Utilities;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.Speech.Cli.Commands.DeviceCommandsSubsystem;

/// <summary>
///     Implements the <c>doctor</c> subcommand: a broader environment/health check than
///     <c>--validate</c>'s five CI-safe checks, reporting native runtime resolvability, the
///     model store's location/writability/available disk space, a summary of detected audio
///     devices, and installed-versus-known model counts, as a pass/fail checklist.
/// </summary>
/// <remarks>
///     <para>
///     <b>Hard failure versus informational note</b>: only the model store being writable is
///     treated as a hard failure (a non-zero exit code). Everything else - the native PortAudio
///     and SherpaOnnx runtimes, and the audio device count - is reported as informational,
///     consistent with this library's designed graceful-degradation philosophy: missing audio
///     hardware or an unresolvable native inference runtime does not prevent the tool itself
///     from running, and every command that actually needs one of them (<c>devices test</c>,
///     and the <c>speak</c>/<c>recognize</c> commands added in later passes) already reports its
///     own clean, specific error at the point of use. The model store, in contrast, is
///     infrastructure this tool itself owns and depends on for every model-management command
///     to function at all, and its writability depends only on file system permissions, not on
///     optional external hardware or native libraries, so a doctor run that cannot even manage
///     models is genuinely unhealthy.
///     </para>
///     <para>
///     <b>SherpaOnnx resolvability scope</b>: this check attempts to load the native
///     <c>sherpa-onnx-c-api</c> shared library by name via
///     <see cref="NativeLibrary.TryLoad(string, out nint)"/>, which only proves the native binary
///     for the current platform/architecture is present and loadable - it does not construct a
///     recognizer or synthesizer, which requires an installed model and is out of scope until
///     the <c>speak</c>/<c>recognize</c> passes. This is a necessary-but-not-sufficient signal,
///     documented here rather than presented as a full inference-path verification.
///     </para>
/// </remarks>
internal static class DoctorCommand
{
    /// <summary>The native SherpaOnnx C API library name probed for resolvability.</summary>
    private const string SherpaOnnxNativeLibraryName = "sherpa-onnx-c-api";

    /// <summary>
    ///     Runs the <c>doctor</c> subcommand against real, composed dependencies: an
    ///     <see cref="AudioDeviceFactory"/>, a <see cref="SpeechModelCatalogAdapter"/>, and the
    ///     resolved model store root path.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when an unsupported extra argument is given.</exception>
    public static void Run(Context context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var factory = new AudioDeviceFactory();
        using var catalog = CliModelCatalogFactory.Create(context);
        var storeOptions = string.IsNullOrEmpty(context.ModelsDir)
            ? null
            : new SpeechModelStoreOptions { RootPathOverride = context.ModelsDir };
        var modelStoreRootPath = new SpeechModelStore(storeOptions).RootPath;

        Run(context, factory.CaptureProbe, factory.PlaybackProbe, catalog, modelStoreRootPath);
    }

    /// <summary>
    ///     Runs the <c>doctor</c> subcommand against injected dependencies, for unit testing
    ///     without real audio hardware, a real <see cref="SpeechModelCatalog"/>, or the real
    ///     per-user model store location.
    /// </summary>
    /// <param name="context">The invocation context. Must not be null.</param>
    /// <param name="captureProbe">The capture device probe to summarize. Must not be null.</param>
    /// <param name="playbackProbe">The playback device probe to summarize. Must not be null.</param>
    /// <param name="catalog">The model catalog seam to summarize. Must not be null.</param>
    /// <param name="modelStoreRootPath">The resolved model store root directory to check. Must not be null or empty.</param>
    /// <exception cref="ArgumentNullException">
    ///     Thrown when <paramref name="context"/>, <paramref name="captureProbe"/>,
    ///     <paramref name="playbackProbe"/>, or <paramref name="catalog"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="modelStoreRootPath"/> is null or empty, or when an
    ///     unsupported extra argument is given.
    /// </exception>
    internal static void Run(
        Context context,
        IAudioCaptureDeviceProbe captureProbe,
        IAudioPlaybackDeviceProbe playbackProbe,
        ICliModelCatalog catalog,
        string modelStoreRootPath)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(captureProbe);
        ArgumentNullException.ThrowIfNull(playbackProbe);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentException.ThrowIfNullOrEmpty(modelStoreRootPath);

        if (context.CommandArgs.Count > 0)
        {
            throw new ArgumentException($"Unsupported argument '{context.CommandArgs[0]}' for 'doctor'.", nameof(context));
        }

        context.WriteLine("Speech CLI environment health check");
        context.WriteLine("");

        CheckNativeRuntimes(context, captureProbe, playbackProbe);
        var modelStoreWritable = CheckModelStore(context, modelStoreRootPath);
        CheckAudioDevices(context, captureProbe, playbackProbe);
        CheckModels(context, catalog);

        context.WriteLine("");
        if (modelStoreWritable)
        {
            context.WriteLine("Overall: HEALTHY");
        }
        else
        {
            context.WriteError("Overall: UNHEALTHY - the model store is not writable; see above.");
        }
    }

    /// <summary>
    ///     Reports whether the PortAudio and SherpaOnnx native runtimes are resolvable.
    ///     Informational only; never affects overall health.
    /// </summary>
    private static void CheckNativeRuntimes(Context context, IAudioCaptureDeviceProbe captureProbe, IAudioPlaybackDeviceProbe playbackProbe)
    {
        context.WriteLine("Native runtimes:");

        // The library's own AudioDeviceFactory composition degrades to the honest Unavailable*
        // probe singletons when PortAudio itself failed to initialize; checking the resolved
        // probe's concrete type is a safe, public way to observe that fallback without any
        // internal access to the library.
        var portAudioAvailable = captureProbe is not UnavailableAudioCaptureDeviceProbe
            && playbackProbe is not UnavailableAudioPlaybackDeviceProbe;
        context.WriteLine(portAudioAvailable
            ? "  [ OK ] PortAudio (audio device backend) is resolvable."
            : "  [INFO] PortAudio (audio device backend) is unavailable on this machine; audio commands will report a clean error when used.");

        var sherpaOnnxAvailable = TryProbeSherpaOnnxNativeLibrary();
        context.WriteLine(sherpaOnnxAvailable
            ? "  [ OK ] SherpaOnnx native inference library is resolvable."
            : "  [INFO] SherpaOnnx native inference library could not be loaded; speech synthesis/recognition commands will report a clean error when used.");

        context.WriteLine("");
    }

    /// <summary>
    ///     Attempts to load the native SherpaOnnx C API shared library by name, never throwing.
    /// </summary>
    /// <returns>
    ///     <see langword="true"/> when the native library for the current platform/architecture
    ///     loads successfully; otherwise <see langword="false"/>.
    /// </returns>
    private static bool TryProbeSherpaOnnxNativeLibrary()
    {
        try
        {
            return NativeLibrary.TryLoad(SherpaOnnxNativeLibraryName, out _);
        }
        // Generic catch is justified here: this is a best-effort environment probe that must
        // never throw regardless of the underlying platform loader's failure mode.
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    ///     Reports the model store root path, whether it can be written to, and the available
    ///     disk space at that location. The only hard-failure check in <c>doctor</c>; see the
    ///     class remarks for why.
    /// </summary>
    /// <returns><see langword="true"/> when the model store root is writable; otherwise <see langword="false"/>.</returns>
    private static bool CheckModelStore(Context context, string rootPath)
    {
        context.WriteLine("Model store:");
        context.WriteLine($"  Root path: {rootPath}");

        bool writable;
        try
        {
            Directory.CreateDirectory(rootPath);
            var probeFile = PathHelpers.SafePathCombine(rootPath, $".speech-cli-doctor-{Guid.NewGuid()}.tmp");
            File.WriteAllText(probeFile, string.Empty);
            File.Delete(probeFile);
            writable = true;
            context.WriteLine("  [ OK ] Model store root is writable.");
        }
        // Generic catch is justified here: any file system failure (permissions, missing drive,
        // invalid path) means the same thing for this check - the root is not writable - and
        // should be reported as a clean, specific message rather than propagating a raw
        // platform exception.
        catch (Exception ex)
        {
            writable = false;
            context.WriteError($"  [FAIL] Model store root is not writable: {ex.Message}");
        }

        context.WriteLine($"  Available disk space: {FormatAvailableDiskSpace(rootPath)}");
        context.WriteLine("");

        return writable;
    }

    /// <summary>
    ///     Formats the available free disk space at the drive containing <paramref name="rootPath"/>,
    ///     never throwing.
    /// </summary>
    private static string FormatAvailableDiskSpace(string rootPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(rootPath);
            var pathRoot = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(pathRoot))
            {
                return "(could not be determined)";
            }

            var drive = new DriveInfo(pathRoot);
            var availableGigabytes = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            return $"{availableGigabytes.ToString("0.0", CultureInfo.InvariantCulture)} GB";
        }
        // Generic catch is justified here: disk space reporting is informational only and must
        // never turn a permission/mount-point quirk into an unhandled exception.
        catch (Exception)
        {
            return "(could not be determined)";
        }
    }

    /// <summary>
    ///     Reports the number of detected input/output audio devices. Informational only,
    ///     mirroring the library's own audio-device graceful-degradation philosophy; this is a
    ///     count summary only, not a full enumeration (that is <c>list-devices</c>'s job).
    /// </summary>
    private static void CheckAudioDevices(Context context, IAudioCaptureDeviceProbe captureProbe, IAudioPlaybackDeviceProbe playbackProbe)
    {
        var inputCount = captureProbe.Enumerate().Count;
        var outputCount = playbackProbe.Enumerate().Count;

        context.WriteLine("Audio devices:");
        context.WriteLine($"  Input devices detected: {inputCount.ToString(CultureInfo.InvariantCulture)}");
        context.WriteLine($"  Output devices detected: {outputCount.ToString(CultureInfo.InvariantCulture)}");
        context.WriteLine("");
    }

    /// <summary>
    ///     Reports installed-versus-known model counts via the model catalog seam. Informational
    ///     only.
    /// </summary>
    private static void CheckModels(Context context, ICliModelCatalog catalog)
    {
        var descriptors = catalog.Enumerate();
        var installedCount = descriptors.Count(descriptor => descriptor.State == SpeechModelState.Downloaded);

        context.WriteLine("Models:");
        context.WriteLine($"  Installed: {installedCount.ToString(CultureInfo.InvariantCulture)} / {descriptors.Count.ToString(CultureInfo.InvariantCulture)} known");
    }
}
