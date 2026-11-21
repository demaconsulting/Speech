package SpeechProofreader_ProofreaderCatalog {
    private import SoftwareItemMetadata::*;

    part def ProofreaderCatalog {
        doc /* Owns the model catalog (with every synthesis model package registered) and the reference-voice library
             * paired with it. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Synthesis/ProofreaderCatalog.cs");
        }

        // Registers the synthesis models in SpeechModelCatalog with store options and pairs it with the
        // reference voices.
        dependency ProofreaderCatalog to SpeechOnnxChatterboxTurbo_OnnxChatterboxTurboEnglishSynthesisModel::OnnxChatterboxTurboEnglishSynthesisModel;
        dependency ProofreaderCatalog to SpeechOnnxChatterboxTurbo_SpeechModelCatalogChatterboxTurboExtensions::SpeechModelCatalogChatterboxTurboExtensions;
        dependency ProofreaderCatalog to SpeechOnnxKokoro_SpeechModelCatalogKokoroExtensions::SpeechModelCatalogKokoroExtensions;
        dependency ProofreaderCatalog to SpeechOnnxPiper_SpeechModelCatalogPiperExtensions::SpeechModelCatalogPiperExtensions;
        dependency ProofreaderCatalog to SpeechSherpa_SpeechModelCatalogSherpaExtensions::SpeechModelCatalogSherpaExtensions;
        dependency ProofreaderCatalog to Speech_ReferenceVoiceLibrary::ReferenceVoiceLibrary;
        dependency ProofreaderCatalog to Speech_SpeechModelCatalog::SpeechModelCatalog;
        dependency ProofreaderCatalog to Speech_SpeechModelContract::SpeechModelContract;
        dependency ProofreaderCatalog to Speech_SpeechModelDescriptor::SpeechModelDescriptor;
        dependency ProofreaderCatalog to Speech_SpeechModelDescriptorEnums::SpeechModelDescriptorEnums;
        dependency ProofreaderCatalog to Speech_SpeechModelStoreOptions::SpeechModelStoreOptions;
    }
}
