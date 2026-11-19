package SpeechPhonemesEn {
    part def LtsModel {
        doc /* Internal trained letter-to-sound model: reads its binary image (validated strictly),
             * extracts the features of a letter and its neighbors, and runs exact integer networks that
             * choose the phone chunk of each letter, the vowel that carries the primary stress and the
             * secondary stresses, giving the same answer on every machine (LtsNetwork is the integer
             * network). Trained on the CMU Pronouncing Dictionary alone by the non-shipped tools. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Phonemes.En/LetterToSoundSubsystem/LtsModel.cs, src/DemaConsulting.Speech.Phonemes.En/LetterToSoundSubsystem/LtsNetwork.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Phonemes.En.Tests/LetterToSoundSubsystemTests.cs */
        comment designRef /* Design: docs/design/speech-phonemes-en/letter-to-sound-subsystem/lts-model.md */
        comment verificationRef /* Verification: docs/verification/speech-phonemes-en/letter-to-sound-subsystem/lts-model.md */
        comment reqRef /* Requirements: docs/reqstream/speech-phonemes-en/letter-to-sound-subsystem/lts-model.yaml */
    }
}
