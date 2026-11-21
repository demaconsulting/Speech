package SpeechTextEn_ClusterCorpusCache {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_GutenbergBody::*;
    private import SpeechTextEn_TsvTable::*;

    part def ClusterCorpusCache {
        doc /* The corpus downloader with its source pins: holds the source list cluster-sources.tsv (one row per book
             * with its license basis and body pin), fetches each used book only over HTTPS from an allowed official
             * mirror with an identifying user agent, no redirects and a polite rate limit, pins and verifies each
             * normalized body, and hands out only bodies that match their pins. It is the only unit that reaches the
             * network or opens a file of the git-ignored corpus cache. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/ClusteringSubsystem/ClusterCorpusCache.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/ClusteringSubsystem/ClusterCorpusCacheTests.cs");
            designFiles = ("docs/design/speech-text-en/clustering-subsystem/cluster-corpus-cache.md");
            verificationFiles = ("docs/verification/speech-text-en/clustering-subsystem/cluster-corpus-cache.md");
            requirementFiles = ("docs/reqstream/speech-text-en/clustering-subsystem/cluster-corpus-cache.yaml");
        }

        // Reads the pinned source list, fetches books and extracts their Gutenberg bodies, reporting
        // failures.
        dependency ClusterCorpusCache to CheckReport;
        dependency ClusterCorpusCache to GutenbergBody;
        dependency ClusterCorpusCache to TsvTable;
    }
}
