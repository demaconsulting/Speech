package SpeechPhonemesEn {
    part def ReadingPronunciations {
        doc /* Internal frozen, reviewed table that maps every reading id of the English text analysis
             * inventory to the pronunciation of its headword (from a dictionary variant, a named rule
             * or authored), with the few listed spellings that the sound rules cannot give, and
             * resolves a spelling under a reading to the headword, a regular inflection of it or an
             * override, reporting everything else as an explicit gap. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/ReadingPronunciations.cs, data/phonemes-en/reading-pronunciations.tsv, data/phonemes-en/form-overrides.tsv */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.Readings.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/lexicon-subsystem/reading-pronunciations.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/lexicon-subsystem/reading-pronunciations.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/lexicon-subsystem/reading-pronunciations.yaml */
    }
}
