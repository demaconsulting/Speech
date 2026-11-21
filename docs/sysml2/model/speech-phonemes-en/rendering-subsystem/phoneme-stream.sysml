package SpeechPhonemesEn_PhonemeStream {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_EnglishPhonemizer::*;
    private import SpeechPhonemesEn_Phone::*;
    private import SpeechPhonemesEn_WeakForms::*;

    part def PhonemeStream {
        doc /* Internal layout and splitting shared by both renderers: joins rendered pieces with one space between
             * words, closing marks attached to the word before and opening marks to the word after; splits a long
             * item sequence between items into parts within a symbol budget at the best phrase boundary without loss
             * (after a clause mark, else before a conjunction or relative word, else after a word that is not a
             * function word, never between a function word and the word after it); and holds the word-shape helpers
             * (WordShape) that the sound rules of both renderers use. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/PhonemeStream.cs", "src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/WordShape.cs", "src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/IpaSymbols.cs");
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/RenderingSubsystemTests.cs");
            designFiles = ("docs/design/speech-phonemes-en/rendering-subsystem/phoneme-stream.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/rendering-subsystem/phoneme-stream.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/rendering-subsystem/phoneme-stream.yaml");
        }

        // Lays out and splits rendered phonemizer output, applying weak forms between words.
        dependency PhonemeStream to EnglishPhonemizer;
        dependency PhonemeStream to Phone;
        dependency PhonemeStream to WeakForms;
    }
}
