// cspell:ignore Alsa ALSA portaudio
using System.Runtime.InteropServices;
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
    ///     Minimal fake implementation of <see cref="IPortAudioApi"/> used by these unit tests.
    /// </summary>
    private sealed class FakePortAudioApi : IPortAudioApi
    {
        /// <summary>
        ///     Gets or sets the exception to throw when <see cref="Initialize"/> is called.
        /// </summary>
        internal Exception? InitializeException { get; init; }

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

        /// <inheritdoc/>
        public int HostApiCount => 0;

        /// <inheritdoc/>
        public int DeviceCount => 0;

        /// <inheritdoc/>
        public void Initialize()
        {
            InitializeCallCount++;

            if (InitializeException is not null)
            {
                throw InitializeException;
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
            Func<int, IReadOnlyList<float>> provideSamples)
        {
            throw new NotSupportedException("Stream opening is outside this test scope.");
        }
    }
}
