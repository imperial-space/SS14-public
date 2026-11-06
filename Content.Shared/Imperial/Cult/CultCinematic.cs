using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cult;

/// <summary>Синематик концовки культа (datum/cinematic/*).</summary>
[Serializable, NetSerializable]
public enum CultCinematic : byte
{
    /// <summary>nuke/cult (BandaStation: флот Центкома BSA).</summary>
    Nuke,

    /// <summary>cult_arm: станция поглощена.</summary>
    Arm,

    /// <summary>cult_fail: Нар'Си убита.</summary>
    Fail,
}
