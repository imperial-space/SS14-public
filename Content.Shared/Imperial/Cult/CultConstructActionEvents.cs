using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cult;

public sealed partial class CultConstructSpawnItemActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId Prototype;
}

public sealed partial class CultConstructSpawnStructureActionEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public EntProtoId Prototype;
}

public sealed partial class CultConstructHealTargetActionEvent : EntityTargetActionEvent { }

public sealed partial class CultConstructCreateFloorActionEvent : WorldTargetActionEvent { }