namespace DemaConsulting.Speech.ModelManagementSubsystem;

/// <summary>
///     Where a reference voice found by <see cref="ReferenceVoiceLibrary"/> came from.
/// </summary>
public enum ReferenceVoiceSource
{
    /// <summary>A clip from a directory the caller named explicitly (for example a <c>--voices-dir</c> option).</summary>
    Directory,

    /// <summary>A clip the user dropped into the library's <c>user</c> folder.</summary>
    User,

    /// <summary>A clip extracted from an installed <see cref="ReferenceVoicePack"/>.</summary>
    Pack,
}
