package SpeechPhonemesEn {
    part def LetterToSound {
        doc /* Internal converter that pronounces an unknown word with the trained model, keeps a
             * bounded thread-safe cache of the answers, speaks a word the model gives no vowel (an
             * initialism such as www) by the names of its letters so that a word with letters is never
             * silent, and reports no pronunciation for text without a letter. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/LetterToSoundSubsystem/LetterToSound.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/LetterToSoundSubsystemTests.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/letter-to-sound-subsystem/letter-to-sound.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/letter-to-sound-subsystem/letter-to-sound.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/letter-to-sound-subsystem/letter-to-sound.yaml */
    }
}
