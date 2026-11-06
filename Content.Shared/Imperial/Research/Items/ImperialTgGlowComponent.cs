using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Временная подсветка от вспышки .38 Flare.</summary>
[RegisterComponent]
public sealed partial class ImperialTgGlowComponent : Component
{
    [ViewVariables]
    public TimeSpan EndTime;

    [ViewVariables]
    public bool AddedLight;
}
