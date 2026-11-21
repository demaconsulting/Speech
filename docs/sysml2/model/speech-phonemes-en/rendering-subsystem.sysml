package SpeechPhonemesEn_RenderingSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_EspeakIpaRenderer::*;
    private import SpeechPhonemesEn_MisakiRenderer::*;
    private import SpeechPhonemesEn_PhonemeStream::*;
    private import SpeechPhonemesEn_WeakForms::*;

    part def RenderingSubsystem {
        doc /* Renders a phonemized text as the phoneme string of a voice model: espeak-style IPA for the Piper voice
             * (EspeakIpaRenderer) and misaki symbols for Kokoro (MisakiRenderer), each using only symbols of its
             * model's own symbol table, with the table of function-word weak forms (WeakForms) and the layout,
             * word-shape helpers and long-text splitting that both renderers share (PhonemeStream). It uses only the
             * result types of the Phonemizer subsystem and the phone types of the Lexicon subsystem. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/RenderingSubsystemTests.cs");
            designFiles = ("docs/design/speech-phonemes-en/rendering-subsystem.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/rendering-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/rendering-subsystem.yaml");
        }

        part espeakIpaRenderer : EspeakIpaRenderer;
        part misakiRenderer : MisakiRenderer;
        part weakForms : WeakForms;
        part phonemeStream : PhonemeStream;
    }
}
