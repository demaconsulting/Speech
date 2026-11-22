package ModelBundler_BundleDefinitions {
    private import SoftwareItemMetadata::*;

    part def BundleDefinitions {
        doc /* Loads and validates bundles.json: per bundle the name parts (model, upstream release, quantization,
             * revision), the fixed bundle date, the catalog model ids (each one safe directory name), the upstream
             * repository and pinned snapshot, the licenses with their committed texts (normalized to LF; each text
             * path must be relative, free of '..' segments and resolve inside the definitions directory), the
             * attribution and the NOTICE text. It never declares a vendor file or a SHA-256 pin. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/ModelBundler/BundleDefinitions.cs", "tools/ModelBundler/bundles.json");
            testFiles = ("test/DemaConsulting.Speech.ModelBundler.Tests/BundleDefinitionsTests.cs");
        }
    }
}