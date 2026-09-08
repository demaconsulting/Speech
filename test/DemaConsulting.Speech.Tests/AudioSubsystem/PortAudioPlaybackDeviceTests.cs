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

        // Act
        var device = new PortAudioPlaybackDevice(environment, preferredFormat: new AudioFormat(24000, 2));

        // Assert
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

        // Act
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

        // Act
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

        // Act
        var device = new PortAudioPlaybackDevice(
            environment,
            diagnostics: diagnostics,
            preferredFormat: new AudioFormat(24000, 4));

        // Assert
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
        // Arrange
        var api = new FakePortAudioApi([new PortAudioDeviceInfo("Speaker", 5, 0, 3, 44100, 0.0, 0.01)])
        {
            FindHostApiIndexResult = 5,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, -1, 0)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act
        var device = new PortAudioPlaybackDevice(environment);

        // Assert
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
    ///     Minimal fake PortAudio seam used by the playback-device tests.
    /// </summary>
    private sealed class FakePortAudioApi(IReadOnlyList<PortAudioDeviceInfo> devices) : IPortAudioApi
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

        /// <inheritdoc/>
        public int HostApiCount => 1;

        /// <inheritdoc/>
        public int DeviceCount => devices.Count;

        /// <inheritdoc/>
        public void Initialize()
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

        /// <inheritdoc/>
        public void Start()
        {
            StartCallCount++;
        }

        /// <inheritdoc/>
        public void Stop()
        {
            StopCallCount++;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            DisposeCallCount++;
        }

        /// <summary>
        ///     Simulates one PortAudio playback callback requesting the given sample count.
        /// </summary>
        /// <param name="sampleCount">The number of samples to request.</param>
        /// <returns>The samples returned by the managed playback callback.</returns>
        internal IReadOnlyList<float> RequestSamples(int sampleCount)
        {
            return ProvideSamples?.Invoke(sampleCount) ?? [];
        }
    }
}
