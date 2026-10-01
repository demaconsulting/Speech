// cspell:ignore Alsa ALSA portaudio
using System.Runtime.InteropServices;
using DemaConsulting.Speech.AudioSubsystem;
using DemaConsulting.Speech.AudioSubsystem.PortAudio;

namespace DemaConsulting.Speech.Tests.AudioSubsystem.PortAudio;

/// <summary>
///     Unit tests for the <see cref="PortAudioEnvironment"/> helper that owns preferred-host-API
///     resolution and one-time PortAudio initialization caching.
/// </summary>
public class PortAudioEnvironmentTests
{
    /// <summary>
    ///     Proves that the real PortAudio API adapter is exposed as a shared singleton instance.
    /// </summary>
    [Fact]
    public void PortAudioApi_Instance_ReadTwice_ReturnsSameInstance()
    {
        // Arrange & Act: read the shared PortAudio API adapter twice
        var first = PortAudioApi.Instance;
        var second = PortAudioApi.Instance;

        // Assert: the adapter is a singleton so the default environment reuses one managed binding instance
        Assert.Same(first, second);
    }

    /// <summary>
    ///     Proves that the default production PortAudio environment is exposed as a shared singleton instance.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_Shared_ReadTwice_ReturnsSameInstance()
    {
        // Arrange & Act: read the shared production environment twice
        var first = PortAudioEnvironment.Shared;
        var second = PortAudioEnvironment.Shared;

        // Assert: the default environment is shared so PortAudio initialization state is cached process-wide
        Assert.Same(first, second);
    }

    /// <summary>
    ///     Proves that Windows maps to the WASAPI host API.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_ResolvePreferredHostApiType_Windows_ReturnsWasapi()
    {
        // Arrange: the Windows platform token
        var platform = OSPlatform.Windows;

        // Act: resolve the preferred PortAudio host API for Windows
        var hostApiType = PortAudioEnvironment.ResolvePreferredHostApiType(platform);

        // Assert: Windows prefers WASAPI
        Assert.Equal(PortAudioHostApiType.Wasapi, hostApiType);
    }

    /// <summary>
    ///     Proves that Linux maps to the ALSA host API.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_ResolvePreferredHostApiType_Linux_ReturnsAlsa()
    {
        // Arrange: the Linux platform token
        var platform = OSPlatform.Linux;

        // Act: resolve the preferred PortAudio host API for Linux
        var hostApiType = PortAudioEnvironment.ResolvePreferredHostApiType(platform);

        // Assert: Linux prefers ALSA
        Assert.Equal(PortAudioHostApiType.Alsa, hostApiType);
    }

    /// <summary>
    ///     Proves that macOS maps to the CoreAudio host API.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_ResolvePreferredHostApiType_MacOs_ReturnsCoreAudio()
    {
        // Arrange: the macOS platform token
        var platform = OSPlatform.OSX;

        // Act: resolve the preferred PortAudio host API for macOS
        var hostApiType = PortAudioEnvironment.ResolvePreferredHostApiType(platform);

        // Assert: macOS prefers CoreAudio
        Assert.Equal(PortAudioHostApiType.CoreAudio, hostApiType);
    }

    /// <summary>
    ///     Proves that unsupported platforms resolve to no preferred host API.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_ResolvePreferredHostApiType_UnsupportedPlatform_ReturnsNull()
    {
        // Arrange: a non-desktop platform token outside the library's supported set
        var platform = OSPlatform.Create("FREEBSD");

        // Act: resolve the preferred PortAudio host API for the unsupported platform
        var hostApiType = PortAudioEnvironment.ResolvePreferredHostApiType(platform);

        // Assert: unsupported platforms have no preferred host API mapping
        Assert.Null(hostApiType);
    }

