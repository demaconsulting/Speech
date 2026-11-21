package Speech_SpeechRecognizerUnavailableException {
    private import SoftwareItemMetadata::*;

    part def SpeechRecognizerUnavailableException {
        doc /* Exception signaling that an operational member of an unavailable speech recognizer engine or session
             * was invoked, or that a session which claimed to be available failed on first use (for example the
             * capture device going unavailable mid-session). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognizerUnavailableException.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SpeechRecognizerUnavailableExceptionTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/speech-recognizer-unavailable-exception.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/speech-recognizer-unavailable-exception.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/speech-recognizer-unavailable-exception.yaml");
        }
    }
}
