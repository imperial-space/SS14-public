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

/// <summary>shapeshift/wizard: облик выбирается один раз, повторный каст возвращает обратно.</summary>
[RegisterComponent]
public sealed partial class ImperialShapeshiftSpellComponent : Component
{
    /// <summary>possible_shapes: существо → прототип превращения.</summary>
    [DataField(required: true)]
    public List<EntProtoId> Shapes = new();

    [ViewVariables]
    public EntProtoId? Chosen;
}
