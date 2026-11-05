using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Ручка СБ: голограмма «СТОЯТЬ» над человеком.</summary>
[RegisterComponent]
public sealed partial class ImperialSecurityPenComponent : Component
{
    [DataField]
    public EntProtoId Hologram = "ImperialSecurityPenHolo";

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public TimeSpan NextUse;
}
