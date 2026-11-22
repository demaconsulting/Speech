package ModelBundler_ReleaseNotesWriter {
    private import SoftwareItemMetadata::*;
    private import ModelBundler_BundleMetadataWriter::*;

    part def ReleaseNotesWriter {
        doc /* Writes and parses SHA256SUMS (the digests verify and upload work from) and writes release-notes.md, the
             * models release body: per bundle its file name, model ids, original source and revision, licenses,
             * attribution, SHA-256 and size, ending with the issues link. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/ModelBundler/ReleaseNotesWriter.cs");
            testFiles = ("test/DemaConsulting.Speech.ModelBundler.Tests/BundleBuilderTests.cs");
        }

        // Shares the issues link with the bundle metadata.
        dependency ReleaseNotesWriter to BundleMetadataWriter;
    }
}