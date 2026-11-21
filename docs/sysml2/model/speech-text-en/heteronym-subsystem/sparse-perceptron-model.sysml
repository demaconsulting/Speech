package SpeechTextEn_SparsePerceptronModel {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_HeteronymFeatureExtractor::*;

    part def SparsePerceptronModel {
        doc /* The immutable, thread-safe sparse averaged-perceptron model of the heteronym classifier and its strict
             * loader: reads the compressed binary model format (header, checksum, bounded sizes, non-zero integer
             * weights of hashed buckets), rejects any corrupt, truncated or oversized input with a fixed-reason
             * exception that never carries file content, and exposes the feature-set version and inventory revision
             * the model was trained for. The internal writer and the binary format primitives produce and describe
             * the same format so tests can build models and the trainer can write the model that the package embeds. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/SparsePerceptronModel.cs", "src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/SparsePerceptronModelWriter.cs", "src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/ModelBinaryFormat.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/SparsePerceptronModelTests.cs");
            designFiles = ("docs/design/speech-text-en/heteronym-subsystem/sparse-perceptron-model.md");
            verificationFiles = ("docs/verification/speech-text-en/heteronym-subsystem/sparse-perceptron-model.md");
            requirementFiles = ("docs/reqstream/speech-text-en/heteronym-subsystem/sparse-perceptron-model.yaml");
        }

        // Relies on the feature extractor's feature hashing so model weights match the runtime features.
        dependency SparsePerceptronModel to HeteronymFeatureExtractor;
    }
}
