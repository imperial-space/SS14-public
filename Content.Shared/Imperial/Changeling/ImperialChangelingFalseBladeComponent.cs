using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>/obj/item/melee/arm_blade/false: исчезает через минуту.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingFalseBladeComponent : Component
{
    [ViewVariables]
    public TimeSpan RemoveAt;

    [ViewVariables]
    public EntityUid? Holder;
}
