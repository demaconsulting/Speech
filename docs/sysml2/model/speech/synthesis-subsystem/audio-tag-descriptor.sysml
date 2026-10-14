package Speech {
    part def AudioTagDescriptor {
        doc /* Supporting record pairing one NaturalLanguageAudioTag with its kind and canonical display
             * information, as reported by AudioTagCatalog.Tags. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/AudioTagDescriptor.cs, src/DemaConsulting.Speech/SynthesisSubsystem/AudioTagCatalog.cs, src/DemaConsulting.Speech/SynthesisSubsystem/NaturalLanguageAudioTag.cs, src/DemaConsulting.Speech/SynthesisSubsystem/NaturalLanguageAudioTagKind.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/AudioTagCatalogTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/audio-tag-parser.md (documented as a supporting type) */
                comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/audio-tag-parser.md (verified within the AudioTagParser unit doc; no dedicated doc) */
                comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/audio-tag-parser.yaml (requirements held in the AudioTagParser unit file; no dedicated file) */    }
}
