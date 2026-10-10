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
    ///     Test-only observation hook invoked with each candidate's <see cref="SessionOptions"/>
    ///     instance immediately after it is constructed, before it is used or disposed.
    /// </summary>
    /// <remarks>
    ///     <see cref="SessionOptions"/> derives from <see cref="System.Runtime.InteropServices.SafeHandle"/>
    ///     and therefore exposes a public <c>IsClosed</c> property, so a test subscribing here can
    ///     assert that every candidate's options instance - including a failed candidate's and the
    ///     eventually-winning candidate's - is disposed by the time <see cref="Create"/> returns.
    ///     There is no other way to observe this from outside the class: the loop-local
    ///     <c>using var options</c> variable itself is never exposed to callers. Production callers
    ///     never set this; it exists solely for
    ///     <c>DemaConsulting.Speech.Onnx.Tests</c> (see <c>InternalsVisibleTo</c> in this project's
    ///     <c>.csproj</c>).
    /// </remarks>
    internal static Action<SessionOptions>? OnCandidateOptionsCreated { get; set; }

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
    /// <param name="validateSession">
    ///     An optional probe that runs a representative inference on each accelerated candidate's
    ///     freshly created session; if it throws <see cref="OnnxRuntimeException"/> the candidate
    ///     is discarded and the next one tried. The CPU fallback is never probed.
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
    ///     Only <see cref="OnnxRuntimeException"/> and <see cref="DllNotFoundException"/> from a
    ///     candidate's construction or probe are treated as "try the next candidate"; any other
    ///     exception type a candidate's construction or <paramref name="validateSession"/> throws
    ///     still disposes that candidate's session before propagating to the caller, rather than
    ///     being silently discarded.
    /// </remarks>
    public static InferenceSession Create(
        string modelPath,
        IReadOnlyList<string>? preferredProviderNames = null,
        Action<InferenceSession>? validateSession = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelPath);

        foreach (var providerName in preferredProviderNames ?? DefaultProviderNames)
        {
            // SessionOptions owns a native OrtSessionOptions handle. InferenceSession does NOT
            // take ownership of the options instance passed to it, so this options instance must
            // always be disposed by this method - whether the session is created successfully or
            // the provider fails to load - without disposing the returned InferenceSession itself.
            using var options = new SessionOptions();
            OnCandidateOptionsCreated?.Invoke(options);
            InferenceSession? session = null;
            var succeeded = false;
            try
            {
                options.AppendExecutionProvider(providerName);
                session = new InferenceSession(modelPath, options);

                // Some providers (notably DirectML) load and create a session successfully yet
                // fail on the first Run() because a graph operator is unsupported for the model's
                // shapes, so a caller-supplied probe inference is the only reliable check.
                validateSession?.Invoke(session);
                succeeded = true;
                return session;
            }
            catch (OnnxRuntimeException)
            {
                // Provider unavailable or failed its probe inference - discard and try the next.
            }
            catch (DllNotFoundException)
            {
                // Some provider native libraries surface a missing dependency this way rather
                // than as an OnnxRuntimeException - treated identically.
            }
            finally
            {
                // Dispose this candidate's session on every path except the one that returns it:
                // a successful candidate's session must survive to be returned to the caller, but
                // any other path - the two expected "try next candidate" exceptions above, or any
                // other exception the probe or session construction might throw - must not leak
                // the native session handle. Exception types other than the two caught above are
                // deliberately not swallowed: they propagate to the caller once this candidate's
                // session has been disposed.
                if (!succeeded)
                {
                    session?.Dispose();
                }
            }
        }


        // The CPU provider ships inside the base package and needs no explicit append - an
        // InferenceSession created with default SessionOptions already uses it.
        return new InferenceSession(modelPath);
    }
}
