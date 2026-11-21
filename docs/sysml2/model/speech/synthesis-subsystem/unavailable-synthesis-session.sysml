package Speech_UnavailableSynthesisSession {
    private import SoftwareItemMetadata::*;
    private import Speech_ISpeechSynthesizerEngine::*;
    private import Speech_ISynthesisSession::*;
    private import Speech_SpeechSynthesizerUnavailableException::*;
    private import Speech_SynthesisSessionState::*;

    part def UnavailableSynthesisSession {
        doc /* Honest "unavailable" ISynthesisSession fallback: reports IsAvailable as false, always reports
             * SynthesisSessionState.Created (it never transitions), and throws SpeechSynthesizerUnavailableException
             * only when SpeakAsync or SynthesizeAsync is invoked; StopAsync and DisposeAsync are safe no-ops. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/UnavailableSynthesisSession.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/UnavailableSynthesisSessionTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/unavailable-synthesis-session.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/unavailable-synthesis-session.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/unavailable-synthesis-session.yaml");
        }

        // Implements the session contract by throwing the synthesizer-unavailable exception.
        dependency UnavailableSynthesisSession to ISpeechSynthesizerEngine;
        dependency UnavailableSynthesisSession to ISynthesisSession;
        dependency UnavailableSynthesisSession to SpeechSynthesizerUnavailableException;
        dependency UnavailableSynthesisSession to SynthesisSessionState;
    }
}
