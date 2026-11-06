using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// item/crusher_trophy.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CrusherTrophyComponent : Component
{
    [DataField(required: true)]
    public CrusherTrophyKind Trophy;

    /// <summary>bonus_value.</summary>
    [DataField]
    public float BonusValue = 10f;

    /// <summary>denied_type: два трофея одной группы не ставятся вместе.</summary>
    [DataField(required: true)]
    public string Group = string.Empty;

    /// <summary>effect_desc().</summary>
    [DataField(required: true)]
    public LocId Effect;

    /// <summary>blaster_tubes deadly_shot.</summary>
    [ViewVariables]
    public bool DeadlyShot;

    [ViewVariables]
    public TimeSpan DeadlyShotReset;

    /// <summary>Перезарядка эффекта трофея (bileworm, ice_demon_cube).</summary>
    [ViewVariables]
    public TimeSpan CooldownEnd;

    /// <summary>Перезарядка использования в руке (broodmother_tongue, legionnaire_spine).</summary>
    [ViewVariables]
    public TimeSpan UseCooldownEnd;

    [DataField]
    public TimeSpan TrophyUseInHandBroodmotherTongueDelay = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan UseCooldown = TimeSpan.FromSeconds(60);

    [DataField]
    public TimeSpan UseCooldown2 = TimeSpan.FromSeconds(4);
}
