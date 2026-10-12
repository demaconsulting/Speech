package Speech {
    part def PortAudioSharp2 {
        doc /* Managed PortAudio binding and carrier for the supported transitive native runtime
             * packages consumed by the Speech PortAudio child subsystem. */

        comment designRef /* Design: docs/design/ots/port-audio-sharp2.md */
        comment verificationRef /* Verification: docs/verification/ots/port-audio-sharp2.md */
        comment reqRef /* Requirements: docs/reqstream/ots/port-audio-sharp2.yaml */
    }

    part def SystemNumericsTensors {
        doc /* Maintained vectorized numeric primitives (TensorPrimitives) used by the Speech
             * AudioSubsystem and SynthesisSubsystem for bulk sample math. */

        comment designRef /* Design: docs/design/ots/system-numerics-tensors.md */
        comment verificationRef /* Verification: docs/verification/ots/system-numerics-tensors.md */
        comment reqRef /* Requirements: docs/reqstream/ots/system-numerics-tensors.yaml */
    }
}

package SpeechOnnx {
    part def MicrosoftMlOnnxRuntime {
        doc /* Managed ONNX Runtime inference API consumed by the SpeechOnnx, SpeechOnnxKokoro,
             * SpeechOnnxNemotronStt, and SpeechOnnxChatterboxTurbo libraries. */

        comment designRef /* Design: docs/design/ots/microsoft-ml-onnxruntime.md */
        comment verificationRef /* Verification: docs/verification/ots/microsoft-ml-onnxruntime.md */
        comment reqRef /* Requirements: docs/reqstream/ots/microsoft-ml-onnxruntime.yaml */
    }
}

package SpeechDemo {
    part def Avalonia {
        doc /* Cross-platform XAML desktop UI framework providing the application host,
             * styling, and view layer for the SpeechDemo application. */

        comment designRef /* Design: docs/design/ots/avalonia.md */
        comment verificationRef /* Verification: docs/verification/ots/avalonia.md */
        comment reqRef /* Requirements: docs/reqstream/ots/avalonia.yaml */
    }

    part def CommunityToolkitMvvm {
        doc /* MVVM source generators supplying the observable-property change notification
             * and the bindable synchronous/asynchronous commands used by the SpeechDemo
             * view models. */

        comment designRef /* Design: docs/design/ots/community-toolkit-mvvm.md */
        comment verificationRef /* Verification: docs/verification/ots/community-toolkit-mvvm.md */
        comment reqRef /* Requirements: docs/reqstream/ots/community-toolkit-mvvm.yaml */
    }
}

package SpeechSherpa {
    part def SherpaOnnx {
        doc /* Managed local speech-inference API and carrier for its per-platform native runtime
             * packages, consumed by the SpeechSherpa recognition and synthesis engines behind
             * Speech's public backend seams. */

        comment designRef /* Design: docs/design/ots/sherpa-onnx.md */
        comment verificationRef /* Verification: docs/verification/ots/sherpa-onnx.md */
        comment reqRef /* Requirements: docs/reqstream/ots/sherpa-onnx.yaml */
    }

    part def SharpCompress {
        doc /* Permissively-licensed managed archive library providing the BZip2 tar
             * (.tar.bz2) decoder the BCL lacks, consumed by the SpeechSherpa
             * ModelManagementSubsystem's TarBz2ArchiveExtractor to unpack downloaded
             * sherpa-onnx model archives. */

        comment designRef /* Design: docs/design/ots/sharp-compress.md */
        comment verificationRef /* Verification: docs/verification/ots/sharp-compress.md */
        comment reqRef /* Requirements: docs/reqstream/ots/sharp-compress.yaml */
    }
}
