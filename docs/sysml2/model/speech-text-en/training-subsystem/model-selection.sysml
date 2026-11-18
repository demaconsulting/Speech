package SpeechTextEn {
    part def ModelSelection {
        doc /* Development-set choices and model construction: keeps the epoch with the most natural
             * development rows correct (earliest on ties), chooses the calibration temperature on a k/20
             * grid by the deterministic negative log-likelihood (smaller k on ties), and builds the
             * compressed and the uncompressed model image whose header carries the inventory version,
             * feature-set version, trainer version and data hash. Also holds the TrainedModel and
             * EpochScore values. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/ModelSelection.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/ModelSelectionTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-subsystem/model-selection.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-subsystem/model-selection.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-subsystem/model-selection.yaml */
    }
}
