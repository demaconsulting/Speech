using System.Runtime.InteropServices;
using DemaConsulting.Speech.AudioSubsystem.PortAudio;
using PortAudioSampleFormat = PortAudioSharp.SampleFormat;
using PortAudioStreamCallbackResult = PortAudioSharp.StreamCallbackResult;
using PortAudioStreamParameters = PortAudioSharp.StreamParameters;

namespace DemaConsulting.Speech.Tests.AudioSubsystem.PortAudio;

/// <summary>
///     Unit tests for the deterministic adapter logic in <see cref="PortAudioApi"/>: native metadata
///     conversion, callback marshalling, count validation and format probing. Real stream opening
///     needs the native runtime and is covered by manual hardware verification only.
/// </summary>
public sealed class PortAudioApiTests
{
    /// <summary>Proves a native host-API info block converts to the managed metadata record.</summary>
    [Fact]
    public void PortAudioApi_ConvertHostApiInfo_NativeBlock_ReturnsManagedMetadata()
    {
        // Arrange: a native block holding a type, name pointer and default device indices
        var namePointer = Marshal.StringToHGlobalAnsi("Windows WASAPI");
        var blockPointer = Marshal.AllocHGlobal(Marshal.SizeOf<PortAudioNativeMethods.HostApiInfoNative>());
        try
        {
            var native = new PortAudioNativeMethods.HostApiInfoNative
            {
                StructVersion = 1,
                Type = PortAudioHostApiType.Wasapi,
                Name = namePointer,
                DeviceCount = 3,
                DefaultInputDevice = 4,
                DefaultOutputDevice = 7
            };
            Marshal.StructureToPtr(native, blockPointer, false);

            // Act
            var info = PortAudioApi.ConvertHostApiInfo(blockPointer);

            // Assert
            Assert.Equal("Windows WASAPI", info.Name);
            Assert.Equal(PortAudioHostApiType.Wasapi, info.Type);
            Assert.Equal(4, info.DefaultInputDeviceIndex);
            Assert.Equal(7, info.DefaultOutputDeviceIndex);
        }
        finally
        {
            Marshal.FreeHGlobal(blockPointer);
            Marshal.FreeHGlobal(namePointer);
        }
    }

    /// <summary>Proves a null native name converts to an empty managed name.</summary>
    [Fact]
    public void PortAudioApi_ConvertHostApiInfo_NullName_ReturnsEmptyName()
    {
        // Arrange
        var blockPointer = Marshal.AllocHGlobal(Marshal.SizeOf<PortAudioNativeMethods.HostApiInfoNative>());
        try
        {
            Marshal.StructureToPtr(
                new PortAudioNativeMethods.HostApiInfoNative { Type = PortAudioHostApiType.Alsa, DefaultInputDevice = -1 },
                blockPointer,
                false);

            // Act
            var info = PortAudioApi.ConvertHostApiInfo(blockPointer);

            // Assert
            Assert.Equal(string.Empty, info.Name);
            Assert.Equal(-1, info.DefaultInputDeviceIndex);
        }
        finally
        {
            Marshal.FreeHGlobal(blockPointer);
        }
    }

    /// <summary>Proves the default sample rate rounds to the nearest whole hertz, halves away from zero.</summary>
    [Theory]
    [InlineData(44100.0, 44100)]
    [InlineData(44100.4, 44100)]
    [InlineData(44100.5, 44101)]
    [InlineData(8000.0, 8000)]
    public void PortAudioApi_ConvertSampleRate_FractionalRate_RoundsToWholeHertz(double rate, int expected)
    {
        // Act
        var converted = PortAudioApi.ConvertSampleRate(rate);

        // Assert
        Assert.Equal(expected, converted);
    }

