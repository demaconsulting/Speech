namespace DemaConsulting.Speech.Demo.SynthesisPanelSubsystem;

/// <summary>
///     The demo's text-to-speech panel playback lifecycle state.
/// </summary>
public enum SynthesisPlaybackState
{
    /// <summary>No synthesis session is in progress.</summary>
    Idle,

    /// <summary>The synthesizer has been asked to speak but audio has not yet started playing.</summary>
    Synthesizing,

    /// <summary>Audio is currently being played back through the selected device.</summary>
    Playing,

    /// <summary>
    ///     The most recent attempt could not proceed: no model selected, no playback device
    ///     available, or the synthesizer/engine reported a fault.
    /// </summary>
    Error,
}
