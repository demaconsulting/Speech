package SpeechProofreader_DiagnosticsSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechProofreader_SentenceDiagnostics::*;

    part def DiagnosticsSubsystem {
        doc /* What the English front end made of a sentence and which phonemes the model consumed, for the
             * diagnostics pane. */

        metadata info : SoftwareInfo {
        }

        part sentenceDiagnostics : SentenceDiagnostics;
    }
}
