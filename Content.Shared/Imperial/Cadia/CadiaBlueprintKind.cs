using System.Numerics;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

[Serializable, NetSerializable]
public enum CadiaBlueprintKind : byte
{
    Sandbags,
    Wall,
    TankTrap,
    RazorWire,
    Hydra,
    Mine,
    Pillbox,
    AtGun,
    BolterNest,
    Stakes,
    AtMine,
    ChaosAltar,
    SpikeBarricade,
    WarpTrap,
}
