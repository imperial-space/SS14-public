using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>/obj/item/organ/body_egg/changeling_egg в мёртвом теле.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingEggComponent : Component
{
    /// <summary>origin: разум генокрада.</summary>
    [ViewVariables]
    public EntityUid? Origin;

    /// <summary>hatch_time: EGG_INCUBATION_TIME = 4 минуты.</summary>
    [ViewVariables]
    public TimeSpan HatchAt;
}
