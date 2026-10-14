package SpeechCli {
    part def SpeechModelCatalogAdapter {
        doc /* Real catalog seam implementation, owning and delegating to a real
             * SpeechModelCatalog (and, for uninstall/cleanup, its Store) without exposing the
             * library's internal-only constructor. Forwards GetPreferredAudioFormat/
             * CreateSynthesizerEngineAsync and CreateRecognizerEngineAsync
             * unchanged to the resolved model's real SpeechSynthesizerFactory/
             * SpeechRecognizerFactory, preserving each factory's own honestly-unavailable
             * fallback contract rather than masking it. Chains AddChatterboxTurboModels after the
             * Sherpa/Kokoro/Nemotron models, sharing one ReferenceVoiceLibrary with the voice
             * members and passing --voices-dir as the extra reference-voice directory. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Cli/Commands/ModelCommandsSubsystem/SpeechModelCatalogAdapter.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Cli.Tests/Commands/ModelCommandsSubsystem/SpeechModelCatalogAdapterTests.cs */
        comment designRef /* Design: docs/design/speech-cli/model-commands-subsystem/speech-model-catalog-adapter.md */
        comment verificationRef /* Verification: docs/verification/speech-cli/model-commands-subsystem/speech-model-catalog-adapter.md */
        comment reqRef /* Requirements: docs/reqstream/speech-cli/model-commands-subsystem/speech-model-catalog-adapter.yaml */
    }
}
