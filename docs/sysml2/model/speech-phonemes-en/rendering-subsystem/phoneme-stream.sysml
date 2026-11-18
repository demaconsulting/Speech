package SpeechPhonemesEn {
    part def PhonemeStream {
        doc /* Internal layout and splitting shared by both renderers: joins rendered pieces with one
             * space between words, closing marks attached to the word before and opening marks to the
             * word after; splits a long item sequence between items into parts within a symbol budget at
             * clause marks without loss; and holds the word-shape helpers (WordShape) that the sound
             * rules of both renderers use. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/PhonemeStream.cs, src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/WordShape.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/RenderingSubsystemTests.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/rendering-subsystem/phoneme-stream.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/rendering-subsystem/phoneme-stream.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/rendering-subsystem/phoneme-stream.yaml */
    }
}
