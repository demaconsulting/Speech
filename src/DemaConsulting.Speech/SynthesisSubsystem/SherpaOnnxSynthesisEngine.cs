using SherpaOnnx;

namespace DemaConsulting.Speech.SynthesisSubsystem;

/// <summary>
///     Real <see cref="ISynthesisEngine"/> implementation wrapping one sherpa-onnx
///     <see cref="OfflineTts"/> instance.
/// </summary>
/// <remarks>
///     This is the only type in the library that calls sherpa-onnx offline text-to-speech
///     inference APIs, which is what makes every other unit of the synthesis subsystem testable
///     without a native runtime.
///     <para>
///     Construction loads the model into native memory and therefore fails (throws) when the
///     native runtime binary for the current RID is absent or the model files are unusable.
///     Callers convert that into the honest <see cref="UnavailableSpeechSynthesizer"/> fallback;
///     see <see cref="SpeechSynthesizerFactory"/>.
///     </para>
///     <para>
///     Instances own unmanaged resources and must be disposed. <see cref="Generate"/> may safely
///     be called from a background thread while <see cref="Dispose"/> is invoked concurrently
///     from another to stop an in-flight session; native TTS synthesis for the in-flight call
///     completes or the underlying library's own thread-safety contract applies, since sherpa-onnx
///     provides no cancellation primitive for a call already in progress.
///     </para>
/// </remarks>
internal sealed class SherpaOnnxSynthesisEngine : ISynthesisEngine
{
    /// <summary>The loaded native offline text-to-speech engine.</summary>
    private readonly OfflineTts _tts;

    /// <summary>Whether <see cref="Dispose"/> has already released the native resources.</summary>
    private bool _isDisposed;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SherpaOnnxSynthesisEngine"/> class,
    ///     loading the model described by a configuration into a native offline TTS engine.
    /// </summary>
    /// <param name="config">
    ///     The model's own engine configuration, as produced by
    ///     <see cref="ModelManagementSubsystem.ISynthesisModel.CreateEngineConfig"/>.
    /// </param>
    /// <remarks>
    ///     Allocates native inference resources. Any failure to load the native library or the
    ///     model files surfaces here as an exception from the sherpa-onnx runtime.
    /// </remarks>
    internal SherpaOnnxSynthesisEngine(OfflineTtsConfig config)
    {
        _tts = new OfflineTts(config);
        SampleRate = _tts.SampleRate;
    }

    /// <inheritdoc/>
    public int SampleRate { get; }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">Thrown when the engine has been disposed.</exception>
    public EngineAudio Generate(string text, float speed, int speakerId)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(text);

        var generated = _tts.Generate(text, speed, speakerId);
        return new EngineAudio(generated.Samples, generated.SampleRate);
    }

    /// <inheritdoc/>
    /// <remarks>Releases the native offline TTS engine. Safe to call more than once.</remarks>
    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _tts.Dispose();
    }
}
