// cspell:ignore Alsa ALSA portaudio
namespace DemaConsulting.Speech.AudioSubsystem.PortAudio;

/// <summary>
///     Represents one opened PortAudio stream behind a mockable, library-owned abstraction.
/// </summary>
/// <remarks>
///     The public audio-device layer owns lifecycle decisions such as when to start, stop, or
///     dispose a stream, while this seam hides the concrete PortAudioSharp2 stream type so unit
///     tests can substitute a fake implementation.
/// </remarks>
internal interface IPortAudioStream : IDisposable
{
    /// <summary>
    ///     Starts PortAudio callback processing for the opened stream.
    /// </summary>
    void Start();

    /// <summary>
    ///     Stops PortAudio callback processing for the opened stream.
    /// </summary>
    void Stop();
}
