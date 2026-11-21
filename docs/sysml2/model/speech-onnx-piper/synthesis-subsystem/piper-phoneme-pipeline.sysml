package SpeechOnnxPiper_PiperPhonemePipeline {
    private import SoftwareItemMetadata::*;
    private import SpeechOnnxPiper_PiperVoiceConfig::*;

    part def PiperPhonemePipeline {
        doc /* Turns the text of one speech segment, with the annotations of the English text analysis when there are
             * any, into the framed phoneme-id sequences the Piper graph takes: phonemization (SpeechPhonemesEn's
             * EnglishPhonemizer), rendering as espeak-style IPA (EspeakIpaRenderer, whose RenderParts cuts a long
             * text into parts of at most 384 symbols, at the best phrase boundary) and encoding through the voice's
             * own symbol table with Piper's framing (begin and pad, each symbol followed by pad, end). A symbol the
             * table lacks is skipped and listed in the PiperUtterance result type. Internal. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/DemaConsulting.Speech.Onnx.Piper/SynthesisSubsystem/PiperPhonemePipeline.cs");
            testFiles = ("test/DemaConsulting.Speech.Onnx.Piper.Tests/SynthesisSubsystem/PiperPhonemePipelineTests.cs");
            designFiles = ("docs/design/speech-onnx-piper/synthesis-subsystem/piper-phoneme-pipeline.md");
            verificationFiles = ("docs/verification/speech-onnx-piper/synthesis-subsystem/piper-phoneme-pipeline.md");
            requirementFiles = ("docs/reqstream/speech-onnx-piper/synthesis-subsystem/piper-phoneme-pipeline.yaml");
        }

        // Phonemizes text, renders it as eSpeak IPA, and maps symbols to ids using the voice config.
        dependency PiperPhonemePipeline to PiperVoiceConfig;
        dependency PiperPhonemePipeline to SpeechPhonemesEn_EnglishPhonemizer::EnglishPhonemizer;
        dependency PiperPhonemePipeline to SpeechPhonemesEn_EspeakIpaRenderer::EspeakIpaRenderer;
        dependency PiperPhonemePipeline to Speech_TextAnalysis::TextAnalysis;
    }
}
