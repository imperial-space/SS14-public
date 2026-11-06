using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Дисциплины псайкеров Империума и Хаоса.</summary>
[Serializable, NetSerializable]
public enum CadiaPsyPower : byte
{
    Smite,
    Dome,
    Scream,
    Foresight,
    Suppression,
    Fury,
    Warpfire,
    Veil,
    Plague,
    Bloodboil,
    Charm,
    Portal,
}
