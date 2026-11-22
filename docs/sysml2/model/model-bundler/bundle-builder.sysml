package ModelBundler_BundleBuilder {
    private import SoftwareItemMetadata::*;
    private import ModelBundler_BundleDefinitions::*;
    private import ModelBundler_BundleMetadataWriter::*;
    private import ModelBundler_ReleaseNotesWriter::*;
    private import ModelBundler_StoredZipWriter::*;
    private import ModelBundler_VendorFileSource::*;

    part def BundleBuilder {
        doc /* Builds a bundle zip: checks the compiled contents against the runtime's own bundle rules, resolves
             * and verifies every vendor file, creates the metadata, refuses a zip of 2 GiB or more, and writes the
             * entries in their fixed order, re-checking each vendor file's SHA-256 as it is written. Builds a set
             * with SHA256SUMS, release-notes.md and the MATCH or UPDATE CATALOG summary, and verifies a build by
             * rebuilding it and comparing byte for byte. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/ModelBundler/BundleBuilder.cs");
            testFiles = ("test/DemaConsulting.Speech.ModelBundler.Tests/BundleBuilderTests.cs");
        }

        // Validates the contents through the runtime bundle type, so every zip it writes installs.
        dependency BundleBuilder to BundleDefinitions;
        dependency BundleBuilder to BundleMetadataWriter;
        dependency BundleBuilder to ReleaseNotesWriter;
        dependency BundleBuilder to StoredZipWriter;
        dependency BundleBuilder to VendorFileSource;
        dependency BundleBuilder to Speech_SpeechModelBundle::SpeechModelBundle;
        dependency BundleBuilder to Speech_SpeechModelDownloadDescriptor::SpeechModelDownloadDescriptor;
    }
}