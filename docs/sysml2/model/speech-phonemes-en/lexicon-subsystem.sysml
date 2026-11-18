package SpeechPhonemesEn {
    part def LexiconSubsystem {
        doc /* The pronunciation data of the package and the laws over it: the embedded CMU
             * Pronouncing Dictionary as a compact image (CmuLexicon), the phone and pronunciation
             * value types and the letter names (Phone), the sound rules for regular inflected,
             * prefixed and contracted forms (Morphology), and the reviewed mapping of every reading
             * id of the text analysis inventory to a pronunciation (ReadingPronunciations). It knows
             * nothing of text, annotations or voices and is used by every other subsystem. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/LexiconSubsystem/NamespaceDoc.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/lexicon-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/lexicon-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/lexicon-subsystem.yaml */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.cs, test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.Morphology.cs, test/DemaConsulting.Speech.Phonemes.En.Tests/LexiconSubsystemTests.Readings.cs */

        part cmuLexicon : CmuLexicon;
        part phone : Phone;
        part morphology : Morphology;
        part readingPronunciations : ReadingPronunciations;
    }
}
