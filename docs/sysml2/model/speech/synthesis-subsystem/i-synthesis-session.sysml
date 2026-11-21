package Speech_ISynthesisSession {
    private import SoftwareItemMetadata::*;
    private import Speech_SynthesisSessionState::*;

    part def ISynthesisSession {
        doc /* Layer 5: a cheap text-to-speech session, bound to exactly one playback device instance for its entire
             * life, obtained from ISpeechSynthesizerEngine.CreateSessionAsync. Exposes an availability flag, a
             * SynthesisSessionState lifecycle state with a StateChanged event, non-overlapping
             * SpeakAsync/SynthesizeAsync operations (a second call while one is in flight throws
             * InvalidOperationException), and StopAsync to request cooperative cancellation of whichever operation is
             * currently in flight. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/ISynthesisSession.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesisSessionTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/i-synthesis-session.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/i-synthesis-session.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/i-synthesis-session.yaml");
        }

        // Belongs to a synthesizer engine and publishes its synthesis session state.
        dependency ISynthesisSession to Speech_ISpeechSynthesizerEngine::ISpeechSynthesizerEngine;
        dependency ISynthesisSession to SynthesisSessionState;
    }
}
