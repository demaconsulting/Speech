package Speech_RecognitionBackend {
    private import SoftwareItemMetadata::*;
    private import Speech_SpeechRecognitionResult::*;

    part def RecognitionBackend {
        doc /* Internal mockable seam over one loaded speech-inference backend, together with its factory and the
             * default factory that asks a model to construct its own backend, confining every inference interop call
             * so the recognition pipeline is testable with pure managed fakes. Concrete backends are supplied by
             * sibling packages such as SpeechSherpa. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/IRecognitionBackend.cs", "src/DemaConsulting.Speech/RecognitionSubsystem/IRecognitionBackendFactory.cs", "src/DemaConsulting.Speech/RecognitionSubsystem/DefaultRecognitionBackendFactory.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/RecognitionSessionTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/recognition-backend.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/recognition-backend.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/recognition-backend.yaml");
        }

        // Is created from model contracts and produces speech recognition results.
        dependency RecognitionBackend to Speech_SpeechModelContract::SpeechModelContract;
        dependency RecognitionBackend to SpeechRecognitionResult;
    }
}
