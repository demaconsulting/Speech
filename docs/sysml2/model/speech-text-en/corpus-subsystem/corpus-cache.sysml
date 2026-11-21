package SpeechTextEn_CorpusCache {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckReport::*;
    private import SpeechTextEn_GutenbergBody::*;
    private import SpeechTextEn_TsvTable::*;

    part def CorpusCache {
        doc /* The corpus downloader with its source pins: holds the source list cluster-sources.tsv (one row per book
             * with its license basis and body pin), fetches each used book only over HTTPS from an allowed official
             * mirror with an identifying user agent, no redirects and a polite rate limit, reports a book whose mirror
             * text differs from its pin (its pin is never overwritten; only missing pins are filled on request), and
             * hands out only bodies that match their pins. It is the only unit that reaches the network or opens a
             * file of the git-ignored corpus cache. Also holds the CorpusSource, CorpusSourceList,
             * GutenbergMirrorFetcher and CorpusFetchResult records. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/CorpusSubsystem/CorpusCache.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/CorpusSubsystem/CorpusCacheTests.cs");
            designFiles = ("docs/design/speech-text-en/corpus-subsystem/corpus-cache.md");
            verificationFiles = ("docs/verification/speech-text-en/corpus-subsystem/corpus-cache.md");
            requirementFiles = ("docs/reqstream/speech-text-en/corpus-subsystem/corpus-cache.yaml");
        }

        // Reads the pinned source list, fetches books and extracts their Gutenberg bodies, reporting
        // failures.
        dependency CorpusCache to CheckReport;
        dependency CorpusCache to GutenbergBody;
        dependency CorpusCache to TsvTable;
    }
}
