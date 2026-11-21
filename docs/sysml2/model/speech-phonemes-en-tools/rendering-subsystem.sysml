package SpeechPhonemesEnTools_RenderingSubsystem {
    private import SoftwareItemMetadata::*;
    private import SpeechPhonemesEnTools_MeasureOracleCommand::*;

    part def RenderingSubsystem {
        doc /* The measure-oracle command: exports the library's renderings of the measurement corpora for comparison
             * with the reference engines outside the repository. */

        metadata info : SoftwareInfo {
        }

        part measureOracleCommand : MeasureOracleCommand;
    }
}