    /// <summary>
    ///     Proves that a successfully initialized environment resolves the runtime index and
    ///     metadata for the current platform's preferred host API.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_TryResolvePreferredHostApi_InitializedAndMapped_ReturnsHostApiInfo()
    {
        // Arrange: a fake PortAudio seam that exposes WASAPI at runtime index 3
        var api = new FakePortAudioApi
        {
            FindHostApiIndexResult = 3,
            HostApiInfo = new PortAudioHostApiInfo("Windows WASAPI", PortAudioHostApiType.Wasapi, 7, 9)
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: resolve the current platform's preferred host API
        var resolved = environment.TryResolvePreferredHostApi(out var hostApiIndex, out var hostApiInfo);

        // Assert: resolution succeeds with the runtime index and metadata returned by the seam
        Assert.True(resolved);
        Assert.Equal(3, hostApiIndex);
        Assert.Equal("Windows WASAPI", hostApiInfo.Name);
        Assert.Equal(7, hostApiInfo.DefaultInputDeviceIndex);
        Assert.Equal(9, hostApiInfo.DefaultOutputDeviceIndex);
    }

    /// <summary>
    ///     Proves that initialization failures are converted into a false return value and that
    ///     the failed initialization attempt is cached rather than retried.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_TryResolvePreferredHostApi_InitializationFails_ReturnsFalseAndCachesFailure()
    {
        // Arrange: a fake PortAudio seam whose initialization always fails
        var api = new FakePortAudioApi { InitializeException = new InvalidOperationException("PortAudio init failed.") };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: resolve twice to prove the same failed initialization result is reused
        var firstResult = environment.TryResolvePreferredHostApi(out _, out _);
        var secondResult = environment.TryResolvePreferredHostApi(out _, out _);

        // Assert: callers observe an honest failure with the cached failure message, and the
        // native initialization call is attempted only once
        Assert.False(firstResult);
        Assert.False(secondResult);
        Assert.Equal("PortAudio init failed.", environment.InitializationFailureMessage);
        Assert.Equal(1, api.InitializeCallCount);
    }

    /// <summary>
    ///     Proves that an initialized runtime that lacks the preferred host API degrades to a
    ///     false return value rather than throwing.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_TryResolvePreferredHostApi_PreferredHostApiMissing_ReturnsFalse()
    {
        // Arrange: a fake PortAudio seam that initializes successfully but exposes no WASAPI host API
        var api = new FakePortAudioApi { FindHostApiIndexResult = null };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);

        // Act: attempt to resolve the missing preferred host API
        var resolved = environment.TryResolvePreferredHostApi(out _, out _);

        // Assert: the absence is reported as a simple false return value
        Assert.False(resolved);
    }

    /// <summary>
    ///     Proves that a refresh with no active streams terminates and reinitializes the runtime.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_Refresh_NoActiveStreams_ReinitializesAndReflectsNewOutcome()
    {
        // Arrange: an environment whose first initialization succeeds
        var api = new FakePortAudioApi();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        Assert.True(environment.IsInitialized);

        // Act: refresh while nothing about the underlying fake has changed
        environment.Refresh();

        // Assert: the prior successful runtime was terminated and a fresh initialization occurred
        // (accessing IsInitialized first forces the lazily-deferred re-initialization attempt)
        Assert.True(environment.IsInitialized);
        Assert.Equal(1, api.TerminateCallCount);
        Assert.Equal(2, api.InitializeCallCount);
    }

    /// <summary>
    ///     Proves that a refresh genuinely reflects a changed initialization outcome: an
    ///     environment whose first initialization failed reports success once the underlying
    ///     fault is cleared and a refresh is requested.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_Refresh_OutcomeChanges_ReflectsNewInitializationResult()
    {
        // Arrange: an environment whose first initialization fails
        var api = new FakePortAudioApi { InitializeException = new InvalidOperationException("PortAudio init failed.") };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        Assert.False(environment.IsInitialized);

        // Act: clear the fault, then refresh so a fresh initialization attempt is made
        api.InitializeException = null;
        environment.Refresh();

        // Assert: the cached outcome flips from failure to success, and Terminate was never
        // called since the runtime had never previously initialized successfully
        Assert.True(environment.IsInitialized);
        Assert.Equal(0, api.TerminateCallCount);
        Assert.Equal(2, api.InitializeCallCount);
    }

    /// <summary>
    ///     Proves that a native <see cref="IPortAudioApi.Terminate"/> fault during refresh
    ///     propagates to the caller unchanged and leaves the previously cached initialization
    ///     state untouched.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_Refresh_TerminateThrows_PropagatesAndLeavesCachedStateUntouched()
    {
        // Arrange: an initialized environment whose Terminate call always fails
        var api = new FakePortAudioApi { TerminateException = new InvalidOperationException("Terminate failed.") };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        Assert.True(environment.IsInitialized);

        // Act & Assert: the native fault propagates unchanged
        var exception = Assert.Throws<InvalidOperationException>(environment.Refresh);
        Assert.Equal("Terminate failed.", exception.Message);

        // Assert: the previously cached successful initialization state is left untouched, and
        // no re-initialization attempt was ever made
        Assert.True(environment.IsInitialized);
        Assert.Equal(1, api.TerminateCallCount);
        Assert.Equal(1, api.InitializeCallCount);
    }

    /// <summary>
    ///     Proves that refreshing while an active stream is registered throws
    ///     <see cref="AudioDeviceInUseException"/> and does not terminate or reinitialize the
    ///     runtime.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_Refresh_ActiveStreamRegistered_ThrowsAudioDeviceInUseExceptionAndDoesNotTerminate()
    {
        // Arrange: an initialized environment with one registered active stream
        var api = new FakePortAudioApi();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        Assert.True(environment.IsInitialized);
        var owner = new object();
        environment.RegisterActiveStream(owner, "Mic");

        // Act & Assert: the refresh is refused, naming the in-use device
        var exception = Assert.Throws<AudioDeviceInUseException>(environment.Refresh);
        Assert.Contains("Mic", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, api.TerminateCallCount);
        Assert.Equal(1, api.InitializeCallCount);
    }

