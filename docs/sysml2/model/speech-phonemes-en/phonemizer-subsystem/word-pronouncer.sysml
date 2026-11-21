package SpeechPhonemesEn_WordPronouncer {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_CmuLexicon::*;
    private import SpeechPhonemesEn_LetterToSound::*;
    private import SpeechPhonemesEn_Morphology::*;
    private import SpeechPhonemesEn_Phone::*;
    private import SpeechPhonemesEn_ReadingPronunciations::*;
    private import SpeechPhonemesEn_WordTokenizer::*;

    part def WordPronouncer {
        doc /* Internal pronunciation of one word by the priority list: the reading the text analysis chose, the
             * reviewed correction of a wrong dictionary entry, the dictionary's first variant, the morphology
             * derivation from a known stem, a productive prefix plus a known word, and the letter-to-sound model,
             * reporting the source of the answer; folds diacritics, pronounces hyphenated compounds part by part with
             * the stress of one word (the closed dictionary spelling's pattern when it has the same sounds; otherwise
             * one primary stress on the first element, or on the root after a productive prefix such as re-, non- or
             * ex-) and records where each element starts, and speaks letters and digits. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Phonemes.En/PhonemizerSubsystem/WordPronouncer.cs");
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/PhonemizerSubsystemTests.cs");
            designFiles = ("docs/design/speech-phonemes-en/phonemizer-subsystem/word-pronouncer.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/phonemizer-subsystem/word-pronouncer.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/phonemizer-subsystem/word-pronouncer.yaml");
        }

        // Pronounces a word from its chosen reading, the lexicon, morphology, or letter-to-sound, in
        // priority order.
        dependency WordPronouncer to CmuLexicon;
        dependency WordPronouncer to SpeechPhonemesEn_EnglishPhonemizer::EnglishPhonemizer;
        dependency WordPronouncer to LetterToSound;
        dependency WordPronouncer to Morphology;
        dependency WordPronouncer to Phone;
        dependency WordPronouncer to ReadingPronunciations;
        dependency WordPronouncer to WordTokenizer;
    }
}
