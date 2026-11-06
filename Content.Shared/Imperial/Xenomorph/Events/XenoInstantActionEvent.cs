using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Xenomorph;

/// <summary>Действие ксеноморфа с ценой в плазме (cooldown/alien с plasma_cost).</summary>
public abstract partial class XenoInstantActionEvent : InstantActionEvent
{
    [DataField]
    public float PlasmaCost;
}
