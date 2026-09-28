using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using System.Numerics;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticStarGazerComponent : Component
{
    /// <summary>Мастер, которому принадлежит Созерцатель.</summary>
    [DataField] public EntityUid? Master;

    // Регенерация HP
    [DataField] public float HealInterval = 1f;
    [DataField] public float HealAmount = 5f;
    public float HealAccum;

    // Star Blast — активный снаряд
    public EntityUid? ActiveProjectile;
    public EntityUid? StarBlastAction;

    // Звёздный взгляд — фаза раскрутки (wind-up)
    // 3.0с всего: визуал появляется при 0.8с до конца (t=2.2с), урон с t=3.0с
    [DataField] public float BeamWindUpDuration = 3.0f;
    [AutoNetworkedField] public bool BeamWindUp;
    public float BeamWindUpTimer;
    public bool BeamVisualShown;
    public EntityUid? BeamDeathGazeAction;

    // Звёздный взгляд — фаза канала
    [DataField] public float BeamChannelDuration = 9.9f;
    [DataField] public float BeamDmgInterval = 0.3f;
    [DataField] public int BeamRange = 20;
    [AutoNetworkedField] public bool BeamChanneling;
    public float BeamChannelTimer;
    public float BeamDmgAccum;

    // Зафиксированная начальная позиция и направление луча
    public Vector2 BeamStartPos;
    public Angle BeamStartRot;

    // Орб зарядки
    public EntityUid? BeamChargeOrb;

    // Последняя позиция курсора, полученная от клиента
    public MapCoordinates? BeamCursorPos;
}
