package ModelBundler {
    private import SoftwareItemMetadata::*;
    private import ModelBundler_BundleBuilder::*;
    private import ModelBundler_BundleCatalog::*;
    private import ModelBundler_BundleDefinitions::*;
    private import ModelBundler_BundleMetadataWriter::*;
    private import ModelBundler_CommandLineOptions::*;
    private import ModelBundler_Program::*;
    private import ModelBundler_ReleaseNotesWriter::*;
    private import ModelBundler_ReleaseUploader::*;
    private import ModelBundler_StoredZipWriter::*;
    private import ModelBundler_VendorFileSource::*;

    doc
    /*
     * ModelBundler is the non-shipped, maintainer-only console tool that repackages the unmodified vendor files of
     * the ONNX model packages into deterministic, SHA-256-pinned bundle zips for the project's append-only models
     * GitHub release, proves a build reproduces byte for byte, and uploads a build without replacing any asset.
     */

    part def ModelBundlerSystem {
        doc /* The non-shipped, maintainer-only ModelBundler console tool as a whole (no design, verification or
             * requirements documents of its own). */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.ModelBundler.Tests/BundleBuilderTests.cs");
        }

        part program : Program;
        part commandLineOptions : CommandLineOptions;
        part bundleDefinitions : BundleDefinitions;
        part bundleCatalog : BundleCatalog;
        part vendorFileSource : VendorFileSource;
        part storedZipWriter : StoredZipWriter;
        part bundleMetadataWriter : BundleMetadataWriter;
        part releaseNotesWriter : ReleaseNotesWriter;
        part bundleBuilder : BundleBuilder;
        part releaseUploader : ReleaseUploader;
    }
}