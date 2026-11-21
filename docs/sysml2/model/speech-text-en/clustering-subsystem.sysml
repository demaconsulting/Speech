package SpeechTextEn_ClusteringSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_ClusterCorpusCache::*;
    private import SpeechTextEn_CorpusLeakageAudit::*;
    private import SpeechTextEn_GutenbergBody::*;

    part def ClusteringSubsystem {
        doc /* The corpus tooling of the non-shipped Text.En trainer that outlives the word-cluster experiment: the
             * pinned source list of US public-domain books with its mirror-only, rate-limited download
             * (ClusterCorpusCache), the pinned body extraction of a downloaded e-book (GutenbergBody) and the
             * content-level leakage audit of the corpus against every evaluation set (CorpusLeakageAudit). Modeled in
             * part only: the Brown word-clustering units (ClusterCorpusReader, ClusterInducer, WordClusterTable,
             * ClusteringRecord, ClusterCommand), the corpus snapshot archive (CorpusSnapshot) and the word-cluster
             * data check are deliberately left out because an in-flight change removes them. */

        metadata info : SoftwareInfo {
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/ClusteringSubsystemTests.cs");
            designFiles = ("docs/design/speech-text-en/clustering-subsystem.md");
            verificationFiles = ("docs/verification/speech-text-en/clustering-subsystem.md");
            requirementFiles = ("docs/reqstream/speech-text-en/clustering-subsystem.yaml");
        }

        part gutenbergBody : GutenbergBody;
        part clusterCorpusCache : ClusterCorpusCache;
        part corpusLeakageAudit : CorpusLeakageAudit;
    }
}
