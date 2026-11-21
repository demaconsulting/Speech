package SpeechPhonemesEnTools_LtsCorpus {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_CmuDictSource::*;
    private import SpeechPhonemesEnTools_DataManifest::*;
    private import SpeechPhonemesEnTools_ToolPaths::*;

    part def LtsCorpus {
        doc /* The words the letter-to-sound model is trained and measured on, with the deterministic stem-family
             * split into training, validation and held-out test words. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/LetterToSoundSubsystem/LtsCorpus.cs");
        }

        // Loads pinned CMUdict words through the source reader and manifest to build the stem-family
        // split.
        dependency LtsCorpus to CmuDictSource;
        dependency LtsCorpus to DataManifest;
        dependency LtsCorpus to ToolPaths;
        dependency LtsCorpus to SpeechPhonemesEn_CmuLexicon::CmuLexicon;
    }
}
