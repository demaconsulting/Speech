using DemaConsulting.Speech.AudioSubsystem.PortAudio;
using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     PortAudio-backed implementation of <see cref="IAudioCaptureDevice"/> that resolves one
///     preferred input device and, when started, forwards captured sample blocks through the
///     managed <see cref="FrameCaptured"/> event.
/// </summary>
/// <remarks>
///     Construction never throws. When PortAudio initialized successfully but no device can be
///     resolved for the current selection or host-API default, the instance honestly reports
///     <see cref="IsAvailable"/> as <see langword="false"/> and operational members throw
///     <see cref="AudioDeviceUnavailableException"/> only when invoked.
/// </remarks>
internal sealed class PortAudioCaptureDevice : IAudioCaptureDevice
{
    /// <summary>
    ///     The PortAudio callback block size requested for capture streams.
    /// </summary>
    internal const uint FramesPerBuffer = 0;

    /// <summary>
    ///     The diagnostics category reported for every event raised by this class.
    /// </summary>
    private const string DiagnosticsCategory = "AudioSubsystem";

    /// <summary>
    ///     Initializes a new instance of the <see cref="PortAudioCaptureDevice"/> class.
    /// </summary>
    /// <param name="environment">
    ///     The PortAudio environment used to resolve and open the selected capture device.
    /// </param>
    /// <param name="selection">
    ///     The requested capture-device selection, or <see langword="null"/> to use the host-
    ///     API-scoped default input device.
    /// </param>
    /// <param name="diagnostics">
    ///     The diagnostics sink for structural selection/start/stop/fault events, or
    ///     <see langword="null"/> to use <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <param name="preferredFormat">
    ///     The preferred capture format to request for the resolved device, or
    ///     <see langword="null"/> to request the device's own default sample rate and full input
    ///     channel capacity.
    /// </param>
    internal PortAudioCaptureDevice(
        PortAudioEnvironment environment,
        AudioDeviceSelection? selection = null,
        ISpeechDiagnostics? diagnostics = null,
        AudioFormat? preferredFormat = null)
    {
        ArgumentNullException.ThrowIfNull(environment);

        _environment = environment;
        _selection = selection ?? AudioDeviceSelection.SystemDefault;
        _diagnostics = diagnostics ?? NullSpeechDiagnostics.Instance;
        _preferredFormat = preferredFormat;
        _resolvedDevice = ResolveDevice();
    }

    /// <summary>
    ///     Synchronizes start/stop transitions so only one stream instance is active at a time.
    /// </summary>
    private readonly object _syncRoot = new();

    /// <summary>
    ///     The PortAudio environment used to resolve and open the selected capture device.
    /// </summary>
    private readonly PortAudioEnvironment _environment;

    /// <summary>
    ///     The persisted selection whose exact-name match or default fallback should be honored.
    /// </summary>
    private readonly AudioDeviceSelection _selection;

    /// <summary>
    ///     The diagnostics sink for structural selection and lifecycle events.
    /// </summary>
    private readonly ISpeechDiagnostics _diagnostics;

    /// <summary>
    ///     The optional preferred capture format to request when resolving the device.
    /// </summary>
    private readonly AudioFormat? _preferredFormat;

    /// <summary>
    ///     The resolved device metadata, or <see langword="null"/> when no capture device could
    ///     be resolved on the preferred host API.
    /// </summary>
    private readonly ResolvedCaptureDevice? _resolvedDevice;

    /// <summary>
    ///     The currently opened PortAudio stream, when capture has been started.
    /// </summary>
    private IPortAudioStream? _stream;

    /// <inheritdoc/>
    public bool IsAvailable => _resolvedDevice is not null;

    /// <inheritdoc/>
    /// <remarks>
    ///     Reports the channel count requested at construction time: either the resolved device's
    ///     own full input-channel capacity, or the caller's preferred channel count clamped down
    ///     to that capability. This is the same count requested when the capture stream is opened
    ///     and therefore the interleaving stride of every <see cref="FrameCaptured"/> payload.
    /// </remarks>
    public int ChannelCount => _resolvedDevice?.ChannelCount ?? 0;

