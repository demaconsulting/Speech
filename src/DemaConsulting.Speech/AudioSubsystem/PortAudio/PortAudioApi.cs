using System.Runtime.InteropServices;
using PortAudioRuntime = PortAudioSharp.PortAudio;
using PortAudioSampleFormat = PortAudioSharp.SampleFormat;
using PortAudioStream = PortAudioSharp.Stream;
using PortAudioStreamCallbackFlags = PortAudioSharp.StreamCallbackFlags;
using PortAudioStreamCallbackResult = PortAudioSharp.StreamCallbackResult;
using PortAudioStreamCallbackTimeInfo = PortAudioSharp.StreamCallbackTimeInfo;
using PortAudioStreamFlags = PortAudioSharp.StreamFlags;
using PortAudioStreamParameters = PortAudioSharp.StreamParameters;

namespace DemaConsulting.Speech.AudioSubsystem.PortAudio;

/// <summary>
///     Real <see cref="IPortAudioApi"/> implementation that composes PortAudioSharp2's managed
///     wrapper with the supplementary native host-API bindings declared in this repository.
/// </summary>
/// <remarks>
///     This adapter is the only unit that touches PortAudioSharp2 directly. All higher-level
///     capture/playback logic depends on <see cref="IPortAudioApi"/> so it can be unit tested with
///     pure managed fakes.
/// </remarks>
internal sealed class PortAudioApi : IPortAudioApi
{
    /// <summary>
    ///     Gets the shared runtime adapter used by the default production environment.
    /// </summary>
    internal static PortAudioApi Instance { get; } = new();

    /// <summary>
    ///     Prevents external construction; the adapter is stateless and safely shareable.
    /// </summary>
    private PortAudioApi()
    {
    }

    /// <inheritdoc/>
    public int HostApiCount => NormalizeCount(PortAudioNativeMethods.Pa_GetHostApiCount(), "host API");

    /// <inheritdoc/>
    public int DeviceCount => NormalizeCount(PortAudioRuntime.DeviceCount, "device");

    /// <inheritdoc/>
    public void Initialize()
    {
        PortAudioRuntime.LoadNativeLibrary();
        PortAudioRuntime.Initialize();
    }

    /// <inheritdoc/>
    public int? FindHostApiIndex(PortAudioHostApiType hostApiType)
    {
        var hostApiIndex = PortAudioNativeMethods.Pa_HostApiTypeIdToHostApiIndex(hostApiType);
        return hostApiIndex >= 0 ? hostApiIndex : null;
    }

    /// <inheritdoc/>
    public PortAudioHostApiInfo GetHostApiInfo(int hostApiIndex)
    {
        var hostApiInfoPointer = PortAudioNativeMethods.Pa_GetHostApiInfo(hostApiIndex);
        if (hostApiInfoPointer == nint.Zero)
        {
            throw new InvalidOperationException($"PortAudio returned no host-API info for index {hostApiIndex}.");
        }

        var hostApiInfoNative = Marshal.PtrToStructure<PortAudioNativeMethods.HostApiInfoNative>(hostApiInfoPointer);
        return new PortAudioHostApiInfo(
            Marshal.PtrToStringAnsi(hostApiInfoNative.Name) ?? string.Empty,
            hostApiInfoNative.Type,
            hostApiInfoNative.DefaultInputDevice,
            hostApiInfoNative.DefaultOutputDevice);
    }

    /// <inheritdoc/>
    public PortAudioDeviceInfo GetDeviceInfo(int deviceIndex)
    {
        var deviceInfo = PortAudioRuntime.GetDeviceInfo(deviceIndex);
        return new PortAudioDeviceInfo(
            deviceInfo.name ?? string.Empty,
            deviceInfo.hostApi,
            deviceInfo.maxInputChannels,
            deviceInfo.maxOutputChannels,
            ConvertSampleRate(deviceInfo.defaultSampleRate),
            deviceInfo.defaultLowInputLatency,
            deviceInfo.defaultLowOutputLatency);
    }

    /// <inheritdoc/>
    public IPortAudioStream OpenCaptureStream(
        int deviceIndex,
        int channelCount,
        int sampleRate,
        uint framesPerBuffer,
        Action<IReadOnlyList<float>> onSamplesCaptured)
    {
        ArgumentNullException.ThrowIfNull(onSamplesCaptured);

        var deviceInfo = GetDeviceInfo(deviceIndex);
        PortAudioStream.Callback callback = (
            nint input,
            nint output,
            uint frameCount,
            ref PortAudioStreamCallbackTimeInfo timeInfo,
            PortAudioStreamCallbackFlags statusFlags,
            nint userData) => CaptureCallback(input, frameCount, channelCount, onSamplesCaptured);

        var stream = new PortAudioStream(
            CreateInputParameters(deviceIndex, channelCount, deviceInfo.DefaultLowInputLatency),
            null,
            sampleRate,
            framesPerBuffer,
            PortAudioStreamFlags.NoFlag,
            callback,
            new object());
        return new PortAudioSharpStream(stream);
    }

