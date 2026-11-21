package SpeechTextEn_CorpusCommand {
    private import SoftwareItemMetadata::*;
    private import SpeechTextEn_CheckDataCommand::*;
    private import SpeechTextEn_CorpusCache::*;
    private import SpeechTextEn_CorpusLeakageAudit::*;
    private import SpeechTextEn_CorpusReader::*;
    private import SpeechTextEn_GutenbergBody::*;
    private import SpeechTextEn_LeakageChecker::*;

    part def CorpusCommand {
        doc /* Command line entry of the trainer's corpus command: exactly one of --fetch [--write-pins] or --audit
             * [--write] per run; refuses a cache inside the repository outside the ignored external folder; prints
             * counts, ids, hashes and timings but never corpus text; returns 0 when done or current, 1 when the
             * removals are stale or a book failed, and 2 on a usage, I/O or data error. The Program.cs entry point is
             * glue that dispatches to this unit. Also holds the CorpusEnvironment, CorpusMode and CorpusOptions
             * records. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Text.En.Trainer/CorpusSubsystem/CorpusCommand.cs", "tools/DemaConsulting.Speech.Text.En.Trainer/Program.cs");
            testFiles = ("test/DemaConsulting.Speech.Text.En.Trainer.Tests/CorpusSubsystem/CorpusCommandTests.cs");
            designFiles = ("docs/design/speech-text-en/corpus-subsystem/corpus-command.md");
            verificationFiles = ("docs/verification/speech-text-en/corpus-subsystem/corpus-command.md");
            requirementFiles = ("docs/reqstream/speech-text-en/corpus-subsystem/corpus-command.yaml");
        }

        // Orchestrates the fetch and the audit: loads the pins, reads the verified bodies, tokenizes them and
        // audits them; finds the data folder and checks the cache path like check-data does.
        dependency CorpusCommand to CorpusCache;
        dependency CorpusCommand to CorpusReader;
        dependency CorpusCommand to CorpusLeakageAudit;
        dependency CorpusCommand to GutenbergBody;
        dependency CorpusCommand to CheckDataCommand;
        dependency CorpusCommand to LeakageChecker;
    }
}
