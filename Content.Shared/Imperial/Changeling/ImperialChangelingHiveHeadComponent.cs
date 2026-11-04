using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>/obj/item/clothing/head/helmet/changeling_hivehead.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingHiveHeadComponent : Component
{
    /// <summary>spawn_count.</summary>
    [DataField]
    public int SpawnCount = 6;

    /// <summary>/mob/living/basic/bee/timed/short: lifespan.</summary>
    [DataField]
    public TimeSpan Lifespan = TimeSpan.FromSeconds(25);
}
