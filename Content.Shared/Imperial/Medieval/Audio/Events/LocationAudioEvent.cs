using System;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Audio.Events;

/// <summary>
/// Событие для воспроизведения основного звука локации
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayLocationSoundEvent : EntityEventArgs
{
    public string SoundPath { get; }
    public bool Loop { get; }

    public PlayLocationSoundEvent(string soundPath, bool loop = true)
    {
        SoundPath = soundPath;
        Loop = loop;
    }
}

/// <summary>
/// Событие для воспроизведения случайного звука локации
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayRandomLocationSoundEvent : EntityEventArgs
{
    public string SoundPath { get; }

    public PlayRandomLocationSoundEvent(string soundPath)
    {
        SoundPath = soundPath;
    }
}

/// <summary>
/// Событие для остановки всех локационных звуков
/// </summary>
[Serializable, NetSerializable]
public sealed class StopAllLocationAudioEvent : EntityEventArgs
{
}
