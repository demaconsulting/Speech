package SpeechTextEn {
    part def HeteronymEvaluator {
        doc /* End-to-end model evaluator: decides each row through the real EnglishTextAnalyzer (no reading
             * counts as wrong), compares it with the direct classifier path for parity, reports three
             * baselines and strata by source, reading and confidence, computes the paired
             * document-clustered bootstrap interval in integer arithmetic, and decides the quality gate.
             * Also holds the SetEvaluation, GroupTally, BootstrapInterval, ClusterStats, ParityCounts,
             * SeedResult and GateVerdict result records. */

        comment sourceRef /* Source: tools/DemaConsulting.Speech.Text.En.Trainer/TrainingSubsystem/HeteronymEvaluator.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Trainer.Tests/TrainingSubsystem/HeteronymEvaluatorTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/training-subsystem/heteronym-evaluator.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/training-subsystem/heteronym-evaluator.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/training-subsystem/heteronym-evaluator.yaml */
    }
}
