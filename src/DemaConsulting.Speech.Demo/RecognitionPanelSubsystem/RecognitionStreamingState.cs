namespace DemaConsulting.Speech.Demo.RecognitionPanelSubsystem;

/// <summary>
///     The demo's speech-to-text panel streaming lifecycle state.
/// </summary>
public enum RecognitionStreamingState
{
    /// <summary>No recognition session is running.</summary>
    Idle,

    /// <summary>Audio is being captured and transcribed.</summary>
    Listening,

    /// <summary>
    ///     The most recent attempt could not proceed: no model selected, no capture device
    ///     available, or the recognizer/engine reported a fault.
    /// </summary>
    Error,
}
