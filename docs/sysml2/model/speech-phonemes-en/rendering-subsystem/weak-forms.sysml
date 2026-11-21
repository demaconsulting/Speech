package SpeechPhonemesEn_WeakForms {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_CmuLexicon::*;
    private import SpeechPhonemesEn_EnglishPhonemizer::*;
    private import SpeechPhonemesEn_Phone::*;
    private import SpeechPhonemesEn_ReadingPronunciations::*;

    part def WeakForms {
        doc /* Internal table of the small English function words that are unstressed in running speech: their reduced
             * forms before a consonant and before a vowel, the words that only lose their primary stress, the details
             * that only the espeak-style voice has, and the grouping of a function word with the article after it. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/WeakForms.cs");
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/RenderingSubsystemTests.cs");
            designFiles = ("docs/design/speech-phonemes-en/rendering-subsystem/weak-forms.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/rendering-subsystem/weak-forms.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/rendering-subsystem/weak-forms.yaml");
        }

        // Derives reduced forms of function words from lexicon and reading pronunciations.
        dependency WeakForms to CmuLexicon;
        dependency WeakForms to EnglishPhonemizer;
        dependency WeakForms to Phone;
        dependency WeakForms to ReadingPronunciations;
    }
}
