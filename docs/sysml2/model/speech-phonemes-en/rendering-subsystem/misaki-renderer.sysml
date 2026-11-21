package SpeechPhonemesEn_MisakiRenderer {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEn_EnglishPhonemizer::*;
    private import SpeechPhonemesEn_Phone::*;
    private import SpeechPhonemesEn_PhonemeStream::*;
    private import SpeechPhonemesEn_WeakForms::*;

    enum def MisakiProfile {
        enum Us;
    }

    part def MisakiRenderer {
        doc /* The public renderer of a phonemized text as the misaki symbol string that Kokoro reads: the ARPAbet to
             * misaki table, the sound rules of that phonemizer (flap, syllabic consonants, schwa environments, stress
             * only on the first part of a compound), the model's own quotation marks and ellipsis, full forms by
             * default and weak forms on request, and RenderParts for Kokoro's token limit. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/MisakiRenderer.cs", "src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/MisakiWordRules.cs");
            testFiles = ("test/DemaConsulting.Speech.Phonemes.En.Tests/RenderingSubsystemTests.cs");
            designFiles = ("docs/design/speech-phonemes-en/rendering-subsystem/misaki-renderer.md");
            verificationFiles = ("docs/verification/speech-phonemes-en/rendering-subsystem/misaki-renderer.md");
            requirementFiles = ("docs/reqstream/speech-phonemes-en/rendering-subsystem/misaki-renderer.yaml");
        }

        // Renders phonemizer output as Misaki symbols, using phoneme stream layout and weak forms.
        dependency MisakiRenderer to EnglishPhonemizer;
        dependency MisakiRenderer to Phone;
        dependency MisakiRenderer to PhonemeStream;
        dependency MisakiRenderer to WeakForms;
    }
}
