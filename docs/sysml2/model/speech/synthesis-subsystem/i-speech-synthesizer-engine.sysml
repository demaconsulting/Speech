package Speech_ISpeechSynthesizerEngine {
    private import SoftwareItemMetadata::*;
    private import Speech_IAudioPlaybackDevice::*;
    private import Speech_ISynthesisSession::*;

    part def ISpeechSynthesizerEngine {
        doc /* Layer 3: the public, loaded, expensive, native-backed text-to-speech model contract - availability
             * flag, async CreateSessionAsync to bind a playback device into an ISynthesisSession, and one-shot
             * SpeakAsync/SynthesizeAsync convenience overloads that create a session internally and dispose it - plus
             * its adjacent, immutable SynthesizedSpeech audio-segment payload type, exposing no inference-engine
             * type. Obtaining this engine is the expensive step; a session obtained from it is cheap and reusable
             * across many calls. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/ISpeechSynthesizerEngine.cs", "src/DemaConsulting.Speech/SynthesisSubsystem/SynthesizedSpeech.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SpeechSynthesizerEngineTests.cs", "test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SynthesizedSpeechTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/i-speech-synthesizer-engine.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/i-speech-synthesizer-engine.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/i-speech-synthesizer-engine.yaml");
        }

        // Creates synthesis sessions bound to a playback device.
        dependency ISpeechSynthesizerEngine to IAudioPlaybackDevice;
        dependency ISpeechSynthesizerEngine to ISynthesisSession;
    }
}
