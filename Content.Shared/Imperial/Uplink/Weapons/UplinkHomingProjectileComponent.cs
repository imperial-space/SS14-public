using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Uplink.Weapons;

/// <summary>
/// Самонаводящаяся пуля (homing_turn_speed): доворачивает к цели, на которую целился стрелок.
/// </summary>
[RegisterComponent]
public sealed partial class UplinkHomingProjectileComponent : Component
{
    /// <summary>Скорость доворота, градусов в секунду.</summary>
    [DataField]
    public float TurnSpeed = 360f;

    /// <summary>Случайный разброс точки наведения, в тайлах.</summary>
    [DataField]
    public float Inaccuracy;

    /// <summary>homing_inaccuracy_min/max: смещение точки наведения, в тайлах (выбирается один раз на пулю).</summary>
    [DataField]
    public float InaccuracyMin;

    [DataField]
    public float InaccuracyMax;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public bool TargetResolved;

    /// <summary>homing_offset_x/y.</summary>
    [ViewVariables]
    public System.Numerics.Vector2 Offset;
}
