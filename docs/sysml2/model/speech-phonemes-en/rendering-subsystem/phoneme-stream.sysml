package SpeechPhonemesEn {
    part def PhonemeStream {
        doc /* Internal layout and splitting shared by both renderers: joins rendered pieces with one
             * space between words, closing marks attached to the word before and opening marks to the
             * word after; splits a long item sequence between items into parts within a symbol budget at
             * the best phrase boundary without loss (after a clause mark, else before a conjunction or
             * relative word, else after a word that is not a function word, never between a function
             * word and the word after it); and holds the word-shape helpers (WordShape) that the sound
             * rules of both renderers use. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/PhonemeStream.cs, src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/WordShape.cs, src/DemaConsulting.Speech.Phonemes.En/RenderingSubsystem/IpaSymbols.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/RenderingSubsystemTests.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/rendering-subsystem/phoneme-stream.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/rendering-subsystem/phoneme-stream.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/rendering-subsystem/phoneme-stream.yaml */
    }
}
