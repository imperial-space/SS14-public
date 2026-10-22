using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cult;

// Совместимость: эти события используются неимперским Resources/Prototypes/Actions/cult.yml.
// Новый культ (1:1 с SS13) использует собственные действия из Imperial/Cult, а эти просто
// перенаправляются на эквивалентное поведение, если старое действие всё же выдано.

public sealed partial class CultCommuneActionEvent : InstantActionEvent;
public sealed partial class CultStunActionEvent : InstantActionEvent;
public sealed partial class CultShacklesActionEvent : InstantActionEvent;
public sealed partial class CultTeleportActionEvent : InstantActionEvent;
public sealed partial class CultEmpActionEvent : InstantActionEvent;
public sealed partial class CultTwistedConstructionActionEvent : InstantActionEvent;
public sealed partial class CultSummonDaggerActionEvent : InstantActionEvent;
public sealed partial class CultSummonEquipmentActionEvent : InstantActionEvent;
public sealed partial class CultConcealPresenceActionEvent : InstantActionEvent;
public sealed partial class CultBloodRitesActionEvent : InstantActionEvent;
public sealed partial class CultRecallBloodSpearActionEvent : InstantActionEvent;
public sealed partial class CultBloodMagicActionEvent : InstantActionEvent;
public sealed partial class CultDarkSpiritReturnActionEvent : InstantActionEvent;
public sealed partial class CultDarkSpiritCommuneActionEvent : InstantActionEvent;

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

public sealed partial class CultConstructHealTargetActionEvent : EntityTargetActionEvent;

public sealed partial class CultConstructCreateFloorActionEvent : WorldTargetActionEvent;
