package SpeechPhonemesEn {
    part def LetterToSoundSubsystem {
        doc /* The pronunciation of a word that no dictionary knows: the trained letter-to-sound
             * model (LtsModel: an exact integer network over the letters of the word, trained on
             * the CMU Pronouncing Dictionary alone) and the converter around it (LetterToSound),
             * which caches answers and speaks a word the model gives no vowel by its letter names
             * so that a word with letters is never silent. It uses only the phone types and the
             * letter names of the Lexicon subsystem. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/LetterToSoundSubsystem/NamespaceDoc.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/letter-to-sound-subsystem.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/letter-to-sound-subsystem.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/letter-to-sound-subsystem.yaml */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/LetterToSoundSubsystemTests.cs */

        part ltsModel : LtsModel;
        part letterToSound : LetterToSound;
    }
}
