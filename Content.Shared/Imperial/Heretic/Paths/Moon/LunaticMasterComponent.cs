using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Actions;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.Paths.Moon;

/// <summary>datum/antagonist/lunatic/master: вознёсшийся еретик Луны, Постановщик.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LunaticMasterComponent : Component
{
    [DataField]
    public ProtoId<FactionIconPrototype> StatusIcon = "LunaticMasterFaction";
}
