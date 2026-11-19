package SpeechOnnxPiper {
    part def PiperPhonemePipeline {
        doc /* Turns the text of one speech segment, with the annotations of the English text analysis when
             * there are any, into the framed phoneme-id sequences the Piper graph takes: phonemization
             * (SpeechPhonemesEn's EnglishPhonemizer), rendering as espeak-style IPA (EspeakIpaRenderer, whose
             * RenderParts cuts a long text into parts of at most 256 symbols) and encoding through the
             * voice's own symbol table with Piper's framing (begin, each symbol followed by pad, end). A
             * symbol the table lacks is skipped and listed in the PiperUtterance result type. Internal. */

        comment sourceRef /* Source: src/DemaConsulting.Speech.Onnx.Piper/SynthesisSubsystem/PiperPhonemePipeline.cs */
        comment testRef /* Test: test/DemaConsulting.Speech.Onnx.Piper.Tests/SynthesisSubsystem/PiperPhonemePipelineTests.cs */
        comment designRef /* Design: docs/design/speech-onnx-piper/synthesis-subsystem/piper-phoneme-pipeline.md */
        comment verificationRef /* Verification: docs/verification/speech-onnx-piper/synthesis-subsystem/piper-phoneme-pipeline.md */
        comment reqRef /* Requirements: docs/reqstream/speech-onnx-piper/synthesis-subsystem/piper-phoneme-pipeline.yaml */
    }
}