    /// <summary>
    ///     Proves that refreshing an environment whose initialization never previously succeeded
    ///     skips calling <see cref="IPortAudioApi.Terminate"/> and still re-attempts initialization.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_Refresh_NotPreviouslyInitialized_SkipsTerminateAndReinitializes()
    {
        // Arrange: an environment whose first initialization fails
        var api = new FakePortAudioApi { InitializeException = new InvalidOperationException("PortAudio init failed.") };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        Assert.False(environment.IsInitialized);

        // Act: refresh without ever having successfully initialized
        environment.Refresh();

        // Assert: Terminate is never called for a runtime that never successfully initialized,
        // but a fresh initialization attempt is still made (accessing IsInitialized forces the
        // lazily-deferred re-initialization attempt)
        Assert.False(environment.IsInitialized);
        Assert.Equal(0, api.TerminateCallCount);
        Assert.Equal(2, api.InitializeCallCount);
    }

    /// <summary>
    ///     Proves that registering then unregistering an active stream allows a subsequent
    ///     refresh to succeed.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_RegisterThenUnregisterActiveStream_Refresh_Succeeds()
    {
        // Arrange: an initialized environment with a stream registered and then unregistered
        var api = new FakePortAudioApi();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        Assert.True(environment.IsInitialized);
        var owner = new object();
        environment.RegisterActiveStream(owner, "Mic");
        environment.UnregisterActiveStream(owner);

        // Act: refresh after the stream was unregistered
        var exception = Record.Exception(environment.Refresh);

        // Assert: the refresh proceeds normally, since no active stream remains registered
        Assert.Null(exception);
        Assert.Equal(1, api.TerminateCallCount);
    }

    /// <summary>
    ///     Proves that each refresh that actually proceeds (rather than being refused) advances
    ///     <see cref="PortAudioEnvironment.Generation"/> by exactly one, so devices resolved
    ///     before it can detect staleness.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_Refresh_NoActiveStreams_IncrementsGeneration()
    {
        // Arrange: a freshly constructed environment starting at generation zero
        var api = new FakePortAudioApi();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        Assert.Equal(0, environment.Generation);

        // Act: refresh twice with no active streams
        environment.Refresh();
        var afterFirst = environment.Generation;
        environment.Refresh();
        var afterSecond = environment.Generation;

        // Assert: each completed refresh advances the generation by exactly one
        Assert.Equal(1, afterFirst);
        Assert.Equal(2, afterSecond);
    }

    /// <summary>
    ///     Proves that a refresh refused because an active stream is registered does not advance
    ///     <see cref="PortAudioEnvironment.Generation"/>, since no device-table change actually
    ///     occurred.
    /// </summary>
    [Fact]
    public void PortAudioEnvironment_Refresh_ActiveStreamRegistered_DoesNotIncrementGeneration()
    {
        // Arrange: an initialized environment with one registered active stream
        var api = new FakePortAudioApi();
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var owner = new object();
        environment.RegisterActiveStream(owner, "Mic");
        var generationBefore = environment.Generation;

        // Act: attempt a refresh that is refused
        Assert.Throws<AudioDeviceInUseException>(environment.Refresh);

        // Assert: the generation counter is unchanged because the refresh never proceeded
        Assert.Equal(generationBefore, environment.Generation);
    }

    /// <summary>
    ///     Proves that a concurrent first evaluation of the cached initialization state and a
    ///     concurrent <see cref="PortAudioEnvironment.Refresh"/> call are fully serialized by the
    ///     shared lock, rather than racing into an unbalanced pair of native
    ///     <see cref="IPortAudioApi.Initialize"/>/<see cref="IPortAudioApi.Terminate"/> calls.
    /// </summary>
    [Fact]
    public async Task PortAudioEnvironment_IsInitialized_ConcurrentWithRefresh_SerializesAndPreservesInitializeTerminateBalance()
    {
        // Arrange: an environment whose native Initialize() call blocks until released, so a
        // concurrent Refresh() call can be reliably interleaved with the first evaluation
        using var startedSignal = new ManualResetEventSlim(false);
        using var releaseGate = new ManualResetEventSlim(false);
        var api = new FakePortAudioApi
        {
            InitializeStartedSignal = startedSignal,
            InitializeReleaseGate = releaseGate
        };
        var environment = new PortAudioEnvironment(api, OSPlatform.Windows);
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act: start the first IsInitialized evaluation on a background thread, wait until it has
        // genuinely entered Initialize(), then start a concurrent Refresh() and release the gate
        var firstEvaluation = Task.Run(() => environment.IsInitialized, cancellationToken);
        Assert.True(
            startedSignal.Wait(TimeSpan.FromSeconds(5), cancellationToken),
            "The first IsInitialized evaluation never entered Initialize() within the timeout.");
        var refreshTask = Task.Run(environment.Refresh, cancellationToken);
        releaseGate.Set();
        await Task.WhenAll(firstEvaluation, refreshTask).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);

