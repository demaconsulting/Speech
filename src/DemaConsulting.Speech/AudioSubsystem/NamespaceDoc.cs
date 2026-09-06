namespace DemaConsulting.Speech.AudioSubsystem;

/// <summary>
///     Cross-platform, mockable audio capture/playback device contracts and their real,
///     PortAudio-backed implementations.
/// </summary>
/// <remarks>
///     Contains the public <see cref="IAudioCaptureDevice"/>/<see cref="IAudioPlaybackDevice"/>
///     contracts and their probe/factory/selection counterparts, the <see cref="AudioFormat"/>
///     value type describing sample rate/channel count/sample format, persisted name-based
///     device selection, honest <c>Unavailable*</c> fallbacks for platforms or environments with
///     no working audio backend, and the internal PortAudio-backed implementations that isolate
///     native interop behind this namespace's public seam.
/// </remarks>
internal static class NamespaceDoc
{
}