    /// <inheritdoc/>
    public IPortAudioStream OpenPlaybackStream(
        int deviceIndex,
        int channelCount,
        int sampleRate,
        uint framesPerBuffer,
        Func<int, IReadOnlyList<float>> provideSamples)
    {
        ArgumentNullException.ThrowIfNull(provideSamples);

        var deviceInfo = GetDeviceInfo(deviceIndex);
        PortAudioStream.Callback callback = (
            nint input,
            nint output,
            uint frameCount,
            ref PortAudioStreamCallbackTimeInfo timeInfo,
            PortAudioStreamCallbackFlags statusFlags,
            nint userData) => PlaybackCallback(output, frameCount, channelCount, provideSamples);

        var stream = new PortAudioStream(
            null,
            CreateOutputParameters(deviceIndex, channelCount, deviceInfo.DefaultLowOutputLatency),
            sampleRate,
            framesPerBuffer,
            PortAudioStreamFlags.NoFlag,
            callback,
            new object());
        return new PortAudioSharpStream(stream);
    }

    /// <summary>
    ///     Converts one capture callback from the native PortAudio buffer shape into a managed
    ///     float-sample block for the higher-level device implementation.
    /// </summary>
    private static PortAudioStreamCallbackResult CaptureCallback(
        nint input,
        uint frameCount,
        int channelCount,
        Action<IReadOnlyList<float>> onSamplesCaptured)
    {
        try
        {
            var sampleCount = checked((int)frameCount * channelCount);
            var samples = new float[sampleCount];
            if (input != nint.Zero && sampleCount > 0)
            {
                Marshal.Copy(input, samples, 0, sampleCount);
            }

            onSamplesCaptured(samples);
            return PortAudioStreamCallbackResult.Continue;
        }
        catch
        {
            return PortAudioStreamCallbackResult.Abort;
        }
    }

    /// <summary>
    ///     Converts one managed playback block into the native PortAudio output-buffer shape.
    /// </summary>
    private static PortAudioStreamCallbackResult PlaybackCallback(
        nint output,
        uint frameCount,
        int channelCount,
        Func<int, IReadOnlyList<float>> provideSamples)
    {
        try
        {
            var sampleCount = checked((int)frameCount * channelCount);
            var providedSamples = provideSamples(sampleCount);
            var outputSamples = new float[sampleCount];
            var copyLength = Math.Min(sampleCount, providedSamples.Count);
            for (var index = 0; index < copyLength; index++)
            {
                outputSamples[index] = providedSamples[index];
            }

            if (output != nint.Zero && sampleCount > 0)
            {
                Marshal.Copy(outputSamples, 0, output, sampleCount);
            }

            return PortAudioStreamCallbackResult.Continue;
        }
        catch
        {
            return PortAudioStreamCallbackResult.Abort;
        }
    }

    /// <summary>
    ///     Builds PortAudio input-stream parameters for one capture device.
    /// </summary>
    private static PortAudioStreamParameters CreateInputParameters(
        int deviceIndex,
        int channelCount,
        double suggestedLatency)
    {
        return new PortAudioStreamParameters
        {
            device = deviceIndex,
            channelCount = channelCount,
            sampleFormat = PortAudioSampleFormat.Float32,
            suggestedLatency = suggestedLatency,
            hostApiSpecificStreamInfo = nint.Zero
        };
    }

    /// <summary>
    ///     Builds PortAudio output-stream parameters for one playback device.
    /// </summary>
    private static PortAudioStreamParameters CreateOutputParameters(
        int deviceIndex,
        int channelCount,
        double suggestedLatency)
    {
        return new PortAudioStreamParameters
        {
            device = deviceIndex,
            channelCount = channelCount,
            sampleFormat = PortAudioSampleFormat.Float32,
            suggestedLatency = suggestedLatency,
            hostApiSpecificStreamInfo = nint.Zero
        };
    }

    /// <summary>
    ///     Converts PortAudio's double-precision default sample rate to the library's integer Hz
    ///     representation.
    /// </summary>
    private static int ConvertSampleRate(double sampleRate)
    {
        return checked((int)Math.Round(sampleRate, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    ///     Validates one PortAudio count-returning API value, converting negative error-style
    ///     returns into managed exceptions with clear context.
    /// </summary>
    private static int NormalizeCount(int count, string noun)
    {
        if (count < 0)
        {
            throw new InvalidOperationException($"PortAudio returned an invalid {noun} count ({count}).");
        }

        return count;
    }

    /// <summary>
    ///     Wraps PortAudioSharp2's concrete <see cref="PortAudioSharp.Stream"/> behind the
    ///     library-owned <see cref="IPortAudioStream"/> seam.
    /// </summary>
    private sealed class PortAudioSharpStream(PortAudioStream stream) : IPortAudioStream
    {
        /// <summary>
        ///     The concrete PortAudioSharp2 stream instance.
        /// </summary>
        private readonly PortAudioStream _stream = stream ?? throw new ArgumentNullException(nameof(stream));

        /// <inheritdoc/>
        public void Start()
        {
            _stream.Start();
        }

        /// <inheritdoc/>
        public void Stop()
        {
            _stream.Stop();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}
