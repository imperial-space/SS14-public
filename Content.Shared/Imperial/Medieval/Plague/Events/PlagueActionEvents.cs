using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Plague;

public sealed partial class OpenPlagueEvolutionMenuActionEvent : InstantActionEvent;

public sealed partial class InfectTargetActionEvent : EntityTargetActionEvent;

public sealed partial class PlagueTeleportInfectedActionEvent : InstantActionEvent;

public sealed partial class PlagueTeleportNotInfectedActionEvent : InstantActionEvent;

public sealed partial class PlaguePolymorphMouseActionEvent : InstantActionEvent
{
    [DataField]
    public int Cost = 0;
}

[ImplicitDataDefinitionForInheritors]
public abstract partial class BasePlagueActionEvent : EntityTargetActionEvent
{
    [DataField]
    public int Cost = 0;

    [DataField]
    public bool AllowIncubation = false;
}

public sealed partial class PlagueForcedVomitActionEvent : BasePlagueActionEvent;

public sealed partial class PlagueAsthmaticActionEvent : BasePlagueActionEvent;

public sealed partial class PlagueDizzinessActionEvent : BasePlagueActionEvent;

public sealed partial class PlagueSleepyActionEvent : BasePlagueActionEvent;

public sealed partial class PlagueInjuryActionEvent : BasePlagueActionEvent;

public sealed partial class PlagueCataractActionEvent : BasePlagueActionEvent;

public sealed partial class PlagueHeartAttackActionEvent : BasePlagueActionEvent;

public sealed partial class PlagueBreakImmunityActionEvent : EntityTargetActionEvent
{
    [DataField]
    public int Cost = 0;
}

public sealed partial class PlagueSpawnEntityActionEvent : WorldTargetActionEvent
{
    [DataField]
    public EntProtoId Prototype = string.Empty;

    [DataField]
    public int Cost = 0;
}
