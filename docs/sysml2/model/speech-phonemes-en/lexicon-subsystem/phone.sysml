package SpeechPhonemesEn_Phone {
    private import SoftwareItemMetadata::*;

    part def Phone {
        doc /* Internal value types of the package: the one-byte ARPAbet phone (consonants and each vowel in three
             * stress classes), the immutable pronunciation sequence with value equality and the dictionary's text
             * form, and the table of the names of the letters as pronunciations (so a spelled A or I is a letter,
             * never the article or the pronoun). */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/Phone.cs", "src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/Pronunciation.cs", "src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/LetterNames.cs");
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.cs");
            designFiles = ("docs/design/speech-phonemes-en/lexicon-subsystem/phone.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/lexicon-subsystem/phone.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/lexicon-subsystem/phone.yaml");
        }
    }
}
