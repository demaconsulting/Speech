package SpeechPhonemesEnTools_LtsTrainer {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_FloatNetwork::*;
    private import SpeechPhonemesEnTools_LtsAligner::*;

    part def LtsTrainer {
        doc /* Trains the three networks of the letter-to-sound model with fixed settings and converts them to the
             * integer-only image the library's LtsModel reads. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/LetterToSoundSubsystem/LtsTrainer.cs");
        }

        // Aligns lexicon words, trains float networks, and quantizes them into the integer LtsModel image.
        dependency LtsTrainer to FloatNetwork;
        dependency LtsTrainer to LtsAligner;
        dependency LtsTrainer to SpeechPhonemesEn_CmuLexicon::CmuLexicon;
        dependency LtsTrainer to SpeechPhonemesEn_LtsModel::LtsModel;
        dependency LtsTrainer to SpeechPhonemesEn_Phone::Phone;
    }
}
