package Speech_SpeechSynthesizerUnavailableException {
    private import SoftwareItemMetadata::*;

    part def SpeechSynthesizerUnavailableException {
        doc /* Exception signaling that an operational member of an unavailable ISpeechSynthesizerEngine or
             * ISynthesisSession was invoked, or that a session which claimed to be available faulted and was asked to
             * operate again. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/SpeechSynthesizerUnavailableException.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/UnavailableSpeechSynthesizerEngineTests.cs", "test/DemaConsulting.Speech.Tests/SynthesisSubsystem/UnavailableSynthesisSessionTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/speech-synthesizer-unavailable-exception.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/speech-synthesizer-unavailable-exception.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/speech-synthesizer-unavailable-exception.yaml");
        }
    }
}
