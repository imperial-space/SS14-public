using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Особая блесна: что именно она ловит (is_catchable_fish).</summary>
public enum ImperialLureKind : byte
{
    None,
    Minnow,
    Plug,
    Dropping,
    Spoon,
    ArtificialFly,
    Led,
    LuckyCoin,
    Algae,
    Grub,
    Buzzbait,
    Spinnerbait,
    DaisyChain,
}
