using System.Linq;
using Content.Server.Polymorph.Components;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Wizard.Spells;

/// <summary>Сколько здоровья отнял soul tap (у лича и духа души нет).</summary>
[RegisterComponent]
public sealed partial class ImperialSoulTappedComponent : Component
{
    [ViewVariables]
    public float Taken;
}
