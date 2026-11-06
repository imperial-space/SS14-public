using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

[Serializable, NetSerializable]
public enum ImperialFiringPinMode : byte
{
    /// <summary>firing_pin/implant/mindshield: только с имплантом защиты разума.</summary>
    Mindshield,
    /// <summary>firing_pin/test_range: только рядом с мишенями тира.</summary>
    TestRange,
    /// <summary>firing_pin/explorer: не стреляет на станции.</summary>
    Explorer,
}
