package SpeechPhonemesEnTools_MorphologyAudit {
    private import SoftwareItemMetadata::*;

    part def MorphologyAudit {
        doc /* Computes the agreement of the morphology rules with the lexicon for each ending, with the disagreement
             * classes. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/MorphologySubsystem/MorphologyAudit.cs");
        }

        // Applies Morphology rules to lexicon stems and compares the resulting phones with CmuLexicon
        // entries.
        dependency MorphologyAudit to SpeechPhonemesEn_CmuLexicon::CmuLexicon;
        dependency MorphologyAudit to SpeechPhonemesEn_Morphology::Morphology;
        dependency MorphologyAudit to SpeechPhonemesEn_Phone::Phone;
    }
}
