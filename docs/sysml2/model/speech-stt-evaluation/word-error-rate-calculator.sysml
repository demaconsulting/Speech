package SpeechSttEvaluation_WordErrorRateCalculator {
    private import SoftwareItemMetadata::*;

    part def WordErrorRateCalculator {
        doc /* Word error rate and word alignment (WordAlignment) of a hypothesis against a reference by minimum edit
             * distance over normalized words (lower case, punctuation removed except apostrophes inside a word), with
             * the invented words after the last aligned reference word and a verbatim split for punctuated
             * comparison. Shared: compiled (linked) into the SpeechOnnxNemotronStt and SpeechSherpa tests. */

        metadata info : SoftwareInfo {
            sourceFiles = ("tools/DemaConsulting.Speech.SttEvaluation/WordErrorRateCalculator.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/Evaluation/WordErrorRateCalculatorTests.cs");
        }
    }
}