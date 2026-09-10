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
    public int HostApiCount
    {
        get
        {
            // Intentional native interop: PortAudioSharp2 does not expose host-API counting.
            return NormalizeCount(PortAudioNativeMethods.Pa_GetHostApiCount(), "host API");
        }
    }

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
        // Intentional native interop: resolving a stable host-API type requires the PortAudio C API.
        var hostApiIndex = PortAudioNativeMethods.Pa_HostApiTypeIdToHostApiIndex(hostApiType);
        return hostApiIndex >= 0 ? hostApiIndex : null;
    }

    /// <inheritdoc/>
    public PortAudioHostApiInfo GetHostApiInfo(int hostApiIndex)
    {
        // Intentional native interop: PortAudioSharp2 does not surface host-API metadata.
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
    public bool IsCaptureFormatSupported(int deviceIndex, int channelCount, int sampleRate)
    {
        var deviceInfo = GetDeviceInfo(deviceIndex);
        var parameters = CreateInputParameters(deviceIndex, channelCount, deviceInfo.DefaultLowInputLatency);
        return IsFormatSupported(parameters, isInput: true, sampleRate);
    }

    /// <inheritdoc/>
    public bool IsPlaybackFormatSupported(int deviceIndex, int channelCount, int sampleRate)
    {
        var deviceInfo = GetDeviceInfo(deviceIndex);
        var parameters = CreateOutputParameters(deviceIndex, channelCount, deviceInfo.DefaultLowOutputLatency);
        return IsFormatSupported(parameters, isInput: false, sampleRate);
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

        // Reused across every invocation of this stream's callback rather than allocated per
        // call: PortAudio always calls back on a single dedicated thread per stream, one
        // invocation at a time, and onSamplesCaptured (PortAudioCaptureDevice.OnSamplesCaptured)
        // always makes its own defensive copy of the samples before this callback returns, so no
        // caller ever observes this buffer being overwritten by the next callback.
        var captureBuffer = new ReusableSampleBuffer();
        PortAudioStream.Callback callback = (
            nint input,
            nint output,
            uint frameCount,
            ref PortAudioStreamCallbackTimeInfo timeInfo,
            PortAudioStreamCallbackFlags statusFlags,
            nint userData) => CaptureCallback(input, frameCount, channelCount, onSamplesCaptured, captureBuffer);

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
        Func<int, float[]> provideSamples)
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
        Action<IReadOnlyList<float>> onSamplesCaptured,
        ReusableSampleBuffer captureBuffer)
    {
        try
        {
            var sampleCount = checked((int)frameCount * channelCount);

            // A stream's callback block size is fixed once opened, so in steady state this never
            // reallocates after the first call - it only ever allocates again if PortAudio were to
            // request a different sample count than last time.
            if (captureBuffer.Samples.Length != sampleCount)
            {
                captureBuffer.Samples = new float[sampleCount];
            }

            var samples = captureBuffer.Samples;
            if (input != nint.Zero && sampleCount > 0)
            {
                Marshal.Copy(input, samples, 0, sampleCount);
            }
            else if (sampleCount > 0)
            {
                // PortAudio supplies a null input pointer when it has no capture data for this
                // callback (e.g. a stream underrun); the reused buffer may still hold stale
                // samples from a prior callback, so it must be zeroed here to preserve the
                // documented "silence when no data" contract instead of forwarding stale audio.
                Array.Clear(samples, 0, sampleCount);
            }

            onSamplesCaptured(samples);
            return PortAudioStreamCallbackResult.Continue;
        }
        catch
        {
            // Intentionally broad: any managed fault on the native callback thread must abort
            // cleanly rather than let an arbitrary exception cross the unmanaged boundary.
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
        Func<int, float[]> provideSamples)
    {
        try
        {
            var sampleCount = checked((int)frameCount * channelCount);
            var providedSamples = provideSamples(sampleCount);

            if (output != nint.Zero && sampleCount > 0)
            {
                Marshal.Copy(providedSamples, 0, output, sampleCount);
            }

            return PortAudioStreamCallbackResult.Continue;
        }
        catch
        {
            // Intentionally broad: any managed fault on the native callback thread must abort
            // cleanly rather than let an arbitrary exception cross the unmanaged boundary.
            return PortAudioStreamCallbackResult.Abort;
        }
    }

    /// <summary>
    ///     A single mutable slot holding one capture stream's reused sample buffer, so
    ///     <see cref="CaptureCallback"/> (a <see langword="static"/> method, shared by every open
    ///     stream) can hold per-stream buffer state via a closure without needing an instance
    ///     field on this stateless adapter type.
    /// </summary>
    private sealed class ReusableSampleBuffer
    {
        /// <summary>Gets or sets the buffer reused across this stream's callback invocations.</summary>
        internal float[] Samples { get; set; } = [];
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
    ///     Probes the native runtime for whether the given stream parameters and sample rate can
    ///     actually be opened, marshaling the managed parameters to unmanaged memory for the
    ///     duration of the native call and always releasing it afterward.
    /// </summary>
    /// <param name="parameters">
    ///     The input or output stream parameters to probe, built the same way the corresponding
    ///     open-stream path builds them.
    /// </param>
    /// <param name="isInput">
    ///     <see langword="true"/> when <paramref name="parameters"/> describes the input side of
    ///     the probe; <see langword="false"/> when it describes the output side.
    /// </param>
    /// <param name="sampleRate">The sample rate, in Hz, to probe.</param>
    /// <returns>
    ///     <see langword="true"/> when the native runtime reports <c>paNoError</c> for the exact
    ///     combination; <see langword="false"/> when it reports any other result or when the
    ///     probe itself fails for any reason - a probe failure must never be worse than the
    ///     un-negotiated behavior it replaces.
    /// </returns>
    private static bool IsFormatSupported(PortAudioStreamParameters parameters, bool isInput, int sampleRate)
    {
        var parametersPointer = nint.Zero;
        try
        {
            parametersPointer = Marshal.AllocHGlobal(Marshal.SizeOf<PortAudioStreamParameters>());
            Marshal.StructureToPtr(parameters, parametersPointer, false);

            var inputParameters = isInput ? parametersPointer : nint.Zero;
            var outputParameters = isInput ? nint.Zero : parametersPointer;
            // Intentional native interop: capability probing requires the PortAudio C API entry point.
            var result = PortAudioNativeMethods.Pa_IsFormatSupported(inputParameters, outputParameters, sampleRate);
            return result == 0;
        }
        catch
        {
            // Intentionally broad: this best-effort native capability probe must fail safe on
            // any interop fault rather than escape the unmanaged interoperability boundary.
            // A probe failure (for example a marshaling fault) must fail safe rather than
            // propagate, since the caller's fallback behavior is strictly no worse than the
            // un-negotiated forwarding this probe replaces.
            return false;
        }
        finally
        {
            if (parametersPointer != nint.Zero)
            {
                Marshal.FreeHGlobal(parametersPointer);
            }
        }
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
