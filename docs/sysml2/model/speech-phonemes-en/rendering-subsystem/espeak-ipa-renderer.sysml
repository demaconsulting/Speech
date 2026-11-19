package SpeechPhonemesEn {
    part def EspeakIpaRenderer {
        doc /* The public renderer of a phonemized text as the espeak-style IPA phoneme string that the
             * Piper voice reads: the ARPAbet to IPA table, the sound rules of the voice (stress marks,
             * flap, linking r, unstressed-vowel environments), pauses for the marks the voice has no
             * symbol for, weak forms by default, host IPA overrides passed through, and
             * RenderParts for a bounded model input. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/EspeakIpaRenderer.cs, src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/EspeakWordRules.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/RenderingSubsystemTests.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/rendering-subsystem/espeak-ipa-renderer.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/rendering-subsystem/espeak-ipa-renderer.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/rendering-subsystem/espeak-ipa-renderer.yaml */
    }
}
