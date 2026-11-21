package Speech_SpeechRecognitionResult {
    private import SoftwareItemMetadata::*;
    private import ScalarValues::*;

    part def SpeechRecognitionResult {
        doc /* Supporting record carrying one recognition result (text, finality and timing) produced while streaming
             * audio through an IRecognitionSession. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognitionResult.cs", "src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognitionEvent.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionResultBufferTests.cs", "test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SpeechRecognitionRecordTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem.yaml");
        }

        attribute text : String;
        attribute isFinal : Boolean;
    }
}
