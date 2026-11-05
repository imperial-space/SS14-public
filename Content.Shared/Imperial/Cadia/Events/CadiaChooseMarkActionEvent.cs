using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

public sealed partial class CadiaChooseMarkActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public CadiaChaosGod God;
}
