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

[Serializable, NetSerializable]
public sealed class CultCinematicEvent(CultCinematic cinematic) : EntityEventArgs
{
    public readonly CultCinematic Cinematic = cinematic;
}

/// <summary>started_narsie_summon / failed_narsie_summon: звёздный свет тускнеет и краснеет.</summary>
[Serializable, NetSerializable]
public sealed class CultStarlightEvent(bool summoning) : EntityEventArgs
{
    public readonly bool Summoning = summoning;
}

/// <summary>Музыка ритуала Нар'Си: один раз за раунд при первом черчении руны Нар'Си.</summary>
[Serializable, NetSerializable]
public sealed class CultRitualMusicEvent : EntityEventArgs;
