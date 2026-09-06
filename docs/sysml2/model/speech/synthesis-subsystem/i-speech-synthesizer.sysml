package Speech {
    part def ISpeechSynthesizer {
        doc /* Public streaming text-to-speech contract (availability flag, streaming/playing
             * SpeakAsync, Stop), plus its immutable SynthesizedSpeech audio-segment payload
             * type, exposing no inference-engine type. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/ISpeechSynthesizer.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SherpaOnnxSpeechSynthesizerTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/i-speech-synthesizer.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/i-speech-synthesizer.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/i-speech-synthesizer.yaml */
    }
}
