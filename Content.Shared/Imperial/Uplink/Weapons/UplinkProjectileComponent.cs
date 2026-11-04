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
