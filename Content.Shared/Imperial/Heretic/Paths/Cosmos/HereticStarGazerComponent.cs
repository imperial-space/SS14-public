using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Content.Shared.Damage;

namespace Content.Shared.Imperial.Heretic.Paths.Cosmos;

[RegisterComponent, AutoGenerateComponentPause, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticStarGazerComponent : Component
{
    /// <summary>Мастер, которому принадлежит Созерцатель.</summary>
    [DataField] public EntityUid? Master;

    // Регенерация HP
    [DataField] public TimeSpan HealInterval = TimeSpan.FromSeconds(1f);
    [DataField] public float HealAmount = 5f;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextHeal;

    // Star Blast — активный снаряд
    [ViewVariables]
    public EntityUid? ActiveProjectile;
    [ViewVariables]
    public EntityUid? StarBlastAction;

    // Звёздный взгляд — фаза раскрутки (wind-up)
    // 3.0с всего: визуал появляется при 0.8с до конца (t=2.2с), урон с t=3.0с
    [DataField] public TimeSpan BeamWindUpDuration = TimeSpan.FromSeconds(3.0f);
    [ViewVariables]
    [AutoNetworkedField] public bool BeamWindUp;
    [ViewVariables, AutoPausedField]
    public TimeSpan BeamWindUpEnd;
    [ViewVariables]
    public bool BeamVisualShown;
    [ViewVariables]
    public EntityUid? BeamDeathGazeAction;

    // Звёздный взгляд — фаза канала
    [DataField] public TimeSpan BeamChannelDuration = TimeSpan.FromSeconds(9.9f);
    [DataField] public TimeSpan BeamDmgInterval = TimeSpan.FromSeconds(0.3f);
    [DataField] public int BeamRange = 20;
    [ViewVariables]
    [AutoNetworkedField] public bool BeamChanneling;
    [ViewVariables, AutoPausedField]
    public TimeSpan BeamChannelEnd;
    [ViewVariables, AutoPausedField]
    public TimeSpan NextBeamDmg;

    // Зафиксированная начальная позиция и направление луча
    [ViewVariables]
    public Vector2 BeamStartPos;
    [ViewVariables]
    public Angle BeamStartRot;

    // Орб зарядки
    [ViewVariables]
    public EntityUid? BeamChargeOrb;

    // Последняя позиция курсора, полученная от клиента
    [ViewVariables]
    public MapCoordinates? BeamCursorPos;

    [DataField]
    public DamageSpecifier BurnDamage = new() { DamageDict = { ["Heat"] = 30 } };

    [DataField]
    public DamageSpecifier WallDamage = new() { DamageDict = { ["Heat"] = 10000 } };

    [DataField]
    public DamageSpecifier DiedBluntDamage = new() { DamageDict = { ["Blunt"] = 200 } };

    [DataField]
    public TimeSpan CosmicExpansionDuration = TimeSpan.FromSeconds(30);

    [DataField]
    public TimeSpan ShootStarBlastDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan CancelBeamDelay = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan CancelBeamCooldown = TimeSpan.FromSeconds(1);

    [DataField]
    public float CosmicExpansionRange = 7f;

    [DataField]
    public float FireBeamTickChance = 0.02f;

    [DataField]
    public float FireBeamTickRange = 0.6f;

    [DataField]
    public float PullTowardsRange = 5f;
}
