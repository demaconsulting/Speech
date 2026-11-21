package SpeechPhonemesEn_EspeakIpaRenderer {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_EnglishPhonemizer::*;
    private import SpeechPhonemesEn_Phone::*;
    private import SpeechPhonemesEn_PhonemeStream::*;
    private import SpeechPhonemesEn_WeakForms::*;

    enum def EspeakIpaProfile {
        enum EnUs;
    }

    part def EspeakIpaRenderer {
        doc /* The public renderer of a phonemized text as the espeak-style IPA phoneme string that the Piper voice
             * reads: the ARPAbet to IPA table, the sound rules of the voice (stress marks, flap, linking r,
             * unstressed-vowel environments), pauses for the marks the voice has no symbol for, weak forms by
             * default, host IPA overrides passed through, and RenderParts for a bounded model input. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/EspeakIpaRenderer.cs", "src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/EspeakWordRules.cs");
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/RenderingSubsystemTests.cs");
            designFiles = ("docs/design/speech-phonemes-en/rendering-subsystem/espeak-ipa-renderer.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/rendering-subsystem/espeak-ipa-renderer.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/rendering-subsystem/espeak-ipa-renderer.yaml");
        }

        // Renders phonemizer output as eSpeak IPA, using phoneme stream layout and weak forms.
        dependency EspeakIpaRenderer to EnglishPhonemizer;
        dependency EspeakIpaRenderer to Phone;
        dependency EspeakIpaRenderer to PhonemeStream;
        dependency EspeakIpaRenderer to WeakForms;
    }
}