        // Force one more evaluation so the post-refresh Lazy is evaluated too
        _ = environment.IsInitialized;

        // Assert: exactly one Initialize()/Terminate() pair ran for the first evaluation, plus one
        // more Initialize() for the post-refresh re-initialization - never an unbalanced count
        Assert.Equal(2, api.InitializeCallCount);
        Assert.Equal(1, api.TerminateCallCount);
    }

    /// <summary>
    ///     Minimal fake implementation of <see cref="IPortAudioApi"/> used by these unit tests.
    /// </summary>
    private sealed class FakePortAudioApi : IPortAudioApi
    {
        /// <summary>
        ///     Gets or sets the exception to throw when <see cref="Initialize"/> is called.
        /// </summary>
        internal Exception? InitializeException { get; set; }

        /// <summary>
        ///     Gets or sets a signal that <see cref="Initialize"/> sets once it has been entered,
        ///     before waiting on <see cref="InitializeReleaseGate"/>, so a test can prove a
        ///     concurrent evaluation has genuinely started before proceeding.
        /// </summary>
        internal ManualResetEventSlim? InitializeStartedSignal { get; set; }

        /// <summary>
        ///     Gets or sets a gate that <see cref="Initialize"/> waits on (after signaling
        ///     <see cref="InitializeStartedSignal"/>) before continuing, so a test can hold one
        ///     evaluation open while starting a concurrent operation.
        /// </summary>
        internal ManualResetEventSlim? InitializeReleaseGate { get; set; }

        /// <summary>
        ///     Gets or sets the exception to throw when <see cref="Terminate"/> is called.
        /// </summary>
        internal Exception? TerminateException { get; init; }

        /// <summary>
        ///     Gets or sets the host-API index returned by <see cref="FindHostApiIndex"/>.
        /// </summary>
        internal int? FindHostApiIndexResult { get; init; }

        /// <summary>
        ///     Gets or sets the host-API info returned by <see cref="GetHostApiInfo"/>.
        /// </summary>
        internal PortAudioHostApiInfo HostApiInfo { get; init; } =
            new("Test Host API", PortAudioHostApiType.Wasapi, -1, -1);

        /// <summary>
        ///     Gets the number of times <see cref="Initialize"/> has been called.
        /// </summary>
        internal int InitializeCallCount { get; private set; }

        /// <summary>
        ///     Gets the number of times <see cref="Terminate"/> has been called.
        /// </summary>
        internal int TerminateCallCount { get; private set; }

        /// <inheritdoc/>
        public int HostApiCount => 0;

        /// <inheritdoc/>
        public int DeviceCount => 0;

        /// <inheritdoc/>
        public void Initialize()
        {
            InitializeCallCount++;

            InitializeStartedSignal?.Set();
            InitializeReleaseGate?.Wait();

            if (InitializeException is not null)
            {
                throw InitializeException;
            }
        }

        /// <inheritdoc/>
        public void Terminate()
        {
            TerminateCallCount++;

            if (TerminateException is not null)
            {
                throw TerminateException;
            }
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
            throw new NotSupportedException("Device enumeration is outside this test scope.");
        }

        /// <inheritdoc/>
        public bool IsCaptureFormatSupported(int deviceIndex, int channelCount, int sampleRate)
        {
            throw new NotSupportedException("Format negotiation is outside this test scope.");
        }

        /// <inheritdoc/>
        public bool IsPlaybackFormatSupported(int deviceIndex, int channelCount, int sampleRate)
        {
            throw new NotSupportedException("Format negotiation is outside this test scope.");
        }

        /// <inheritdoc/>
        public IPortAudioStream OpenCaptureStream(
            int deviceIndex,
            int channelCount,
            int sampleRate,
            uint framesPerBuffer,
            Action<IReadOnlyList<float>> onSamplesCaptured)
        {
            throw new NotSupportedException("Stream opening is outside this test scope.");
        }

        /// <inheritdoc/>
        public IPortAudioStream OpenPlaybackStream(
            int deviceIndex,
            int channelCount,
            int sampleRate,
            uint framesPerBuffer,
            Func<int, float[]> provideSamples)
        {
            throw new NotSupportedException("Stream opening is outside this test scope.");
        }
    }
}
