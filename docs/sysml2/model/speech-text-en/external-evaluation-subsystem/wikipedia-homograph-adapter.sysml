package SpeechTextEn_WikipediaHomographAdapter {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_ExternalRowConversion::*;
    private import SpeechTextEn_ExternalSourceCache::*;

    part def WikipediaHomographAdapter {
        doc /* Reader of the pinned Google WikipediaHomographData files: the tab-separated files with CSV-style quoted
             * fields, converts the byte offsets to character offsets, skips a row whose converted span is not its
             * homograph, and reads the training and evaluation splits from their own pinned files in ordinal order
             * through the verified cache. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/ExternalEvaluationSubsystem/WikipediaHomographAdapter.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/ExternalEvaluationSubsystem/WikipediaHomographAdapterTests.cs");
            designFiles = ("docs/design/speech-text-en/external-evaluation-subsystem/wikipedia-homograph-adapter.md");
            verificationFiles = ("docs/verification/speech-text-en/external-evaluation-subsystem/wikipedia-homograph-adapter.md");
            requirementFiles = ("docs/reqstream/speech-text-en/external-evaluation-subsystem/wikipedia-homograph-adapter.yaml");
        }

        // Reads WikipediaHomographData from the source cache and converts its rows with the shared
        // conversion.
        dependency WikipediaHomographAdapter to ExternalRowConversion;
        dependency WikipediaHomographAdapter to ExternalSourceCache;
    }
}
