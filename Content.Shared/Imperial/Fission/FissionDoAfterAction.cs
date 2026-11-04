using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

[Serializable, NetSerializable]
public enum FissionDoAfterAction : byte
{
    ChamberRaise,
    ChamberLower,
    ChamberWeld,
    ChamberRepair,
    ReactorDig,
    ReactorCrowbar,
    ReactorPlastitanium,
    ReactorPatch,
    ReactorWrench,
    ReactorWeld,
    ReactorPlasteel,
    ReactorScrew,
    ReactorCloseVent,
    ReactorControlRod,
    ReactorTerminal,
    NodeFlip,
    WasteClear,
}
