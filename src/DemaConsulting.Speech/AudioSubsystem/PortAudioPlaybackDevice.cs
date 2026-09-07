using System.Collections.Concurrent;
using DemaConsulting.Speech.AudioSubsystem.PortAudio;
using DemaConsulting.Speech.Diagnostics;

namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     PortAudio-backed implementation of <see cref="IAudioPlaybackDevice"/> that resolves one
///     preferred output device and feeds PortAudio callback requests from a managed sample queue.
/// </summary>
/// <remarks>
///     Construction never throws. When PortAudio initialized successfully but no device can be
///     resolved for the current selection or host-API default, the instance honestly reports
///     <see cref="IsAvailable"/> as <see langword="false"/> and operational members throw
///     <see cref="AudioDeviceUnavailableException"/> only when invoked.
/// </remarks>
internal sealed class PortAudioPlaybackDevice : IAudioPlaybackDevice
{
    /// <summary>
    ///     The PortAudio callback block size requested for playback streams.
    /// </summary>
    internal const uint FramesPerBuffer = 0;

    /// <summary>
    ///     The diagnostics category reported for every event raised by this class.
    /// </summary>
    private const string DiagnosticsCategory = "AudioSubsystem";

    /// <summary>
    ///     Initializes a new instance of the <see cref="PortAudioPlaybackDevice"/> class.
    /// </summary>
    /// <param name="environment">
    ///     The PortAudio environment used to resolve and open the selected playback device.
    /// </param>
    /// <param name="selection">
    ///     The requested playback-device selection, or <see langword="null"/> to use the host-
    ///     API-scoped default output device.
    /// </param>
    /// <param name="diagnostics">
    ///     The diagnostics sink for structural selection/start/stop/fault events, or
    ///     <see langword="null"/> to use <see cref="NullSpeechDiagnostics.Instance"/>.
    /// </param>
    /// <param name="preferredFormat">
    ///     The preferred playback format to request for the resolved device, or
    ///     <see langword="null"/> to request the device's own default sample rate and full output
    ///     channel capacity. The preferred sample rate is only honored when the resolved device's
    ///     host API confirms it can actually be opened; otherwise the device's default sample
    ///     rate is used instead.
    /// </param>
    internal PortAudioPlaybackDevice(
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
    ///     The managed sample queue drained by the PortAudio playback callback.
    /// </summary>
    private readonly ConcurrentQueue<float> _queuedSamples = new();

    /// <summary>
    ///     The number of samples enqueued via <see cref="Write"/> that <see cref="ProvideSamples"/>
    ///     has not yet dequeued for actual hardware rendering. Updated with <see cref="Interlocked"/>
    ///     because <see cref="Write"/> runs on caller threads while <see cref="ProvideSamples"/>
    ///     runs on PortAudio's own real-time callback thread.
    /// </summary>
    private long _pendingSampleCount;

    /// <summary>
    ///     The PortAudio environment used to resolve and open the selected playback device.
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
    ///     The optional preferred playback format to request when resolving the device.
    /// </summary>
    private readonly AudioFormat? _preferredFormat;

    /// <summary>
    ///     The resolved device metadata, or <see langword="null"/> when no playback device could
    ///     be resolved on the preferred host API.
    /// </summary>
    private readonly ResolvedPlaybackDevice? _resolvedDevice;

    /// <summary>
    ///     The currently opened PortAudio stream, when playback has been started.
    /// </summary>
    private IPortAudioStream? _stream;

    /// <inheritdoc/>
    public bool IsAvailable => _resolvedDevice is not null;

    /// <inheritdoc/>
    /// <remarks>
    ///     Reports the channel count requested at construction time: either the resolved device's
    ///     own full output-channel capacity, or the caller's preferred channel count clamped down
    ///     to that capability. This is the same count requested when the playback stream is
    ///     opened and therefore the interleaving stride every <see cref="Write"/> call must
    ///     supply.
    /// </remarks>
    public int ChannelCount => _resolvedDevice?.ChannelCount ?? 0;

    /// <inheritdoc/>
    /// <remarks>
    ///     Reports the sample rate requested at construction time: either the resolved device's
    ///     own PortAudio-reported default rate, or the caller's preferred sample rate when one
    ///     was supplied and confirmed openable on the resolved device's host API - a preferred
    ///     rate the host API cannot open falls back to the device's default rate instead. This is
    ///     the same rate requested when the playback stream is opened.
    /// </remarks>
    public int SampleRate => _resolvedDevice?.SampleRate ?? 0;

    /// <inheritdoc/>
    public void Start()
    {
        if (_resolvedDevice is null)
        {
            throw new AudioDeviceUnavailableException(
                "Cannot start playback: no PortAudio playback device could be resolved.");
        }

        lock (_syncRoot)
        {
            if (_stream is not null)
            {
                return;
            }

            try
            {
                _stream = _environment.Api.OpenPlaybackStream(
                    _resolvedDevice.DeviceIndex,
                    _resolvedDevice.ChannelCount,
                    _resolvedDevice.SampleRate,
                    FramesPerBuffer,
                    ProvideSamples);
                _stream.Start();
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Info,
                    DiagnosticsCategory,
                    $"Started PortAudio playback on '{_resolvedDevice.Name}'.");
            }
            catch (Exception ex)
            {
                _stream?.Dispose();
                _stream = null;
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Error,
                    DiagnosticsCategory,
                    $"Failed to start PortAudio playback on '{_resolvedDevice.Name}': {ex.Message}");
                throw new AudioDeviceUnavailableException(
                    $"Failed to start playback on '{_resolvedDevice.Name}'.",
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
                "Cannot stop playback: no PortAudio playback device could be resolved.");
        }

        // Held for the entire stop/dispose sequence (not just the field swap) so a concurrent
        // Start() cannot open a replacement stream while this one is still shutting down.
        lock (_syncRoot)
        {
            var streamToStop = _stream;
            _stream = null;

            if (streamToStop is null)
            {
                ClearQueuedSamples();
                return;
            }

            Exception? stopException = null;
            try
            {
                streamToStop.Stop();
                ClearQueuedSamples();
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Info,
                    DiagnosticsCategory,
                    $"Stopped PortAudio playback on '{_resolvedDevice.Name}'.");
            }
            catch (Exception ex)
            {
                stopException = ex;
                ClearQueuedSamples();
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Error,
                    DiagnosticsCategory,
                    $"Failed to stop PortAudio playback on '{_resolvedDevice.Name}': {ex.Message}");
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
                    $"Failed to dispose PortAudio playback stream on '{_resolvedDevice.Name}': {ex.Message}");
                throw new AudioDeviceUnavailableException(
                    $"Failed to dispose playback stream on '{_resolvedDevice.Name}' after stopping it.",
                    ex);
            }
            catch (Exception ex)
            {
                _diagnostics.Report(
                    SpeechDiagnosticLevel.Error,
                    DiagnosticsCategory,
                    $"Failed to dispose PortAudio playback stream on '{_resolvedDevice.Name}' " +
                    "after a stop failure: " +
                    $"{ex.Message}");
            }

            if (stopException is not null)
            {
                throw new AudioDeviceUnavailableException(
                    $"Failed to stop playback on '{_resolvedDevice.Name}'.",
                    stopException);
            }
        }
    }

    /// <inheritdoc/>
    public void Write(IReadOnlyList<float> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        if (_resolvedDevice is null)
        {
            throw new AudioDeviceUnavailableException(
                "Cannot write audio: no PortAudio playback device could be resolved.");
        }

        if (samples.Count == 0)
        {
            return;
        }

        for (var index = 0; index < samples.Count; index++)
        {
            _queuedSamples.Enqueue(samples[index]);
        }

        Interlocked.Add(ref _pendingSampleCount, samples.Count);
    }

    /// <inheritdoc/>
    /// <remarks>
    ///     Reflects samples enqueued via <see cref="Write"/> that <see cref="ProvideSamples"/> has
    ///     not yet dequeued for the playback callback. Reads <c>0</c> once
    ///     <see cref="IsAvailable"/> is <see langword="false"/> or once <see cref="Stop"/> has
    ///     cleared the queue, matching every other zero-when-unavailable member of this type.
    /// </remarks>
    public long PendingSampleCount => _resolvedDevice is null ? 0 : Interlocked.Read(ref _pendingSampleCount);

    /// <summary>
    ///     Resolves the selected playback device or the host-API-scoped default playback device.
    /// </summary>
    /// <returns>
    ///     The resolved playback device metadata, or <see langword="null"/> when no device is
    ///     available on the preferred host API.
    /// </returns>
    private ResolvedPlaybackDevice? ResolveDevice()
    {
        if (!_environment.TryResolvePreferredHostApi(out var hostApiIndex, out var hostApiInfo))
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                "PortAudio playback device resolution is unavailable because the preferred host API could not be resolved.");
            return null;
        }

        var eligibleDevices = new List<(int DeviceIndex, PortAudioDeviceInfo DeviceInfo)>();
        for (var deviceIndex = 0; deviceIndex < _environment.Api.DeviceCount; deviceIndex++)
        {
            var deviceInfo = _environment.Api.GetDeviceInfo(deviceIndex);
            if (deviceInfo.HostApiIndex != hostApiIndex || deviceInfo.MaxOutputChannels <= 0)
            {
                continue;
            }

            eligibleDevices.Add((deviceIndex, deviceInfo));
        }

        var selectedDevice = ResolveSelectedDevice(eligibleDevices, hostApiInfo.DefaultOutputDeviceIndex);
        if (selectedDevice is null)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Warning,
                DiagnosticsCategory,
                "No PortAudio playback device matched the requested selection or host-API default.");
            return null;
        }

        // Only the device actually selected needs its preferred format negotiated: probing every
        // eligible device would perform unnecessary native calls and could report a misleading
        // fallback diagnostic for a device that was never going to be used.
        var (selectedDeviceIndex, selectedDeviceInfo) = selectedDevice.Value;
        var resolvedChannelCount = ResolveChannelCount(selectedDeviceInfo.MaxOutputChannels);
        var resolvedDevice = new ResolvedPlaybackDevice(
            selectedDeviceIndex,
            selectedDeviceInfo.Name,
            resolvedChannelCount,
            ResolveSampleRate(selectedDeviceIndex, resolvedChannelCount, selectedDeviceInfo));

        var resolutionBasis = string.Equals(resolvedDevice.Name, _selection.DeviceName, StringComparison.Ordinal)
            ? "selection"
            : "host API default";
        _diagnostics.Report(
            SpeechDiagnosticLevel.Info,
            DiagnosticsCategory,
            $"Resolved PortAudio playback device '{resolvedDevice.Name}' via {resolutionBasis}.");
        return resolvedDevice;
    }

    /// <summary>
    ///     Resolves a named device when present, otherwise falls back to the host-API-scoped
    ///     default output device.
    /// </summary>
    /// <param name="eligibleDevices">
    ///     The playback-capable devices available on the preferred host API.
    /// </param>
    /// <param name="defaultDeviceIndex">
    ///     The host-API-scoped default output-device index.
    /// </param>
    /// <returns>
    ///     The device index and metadata for the resolved device when one is available;
    ///     otherwise, <see langword="null"/>.
    /// </returns>
    private (int DeviceIndex, PortAudioDeviceInfo DeviceInfo)? ResolveSelectedDevice(
        IReadOnlyList<(int DeviceIndex, PortAudioDeviceInfo DeviceInfo)> eligibleDevices,
        int defaultDeviceIndex)
    {
        if (_selection.DeviceName is not null)
        {
            var selectedByName = eligibleDevices.FirstOrDefault(
                device => string.Equals(device.DeviceInfo.Name, _selection.DeviceName, StringComparison.Ordinal));
            if (selectedByName.DeviceInfo is not null)
            {
                return selectedByName;
            }
        }

        var selectedByDefault = eligibleDevices.FirstOrDefault(device => device.DeviceIndex == defaultDeviceIndex);
        return selectedByDefault.DeviceInfo is not null ? selectedByDefault : null;
    }

    /// <summary>
    ///     Resolves the playback channel count to request for one device, clamping any preferred
    ///     value to the device's advertised capability.
    /// </summary>
    /// <param name="maxOutputChannels">
    ///     The device's maximum supported output-channel count.
    /// </param>
    /// <returns>
    ///     The preferred channel count when one was supplied and does not exceed the device's
    ///     capability; otherwise the device capability itself.
    /// </returns>
    private int ResolveChannelCount(int maxOutputChannels)
    {
        var resolvedChannelCount = Math.Min(_preferredFormat?.ChannelCount ?? maxOutputChannels, maxOutputChannels);
        if (_preferredFormat is not null && _preferredFormat.ChannelCount > maxOutputChannels)
        {
            _diagnostics.Report(
                SpeechDiagnosticLevel.Info,
                DiagnosticsCategory,
                $"Clamped preferred playback channel count {_preferredFormat.ChannelCount} to device capability {maxOutputChannels}.");
        }

        return resolvedChannelCount;
    }

    /// <summary>
    ///     Resolves the playback sample rate to request for one device, negotiating any
    ///     preferred rate against the device/host API's actual capability before honoring it.
    /// </summary>
    /// <param name="deviceIndex">The PortAudio runtime device index being resolved.</param>
    /// <param name="channelCount">The resolved playback channel count for this device.</param>
    /// <param name="deviceInfo">The device metadata reported for this device index.</param>
    /// <returns>
    ///     The preferred sample rate when one was supplied and confirmed openable by the host
    ///     API; otherwise, the device's own default sample rate.
    /// </returns>
    private int ResolveSampleRate(int deviceIndex, int channelCount, PortAudioDeviceInfo deviceInfo)
    {
        if (_preferredFormat is null)
        {
            return deviceInfo.DefaultSampleRate;
        }

        var preferredSampleRate = _preferredFormat.SampleRate;
        if (_environment.Api.IsPlaybackFormatSupported(deviceIndex, channelCount, preferredSampleRate))
        {
            return preferredSampleRate;
        }

        _diagnostics.Report(
            SpeechDiagnosticLevel.Info,
            DiagnosticsCategory,
            $"Preferred playback sample rate {preferredSampleRate} Hz is not supported by device " +
            $"'{deviceInfo.Name}'; falling back to the device's default sample rate " +
            $"{deviceInfo.DefaultSampleRate} Hz.");
        return deviceInfo.DefaultSampleRate;
    }

    /// <summary>
    ///     Dequeues up to the requested number of interleaved samples, zero-filling any shortfall.
    /// </summary>
    /// <param name="sampleCount">
    ///     The exact number of samples the PortAudio playback callback requested.
    /// </param>
    /// <returns>
    ///     A buffer containing exactly <paramref name="sampleCount"/> samples.
    /// </returns>
    private IReadOnlyList<float> ProvideSamples(int sampleCount)
    {
        if (sampleCount <= 0)
        {
            return [];
        }

        var samples = new float[sampleCount];
        var dequeuedCount = 0;
        for (var index = 0; index < sampleCount; index++)
        {
            if (_queuedSamples.TryDequeue(out var sample))
            {
                samples[index] = sample;
                dequeuedCount++;
            }
        }

        if (dequeuedCount > 0)
        {
            Interlocked.Add(ref _pendingSampleCount, -dequeuedCount);
        }

        return samples;
    }

    /// <summary>
    ///     Drops any buffered samples so a restarted stream never replays stale audio.
    /// </summary>
    private void ClearQueuedSamples()
    {
        _queuedSamples.Clear();
        Interlocked.Exchange(ref _pendingSampleCount, 0);
    }

    /// <summary>
    ///     Immutable metadata for one resolved playback device.
    /// </summary>
    /// <param name="DeviceIndex">The PortAudio runtime device index.</param>
    /// <param name="Name">The PortAudio-reported device name.</param>
    /// <param name="ChannelCount">The playback channel count to request.</param>
    /// <param name="SampleRate">The playback sample rate to request.</param>
    private sealed record ResolvedPlaybackDevice(int DeviceIndex, string Name, int ChannelCount, int SampleRate);
}
