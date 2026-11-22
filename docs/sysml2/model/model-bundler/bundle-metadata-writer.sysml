package ModelBundler_BundleMetadataWriter {
    private import SoftwareItemMetadata::*;
    private import ModelBundler_BundleDefinitions::*;
    private import ModelBundler_VendorFileSource::*;

    part def BundleMetadataWriter {
        doc /* Creates the metadata entries of a bundle, as UTF-8 without a byte order mark and with LF line endings:
             * README.txt (repackaged by DEMA Consulting for DemaConsulting.Speech, the unmodified vendor files with
             * their original URLs and SHA-256s, the added files, the licenses, the attribution and the issues link),
             * LICENSE (the license texts), NOTICE (the notices of origin and the unmodified statement) and model.json
             * (fixed key order, files sorted by path). */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/ModelBundler/BundleMetadataWriter.cs");
            testFiles = ("test/DemaConsulting.Speech.ModelBundler.Tests/BundleMetadataWriterTests.cs");
        }

        // Writes the definition's texts and the verified files' facts.
        dependency BundleMetadataWriter to BundleDefinitions;
        dependency BundleMetadataWriter to VendorFileSource;
    }
}