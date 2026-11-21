package SpeechDemo_ModelCatalogComposition {
    private import SoftwareItemMetadata::*;

    part def ModelCatalogComposition {
        doc /* Supporting record pairing one composed SpeechModelCatalog with the single ReferenceVoiceLibrary created
             * alongside it, so a catalog rebuild always replaces both. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Demo/ModelCatalogSubsystem/ModelCatalogComposition.cs");
            testFiles = ("test/DemaConsulting.Speech.Demo.Tests/ModelCatalogSubsystem/ModelCatalogServiceTests.cs");
            designFiles = ("docs/design/speech-demo/model-catalog-subsystem.md");
            verificationFiles = ("docs/verification/speech-demo/model-catalog-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-demo/model-catalog-subsystem.yaml");
        }

        // Pairs a SpeechModelCatalog with its ReferenceVoiceLibrary so that a rebuild replaces both
        // together.
        dependency ModelCatalogComposition to Speech_ReferenceVoiceLibrary::ReferenceVoiceLibrary;
        dependency ModelCatalogComposition to Speech_SpeechModelCatalog::SpeechModelCatalog;
    }
}
