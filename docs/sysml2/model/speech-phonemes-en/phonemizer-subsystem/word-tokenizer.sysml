package SpeechPhonemesEn_WordTokenizer {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_CmuLexicon::*;

    enum def TokenKind {
        enum Word;
        enum Letter;
        enum Digits;
        enum Clitic;
        enum Punctuation;
        enum Unsupported;
    }

    part def WordTokenizer {
        doc /* Internal preparation of the text for pronunciation: builds the spoken text by applying the
             * normalization and spell-out replacement annotations while remembering what is original text
             * (SpokenText), indexes the reading and pronunciation annotations by the original span they cover
             * (AnnotationIndex), and cuts the spoken text into word, letter, digit, clitic, punctuation and
             * unsupported tokens by a fixed rule order. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/WordTokenizer.cs", "src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/SpokenText.cs", "src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/AnnotationIndex.cs");
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/PhonemizerSubsystemTests.cs");
            designFiles = ("docs/design/speech-phonemes-en/phonemizer-subsystem/word-tokenizer.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/phonemizer-subsystem/word-tokenizer.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/phonemizer-subsystem/word-tokenizer.yaml");
        }

        // Applies text-analysis normalization annotations and pronunciation overrides to build the spoken
        // words.
        dependency WordTokenizer to CmuLexicon;
        dependency WordTokenizer to SpeechTextEn_PronunciationOverrides::PronunciationOverrides;
        dependency WordTokenizer to SpeechTextEn_TextEnAnnotations::TextEnAnnotations;
        dependency WordTokenizer to Speech_TextAnalysis::TextAnalysis;
    }
}
