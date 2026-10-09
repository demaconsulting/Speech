using Microsoft.ML.OnnxRuntime;

namespace DemaConsulting.Speech.Onnx.OnnxRuntimeSubsystem;

/// <summary>
///     Builds an ONNX Runtime <see cref="InferenceSession"/>, opportunistically preferring an
///     accelerated execution provider when one is actually available at runtime, and always
///     falling back to the built-in CPU provider otherwise.
/// </summary>
/// <remarks>
///     This package deliberately references only the base <c>Microsoft.ML.OnnxRuntime</c>
///     package, never an accelerated provider package such as <c>Microsoft.ML.OnnxRuntime.Gpu</c>
///     or <c>Microsoft.ML.OnnxRuntime.DirectML</c> - see the project file's remarks for why. That
///     means this class cannot call a provider-specific, compile-time-bound method such as
///     <c>SessionOptions.AppendExecutionProvider_CUDA</c> (those methods exist only in the
///     provider-specific package's own build of the managed assembly). Instead it uses ONNX
///     Runtime's own generic, string-named <c>SessionOptions.AppendExecutionProvider(string,
///     IReadOnlyDictionary&lt;string,string&gt;?)</c> overload, which resolves the named provider's
///     native shared library dynamically at session-creation time: when a consuming application
///     has supplied the matching native runtime (for example by adding
///     <c>Microsoft.ML.OnnxRuntime.Gpu</c> as its own direct dependency, or by placing the
///     provider's native binary alongside the host executable), the provider loads and is used;
///     when it has not, ONNX Runtime throws, and this class catches that failure and tries the
///     next candidate, ending with the CPU provider, which ships inside the base package and is
///     therefore always available. Stage 1 of this library's ONNX model support intentionally
///     keeps the candidate list CPU-only (<see cref="DefaultProviderNames"/> is empty) - this
///     class's layered try/fall-back design is kept in place from the very first model so that
///     enabling GPU acceleration later needs only a config change here, not a redesign.
/// </remarks>
public static class OnnxExecutionProviderSelector
{
    /// <summary>
    ///     The default, Stage-1 candidate execution provider names to try before falling back to
    ///     CPU. Deliberately empty: Stage 1 of this library's ONNX model support targets CPU-only
    ///     inference, matching the earlier "start simple" design decision for the raw-ONNX model
    ///     family. A later pass can populate this (for example with <c>"CUDAExecutionProvider"</c>
    ///     or <c>"DmlExecutionProvider"</c>) without changing any other code in this class.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultProviderNames = [];

    /// <summary>
    ///     Creates an <see cref="InferenceSession"/> for the ONNX model at <paramref name="modelPath"/>,
    ///     trying each of <paramref name="preferredProviderNames"/> in order before falling back to
    ///     the CPU provider.
    /// </summary>
    /// <param name="modelPath">The absolute path of the <c>.onnx</c> model file to load. Must not be null or empty.</param>
    /// <param name="preferredProviderNames">
    ///     The ordered, accelerated execution provider names to attempt first (for example
    ///     <c>"CUDAExecutionProvider"</c>), or <see langword="null"/> to use
    ///     <see cref="DefaultProviderNames"/>. Each is tried with its own fresh
    ///     <see cref="SessionOptions"/>, so one candidate's failed native-library lookup can never
    ///     leave a prior candidate's partially-applied configuration behind.
    /// </param>
    /// <returns>
    ///     A loaded <see cref="InferenceSession"/> using the first provider that both appended and
    ///     loaded successfully, or the CPU provider when none did.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="modelPath"/> is null or empty.</exception>
    /// <remarks>
    ///     Never throws due to an unavailable accelerated provider: the CPU provider is always
    ///     attempted last and is guaranteed to succeed for a well-formed model, since it ships
    ///     inside the base <c>Microsoft.ML.OnnxRuntime</c> package this class's own project
    ///     references. A genuinely malformed or unreadable model file still throws from that final
    ///     CPU attempt, exactly as a direct <c>new InferenceSession(modelPath)</c> call would.
    /// </remarks>
    public static InferenceSession Create(string modelPath, IReadOnlyList<string>? preferredProviderNames = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelPath);

        foreach (var providerName in preferredProviderNames ?? DefaultProviderNames)
        {
            // SessionOptions owns a native OrtSessionOptions handle. InferenceSession does NOT
            // take ownership of the options instance passed to it, so this options instance must
            // always be disposed by this method - whether the session is created successfully or
            // the provider fails to load - without disposing the returned InferenceSession itself.
            using var options = new SessionOptions();
            options.AppendExecutionProvider(providerName);
            try
            {
                return new InferenceSession(modelPath, options);
            }
            catch (OnnxRuntimeException)
            {
                // This candidate provider's native shared library is not available in this
                // process - fall through and try the next candidate, ending with CPU below.
            }
            catch (DllNotFoundException)
            {
                // Some provider native libraries surface a missing dependency this way rather
                // than as an OnnxRuntimeException - treated identically.
            }
        }

        // The CPU provider ships inside the base package and needs no explicit append - an
        // InferenceSession created with default SessionOptions already uses it.
        return new InferenceSession(modelPath);
    }
}
