using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared.Imperial.Heretic.Paths.Cosmos;

[RegisterComponent, AutoGenerateComponentPause, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticStarGazerComponent : Component
{
    /// <summary>Мастер, которому принадлежит Созерцатель.</summary>
    [DataField] public EntityUid? Master;

    // Регенерация HP
    [DataField] public float HealInterval = 1f;
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
    [DataField] public float BeamWindUpDuration = 3.0f;
    [ViewVariables]
    [AutoNetworkedField] public bool BeamWindUp;
    [ViewVariables, AutoPausedField]
    public TimeSpan BeamWindUpEnd;
    [ViewVariables]
    public bool BeamVisualShown;
    [ViewVariables]
    public EntityUid? BeamDeathGazeAction;

    // Звёздный взгляд — фаза канала
    [DataField] public float BeamChannelDuration = 9.9f;
    [DataField] public float BeamDmgInterval = 0.3f;
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
}