    /// <inheritdoc/>
    /// <remarks>
    ///     Reports the sample rate requested at construction time: either the resolved device's
    ///     own PortAudio-reported default rate, or the caller's preferred sample rate when one
    ///     was supplied. This is the same rate requested when the capture stream is opened.
    /// </remarks>
    public int SampleRate => _resolvedDevice?.SampleRate ?? 0;

    /// <inheritdoc/>
    public event EventHandler<AudioCaptureFrameEventArgs>? FrameCaptured;

    /// <inheritdoc/>
    public void Start()
    {
        if (_resolvedDevice is null)
        {
            throw new AudioDeviceUnavailableException(
                "Cannot start capture: no PortAudio capture device could be resolved.");
        }

        lock (_syncRoot)
        {
            if (_stream is not null)
            {
                return;
            }

            try
            {
                _stream = _environment.Api.OpenCaptureStream(
                    _resolvedDevice.DeviceIndex,
                    _resolvedDevice.ChannelCount,
                    _resolvedDevice.SampleRate,
                    FramesPerBuffer,
                    OnSamplesCaptured);
                _stream.Start();
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Info,
                    DiagnosticsCategory,
                    $"Started PortAudio capture on '{_resolvedDevice.Name}'.");
            }
            catch (Exception ex)
            {
                _stream?.Dispose();
                _stream = null;
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Error,
                    DiagnosticsCategory,
                    $"Failed to start PortAudio capture on '{_resolvedDevice.Name}': {ex.Message}");
                throw new AudioDeviceUnavailableException(
                    $"Failed to start capture on '{_resolvedDevice.Name}'.",
                    ex);
            }
        }
    }

    /// <inheritdoc/>
    public void Stop()
    {
        if (_resolvedDevice is null)
        {
            throw new AudioDeviceUnavailableException(
                "Cannot stop capture: no PortAudio capture device could be resolved.");
        }

        IPortAudioStream? streamToStop;
        lock (_syncRoot)
        {
            streamToStop = _stream;
            _stream = null;
        }

        if (streamToStop is null)
        {
            return;
        }

        Exception? stopException = null;
        try
        {
            streamToStop.Stop();
            _diagnostics.Report(
                SpeechDiagnosticLevel.Info,
                DiagnosticsCategory,
                $"Stopped PortAudio capture on '{_resolvedDevice.Name}'.");
        }
        catch (Exception ex)
        {
            stopException = ex;
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Failed to stop PortAudio capture on '{_resolvedDevice.Name}': {ex.Message}");
        }

        try
        {
            streamToStop.Dispose();
        }
        catch (Exception ex) when (stopException is null)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Failed to dispose PortAudio capture stream on '{_resolvedDevice.Name}': {ex.Message}");
            throw new AudioDeviceUnavailableException(
                $"Failed to stop capture on '{_resolvedDevice.Name}'.",
                ex);
        }
        catch (Exception ex)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"Failed to dispose PortAudio capture stream on '{_resolvedDevice.Name}' " +
                "after a stop failure: " +
                $"{ex.Message}");
        }

        if (stopException is not null)
        {
            throw new AudioDeviceUnavailableException(
                $"Failed to stop capture on '{_resolvedDevice.Name}'.",
                stopException);
        }
    }

    /// <summary>
    ///     Resolves the selected capture device or the host-API-scoped default capture device.
    /// </summary>
    /// <returns>
    ///     The resolved capture device metadata, or <see langword="null"/> when no device is
    ///     available on the preferred host API.
    /// </returns>
    private ResolvedCaptureDevice? ResolveDevice()
    {
        if (!_environment.TryResolvePreferredHostApi(out var hostApiIndex, out var hostApiInfo))
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                "PortAudio capture device resolution is unavailable because the preferred host API could not be resolved.");
            return null;
        }

        var eligibleDevices = new List<ResolvedCaptureDevice>();
        for (var deviceIndex = 0; deviceIndex < _environment.Api.DeviceCount; deviceIndex++)
        {
            var deviceInfo = _environment.Api.GetDeviceInfo(deviceIndex);
            if (deviceInfo.HostApiIndex != hostApiIndex || deviceInfo.MaxInputChannels <= 0)
            {
                continue;
            }

            eligibleDevices.Add(
                new ResolvedCaptureDevice(
                    deviceIndex,
                    deviceInfo.Name,
                    ResolveChannelCount(deviceInfo.MaxInputChannels),
                    _preferredFormat?.SampleRate ?? deviceInfo.DefaultSampleRate));
        }

        var selectedDevice = ResolveSelectedDevice(eligibleDevices, hostApiInfo.DefaultInputDeviceIndex);
        if (selectedDevice is null)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                "No PortAudio capture device matched the requested selection or host-API default.");
            return null;
        }

        var resolutionBasis = string.Equals(selectedDevice.Name, _selection.DeviceName, StringComparison.Ordinal)
            ? "selection"
            : "host API default";
        _diagnostics.Report(
            SpeechDiagnosticLevel.Info,
            DiagnosticsCategory,
            $"Resolved PortAudio capture device '{selectedDevice.Name}' via {resolutionBasis}.");
        return selectedDevice;
    }

    /// <summary>
    ///     Resolves a named device when present, otherwise falls back to the host-API-scoped
    ///     default input device.
    /// </summary>
    /// <param name="eligibleDevices">
    ///     The capture-capable devices available on the preferred host API.
    /// </param>
    /// <param name="defaultDeviceIndex">
    ///     The host-API-scoped default input-device index.
    /// </param>
    /// <returns>
    ///     The resolved device when one is available; otherwise, <see langword="null"/>.
    /// </returns>
    private ResolvedCaptureDevice? ResolveSelectedDevice(
        IReadOnlyList<ResolvedCaptureDevice> eligibleDevices,
        int defaultDeviceIndex)
    {
        if (_selection.DeviceName is not null)
        {
            var selectedByName = eligibleDevices.FirstOrDefault(
                device => string.Equals(device.Name, _selection.DeviceName, StringComparison.Ordinal));
            if (selectedByName is not null)
            {
                return selectedByName;
            }
        }

        return eligibleDevices.FirstOrDefault(device => device.DeviceIndex == defaultDeviceIndex);
    }

    /// <summary>
    ///     Resolves the capture channel count to request for one device, clamping any preferred
    ///     value to the device's advertised capability.
    /// </summary>
    /// <param name="maxInputChannels">
    ///     The device's maximum supported input-channel count.
    /// </param>
    /// <returns>
    ///     The preferred channel count when one was supplied and does not exceed the device's
    ///     capability; otherwise the device capability itself.
    /// </returns>
    private int ResolveChannelCount(int maxInputChannels)
    {
        var resolvedChannelCount = Math.Min(_preferredFormat?.ChannelCount ?? maxInputChannels, maxInputChannels);
        if (_preferredFormat is not null && _preferredFormat.ChannelCount > maxInputChannels)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Info,
                DiagnosticsCategory,
                $"Clamped preferred capture channel count {_preferredFormat.ChannelCount} to device capability {maxInputChannels}.");
        }

        return resolvedChannelCount;
    }

    /// <summary>
    ///     Raises <see cref="FrameCaptured"/> for one managed capture block.
    /// </summary>
    /// <param name="samples">
    ///     The interleaved samples delivered by the PortAudio seam.
    /// </param>
    private void OnSamplesCaptured(IReadOnlyList<float> samples)
    {
        try
        {
            FrameCaptured?.Invoke(this, new AudioCaptureFrameEventArgs(samples.ToArray()));
        }
        catch (Exception ex)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Error,
                DiagnosticsCategory,
                $"An audio capture callback handler failed on '{_resolvedDevice?.Name}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Immutable metadata for one resolved capture device.
    /// </summary>
    /// <param name="DeviceIndex">The PortAudio runtime device index.</param>
    /// <param name="Name">The PortAudio-reported device name.</param>
    /// <param name="ChannelCount">The capture channel count to request.</param>
    /// <param name="SampleRate">The capture sample rate to request.</param>
    private sealed record ResolvedCaptureDevice(int DeviceIndex, string Name, int ChannelCount, int SampleRate);
}
