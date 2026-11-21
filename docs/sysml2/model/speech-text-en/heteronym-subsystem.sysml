package SpeechTextEn_HeteronymSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_HeteronymClassifier::*;
    private import SpeechTextEn_HeteronymFeatureExtractor::*;
    private import SpeechTextEn_ReadingInventory::*;
    private import SpeechTextEn_SparsePerceptronModel::*;

    part def HeteronymSubsystem {
        doc /* The English heteronym reading support: the reading inventory (which readings a word such as record can
             * have, with the public ReadingInventory), the deterministic feature extraction that turns a tagged
             * sentence into hashed features, the sparse averaged-perceptron model format with its strict loader, and
             * the classifier that picks one of a word's candidate readings with a calibrated confidence. Only
             * ReadingInventory is public; everything else is internal. The inventory tables and the trained model
             * (heteronyms.bin) are embedded in the package, so the classifier is effective for the default analyzer
             * and only where an analyzer has a model source. Production code of this subsystem is independent of the
             * Analysis subsystem, the DemaConsulting.PartOfSpeech package and the diagnostics sink; the Analysis
             * subsystem depends on it, never the reverse. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/HeteronymSubsystemTests.cs");
            designFiles = ("docs/design/speech-text-en/heteronym-subsystem.md");
            verificationFiles = ("docs/verification/speech-text-en/heteronym-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-text-en/heteronym-subsystem.yaml");
        }

        part readingInventory : ReadingInventory;
        part heteronymFeatureExtractor : HeteronymFeatureExtractor;
        part sparsePerceptronModel : SparsePerceptronModel;
        part heteronymClassifier : HeteronymClassifier;
    }
}
