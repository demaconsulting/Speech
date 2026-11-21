package SpeechDemo_IRecognizerSessionFactory {
    private import SoftwareItemMetadata::*;

    part def IRecognizerSessionFactory {
        doc /* Demo-owned seam accepting the library's public ISpeechModel contract to compose a speech recognizer
             * engine, so the panel can be tested with a plain fake model with no InternalsVisibleTo grant from the
             * library. Exposes a single LoadAsync member returning an ISpeechRecognizerEngine; per-run capture-device
             * binding is done later, directly against the returned engine's CreateSessionAsync, so a host can load
             * one engine per model and reuse it across many IRecognitionSession runs. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/RecognitionPanelSubsystem/IRecognizerSessionFactory.cs");
            designFiles = ("docs/design/speech-demo/recognition-panel-subsystem/recognizer-session-factory.md");
            verificationFiles = ("docs/verification/speech-demo/recognition-panel-subsystem/recognizer-session-factory.md");
            requirementFiles = ("docs/reqstream/speech-demo/recognition-panel-subsystem/recognizer-session-factory.yaml");
        }

        // Turns a public speech model contract into a recognizer engine so the panel can use a fake in
        // tests.
        dependency IRecognizerSessionFactory to Speech_ISpeechRecognizerEngine::ISpeechRecognizerEngine;
        dependency IRecognizerSessionFactory to Speech_SpeechModelContract::SpeechModelContract;
    }
}
