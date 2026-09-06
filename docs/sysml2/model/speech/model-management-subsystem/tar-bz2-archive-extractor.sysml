package Speech {
    part def TarBz2ArchiveExtractor {
        doc /* Shared internal helper that extracts a downloaded .tar.bz2 archive into a
             * directory and deletes the original archive file, using the SharpCompress OTS
             * package (the BCL has no BZip2 decoder). Used by both
             * SherpaOnnxZipformerEnRecognitionModel and
             * SherpaOnnxNemotronStreamingEnRecognitionModel's InstallAsync overrides. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/TarBz2ArchiveExtractor.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/TarBz2ArchiveExtractorTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/tar-bz2-archive-extractor.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/tar-bz2-archive-extractor.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/tar-bz2-archive-extractor.yaml */
    }
}
