package Speech_UnavailableSpeechSynthesizerEngine {
    private import SoftwareItemMetadata::*;
    private import Speech_IAudioPlaybackDevice::*;
    private import Speech_ISpeechSynthesizerEngine::*;
    private import Speech_ISynthesisSession::*;
    private import Speech_UnavailableAudioPlaybackDevice::*;
    private import Speech_UnavailableSynthesisSession::*;

    part def UnavailableSpeechSynthesizerEngine {
        doc /* Honest "unavailable" ISpeechSynthesizerEngine fallback: reports IsAvailable as false;
             * CreateSessionAsync always succeeds, returning the shared UnavailableSynthesisSession instance, since
             * binding a device to an already unavailable engine is itself an ordinary (if useless) composition, not
             * an error - only the returned session's operational members throw SpeechSynthesizerUnavailableException. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/UnavailableSpeechSynthesizerEngine.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/UnavailableSpeechSynthesizerEngineTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/unavailable-speech-synthesizer-engine.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/unavailable-speech-synthesizer-engine.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/unavailable-speech-synthesizer-engine.yaml");
        }

        // Implements the engine contract and returns only unavailable synthesis sessions.
        dependency UnavailableSpeechSynthesizerEngine to IAudioPlaybackDevice;
        dependency UnavailableSpeechSynthesizerEngine to ISpeechSynthesizerEngine;
        dependency UnavailableSpeechSynthesizerEngine to ISynthesisSession;
        dependency UnavailableSpeechSynthesizerEngine to UnavailableAudioPlaybackDevice;
        dependency UnavailableSpeechSynthesizerEngine to UnavailableSynthesisSession;
    }
}
