package Speech_SpeechSynthesizerFactory {
    private import SoftwareItemMetadata::*;
    private import Speech_ISpeechDiagnostics::*;
    private import Speech_ISpeechSynthesizerEngine::*;
    private import Speech_NullSpeechDiagnostics::*;
    private import Speech_SpeechDiagnosticLevel::*;
    private import Speech_SpeechModelCatalog::*;
    private import Speech_SpeechModelContract::*;
    private import Speech_SpeechModelDescriptorEnums::*;
    private import Speech_SpeechModelParameters::*;
    private import Speech_SpeechModelStore::*;
    private import Speech_SpeechSynthesizerEngine::*;
    private import Speech_SynthesisBackend::*;
    private import Speech_UnavailableSpeechSynthesizerEngine::*;

    part def SpeechSynthesizerFactory {
        doc /* Composition entry point that asynchronously loads an installed synthesis model into a working
             * ISpeechSynthesizerEngine, or returns an honest UnavailableSpeechSynthesizerEngine fallback, without
             * ever throwing for an ordinary machine state. No longer takes a playback device parameter - a device is
             * bound later, per session, via ISpeechSynthesizerEngine.CreateSessionAsync, so the model-load step and
             * the device-bind step can fail and be retried independently. Forwards an optional, engine-level
             * parameterValues bag (for example a selected voice) unchanged to the constructed engine. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech/SynthesisSubsystem/SpeechSynthesizerFactory.cs");
            testFiles = ("test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SpeechSynthesizerFactoryTests.cs");
            designFiles = ("docs/design/speech/synthesis-subsystem/speech-synthesizer-factory.md");
            verificationFiles = ("docs/verification/speech/synthesis-subsystem/speech-synthesizer-factory.md");
            requirementFiles = ("docs/reqstream/speech/synthesis-subsystem/speech-synthesizer-factory.yaml");
        }

        // Finds the model in the catalog and store, loads its backend, and falls back to an unavailable
        // engine.
        dependency SpeechSynthesizerFactory to ISpeechDiagnostics;
        dependency SpeechSynthesizerFactory to ISpeechSynthesizerEngine;
        dependency SpeechSynthesizerFactory to NullSpeechDiagnostics;
        dependency SpeechSynthesizerFactory to SpeechDiagnosticLevel;
        dependency SpeechSynthesizerFactory to SpeechModelCatalog;
        dependency SpeechSynthesizerFactory to SpeechModelContract;
        dependency SpeechSynthesizerFactory to SpeechModelDescriptorEnums;
        dependency SpeechSynthesizerFactory to SpeechModelParameters;
        dependency SpeechSynthesizerFactory to SpeechModelStore;
        dependency SpeechSynthesizerFactory to SpeechSynthesizerEngine;
        dependency SpeechSynthesizerFactory to SynthesisBackend;
        dependency SpeechSynthesizerFactory to UnavailableSpeechSynthesizerEngine;
    }
}