    /// <summary>Proves a non-negative native count is returned and a negative one is rejected.</summary>
    [Fact]
    public void PortAudioApi_NormalizeCount_NegativeCount_ThrowsWithContext()
    {
        // Act
        var ok = PortAudioApi.NormalizeCount(3, "device");
        var error = Assert.Throws<InvalidOperationException>(() => PortAudioApi.NormalizeCount(-9996, "host API"));

        // Assert
        Assert.Equal(3, ok);
        Assert.Contains("host API", error.Message, StringComparison.Ordinal);
        Assert.Contains("-9996", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Proves the capture callback copies the native block to a managed block and continues.</summary>
    [Fact]
    public void PortAudioApi_CaptureCallback_NativeInput_DeliversManagedSamples()
    {
        // Arrange: two stereo frames in native memory
        float[] native = [0.1f, -0.2f, 0.3f, -0.4f];
        var input = Marshal.AllocHGlobal(native.Length * sizeof(float));
        try
        {
            Marshal.Copy(native, 0, input, native.Length);
            float[]? delivered = null;

            // Act
            var result = PortAudioApi.CaptureCallback(
                input,
                2,
                2,
                samples => delivered = [.. samples],
                new PortAudioApi.ReusableSampleBuffer());

            // Assert
            Assert.Equal(PortAudioStreamCallbackResult.Continue, result);
            Assert.NotNull(delivered);
            Assert.Equal(native, delivered);
        }
        finally
        {
            Marshal.FreeHGlobal(input);
        }
    }

    /// <summary>Proves a null native input delivers silence rather than stale samples from a prior block.</summary>
    [Fact]
    public void PortAudioApi_CaptureCallback_NullInputAfterData_DeliversSilence()
    {
        // Arrange: first a real block, then a null-input block through the same reused buffer
        float[] native = [0.5f, 0.5f];
        var input = Marshal.AllocHGlobal(native.Length * sizeof(float));
        var buffer = new PortAudioApi.ReusableSampleBuffer();
        try
        {
            Marshal.Copy(native, 0, input, native.Length);
            PortAudioApi.CaptureCallback(input, 2, 1, _ => { }, buffer);
            float[]? delivered = null;

            // Act
            var result = PortAudioApi.CaptureCallback(nint.Zero, 2, 1, samples => delivered = [.. samples], buffer);

            // Assert
            Assert.Equal(PortAudioStreamCallbackResult.Continue, result);
            Assert.NotNull(delivered);
            Assert.Equal([0f, 0f], delivered);
        }
        finally
        {
            Marshal.FreeHGlobal(input);
        }
    }

    /// <summary>Proves a fault inside the capture handler aborts the stream instead of escaping.</summary>
    [Fact]
    public void PortAudioApi_CaptureCallback_HandlerThrows_ReturnsAbort()
    {
        // Act
        var result = PortAudioApi.CaptureCallback(
            nint.Zero,
            2,
            1,
            _ => throw new InvalidOperationException("boom"),
            new PortAudioApi.ReusableSampleBuffer());

        // Assert
        Assert.Equal(PortAudioStreamCallbackResult.Abort, result);
    }

    /// <summary>Proves the playback callback copies the provided managed block into native memory.</summary>
    [Fact]
    public void PortAudioApi_PlaybackCallback_ProvidedSamples_AreWrittenToNativeOutput()
    {
        // Arrange
        var output = Marshal.AllocHGlobal(4 * sizeof(float));
        try
        {
            var requested = 0;

            // Act
            var result = PortAudioApi.PlaybackCallback(
                output,
                2,
                2,
                count =>
                {
                    requested = count;
                    return [0.25f, -0.25f, 0.5f, -0.5f];
                });
            var written = new float[4];
            Marshal.Copy(output, written, 0, 4);

            // Assert
            Assert.Equal(PortAudioStreamCallbackResult.Continue, result);
            Assert.Equal(4, requested);
            Assert.Equal([0.25f, -0.25f, 0.5f, -0.5f], written);
        }
        finally
        {
            Marshal.FreeHGlobal(output);
        }
    }

    /// <summary>Proves a fault inside the playback provider aborts the stream instead of escaping.</summary>
    [Fact]
    public void PortAudioApi_PlaybackCallback_ProviderThrows_ReturnsAbort()
    {
        // Arrange
        var output = Marshal.AllocHGlobal(2 * sizeof(float));
        try
        {
            // Act
            var result = PortAudioApi.PlaybackCallback(
                output,
                2,
                1,
                _ => throw new InvalidOperationException("boom"));

            // Assert
            Assert.Equal(PortAudioStreamCallbackResult.Abort, result);
        }
        finally
        {
            Marshal.FreeHGlobal(output);
        }
    }

    /// <summary>Proves the format probe reports support only for a native no-error result, passing the right side.</summary>
    [Theory]
    [InlineData(0, true, true)]
    [InlineData(-9997, true, false)]
    [InlineData(0, false, true)]
    [InlineData(-9997, false, false)]
    public void PortAudioApi_IsFormatSupported_NativeResult_MapsToSupport(int nativeResult, bool isInput, bool expected)
    {
        // Arrange
        var parameters = new PortAudioStreamParameters
        {
            device = 2,
            channelCount = 1,
            sampleFormat = PortAudioSampleFormat.Float32,
            suggestedLatency = 0.01
        };
        var inputIsSet = false;
        var outputIsSet = false;
        var rateSeen = 0.0;

        // Act
        var supported = PortAudioApi.IsFormatSupported(
            parameters,
            isInput,
            16000,
            (inputPointer, outputPointer, rate) =>
            {
                inputIsSet = inputPointer != nint.Zero;
                outputIsSet = outputPointer != nint.Zero;
                rateSeen = rate;
                return nativeResult;
            });

        // Assert
        Assert.Equal(expected, supported);
        Assert.Equal(isInput, inputIsSet);
        Assert.Equal(!isInput, outputIsSet);
        Assert.Equal(16000.0, rateSeen);
    }

    /// <summary>Proves a fault in the native probe is reported as unsupported rather than propagated.</summary>
    [Fact]
    public void PortAudioApi_IsFormatSupported_NativeCallThrows_ReturnsFalse()
    {
        // Act
        var supported = PortAudioApi.IsFormatSupported(
            new PortAudioStreamParameters(),
            true,
            16000,
            (_, _, _) => throw new DllNotFoundException("portaudio"));

        // Assert
        Assert.False(supported);
    }
}
