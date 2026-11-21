package SpeechPhonemesEnTools_LexiconImageWriter {
    private import SoftwareItemMetadata::*;

    part def LexiconImageWriter {
        doc /* Serializes lexicon entries into the front-coded image format that the library's CmuLexicon reads. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/LexiconSubsystem/LexiconImageWriter.cs");
        }

        // Writes entries in the front-coded image format that CmuLexicon defines and reads.
        dependency LexiconImageWriter to SpeechPhonemesEn_CmuLexicon::CmuLexicon;
    }
}
