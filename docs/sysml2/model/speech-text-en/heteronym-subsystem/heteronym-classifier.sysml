package SpeechTextEn_HeteronymClassifier {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_HeteronymFeatureExtractor::*;
    private import SpeechTextEn_ReadingInventory::*;
    private import SpeechTextEn_SparsePerceptronModel::*;

    enum def ModelLoadStatus {
        enum NotShipped;
        enum Loaded;
        enum Invalid;
        enum Incompatible;
    }

    part def HeteronymClassifier {
        doc /* The internal classifier that chooses which reading of an inventory form a word occurrence has: scores
             * only the form's candidate readings with the sparse perceptron model (so a reading that is not a
             * candidate can never win), breaks exact ties by inventory row order, and reports a
             * temperature-calibrated confidence; a form with one candidate is returned with confidence 1 without
             * touching the model. The decision uses integers only. It also holds the lazy, once-only model provider
             * that opens and loads the model on first use and records a missing, invalid or incompatible model as a
             * cached status instead of throwing. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/HeteronymClassifier.cs", "src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/HeteronymModelProvider.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/HeteronymClassifierTests.cs");
            designFiles = ("docs/design/speech-text-en/heteronym-subsystem/heteronym-classifier.md");
            verificationFiles = ("docs/verification/speech-text-en/heteronym-subsystem/heteronym-classifier.md");
            requirementFiles = ("docs/reqstream/speech-text-en/heteronym-subsystem/heteronym-classifier.yaml");
        }

        // Scores a form's inventory candidates with the perceptron model over the extracted context
        // features.
        dependency HeteronymClassifier to HeteronymFeatureExtractor;
        dependency HeteronymClassifier to ReadingInventory;
        dependency HeteronymClassifier to SparsePerceptronModel;
    }
}
