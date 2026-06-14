namespace VrAudioSwitcher.Audio;

/// <summary>
/// A serializable, UI-friendly description of an audio endpoint.
/// <see cref="Id"/> is the stable Windows endpoint id used for matching/applying
/// (the friendly <see cref="Name"/> can change with port/format and must not be
/// used as a key).
/// </summary>
public sealed record AudioDeviceInfo(string Id, string Name, bool IsCapture)
{
    public override string ToString() => Name;
}
