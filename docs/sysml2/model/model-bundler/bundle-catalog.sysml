package ModelBundler_BundleCatalog {
    private import SoftwareItemMetadata::*;

    part def BundleCatalog {
        doc /* The compiled facts the tool takes from the model packages: each bundle's vendor files with their
             * original URLs and SHA-256 pins (the packages' internal BundleContents, reached through
             * InternalsVisibleTo, so no pin is declared twice) and the zip pins the packages publish. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/ModelBundler/BundleCatalog.cs");
            testFiles = ("test/DemaConsulting.Speech.ModelBundler.Tests/BundleDefinitionsTests.cs");
        }

        // Reads the bundle contents of the four ONNX model packages.
        dependency BundleCatalog to Speech_SpeechModelDownloadDescriptor::SpeechModelDownloadDescriptor;
        dependency BundleCatalog to SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboEnglishSynthesisModel::OnnxChatterboxTurboEnglishSynthesisModel;
        dependency BundleCatalog to SpeechOnnxKokoro_OnnxKokoroEnglishSynthesisModel::OnnxKokoroEnglishSynthesisModel;
        dependency BundleCatalog to SpeechOnnxNemotronStt_OnnxNemotronMultilingualRecognitionModel::OnnxNemotronMultilingualRecognitionModel;
        dependency BundleCatalog to SpeechOnnxPiper_OnnxPiperLibriTtsEnSynthesisModel::OnnxPiperLibriTtsEnSynthesisModel;
    }
}