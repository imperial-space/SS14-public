using Content.Shared.Actions;
using Content.Shared.Flash.Components;
using Content.Shared.StatusEffect;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Illitid;

public sealed partial class IllitidThoughtActionEvent : EntityTargetActionEvent
{
}

public sealed partial class IllitidMassThoughtActionEvent : InstantActionEvent
{
}

public sealed partial class IllitidForceSayActionEvent : EntityTargetActionEvent
{
}

public sealed partial class IllitidBlindnessActionEvent : EntityTargetActionEvent
{
}

public abstract class SharedIllitidSystem : EntitySystem
{
    public ProtoId<StatusEffectPrototype> IllitidFlashedKey = "IllitidFlashed";
}
