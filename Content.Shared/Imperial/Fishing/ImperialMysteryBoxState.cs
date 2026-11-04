using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Состояние сундука (MYSTERY_BOX_*).</summary>
public enum ImperialMysteryBoxState : byte
{
    CoolingDown,
    Standby,
    Choosing,
    Presenting,
}
