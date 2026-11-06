using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>category способности в магазине эволюции.</summary>
public enum ImperialChangelingCategory : byte
{
    Stings,
    Combat,
    Stealth,
    Utility,
}
