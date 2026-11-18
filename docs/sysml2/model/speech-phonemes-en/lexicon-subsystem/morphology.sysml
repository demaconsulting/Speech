package SpeechPhonemesEn {
    part def Morphology {
        doc /* Internal sound rules for regular word endings: chooses the sound of a plural, past
             * tense, -ing, comparative, superlative, -ly, possessive or contraction ending by the
             * last phone of the stem, attaches clitics to a host pronunciation, and derives the
             * pronunciation of a word that is a known stem plus a regular ending (SuffixKind and
             * Derivation describe the endings and the result). */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/Morphology.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.Morphology.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/lexicon-subsystem/morphology.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/lexicon-subsystem/morphology.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/lexicon-subsystem/morphology.yaml */
    }
}
