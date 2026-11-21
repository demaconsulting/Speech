package SpeechPhonemesEn_LetterToSoundSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_LetterToSound::*;
    private import SpeechPhonemesEn_LtsModel::*;

    part def LetterToSoundSubsystem {
        doc /* The pronunciation of a word that no dictionary knows: the trained letter-to-sound model (LtsModel: an
             * exact integer network over the letters of the word, trained on the CMU Pronouncing Dictionary alone)
             * and the converter around it (LetterToSound), which caches answers and speaks a word the model gives no
             * vowel by its letter names so that a word with letters is never silent. It uses only the phone types and
             * the letter names of the Lexicon subsystem. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/LetterToSoundSubsystemTests.cs");
            designFiles = ("docs/design/speech-phonemes-en/letter-to-sound-subsystem.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/letter-to-sound-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/letter-to-sound-subsystem.yaml");
        }

        part ltsModel : LtsModel;
        part letterToSound : LetterToSound;
    }
}
