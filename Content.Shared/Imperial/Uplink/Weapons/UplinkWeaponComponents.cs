using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Uplink.Weapons;

/// <summary>
/// Свойства пуль TG, которых нет в SS14: armour_penetration, weak_against_armour и эффекты on_hit.
/// </summary>
[RegisterComponent]
public sealed partial class UplinkProjectileComponent : Component
{
    /// <summary>Доля брони, которую пуля игнорирует (armour_penetration / 100).</summary>
    [DataField]
    public float ArmorPenetration;

    /// <summary>weak_against_armour: броня действует вдвое сильнее.</summary>
    [DataField]
    public bool WeakAgainstArmor;

    /// <summary>Сколько огненных стаков добавить цели (fire_stacks), с поджогом.</summary>
    [DataField]
    public float FireStacks;

    /// <summary>Радиус очагов огня вокруг цели (hotspot в RANGE_TURFS).</summary>
    [DataField]
    public int HotspotRadius = -1;

    /// <summary>Принудительный сон цели.</summary>
    [DataField]
    public TimeSpan Sleep = TimeSpan.Zero;

    [DataField]
    public TimeSpan Knockdown = TimeSpan.Zero;

    [DataField]
    public TimeSpan Stun = TimeSpan.Zero;

    /// <summary>ЭМИ в точке попадания.</summary>
    [DataField]
    public float EmpRange;

    [DataField]
    public float EmpEnergy = 50000f;

    [DataField]
    public TimeSpan EmpDuration = TimeSpan.FromSeconds(15);

    /// <summary>projectile_phasing: пролетает сквозь всё, кроме живых существ.</summary>
    [DataField]
    public bool Phasing;

    /// <summary>Сколько добавить к кровотечению цели (скорость, максимум 10).</summary>
    [DataField]
    public float Bleed;

    /// <summary>bleed(): мгновенная потеря крови (Paradise, снайперские «Шреддеры»).</summary>
    [DataField]
    public float BloodLoss;

    /// <summary>ex_act() по не-мобам: пуля взрывает постройки в точке попадания (снайперские HE).</summary>
    [DataField]
    public bool StructureExplosion;
}

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

/// <summary>
/// Смарт-ПП Abielle (/obj/item/gun/ballistic/automatic/smartgun, c160smart/ready_proj):
/// пули захватывают цель в момент выстрела — то, по чему кликнули, а при клике по полу ближайшее живое существо
/// в пределах auto_lock_range; на цели вспыхивает голографическое перекрестие.
/// </summary>
[RegisterComponent]
public sealed partial class UplinkSmartgunComponent : Component
{
    /// <summary>auto_lock_range.</summary>
    [DataField]
    public float AutoLockRange = 2f;

    /// <summary>/obj/effect/temp_visual/smartgun_target.</summary>
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId Reticle = "ImperialSmartgunReticle";
}

/// <summary>
/// Силовой кулак: газовый поршень добавляет урон и отбрасывает цель.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PowerFistComponent : Component
{
    [DataField]
    public string TankSlot = "gas_tank";

    /// <summary>1 — низкое, 2 — среднее, 3 — высокое давление.</summary>
    [DataField, AutoNetworkedField]
    public int Pressure = 1;

    /// <summary>gas_per_fist — молей за удар на единицу давления.</summary>
    [DataField]
    public float GasPerFist = 3f;

    /// <summary>force кулака.</summary>
    [DataField]
    public float Force = 20f;
}

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

/// <summary>
/// Кейс оружия агента (toolbox/guncase/traitor): Alt-ПКМ взводит бомбу уничтожения улик,
/// через 5 секунд кейс взрывается вместе с содержимым.
/// </summary>
[RegisterComponent]
public sealed partial class UplinkEvidenceBombComponent : Component
{
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public TimeSpan? DetonateAt;
}

[Robust.Shared.Serialization.NetSerializable, Serializable]
public enum UplinkEvidenceBombVisuals : byte
{
    Exploding,
}
