package SpeechTextEn {
    part def PartOfSpeechAdapter {
        doc /* Internal seam that isolates every use of the DemaConsulting.PartOfSpeech package:
             * splits a text into sentences and tokens (offsets index the given text) and, on
             * request, tags them, obtaining the shared tagger lazily and at most once, and returns
             * plain values so no tagger type reaches a public signature. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/PartOfSpeechAdapter.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/PartOfSpeechAdapterTests.cs */
        comment designRef /* Design: docs/design/speech-text-en/analysis-subsystem/part-of-speech-adapter.md */
        comment verificationRef /* Verification: docs/verification/speech-text-en/analysis-subsystem/part-of-speech-adapter.md */
        comment reqRef /* Requirements: docs/reqstream/speech-text-en/analysis-subsystem/part-of-speech-adapter.yaml */
    }
}
