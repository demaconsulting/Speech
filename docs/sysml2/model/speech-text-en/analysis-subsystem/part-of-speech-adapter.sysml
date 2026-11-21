package SpeechTextEn_PartOfSpeechAdapter {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_HeteronymFeatureExtractor::*;

    part def PartOfSpeechAdapter {
        doc /* Internal seam that isolates every use of the DemaConsulting.PartOfSpeech package: splits a text into
             * sentences and tokens (offsets index the given text) and, on request, tags them, obtaining the shared
             * tagger lazily and at most once, and returns plain values so no tagger type reaches a public signature. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Text.En/AnalysisSubsystem/PartOfSpeechAdapter.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Tests/AnalysisSubsystem/PartOfSpeechAdapterTests.cs");
            designFiles = ("docs/design/speech-text-en/analysis-subsystem/part-of-speech-adapter.md");
            verificationFiles = ("docs/verification/speech-text-en/analysis-subsystem/part-of-speech-adapter.md");
            requirementFiles = ("docs/reqstream/speech-text-en/analysis-subsystem/part-of-speech-adapter.yaml");
        }

        // Wraps the PartOfSpeech package for tokens and tags and supplies them as heteronym features.
        dependency PartOfSpeechAdapter to HeteronymFeatureExtractor;
        dependency PartOfSpeechAdapter to OtsDependencies::PartOfSpeech;
    }
}
