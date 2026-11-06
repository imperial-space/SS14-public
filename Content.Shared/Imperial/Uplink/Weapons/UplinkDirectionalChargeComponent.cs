using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Uplink.Weapons;

/// <summary>
/// Направленный заряд X-4: если приклеен к плотному объекту (стене, окну), взрыв уходит от поставившего,
/// а сам объект получает удар как от сильного взрыва (EX_ACT heavy).
/// </summary>
[RegisterComponent]
public sealed partial class UplinkDirectionalChargeComponent : Component
{
    /// <summary>Ключ триггера, по которому происходит взрыв.</summary>
    [DataField]
    public string KeyIn = "timer";

    /// <summary>На сколько тайлов за цель смещается эпицентр направленного взрыва.</summary>
    [DataField]
    public float Offset = 2f;

    [DataField]
    public float TotalIntensity = 300f;

    [DataField]
    public float IntensitySlope = 3f;

    [DataField]
    public float MaxIntensity = 12f;

    /// <summary>Урон по объекту, к которому приклеен заряд.</summary>
    [DataField]
    public float TargetStructuralDamage = 600f;

    [ViewVariables]
    public EntityUid? Target;

    [ViewVariables]
    public System.Numerics.Vector2 Direction;
}
