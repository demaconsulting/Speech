package Speech_SpeechRecognizerFactory {
    private import SoftwareItemMetadata::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_ISpeechRecognizerEngine::*;
    private import Speech_NullSpeechDiagnostics::*;
    private import Speech_RecognitionBackend::*;
    private import Speech_SpeechDiagnosticLevel::*;
    private import Speech_SpeechModelCatalog::*;
    private import Speech_SpeechModelContract::*;
    private import Speech_SpeechModelDescriptorEnums::*;
    private import Speech_SpeechModelParameters::*;
    private import Speech_SpeechModelStore::*;
    private import Speech_SpeechRecognizerEngine::*;
    private import Speech_UnavailableSpeechRecognizerEngine::*;

    part def SpeechRecognizerFactory {
        doc /* Composition entry point whose LoadAsync overloads return a working ISpeechRecognizerEngine for an
             * installed recognition model, or an honest UnavailableSpeechRecognizerEngine fallback, without ever
             * throwing for an ordinary machine state. Loading runs on a dedicated worker thread so the expensive
             * native backend construction never blocks the calling thread. A capture device is bound later, per
             * session, via ISpeechRecognizerEngine.CreateSessionAsync - not here - so one loaded engine can be reused
             * across many devices or many sequential sessions. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/RecognitionSubsystem/SpeechRecognizerFactory.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/RecognitionSubsystem/SpeechRecognizerFactoryTests.cs");
            designFiles = ("docs/design/speech/recognition-subsystem/speech-recognizer-factory.md");
            verificationFiles = ("docs/verification/speech/recognition-subsystem/speech-recognizer-factory.md");
            requirementFiles = ("docs/reqstream/speech/recognition-subsystem/speech-recognizer-factory.yaml");
        }

        // Finds the model in the catalog and store, loads its backend, and falls back to an unavailable
        // engine.
        dependency SpeechRecognizerFactory to ISpeechDiagnostics;
        dependency SpeechRecognizerFactory to ISpeechRecognizerEngine;
        dependency SpeechRecognizerFactory to NullSpeechDiagnostics;
        dependency SpeechRecognizerFactory to RecognitionBackend;
        dependency SpeechRecognizerFactory to SpeechDiagnosticLevel;
        dependency SpeechRecognizerFactory to SpeechModelCatalog;
        dependency SpeechRecognizerFactory to SpeechModelContract;
        dependency SpeechRecognizerFactory to SpeechModelDescriptorEnums;
        dependency SpeechRecognizerFactory to SpeechModelParameters;
        dependency SpeechRecognizerFactory to SpeechModelStore;
        dependency SpeechRecognizerFactory to SpeechRecognizerEngine;
        dependency SpeechRecognizerFactory to UnavailableSpeechRecognizerEngine;
    }
}
