package Speech_RecognitionEngineBusyException {
    private import SoftwareItemMetadata::*;

    part def RecognitionEngineBusyException {
        doc /* Exception signaling that ISpeechRecognizerEngine.CreateSessionAsync was called while the engine's
             * single lease is already held by another live IRecognitionSession. The engine fails fast (no queueing)
             * rather than blocking the caller. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/RecognitionEngineBusyException.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SpeechRecognizerEngineTests.cs", "test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionEngineBusyExceptionTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/recognition-engine-busy-exception.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/recognition-engine-busy-exception.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/recognition-engine-busy-exception.yaml");
        }
    }
}
