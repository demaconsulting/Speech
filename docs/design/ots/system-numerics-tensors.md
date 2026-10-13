## System.Numerics.Tensors

### Purpose

System.Numerics.Tensors is used as the Speech library's source of vectorized numeric primitives
(`TensorPrimitives`) for per-sample audio math. It was chosen because it is a Microsoft-maintained
package that accelerates bulk float operations with the hardware vector instructions available at
run time, avoiding hand-written SIMD code and keeping the audio pipeline's inner loops short.

### Features Used

- `TensorPrimitives.ConvertChecked` and `TensorPrimitives.Divide` to convert 16-bit PCM samples to
  normalized floats in `WavFileAudioCaptureDevice`
- `TensorPrimitives.Dot` to apply the windowed-sinc kernel in `WindowedSincLowpassFilter`, the
  anti-aliasing filter that `AudioFrameResampler` and `PlaybackAudioResampler` apply before
  down-sampling (`AudioResampler` uses its own scalar convolution and does not use the package)
- `TensorPrimitives.Multiply` and `TensorPrimitives.Clamp` to apply and bound the volume override
  in `SynthesisSession`

### Integration Pattern

`DemaConsulting.Speech.csproj` references `System.Numerics.Tensors` directly and is the single
source of truth for the pinned version. The package is used only inside internal implementation
code over `Span<float>` buffers; no `System.Numerics.Tensors` type appears in any public API. The
SpeechCli tool's `devices test` command also uses `TensorPrimitives.MaxMagnitude`, taking the
package transitively through its Speech reference, for peak-level measurement. That use is a
diagnostic convenience of the CLI, not a required feature of this OTS item: it is intentionally
not part of the requirement or the OTS verification evidence.
