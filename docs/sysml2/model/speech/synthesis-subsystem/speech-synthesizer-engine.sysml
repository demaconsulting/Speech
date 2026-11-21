package Speech_SpeechSynthesizerEngine {
    private import SoftwareItemMetadata::*;
    private import Speech_IAudioPlaybackDevice::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_ISpeechSynthesizerEngine::*;
    private import Speech_ISynthesisSession::*;
    private import Speech_NullSpeechDiagnostics::*;
    private import Speech_SpeechDiagnosticLevel::*;
    private import Speech_SpeechModelContract::*;
    private import Speech_SynthesisBackend::*;
    private import Speech_SynthesisEngineBusyException::*;
    private import Speech_SynthesisSession::*;
    private import Speech_UnavailableAudioPlaybackDevice::*;

    part def SpeechSynthesizerEngine {
        doc /* Real ISpeechSynthesizerEngine implementation: holds one loaded SynthesisBackend, enforces
             * single-session exclusivity with a fail-fast binary semaphore lease held for a leased session's entire
             * life through DisposeAsync completion (a concurrent CreateSessionAsync call while the lease is held
             * throws SynthesisEngineBusyException), and constructs SynthesisSession instances bound to a
             * caller-supplied playback device. Disposing the engine disposes any still-active leased session first,
             * then the owned backend. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/SpeechSynthesizerEngine.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SpeechSynthesizerEngineTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/speech-synthesizer-engine.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/speech-synthesizer-engine.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/speech-synthesizer-engine.yaml");
        }

        // Wraps a loaded synthesis backend and hands out one exclusive synthesis session at a time.
        dependency SpeechSynthesizerEngine to IAudioPlaybackDevice;
        dependency SpeechSynthesizerEngine to ISpeechDiagnostics;
        dependency SpeechSynthesizerEngine to ISpeechSynthesizerEngine;
        dependency SpeechSynthesizerEngine to ISynthesisSession;
        dependency SpeechSynthesizerEngine to NullSpeechDiagnostics;
        dependency SpeechSynthesizerEngine to SpeechDiagnosticLevel;
        dependency SpeechSynthesizerEngine to SpeechModelContract;
        dependency SpeechSynthesizerEngine to SynthesisBackend;
        dependency SpeechSynthesizerEngine to SynthesisEngineBusyException;
        dependency SpeechSynthesizerEngine to SynthesisSession;
        dependency SpeechSynthesizerEngine to UnavailableAudioPlaybackDevice;
    }
}
