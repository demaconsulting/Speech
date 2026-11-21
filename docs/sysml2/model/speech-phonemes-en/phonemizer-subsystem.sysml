package SpeechPhonemesEn_PhonemizerSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_EnglishPhonemizer::*;
    private import SpeechPhonemesEn_WordPronouncer::*;
    private import SpeechPhonemesEn_WordTokenizer::*;

    part def PhonemizerSubsystem {
        doc /* Turns the text of one speech segment, with or without the annotations of the English text analysis,
             * into a stream of word, punctuation and unsupported items with ARPAbet pronunciations and their
             * provenance: the public EnglishPhonemizer and its result types, the spoken text, annotation index and
             * tokenizer (WordTokenizer) and the pronouncer that applies the priority list override, reading,
             * dictionary, morphology, letter-to-sound model to one word (WordPronouncer). It uses the Lexicon and
             * LetterToSound subsystems and knows nothing of voices. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/PhonemizerSubsystemTests.cs");
            designFiles = ("docs/design/speech-phonemes-en/phonemizer-subsystem.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/phonemizer-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/phonemizer-subsystem.yaml");
        }

        part englishPhonemizer : EnglishPhonemizer;
        part wordTokenizer : WordTokenizer;
        part wordPronouncer : WordPronouncer;
    }
}
