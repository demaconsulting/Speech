package Speech {
    part def ISpeechSynthesizerEngine {
        doc /* Layer 3: the public, loaded, expensive, native-backed text-to-speech model
             * contract - availability flag, async CreateSessionAsync to bind a playback device
             * into an ISynthesisSession, and one-shot SpeakAsync/SynthesizeAsync convenience
             * overloads that create a session internally and dispose it - plus its adjacent,
             * immutable SynthesizedSpeech audio-segment payload type, exposing no
             * inference-engine type. Obtaining this engine is the expensive step; a session
             * obtained from it is cheap and reusable across many calls. */

        comment sourceRef /* Source: src/DemaConsulting.Speech/SynthesisSubsystem/ISpeechSynthesizerEngine.cs, SynthesizedSpeech.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Tests/SynthesisSubsystem/SherpaOnnxSpeechSynthesizerEngineTests.cs */
        comment designRef /* Design: docs/design/speech/synthesis-subsystem/i-speech-synthesizer-engine.md */
        comment verificationRef /* Verification: docs/verification/speech/synthesis-subsystem/i-speech-synthesizer-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech/synthesis-subsystem/i-speech-synthesizer-engine.yaml */
    }
}
