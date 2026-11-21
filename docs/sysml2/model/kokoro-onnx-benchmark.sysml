package KokoroOnnxBenchmark {
    private import SoftwareItemMetadata::*;
    private import KokoroOnnxBenchmark_Program::*;
    private import OtsDependencies::*;

    doc
    /*
     * KokoroOnnxBenchmark is a non-shipped, development-only spike that benchmarks Kokoro ONNX Runtime inference on
     * the CPU and DirectML execution providers.
     */

    part def KokoroOnnxBenchmarkSystem {
        doc /* The non-shipped, development-only Kokoro ONNX benchmark spike as a whole (no design, verification or
             * requirements documents of its own). */

        metadata info : SoftwareInfo {
        }

        part microsoftMlOnnxRuntime : MicrosoftMlOnnxRuntime;
        part program : Program;
    }
}
