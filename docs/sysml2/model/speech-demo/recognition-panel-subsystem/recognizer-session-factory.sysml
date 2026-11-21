package SpeechDemo_RecognizerSessionFactory {
    private import SoftwareItemMetadata::*;
    private import SpeechDemo_IRecognizerSessionFactory::*;

    part def RecognizerSessionFactory {
        doc /* Real seam implementation: resolves the model's installed-files directory from the shared
             * SpeechModelStore, narrows the public model to IRecognitionModel, and forwards to
             * SpeechRecognizerFactory.LoadAsync, returning the library's own UnavailableSpeechRecognizerEngine for a
             * model of the wrong role. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/RecognizerSessionFactory.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/RecognitionPanelSubsystem/RecognizerSessionFactoryTests.cs");
            designFiles = ("docs/design/speech-demo/recognition-panel-subsystem/recognizer-session-factory.md");
            verificationFiles = ("docs/verification/speech-demo/recognition-panel-subsystem/recognizer-session-factory.md");
            requirementFiles = ("docs/reqstream/speech-demo/recognition-panel-subsystem/recognizer-session-factory.yaml");
        }

        // Builds recognizers with SpeechRecognizerFactory from store-installed models, else an unavailable
        // fallback.
        dependency RecognizerSessionFactory to IRecognizerSessionFactory;
        dependency RecognizerSessionFactory to Speech_ISpeechRecognizerEngine::ISpeechRecognizerEngine;
        dependency RecognizerSessionFactory to Speech_SpeechModelContract::SpeechModelContract;
        dependency RecognizerSessionFactory to Speech_SpeechModelStore::SpeechModelStore;
        dependency RecognizerSessionFactory to Speech_SpeechRecognizerFactory::SpeechRecognizerFactory;
        dependency RecognizerSessionFactory to Speech_UnavailableSpeechRecognizerEngine::UnavailableSpeechRecognizerEngine;
    }
}
