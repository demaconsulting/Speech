package SpeechPhonemesEnTools_LtsAligner {
    private import SoftwareItemMetadata::*;

    part def LtsAligner {
        doc /* Aligns the letters of words with their phones so that every letter owns a chunk of zero, one or two
             * phones. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Phonemes.En.Tools/LetterToSoundSubsystem/LtsAligner.cs");
        }

        // Produces letter-to-phone chunk alignments in the chunk inventory LtsModel defines.
        dependency LtsAligner to SpeechPhonemesEn_LtsModel::LtsModel;
    }
}
