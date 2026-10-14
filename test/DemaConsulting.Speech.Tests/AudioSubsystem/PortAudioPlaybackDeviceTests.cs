using System.Runtime.InteropServices;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.AudioSubsystem.PortAudio;
using DemaConsulting.Speech.Diagnostics;
using NSubstitute;

namespace DemaConsulting.Speech.Tests.AudioSubsystem;

/// <summary>
///     Unit tests for <see cref="PortAudioPlaybackDevice"/>.
/// </summary>
public class PortAudioPlaybackDeviceTests
{
    /// <summary>
    ///     Proves that a stale named selection falls back to the host-API-scoped default device.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Constructor_StaleSelection_FallsBackToDefaultDevice()
    {
        // Arrange: a fake runtime whose selected name is absent but whose host-API default exists
        var api = new FakePortAudioApi(
        [
            new PortAudioDeviceInfo("Default Speaker", 5, 0, 2, 48000, 0.0, 0.01),
            new PortAudioDeviceInfo("Backup Speaker", 5, 0, 1, 16000, 0.0, 0.01)
        ])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
        };
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct with a stale persisted selection
        var device = new PortAudioPlaybackDevice(environment, new AudioDeviceSelection("Missing Speaker"), diagnostics);

        // Assert: the real device remains available by falling back to the host-API default
        Assert.True(device.IsAvailable);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Info,
            "AudioSubsystem",
            "Resolved PortAudio playback device 'Default Speaker' via host API default.");
    }

    /// <summary>
    ///     Proves that queued samples are drained in order and zero-filled on underrun.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Write_StreamRequestsSamples_DrainsQueueAndZeroFills()
    {
        // Arrange: a fake runtime with one default speaker and a playback-stream fake
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment);

        // Act: queue three samples, start playback, request five samples, then stop
        device.Write([0.25f, -0.5f, 0.75f]);
        device.Start();
        var drainedSamples = stream.RequestSamples(5);
        device.Stop();

        // Assert: samples preserve order and the shortfall is padded with silence
        Assert.Equal([0.25f, -0.5f, 0.75f, 0.0f, 0.0f], drainedSamples);
        Assert.Equal(1, stream.StartCallCount);
        Assert.Equal(1, stream.StopCallCount);
        Assert.Equal(1, stream.DisposeCallCount);
    }

    /// <summary>
    ///     Proves that a seam open failure is surfaced as <see cref="AudioDeviceUnavailableException"/>.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Start_OpenFails_ThrowsAudioDeviceUnavailableException()
    {
        // Arrange: a fake runtime whose playback-stream open attempt fails for the resolved default device
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            OpenPlaybackException = new InvalidOperationException("Native stream open failed.")
        };
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment, diagnostics: diagnostics);

        // Act / Assert: the device degrades a native open failure into the documented exception
        var exception = Assert.Throws<AudioDeviceUnavailableException>(device.Start);
        Assert.Equal("Failed to start playback on 'Speaker'.", exception.Message);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "AudioSubsystem",
            "Failed to start PortAudio playback on 'Speaker': Native stream open failed.");
    }

    /// <summary>
    ///     Proves that a runtime with no resolvable playback device honestly reports itself as unavailable.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_IsAvailable_NoResolvableDevice_ReturnsFalse()
    {
        // Arrange: a fake runtime whose preferred host API exists but has no playback-capable devices
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Mic", 5, 2, 0, 48000, 0.01, 0.0)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, -1)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct the device against the empty playback catalog
        var device = new PortAudioPlaybackDevice(environment);

        // Assert: the device is honestly unavailable, write throws the documented exception, and
        // pending samples honestly reports zero rather than a value it could never deliver
        Assert.False(device.IsAvailable);
        Assert.Throws<AudioDeviceUnavailableException>(() => device.Write([0.1f]));
        Assert.Equal(0, device.PendingSampleCount);
    }

    /// <summary>
    ///     Proves that a resolved playback device reports the resolved device's own channel count
    ///     and sample rate, so a consumer can upmix and resample synthesized audio correctly.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_PlaybackFormat_ResolvedDevice_ReflectsResolvedDeviceFormat()
    {
        // Arrange: a fake runtime whose host-API default speaker is stereo at 48 kHz
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct the device against the resolvable playback catalog
        var device = new PortAudioPlaybackDevice(environment);

        // Assert: the reported format matches the resolved device
        Assert.True(device.IsAvailable);
        Assert.Equal(2, device.ChannelCount);
        Assert.Equal(48000, device.SampleRate);
    }

    /// <summary>
    ///     Proves that a preferred playback format within device capability is honored.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Constructor_PreferredFormatWithinCapability_UsesPreferredFormat()
    {
        // Arrange: a device with more channels and a different default rate than requested
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 4, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct the device with a preferred 24 kHz stereo format
        var device = new PortAudioPlaybackDevice(environment, preferredFormat: new AudioFormat(24000, 2));

        // Assert: the preferred format is honored although the device default differs
        Assert.Equal(24000, device.SampleRate);
        Assert.Equal(2, device.ChannelCount);
    }

    /// <summary>
    ///     Proves that a preferred sample rate the host API cannot open falls back to the
    ///     device's default sample rate and reports an Info diagnostic, so hardware whose native
    ///     mix rate differs from the caller's preference never fails to start.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Constructor_PreferredSampleRateUnsupported_FallsBackToDeviceDefaultSampleRateAndReportsDiagnostic()
    {
        // Arrange: a device that rejects the preferred sample rate via the format-negotiation probe
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            IsPlaybackFormatSupportedResult = false
        };
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct the device with an unsupported preferred sample rate
        var device = new PortAudioPlaybackDevice(
            environment,
            diagnostics: diagnostics,
            preferredFormat: new AudioFormat(24000, 2));

        // Assert: the device falls back to the device default rather than the unsupported preference
        Assert.Equal(48000, device.SampleRate);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Info,
            "AudioSubsystem",
            "Preferred playback sample rate 24000 Hz is not supported by device 'Speaker'; falling " +
            "back to the device's default sample rate 48000 Hz.");
    }

    /// <summary>
    ///     Proves that a preferred sample rate the host API confirms it can open is honored.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Constructor_PreferredSampleRateSupported_UsesPreferredFormat()
    {
        // Arrange: a device that confirms the preferred sample rate via the format-negotiation probe
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            IsPlaybackFormatSupportedResult = true
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct the device with a supported preferred format
        var device = new PortAudioPlaybackDevice(environment, preferredFormat: new AudioFormat(24000, 2));

        // Assert: the confirmed-openable preferred rate is used, not the device default
        Assert.Equal(24000, device.SampleRate);
    }

    /// <summary>
    ///     Proves that an over-large preferred channel count is clamped to the device capability.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Constructor_PreferredChannelCountExceedsCapability_ClampsAndReportsDiagnostic()
    {
        // Arrange: a stereo-capable device and a larger preferred channel count
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
        };
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct the device with a preferred channel count above its capability
        var device = new PortAudioPlaybackDevice(
            environment,
            diagnostics: diagnostics,
            preferredFormat: new AudioFormat(24000, 4));

        // Assert: the channel count is clamped and reported
        Assert.Equal(24000, device.SampleRate);
        Assert.Equal(2, device.ChannelCount);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Info,
            "AudioSubsystem",
            "Clamped preferred playback channel count 4 to device capability 2.");
    }

    /// <summary>
    ///     Proves that omitting a preferred format preserves the previous default behavior.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Constructor_PreferredFormatOmitted_UsesDeviceDefaultFormat()
    {
        // Arrange: a three-channel 44.1 kHz device and no preferred format
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 3, 44100, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct the device without a preferred format
        var device = new PortAudioPlaybackDevice(environment);

        // Assert: the device's own default format is reported
        Assert.Equal(44100, device.SampleRate);
        Assert.Equal(3, device.ChannelCount);
    }

    /// <summary>
    ///     Proves that an unresolved playback device reports a zero playback format, matching its
    ///     false availability flag instead of advertising a format it cannot deliver.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_PlaybackFormat_NoResolvableDevice_ReturnsZeroRateAndChannelCount()
    {
        // Arrange: a fake runtime whose preferred host API has no playback-capable devices
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Mic", 5, 2, 0, 48000, 0.01, 0.0)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, -1)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct the device against the empty playback catalog
        var device = new PortAudioPlaybackDevice(environment);

        // Assert: both format values are zero
        Assert.False(device.IsAvailable);
        Assert.Equal(0, device.ChannelCount);
        Assert.Equal(0, device.SampleRate);
    }

    /// <summary>
    ///     Proves that <see cref="PortAudioPlaybackDevice.PendingSampleCount"/> tracks samples
    ///     enqueued via <see cref="PortAudioPlaybackDevice.Write"/> that the callback has not yet
    ///     dequeued, decreasing only by however many real samples a callback actually drained
    ///     (never by a zero-fill shortfall), and resets to zero once <see cref="PortAudioPlaybackDevice.Stop"/>
    ///     clears the queue.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_PendingSampleCount_WriteDrainAndStop_TracksQueueDepth()
    {
        // Arrange: a fake runtime with one default speaker and a playback-stream fake
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment);

        // Act & Assert: writing samples increases the pending count before any playback starts
        device.Write([0.1f, 0.2f, 0.3f]);
        Assert.Equal(3, device.PendingSampleCount);

        device.Start();

        // A callback draining fewer samples than queued reduces the pending count by exactly that many
        stream.RequestSamples(2);
        Assert.Equal(1, device.PendingSampleCount);

        // A callback requesting more than remains drains to exactly zero - the zero-fill padding
        // used to complete the callback's buffer must never be counted as still pending
        stream.RequestSamples(5);
        Assert.Equal(0, device.PendingSampleCount);

        // Stopping after further writes clears the queue, so pending samples reset to zero too
        device.Write([0.4f, 0.5f]);
        Assert.Equal(2, device.PendingSampleCount);
        device.Stop();
        Assert.Equal(0, device.PendingSampleCount);
    }

    /// <summary>
    ///     Proves that multiple <see cref="PortAudioPlaybackDevice.Write"/> calls of varying block
    ///     sizes are queued as separate blocks and drained by the playback callback in the exact
    ///     order they were written, with <see cref="PortAudioPlaybackDevice.PendingSampleCount"/>
    ///     staying accurate throughout.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Write_MultipleBlocksOfVaryingSize_DrainsInOrder()
    {
        // Arrange: a fake runtime with one default speaker and a playback-stream fake
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment);

        // Act: queue three separately sized blocks, then drain exactly their combined length
        device.Write([0.1f]);
        device.Write([0.2f, 0.3f]);
        device.Write([0.4f, 0.5f, 0.6f]);
        Assert.Equal(6, device.PendingSampleCount);

        device.Start();
        var drainedSamples = stream.RequestSamples(6);
        device.Stop();

        // Assert: samples are returned in write order regardless of block boundaries
        Assert.Equal([0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f], drainedSamples);
        Assert.Equal(0, device.PendingSampleCount);
    }

    /// <summary>
    ///     Proves that a single <see cref="PortAudioPlaybackDevice.Write"/> call larger than one
    ///     playback callback request is drained across multiple callback invocations, with the
    ///     unconsumed remainder of the block preserved and returned correctly on the next call.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Write_BlockLargerThanRequest_DrainsRemainderOnNextCall()
    {
        // Arrange: a fake runtime with one default speaker and a playback-stream fake
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment);

        // Act: queue one block larger than the first requested callback size
        device.Write([0.1f, 0.2f, 0.3f, 0.4f, 0.5f]);
        device.Start();
        var firstRequest = stream.RequestSamples(2);
        Assert.Equal(3, device.PendingSampleCount);
        var secondRequest = stream.RequestSamples(3);
        device.Stop();

        // Assert: the leftover remainder of the block is preserved and returned on the next call
        Assert.Equal([0.1f, 0.2f], firstRequest);
        Assert.Equal([0.3f, 0.4f, 0.5f], secondRequest);
        Assert.Equal(0, device.PendingSampleCount);
    }

    /// <summary>
    ///     Proves that a <see cref="PortAudioPlaybackDevice.Write"/> smaller than one playback
    ///     callback request, combined with a subsequent <see cref="PortAudioPlaybackDevice.Write"/>,
    ///     drains as a seamless concatenation across the block boundary within a single callback
    ///     invocation.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Write_SmallBlockFollowedByAnotherWrite_ConcatenatesAcrossBoundary()
    {
        // Arrange: a fake runtime with one default speaker and a playback-stream fake
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment);

        // Act: queue a small block, then a second block, and drain both in a single request that
        // spans the boundary between them
        device.Write([0.1f, 0.2f]);
        device.Write([0.3f]);
        Assert.Equal(3, device.PendingSampleCount);

        device.Start();
        var drainedSamples = stream.RequestSamples(3);
        device.Stop();

        // Assert: the two blocks concatenate seamlessly in write order
        Assert.Equal([0.1f, 0.2f, 0.3f], drainedSamples);
        Assert.Equal(0, device.PendingSampleCount);
    }

    /// <summary>
    ///     Proves that a partially consumed block never replays after <see cref="PortAudioPlaybackDevice.Stop"/>
    ///     and a subsequent <see cref="PortAudioPlaybackDevice.Start"/>: the first callback after
    ///     restart returns all zeros rather than the stale leftover remainder.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Stop_PartiallyConsumedBlock_RestartNeverReplaysStaleAudio()
    {
        // Arrange: a fake runtime with one default speaker and a playback-stream fake
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment);

        // Act: queue a block, partially consume it via the callback, then stop with a leftover
        // remainder still unconsumed, and restart
        device.Write([0.1f, 0.2f, 0.3f, 0.4f]);
        device.Start();
        stream.RequestSamples(2);
        device.Stop();

        device.Start();
        var drainedSamples = stream.RequestSamples(4);
        device.Stop();

        // Assert: the leftover remainder never replays; the restarted stream sees pure silence
        Assert.Equal([0.0f, 0.0f, 0.0f, 0.0f], drainedSamples);
        Assert.Equal(0, device.PendingSampleCount);
    }

    /// <summary>
    ///     Proves that starting a playback device registers an active stream with its
    ///     environment, so a refresh attempted while the stream is running is refused.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Start_RegistersActiveStreamWithEnvironment_RefreshThrowsWhileStarted()
    {
        // Arrange: a real environment and device, with a playback stream ready to start
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment);

        // Act: start the device
        device.Start();

        // Assert: a refresh while the stream is active is refused
        var exception = Assert.Throws<AudioDeviceInUseException>(environment.Refresh);
        Assert.Contains("Speaker", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Proves that stopping a playback device unregisters its active stream, so a refresh
    ///     attempted afterward succeeds.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Stop_UnregistersActiveStream_RefreshSucceedsAfterStop()
    {
        // Arrange: a real environment and device, started and then stopped
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment);
        device.Start();
        device.Stop();

        // Act: refresh after the stream has stopped
        var exception = Record.Exception(environment.Refresh);

        // Assert: the refresh succeeds because no active stream remains registered
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that when the native stream both fails to start and then fails to dispose
    ///     during start-failure cleanup, the device still unregisters its active-stream
    ///     registration and reports/throws the original start failure rather than the dispose
    ///     failure.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Start_DisposeThrowsDuringStartFailureCleanup_UnregistersActiveStreamAndThrowsOriginalStartFailure()
    {
        // Arrange: a stream whose Start() and Dispose() both throw
        var startException = new InvalidOperationException("Native start failed.");
        var disposeException = new InvalidOperationException("Native dispose failed.");
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = new FakePortAudioStream
            {
                StartException = startException,
                DisposeException = disposeException
            }
        };
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment, diagnostics: diagnostics);

        // Act: start the device, which fails to open the stream and then fails to dispose it
        var exception = Assert.Throws<AudioDeviceUnavailableException>(device.Start);

        // Assert: the thrown exception wraps the original start failure, not the dispose failure
        Assert.Same(startException, exception.InnerException);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "AudioSubsystem",
            Arg.Is<string>(message => message.Contains("Native dispose failed.", StringComparison.Ordinal)));

        // Assert: the active-stream registration was still released despite the dispose fault, so
        // a subsequent refresh succeeds rather than being refused as in-use
        var refreshException = Record.Exception(environment.Refresh);
        Assert.Null(refreshException);
    }

    /// <summary>
    ///     Proves that a playback device resolved before a device-table refresh refuses to start
    ///     afterward, because its cached device index may no longer be valid.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Start_EnvironmentRefreshedAfterConstruction_ThrowsAudioDeviceUnavailableException()
    {
        // Arrange: a device resolved before the environment is refreshed
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment);

        // Act: refresh the environment after the device was resolved
        environment.Refresh();

        // Assert: starting the now-stale device throws, and the stream was never registered active
        var exception = Assert.Throws<AudioDeviceUnavailableException>(device.Start);
        Assert.Contains("Speaker", exception.Message, StringComparison.Ordinal);
        Assert.Null(Record.Exception(environment.Refresh));
    }

    /// <summary>
    ///     Proves that a playback device resolved after a device-table refresh starts normally,
    ///     because its captured generation matches the environment's current generation.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Start_ConstructedAfterRefresh_StartsNormally()
    {
        // Arrange: refresh the environment first, then resolve the device afterward
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        environment.Refresh();
        var device = new PortAudioPlaybackDevice(environment);

        // Act / Assert: starting the freshly-resolved device does not throw
        var exception = Record.Exception(device.Start);
        Assert.Null(exception);
    }

    /// <summary>
    ///     Proves that a fault raised while querying the device table during construction is
    ///     contained: the device reports itself unavailable and records an error diagnostic.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Constructor_DeviceQueryFaults_ReportsUnavailableAndDiagnostic()
    {
        // Arrange: a runtime whose device enumeration faults
        var api = new FakePortAudioApi([])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            DeviceCountException = new InvalidOperationException("Native enumeration failed.")
        };
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct the device
        var device = new PortAudioPlaybackDevice(environment, diagnostics: diagnostics);

        // Assert: construction did not throw, the device is unavailable and the fault was reported
        Assert.False(device.IsAvailable);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "AudioSubsystem",
            Arg.Is<string>(message => message.Contains("Native enumeration failed.", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Proves that starting, stopping and writing on an unresolved device throw
    ///     <see cref="AudioDeviceUnavailableException"/> while a null write is rejected.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Operations_NoResolvableDevice_ThrowAudioDeviceUnavailableException()
    {
        // Arrange: a runtime with only an input device, so no playback device resolves
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Mic", 5, 2, 0, 48000, 0.01, 0.0)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 0, -1)
        };
        var device = new PortAudioPlaybackDevice(new PortAudioEnvironment(api, OSPlatform.Windows));

        // Act / Assert: every operational member refuses, and a null write is an argument error
        Assert.Throws<AudioDeviceUnavailableException>(device.Start);
        Assert.Throws<AudioDeviceUnavailableException>(device.Stop);
        Assert.Throws<AudioDeviceUnavailableException>(() => device.Write([0.1f]));
        Assert.Throws<ArgumentNullException>(() => device.Write(null!));
        Assert.Equal(0, device.PendingSampleCount);
    }

    /// <summary>
    ///     Proves that an empty write is ignored and a write from a non-collection list is queued
    ///     element by element.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Write_EmptyAndNonCollectionLists_HandledCorrectly()
    {
        // Arrange: a started device
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var device = new PortAudioPlaybackDevice(new PortAudioEnvironment(api, OSPlatform.Windows));
        device.Start();

        // Act: write an empty block and then a list that is not an ICollection<float>
        device.Write([]);
        var emptyPending = device.PendingSampleCount;
        device.Write(new ReadOnlyOnlyList([0.25f, 0.5f]));

        // Assert: the empty write queued nothing and the other block drains intact
        Assert.Equal(0, emptyPending);
        Assert.Equal(2, device.PendingSampleCount);
        Assert.Equal([0.25f, 0.5f], stream.RequestSamples(2));
        Assert.Equal(0, device.PendingSampleCount);
    }

    /// <summary>
    ///     Proves that starting a started device is a no-op and that the stream is opened with the
    ///     resolved channel count and sample rate.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Start_AlreadyStarted_OpensResolvedFormatOnce()
    {
        // Arrange: a stereo 44.1 kHz speaker
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 44100, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var device = new PortAudioPlaybackDevice(new PortAudioEnvironment(api, OSPlatform.Windows));

        // Act: start twice
        device.Start();
        device.Start();

        // Assert: one native start and the resolved format was requested
        Assert.Equal(1, stream.StartCallCount);
        Assert.Equal(2, api.OpenedChannelCount);
        Assert.Equal(44100, api.OpenedSampleRate);
    }

    /// <summary>
    ///     Proves that a native stop failure is wrapped, the stream stays registered as active
    ///     (so a refresh is refused and a restart is rejected), and a retried stop that succeeds
    ///     disposes the stream and releases the registration.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Stop_NativeStopFails_KeepsStreamActiveUntilRetrySucceeds()
    {
        // Arrange: a started device whose stream fails to stop and has queued audio
        var stopException = new InvalidOperationException("Native stop failed.");
        var stream = new FakePortAudioStream { StopException = stopException };
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment);
        device.Start();
        device.Write([0.1f, 0.2f]);

        // Act: stop the device, then try to restart and refresh while the stop is unresolved
        var exception = Assert.Throws<AudioDeviceUnavailableException>(device.Stop);
        var startException = Record.Exception(device.Start);
        var refreshException = Record.Exception(environment.Refresh);

        // Assert: the original failure is wrapped, the stream is neither disposed nor
        // unregistered, the pending count reset, the restart rejected and the refresh refused
        Assert.Same(stopException, exception.InnerException);
        Assert.Equal(0, stream.DisposeCallCount);
        Assert.Equal(0, device.PendingSampleCount);
        Assert.IsType<AudioDeviceUnavailableException>(startException);
        Assert.Equal(1, stream.StartCallCount);
        Assert.IsType<AudioDeviceInUseException>(refreshException);

        // Act: retry the stop once the native stream can stop
        stream.StopException = null;
        device.Stop();

        // Assert: the retry stopped the same stream, disposed it and released the registration
        Assert.Equal(2, stream.StopCallCount);
        Assert.Equal(1, stream.DisposeCallCount);
        Assert.Null(Record.Exception(environment.Refresh));
    }

    /// <summary>
    ///     Proves that a dispose failure after a successful stop is wrapped and reported.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Stop_DisposeFailsAfterStop_ThrowsWrappedDisposeFailure()
    {
        // Arrange: a started device whose stream stops cleanly but fails to dispose
        var disposeException = new InvalidOperationException("Native dispose failed.");
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = new FakePortAudioStream { DisposeException = disposeException }
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment);
        device.Start();

        // Act: stop the device
        var exception = Assert.Throws<AudioDeviceUnavailableException>(device.Stop);

        // Assert: the dispose failure is wrapped and the registration was still released
        Assert.Same(disposeException, exception.InnerException);
        Assert.Null(Record.Exception(environment.Refresh));
    }

    /// <summary>
    ///     Proves that a callback request for zero samples returns an empty block without
    ///     consuming queued audio.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_ProvideSamples_NonPositiveRequest_ReturnsEmptyAndKeepsQueue()
    {
        // Arrange: a started device with one queued sample
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var device = new PortAudioPlaybackDevice(new PortAudioEnvironment(api, OSPlatform.Windows));
        device.Start();
        device.Write([0.5f]);

        // Act: request zero samples
        var provided = stream.RequestSamples(0);

        // Assert: nothing was returned or consumed
        Assert.Empty(provided);
        Assert.Equal(1, device.PendingSampleCount);
    }

    /// <summary>
    ///     Proves that a callback firing after a failed native stop still sees consistent head
    ///     state (the unconsumed remainder is delivered exactly once, with an exact pending
    ///     count), and that once a retried stop succeeds a restarted stream sees silence.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Stop_NativeStopFailsThenCallbackFires_HeadStateStaysConsistent()
    {
        // Arrange: a started device whose first native stop will fail, with a block of which only
        // half has been consumed by the callback
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment);
        device.Write([0.1f, 0.2f, 0.3f, 0.4f]);
        device.Start();
        stream.RequestSamples(2);
        stream.StopException = new InvalidOperationException("Native stop failed.");
        Assert.Throws<AudioDeviceUnavailableException>(device.Stop);

        // Act: the still-running callback fires after the failed stop, then the stop is retried
        // successfully and the device restarted
        var callbackSamples = stream.RequestSamples(2);
        var pendingAfterCallback = device.PendingSampleCount;
        stream.StopException = null;
        device.Stop();
        device.Start();
        var drainedSamples = stream.RequestSamples(4);

        // Assert: the callback received the unconsumed remainder exactly once with an exact
        // pending count, and the restarted stream sees silence rather than a stale replay
        Assert.Equal([0.3f, 0.4f], callbackSamples);
        Assert.Equal(0, pendingAfterCallback);
        Assert.Equal([0.0f, 0.0f, 0.0f, 0.0f], drainedSamples);
        Assert.Equal(0, device.PendingSampleCount);
        device.Write([0.7f, 0.8f]);
        Assert.Equal(2, device.PendingSampleCount);
        Assert.Equal([0.7f, 0.8f], stream.RequestSamples(2));
        Assert.Equal(0, device.PendingSampleCount);
    }

    /// <summary>
    ///     Proves that a native <c>Start()</c> failure disposes the already-opened stream and
    ///     releases the active-stream registration, with the original failure wrapped.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Start_NativeStreamStartFails_DisposesStreamAndReleasesRegistration()
    {
        // Arrange: a stream that opens but fails to start, and disposes cleanly
        var startException = new InvalidOperationException("Native start failed.");
        var stream = new FakePortAudioStream { StartException = startException };
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment);

        // Act: start the device
        var exception = Assert.Throws<AudioDeviceUnavailableException>(device.Start);

        // Assert: the opened stream was disposed once and the registration released
        Assert.Same(startException, exception.InnerException);
        Assert.Equal(1, stream.DisposeCallCount);
        Assert.Null(Record.Exception(environment.Refresh));
    }

    /// <summary>
    ///     Proves that a null environment is rejected at construction.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Constructor_NullEnvironment_ThrowsArgumentNullException()
    {
        // Act & Assert: a null environment is the only construction-time exception
        Assert.Throws<ArgumentNullException>(() => new PortAudioPlaybackDevice(null!));
    }

    /// <summary>
    ///     Proves that an exact-name selection resolves that device (not the default) and that the
    ///     diagnostic names the selection as the resolution basis.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Constructor_NamedSelectionPresent_ResolvesNamedDevice()
    {
        // Arrange: two output devices, the default being the first
        var api = new FakePortAudioApi(
        [
            new PortAudioDeviceInfo("Default Speaker", 5, 0, 2, 48000, 0.0, 0.01),
            new PortAudioDeviceInfo("Backup Speaker", 5, 0, 1, 16000, 0.0, 0.01)
        ])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
        };
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: construct with a selection naming the non-default device
        var device = new PortAudioPlaybackDevice(environment, new AudioDeviceSelection("Backup Speaker"), diagnostics);

        // Assert: the named device and its format were resolved
        Assert.True(device.IsAvailable);
        Assert.Equal(1, device.ChannelCount);
        Assert.Equal(16000, device.SampleRate);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Info,
            "AudioSubsystem",
            "Resolved PortAudio playback device 'Backup Speaker' via selection.");
    }

    /// <summary>
    ///     Proves that an unresolvable preferred host API leaves the device unavailable.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Constructor_HostApiUnresolvable_ReportsUnavailable()
    {
        // Arrange: a runtime that cannot find the preferred host API
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = null
        };

        // Act: construct the device
        var device = new PortAudioPlaybackDevice(new PortAudioEnvironment(api, OSPlatform.Windows));

        // Assert: honestly unavailable
        Assert.False(device.IsAvailable);
    }

    /// <summary>
    ///     Proves that a write racing with a stop-time queue clear keeps the pending count equal
    ///     to the samples still queued (sequential model of the interleaving: write after stop).
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Write_AfterStop_PendingCountMatchesQueuedSamples()
    {
        // Arrange: a device that was started, written to, stopped, then written to again
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var device = new PortAudioPlaybackDevice(new PortAudioEnvironment(api, OSPlatform.Windows));
        device.Start();
        device.Write([0.1f, 0.2f, 0.3f]);
        device.Stop();

        // Act: write again after the stop, then restart and drain
        device.Write([0.4f, 0.5f]);
        device.Start();
        var drained = stream.RequestSamples(3);

        // Assert: only the post-stop samples remain, counted exactly
        Assert.Equal([0.4f, 0.5f, 0.0f], drained);
        Assert.Equal(0, device.PendingSampleCount);
    }

    /// <summary>
    ///     Proves that <see cref="PortAudioPlaybackDevice.Write"/> copies the caller's buffer, so
    ///     mutating it after the call does not alter the queued audio.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Write_CallerMutatesBufferAfterWrite_QueuedAudioUnchanged()
    {
        // Arrange: a started device and a caller-owned buffer
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var device = new PortAudioPlaybackDevice(new PortAudioEnvironment(api, OSPlatform.Windows));
        device.Start();
        var buffer = new[] { 0.1f, 0.2f };

        // Act: write, then overwrite the caller's buffer
        device.Write(buffer);
        buffer[0] = 0.9f;
        buffer[1] = 0.9f;

        // Assert: the original samples are delivered
        Assert.Equal([0.1f, 0.2f], stream.RequestSamples(2));
    }

    /// <summary>
    ///     Proves that a native stop failure is reported as an error diagnostic and the stream is not
    ///     disposed while quiescence is unproven.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Stop_NativeStopFails_ReportsErrorAndDoesNotDisposeStream()
    {
        // Arrange: a started device whose stream fails to stop (and would also fail to dispose)
        var stopException = new InvalidOperationException("Native stop failed.");
        var stream = new FakePortAudioStream
        {
            StopException = stopException,
            DisposeException = new InvalidOperationException("Native dispose failed.")
        };
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var diagnostics = Substitute.For<ISpeechDiagnostics>();
        var device = new PortAudioPlaybackDevice(new PortAudioEnvironment(api, OSPlatform.Windows), diagnostics: diagnostics);
        device.Start();

        // Act: stop the device
        var exception = Assert.Throws<AudioDeviceUnavailableException>(device.Stop);

        // Assert: the stop failure is thrown, reported, and the stream was left undisposed
        Assert.Same(stopException, exception.InnerException);
        Assert.Equal(0, stream.DisposeCallCount);
        diagnostics.Received().Report(
            SpeechDiagnosticLevel.Error,
            "AudioSubsystem",
            Arg.Is<string>(message => message.Contains("Failed to stop PortAudio playback", StringComparison.Ordinal)));
    }

    /// <summary>
    ///     Proves that a throwing diagnostics sink does not fail a successful start and the
    ///     stream stays started and registered.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Start_ThrowingDiagnosticsSink_StartSucceedsAndStaysRegistered()
    {
        // Arrange: a device whose diagnostics sink throws on every report
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment, diagnostics: new ThrowingDiagnostics());

        // Act: start the device
        var exception = Record.Exception(device.Start);

        // Assert: start succeeded, the stream was started, not disposed, and remains registered
        Assert.Null(exception);
        Assert.Equal(1, stream.StartCallCount);
        Assert.Equal(0, stream.DisposeCallCount);
        Assert.Throws<AudioDeviceInUseException>(environment.Refresh);
    }

    /// <summary>
    ///     Proves that a throwing diagnostics sink does not skip stream disposal or leave the
    ///     active-stream registration behind on a successful stop.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Stop_ThrowingDiagnosticsSink_DisposesStreamAndUnregisters()
    {
        // Arrange: a started device whose diagnostics sink throws on every report
        var stream = new FakePortAudioStream();
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment, diagnostics: new ThrowingDiagnostics());
        device.Start();

        // Act: stop the device
        var exception = Record.Exception(device.Stop);

        // Assert: stop succeeded, the stream was disposed, and a refresh is no longer refused
        Assert.Null(exception);
        Assert.Equal(1, stream.StopCallCount);
        Assert.Equal(1, stream.DisposeCallCount);
        Assert.Null(Record.Exception(environment.Refresh));
    }

    /// <summary>
    ///     Proves that a throwing diagnostics sink does not mask the start failure or leak the
    ///     registration when the native start fails.
    /// </summary>
    [Fact]
    public void PortAudioPlaybackDevice_Start_ThrowingDiagnosticsSinkAndStartFails_ThrowsOriginalFailureAndUnregisters()
    {
        // Arrange: a throwing sink and a stream whose native start fails
        var stream = new FakePortAudioStream { StartException = new InvalidOperationException("Native start failed.") };
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 2, 48000, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0),
            PlaybackStream = stream
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var device = new PortAudioPlaybackDevice(environment, diagnostics: new ThrowingDiagnostics());

        // Act: start the device
        var exception = Assert.Throws<AudioDeviceUnavailableException>(device.Start);

        // Assert: the documented failure surfaces, the stream was disposed, and nothing stays registered
        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Equal(1, stream.DisposeCallCount);
        Assert.Null(Record.Exception(environment.Refresh));
    }

    /// <summary>
    ///     A diagnostics sink that throws for every report.
    /// </summary>
    private sealed class ThrowingDiagnostics : ISpeechDiagnostics
    {
        /// <inheritdoc/>
        public void Report(SpeechDiagnosticLevel level, string category, string message) =>
            throw new InvalidOperationException("Sink failure.");
    }

    /// <summary>
    ///     A read-only list that deliberately does not implement <see cref="ICollection{T}"/>.
    /// </summary>
    /// <param name="items">The items exposed by the list.</param>
    private sealed class ReadOnlyOnlyList(float[] items) : IReadOnlyList<float>
    {
        /// <inheritdoc/>
        public int Count => items.Length;

        /// <inheritdoc/>
        public float this[int index] => items[index];

        /// <inheritdoc/>
        public IEnumerator<float> GetEnumerator()
        {
            return ((IEnumerable<float>)items).GetEnumerator();
        }

        /// <inheritdoc/>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }

    /// <summary>
    ///     Minimal fake PortAudio seam used by the playback-device tests.
    /// </summary>
    private sealed class FakePortAudioApi(PortAudioDeviceInfo[] devices) : IPortAudioApi
    {
        /// <summary>
        ///     Gets or sets the host-API index returned by <see cref="FindHostApiIndex"/>.
        /// </summary>
        internal int? FindHostApiIndexResult { get; init; }

        /// <summary>
        ///     Gets or sets the host-API info returned by <see cref="GetHostApiInfo"/>.
        /// </summary>
        internal PortAudioHostApiInfo HostApiInfo { get; init; } =
            new("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, -1);

        /// <summary>
        ///     Gets or sets the playback stream instance returned by <see cref="OpenPlaybackStream"/>.
        /// </summary>
        internal FakePortAudioStream PlaybackStream { get; init; } = new();

        /// <summary>
        ///     Gets or sets the exception thrown by <see cref="OpenPlaybackStream"/>.
        /// </summary>
        internal Exception? OpenPlaybackException { get; init; }

        /// <summary>
        ///     Gets or sets the result returned by <see cref="IsPlaybackFormatSupported"/>.
        ///     Defaults to <see langword="true"/> so every existing test that does not care about
        ///     format negotiation is unaffected.
        /// </summary>
        internal bool IsPlaybackFormatSupportedResult { get; init; } = true;

        /// <summary>
        ///     Gets or sets the exception thrown when <see cref="DeviceCount"/> is read.
        /// </summary>
        internal Exception? DeviceCountException { get; init; }

        /// <summary>
        ///     Gets the channel count most recently passed to <see cref="OpenPlaybackStream"/>.
        /// </summary>
        internal int OpenedChannelCount { get; private set; }

        /// <summary>
        ///     Gets the sample rate most recently passed to <see cref="OpenPlaybackStream"/>.
        /// </summary>
        internal int OpenedSampleRate { get; private set; }

        /// <inheritdoc/>
        public int HostApiCount => 1;

        /// <inheritdoc/>
        public int DeviceCount => DeviceCountException is null ? devices.Length : throw DeviceCountException;

        /// <inheritdoc/>
        public void Initialize()
        {
        }

        /// <inheritdoc/>
        public void Terminate()
        {
        }

        /// <inheritdoc/>
        public int? FindHostApiIndex(PortAudioHostApiType hostApiType)
        {
            return FindHostApiIndexResult;
        }

        /// <inheritdoc/>
        public PortAudioHostApiInfo GetHostApiInfo(int hostApiIndex)
        {
            return HostApiInfo;
        }

        /// <inheritdoc/>
        public PortAudioDeviceInfo GetDeviceInfo(int deviceIndex)
        {
            return devices[deviceIndex];
        }

        /// <inheritdoc/>
        public bool IsCaptureFormatSupported(int deviceIndex, int channelCount, int sampleRate)
        {
            throw new NotSupportedException("Capture is outside this test scope.");
        }

        /// <inheritdoc/>
        public bool IsPlaybackFormatSupported(int deviceIndex, int channelCount, int sampleRate)
        {
            return IsPlaybackFormatSupportedResult;
        }

        /// <inheritdoc/>
        public IPortAudioStream OpenCaptureStream(
            int deviceIndex,
            int channelCount,
            int sampleRate,
            uint framesPerBuffer,
            Action<IReadOnlyList<float>> onSamplesCaptured)
        {
            throw new NotSupportedException("Capture is outside this test scope.");
        }

        /// <inheritdoc/>
        public IPortAudioStream OpenPlaybackStream(
            int deviceIndex,
            int channelCount,
            int sampleRate,
            uint framesPerBuffer,
            Func<int, float[]> provideSamples)
        {
            if (OpenPlaybackException is not null)
            {
                throw OpenPlaybackException;
            }

            OpenedChannelCount = channelCount;
            OpenedSampleRate = sampleRate;

            PlaybackStream.ProvideSamples = provideSamples;
            return PlaybackStream;
        }
    }

    /// <summary>
    ///     Minimal fake PortAudio stream used by the playback-device tests.
    /// </summary>
    private sealed class FakePortAudioStream : IPortAudioStream
    {
        /// <summary>
        ///     Gets or sets the callback used to provide one requested output block.
        /// </summary>
        internal Func<int, float[]>? ProvideSamples { get; set; }

        /// <summary>
        ///     Gets the number of times <see cref="Start"/> has been called.
        /// </summary>
        internal int StartCallCount { get; private set; }

        /// <summary>
        ///     Gets the number of times <see cref="Stop"/> has been called.
        /// </summary>
        internal int StopCallCount { get; private set; }

        /// <summary>
        ///     Gets the number of times <see cref="Dispose"/> has been called.
        /// </summary>
        internal int DisposeCallCount { get; private set; }

        /// <summary>
        ///     Gets or sets the exception thrown by <see cref="Start"/>, or <see langword="null"/>
        ///     for the default no-op behavior.
        /// </summary>
        internal Exception? StartException { get; set; }

        /// <summary>
        ///     Gets or sets the exception thrown by <see cref="Stop"/> after
        ///     <see cref="StopCallCount"/> is incremented, or <see langword="null"/> for the
        ///     default no-op behavior.
        /// </summary>
        internal Exception? StopException { get; set; }

        /// <summary>
        ///     Gets or sets the exception thrown by <see cref="Dispose"/> after
        ///     <see cref="DisposeCallCount"/> is incremented, or <see langword="null"/> for the
        ///     default no-op behavior.
        /// </summary>
        internal Exception? DisposeException { get; set; }

        /// <inheritdoc/>
        public void Start()
        {
            StartCallCount++;

            if (StartException is not null)
            {
                throw StartException;
            }
        }

        /// <inheritdoc/>
        public void Stop()
        {
            StopCallCount++;

            if (StopException is not null)
            {
                throw StopException;
            }
        }

        /// <inheritdoc/>
        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Major Code Smell",
            "S3877:Exceptions should not be thrown from unexpected methods",
            Justification = "Deliberately simulates a faulting native Dispose() to test start-failure cleanup.")]
        public void Dispose()
        {
            DisposeCallCount++;

            if (DisposeException is not null)
            {
                throw DisposeException;
            }
        }

        /// <summary>
        ///     Simulates one PortAudio playback callback requesting the given sample count.
        /// </summary>
        /// <param name="sampleCount">The number of samples to request.</param>
        /// <returns>The samples returned by the managed playback callback.</returns>
        internal float[] RequestSamples(int sampleCount)
        {
            // Copied because the device reuses its callback buffer between calls, so a retained
            // reference would be overwritten by the next request.
            return ProvideSamples?.Invoke(sampleCount).ToArray() ?? [];
        }
    }
}
