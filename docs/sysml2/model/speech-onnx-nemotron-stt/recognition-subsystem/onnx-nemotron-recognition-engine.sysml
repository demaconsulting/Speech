package SpeechOnnxNemotronStt {
    part def OnnxNemotronRecognitionEngine {
        doc /* Real IRecognitionBackend implementation running the Nemotron model through ONNX
             * Runtime: limiter, dither, feature extraction, 56-frame chunking with a 9-frame
             * pre-encode cache, cache-aware encoder, and RNN-T greedy decoding, with endpointing
             * on the input clock and on consecutive token-free chunks. Constructed by
             * OnnxNemotronMultilingualRecognitionModel.CreateBackend. Also documents its ONNX
             * collaborators NemotronEncoder (INemotronEncoder) and NemotronRnntNetwork. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/OnnxNemotronRecognitionEngine.cs, src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/INemotronEncoder.cs, src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronEncoder.cs, src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/IRnntNetwork.cs, src/DemaConsulting.Speech.Onnx.NemotronStt/RecognitionSubsystem/NemotronRnntNetwork.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.NemotronStt.Tests/RecognitionSubsystem/OnnxNemotronRecognitionEngineTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-nemotron-stt/recognition-subsystem/onnx-nemotron-recognition-engine.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-nemotron-stt/recognition-subsystem/onnx-nemotron-recognition-engine.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-nemotron-stt/recognition-subsystem/onnx-nemotron-recognition-engine.yaml */
    }
}
