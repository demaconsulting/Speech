package Speech {
    part def SpeechModelContract {
        doc /* The common per-model contract (ISpeechModel: Id, DisplayName, Role, Parameters,
             * AudioTagSupport, DownloadDescriptor) and its two empty role-marker interfaces,
             * IRecognitionModel and ISynthesisModel - the seam Phase 3/4 extend with real
             * engine-construction methods (CreateEngineConfig) and, for synthesis, a
             * default-hook CapabilityProfile member. Phase 12 adds a recognition-only
             * IRecognitionModel.NormalizeText(text, isFinal) default hook, distinct from the
             * shared ISpeechModel.NormalizeText(text) used on the synthesis side. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/ModelManagementSubsystem/ISpeechModel.cs, IRecognitionModel.cs, ISynthesisModel.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/ModelManagementSubsystem/SpeechModelContractTests.cs */
        comment designRef /* Design: docs/design/speech/model-management-subsystem/speech-model-contract.md */
        comment verificationRef /* Verification: docs/verification/speech/model-management-subsystem/speech-model-contract.md */
        comment reqRef /* Requirements: docs/reqstream/speech/model-management-subsystem/speech-model-contract.yaml */
    }
}
