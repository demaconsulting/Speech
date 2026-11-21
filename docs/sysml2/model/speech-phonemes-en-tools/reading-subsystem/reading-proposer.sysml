package SpeechPhonemesEnTools_ReadingProposer {
    private import SoftwareItemMetadata::*;

    part def ReadingProposer {
        doc /* Proposes a pronunciation for each reading of the Text.En inventory from the dictionary variants and the
             * reading's part of speech. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/ReadingSubsystem/ReadingProposer.cs");
        }

        // Chooses each reading's pronunciation from the CmuLexicon variants, expressed as Phone sequences.
        dependency ReadingProposer to SpeechPhonemesEn_CmuLexicon::CmuLexicon;
        dependency ReadingProposer to SpeechPhonemesEn_Phone::Phone;
    }
}
