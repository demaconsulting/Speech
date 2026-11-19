package SpeechPhonemesEn {
    part def MisakiRenderer {
        doc /* The public renderer of a phonemized text as the misaki symbol string that Kokoro reads:
             * the ARPAbet to misaki table, the sound rules of that phonemizer (flap, syllabic
             * consonants, schwa environments, stress only on the first part of a compound), the
             * model's own quotation marks and ellipsis, full forms by default and weak forms on
             * request, and RenderParts for Kokoro's token limit. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/MisakiRenderer.cs, src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/MisakiWordRules.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/RenderingSubsystemTests.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/rendering-subsystem/misaki-renderer.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/rendering-subsystem/misaki-renderer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/rendering-subsystem/misaki-renderer.yaml */
    }
}
