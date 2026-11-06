using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>TOOL_* TG, которыми обрабатывают еду.</summary>
public enum ImperialKitchenTool : byte
{
    Knife,
    Saw,
    RollingPin,
    Scalpel,
}
