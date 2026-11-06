using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Сущность в прыжке-захвате.</summary>
[RegisterComponent]
public sealed partial class ImperialTacklingComponent : Component
{
    [ViewVariables]
    public EntityUid Gloves;
}
