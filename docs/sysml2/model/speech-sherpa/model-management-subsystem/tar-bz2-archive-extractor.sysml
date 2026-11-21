package SpeechSherpa_TarBz2ArchiveExtractor {
    private import SoftwareItemMetadata::*;

    part def TarBz2ArchiveExtractor {
        doc /* Shared internal helper that extracts a downloaded .tar.bz2 archive into a directory and deletes the
             * original archive file, using the SharpCompress OTS package (the BCL has no BZip2 decoder). Used by both
             * SherpaOnnxZipformerEnRecognitionModel and SherpaOnnxNemotronStreamingEnRecognitionModel's InstallAsync
             * overrides. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Sherpa/ModelManagementSubsystem/TarBz2ArchiveExtractor.cs");
            testFiles = ("test/DemaConsulting.Speech.Sherpa.Tests/ModelManagementSubsystem/TarBz2ArchiveExtractorTests.cs");
            designFiles = ("docs/design/speech-sherpa/model-management-subsystem/tar-bz2-archive-extractor.md");
            verificationFiles = ("docs/verification/speech-sherpa/model-management-subsystem/tar-bz2-archive-extractor.md");
            requirementFiles = ("docs/reqstream/speech-sherpa/model-management-subsystem/tar-bz2-archive-extractor.yaml");
        }

        // Uses SharpCompress to extract downloaded .tar.bz2 model archives.
        dependency TarBz2ArchiveExtractor to OtsDependencies::SharpCompress;
    }
}
