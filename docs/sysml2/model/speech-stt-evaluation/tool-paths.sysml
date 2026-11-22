package SpeechSttEvaluation_ToolPaths {
    private import SoftwareItemMetadata::*;

    part def ToolPaths {
        doc /* Locates the repository folders the evaluation tool reads and writes: the repository root (the folder
             * holding Speech.slnx), the default data folder data/stt-en, and the output folder, which must lie inside
             * the git-ignored artifacts folder so no report or third-party transcript can be committed. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.SttEvaluation/ToolPaths.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/Evaluation/ToolPathsTests.cs");
        }
    }
}