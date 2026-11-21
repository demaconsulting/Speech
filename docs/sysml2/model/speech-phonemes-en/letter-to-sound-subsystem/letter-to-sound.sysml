package SpeechPhonemesEn_LetterToSound {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_LtsModel::*;
    private import SpeechPhonemesEn_Phone::*;

    part def LetterToSound {
        doc /* Internal converter that pronounces an unknown word with the trained model, keeps a bounded thread-safe
             * cache of the answers, speaks a word the model gives no vowel (an initialism such as www) by the names
             * of its letters so that a word with letters is never silent, and reports no pronunciation for text
             * without a letter. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Phonemes.En/LetterToSoundSubsystem/LetterToSound.cs");
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/LetterToSoundSubsystemTests.cs");
            designFiles = ("docs/design/speech-phonemes-en/letter-to-sound-subsystem/letter-to-sound.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/letter-to-sound-subsystem/letter-to-sound.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/letter-to-sound-subsystem/letter-to-sound.yaml");
        }

        // Runs the trained LTS model to predict phones for words not found in the lexicon.
        dependency LetterToSound to LtsModel;
        dependency LetterToSound to Phone;
    }
}
