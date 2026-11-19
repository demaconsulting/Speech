package SpeechPhonemesEn {
    part def RenderingSubsystem {
        doc /* Renders a phonemized text as the phoneme string of a voice model: espeak-style IPA for
             * the Piper voice (EspeakIpaRenderer) and misaki symbols for Kokoro (MisakiRenderer),
             * each using only symbols of its model's own symbol table, with the table of
             * function-word weak forms (WeakForms) and the layout, word-shape helpers and
             * long-text splitting that both renderers share (PhonemeStream). It uses only the
             * result types of the Phonemizer subsystem and the phone types of the Lexicon
             * subsystem. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/NamespaceDoc.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/rendering-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/rendering-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/rendering-subsystem.yaml */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/RenderingSubsystemTests.cs */

        part espeakIpaRenderer : EspeakIpaRenderer;
        part misakiRenderer : MisakiRenderer;
        part weakForms : WeakForms;
        part phonemeStream : PhonemeStream;
    }
}
