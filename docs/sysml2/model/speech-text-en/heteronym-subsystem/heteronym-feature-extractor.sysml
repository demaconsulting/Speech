package SpeechTextEn_HeteronymFeatureExtractor {
    private import SoftwareItemMetadata::*;

    part def HeteronymFeatureExtractor {
        doc /* Internal, deterministic feature extraction for one heteronym occurrence: builds the sentence context
             * (word class, shape and part of speech code of each token) from the tagged tokens, derives the fixed
             * feature templates of the target word and its neighbors, and hashes each feature with stable integer
             * hash functions (FeatureHashing) so the same sentence yields the same features on every platform. The
             * feature set carries a version number that a model records; extraction allocates nothing on the heap per
             * occurrence and does no culture-aware comparison. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/HeteronymFeatureExtractor.cs", "src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/HeteronymContext.cs", "src/DemaConsulting.Speech.Text.En/HeteronymSubsystem/FeatureHashing.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/HeteronymSubsystem/HeteronymFeatureExtractorTests.cs");
            designFiles = ("docs/design/speech-text-en/heteronym-subsystem/heteronym-feature-extractor.md");
            verificationFiles = ("docs/verification/speech-text-en/heteronym-subsystem/heteronym-feature-extractor.md");
            requirementFiles = ("docs/reqstream/speech-text-en/heteronym-subsystem/heteronym-feature-extractor.yaml");
        }
    }
}
