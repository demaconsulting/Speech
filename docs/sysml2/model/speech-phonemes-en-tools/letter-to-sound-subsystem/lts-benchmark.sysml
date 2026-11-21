package SpeechPhonemesEnTools_LtsBenchmark {
    private import SoftwareItemMetadata::*;

    part def LtsBenchmark {
        doc /* Scores a letter-to-sound candidate on the held-out words (word and phone accuracy metrics). */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/LetterToSoundSubsystem/LtsBenchmark.cs");
        }

        // Compares LtsModel phone predictions with CmuLexicon reference pronunciations to compute
        // accuracy.
        dependency LtsBenchmark to SpeechPhonemesEn_CmuLexicon::CmuLexicon;
        dependency LtsBenchmark to SpeechPhonemesEn_LtsModel::LtsModel;
        dependency LtsBenchmark to SpeechPhonemesEn_Phone::Phone;
    }
}
