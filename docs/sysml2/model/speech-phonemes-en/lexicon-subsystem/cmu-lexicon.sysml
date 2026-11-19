package SpeechPhonemesEn {
    part def CmuLexicon {
        doc /* Internal lexicon of the CMU Pronouncing Dictionary: a compact front-coded image that is
             * embedded in the package, searched by binary search over block heads, and answers for a
             * word (any case, straight or typographic apostrophe) all its pronunciation variants in
             * dictionary order, and none for an absent or invalid word. Includes the documented image
             * format shared with the non-shipped tools project. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/CmuLexicon.cs, src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/LexiconImageFormat.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.cs, test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.Images.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/lexicon-subsystem/cmu-lexicon.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/lexicon-subsystem/cmu-lexicon.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/lexicon-subsystem/cmu-lexicon.yaml */
    }
}
