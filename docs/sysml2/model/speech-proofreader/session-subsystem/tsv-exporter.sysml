package SpeechProofreader_TsvExporter {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_FlagCategory::*;
    private import SpeechProofreader_ProofreadSession::*;

    part def TsvExporter {
        doc /* Exports flags as tab-separated text for spreadsheets and for lifting rows into golden test data. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.Proofreader/Session/TsvExporter.cs");
            testFiles = ("test/DemaConsulting.Speech.Proofreader.Tests/TsvExporterTests.cs");
        }

        // Writes ProofreadSession flags and their FlagCategory values as TSV rows.
        dependency TsvExporter to FlagCategory;
        dependency TsvExporter to ProofreadSession;
    }
}
