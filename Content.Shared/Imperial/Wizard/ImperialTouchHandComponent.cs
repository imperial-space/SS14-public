using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Рука заклинания касания (/obj/item/melee/touch_attack).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialTouchHandComponent : Component
{
    [ViewVariables]
    public EntityUid? Spell;
}
