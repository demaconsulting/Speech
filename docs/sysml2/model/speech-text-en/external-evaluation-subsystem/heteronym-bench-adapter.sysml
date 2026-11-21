package SpeechTextEn_HeteronymBenchAdapter {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_ExternalRowConversion::*;
    private import SpeechTextEn_ExternalSourceCache::*;

    part def HeteronymBenchAdapter {
        doc /* Reader of the pinned English read set of heteronym-bench: parses the items with their strata and
             * labels, skips the items copied from WikipediaHomographData as duplicates, repairs a span that counts
             * bytes when that gives the target word, and skips what cannot be placed. A malformed document is an
             * error that names the item. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/ExternalEvaluationSubsystem/HeteronymBenchAdapter.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/HeteronymBenchAdapterTests.cs");
            designFiles = ("docs/design/speech-text-en/external-evaluation-subsystem/heteronym-bench-adapter.md");
            verificationFiles = ("docs/verification/speech-text-en/external-evaluation-subsystem/heteronym-bench-adapter.md");
            requirementFiles = ("docs/reqstream/speech-text-en/external-evaluation-subsystem/heteronym-bench-adapter.yaml");
        }

        // Reads heteronym-bench files from the source cache and converts its rows with the shared
        // conversion.
        dependency HeteronymBenchAdapter to ExternalRowConversion;
        dependency HeteronymBenchAdapter to ExternalSourceCache;
    }
}
