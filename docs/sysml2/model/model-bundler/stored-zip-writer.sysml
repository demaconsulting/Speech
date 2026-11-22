package ModelBundler_StoredZipWriter {
    private import SoftwareItemMetadata::*;

    part def StoredZipWriter {
        doc /* The tool's own deterministic zip writer: stored entries with a CRC-32 each, one fixed DOS timestamp
             * (the bundle date at midnight), fixed version fields, zero attributes, and no extra fields, comments,
             * data descriptors or Zip64. It refuses names that are not portable ASCII, sizes or offsets of 4 GiB or
             * more, and archives that would reach the caller's size limit, and returns each entry's SHA-256. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/ModelBundler/StoredZipWriter.cs", "tools/ModelBundler/Crc32.cs");
            testFiles = ("test/DemaConsulting.Speech.ModelBundler.Tests/StoredZipWriterTests.cs");
        }
    }
}