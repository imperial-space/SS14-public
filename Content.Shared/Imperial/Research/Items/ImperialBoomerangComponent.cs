using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Бумеранг (baton/security/boomerang): после броска возвращается к бросившему.</summary>
[RegisterComponent]
public sealed partial class ImperialBoomerangComponent : Component
{
    [DataField]
    public float ReturnRange = 10f;

    [DataField]
    public float ReturnSpeed = 10f;

    [ViewVariables]
    public bool Returning;
}
