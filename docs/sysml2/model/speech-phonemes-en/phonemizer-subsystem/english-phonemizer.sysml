package SpeechPhonemesEn_EnglishPhonemizer {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_CmuLexicon::*;
    private import SpeechPhonemesEn_LetterToSound::*;
    private import SpeechPhonemesEn_Morphology::*;
    private import SpeechPhonemesEn_Phone::*;
    private import SpeechPhonemesEn_ReadingPronunciations::*;
    private import SpeechPhonemesEn_WordPronouncer::*;
    private import SpeechPhonemesEn_WordTokenizer::*;

    enum def PhonemizedItemKind {
        enum Word;
        enum Punctuation;
        enum Unsupported;
    }

    enum def PronunciationSource {
        enum None;
        enum Override;
        enum Reading;
        enum Lexicon;
        enum Morphology;
        enum Spelled;
        enum Digits;
        enum LetterToSound;
    }

    part def EnglishPhonemizer {
        doc /* The public entry point of the package: phonemizes the text of one segment, with or without the
             * annotations of the English text analysis, by building the spoken text, tokenizing it and pronouncing
             * each token (applying host pronunciation overrides and readings to unchanged original words), and
             * returns a PhonemizedText of PhonemizedItem values (word, punctuation or unsupported kind, ARPAbet,
             * source, reading, out-of-vocabulary flag) with the lists of the unknown words and the unsupported
             * tokens. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/EnglishPhonemizer.cs", "src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/PhonemizedText.cs", "src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/PhonemizedItem.cs");
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/PhonemizerSubsystemTests.cs");
            designFiles = ("docs/design/speech-phonemes-en/phonemizer-subsystem/english-phonemizer.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/phonemizer-subsystem/english-phonemizer.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/phonemizer-subsystem/english-phonemizer.yaml");
        }

        // Tokenizes the analyzed text and runs each word through the word pronouncer (lexicon, LTS,
        // morphology).
        dependency EnglishPhonemizer to CmuLexicon;
        dependency EnglishPhonemizer to LetterToSound;
        dependency EnglishPhonemizer to Morphology;
        dependency EnglishPhonemizer to Phone;
        dependency EnglishPhonemizer to ReadingPronunciations;
        dependency EnglishPhonemizer to WordPronouncer;
        dependency EnglishPhonemizer to WordTokenizer;
        dependency EnglishPhonemizer to SpeechTextEn_PronunciationOverrides::PronunciationOverrides;
        dependency EnglishPhonemizer to Speech_TextAnalysis::TextAnalysis;
    }
}
