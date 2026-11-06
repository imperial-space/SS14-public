using System.Numerics;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Оружие техники: снаряд, скорость, разброс, темп или перезарядка, боезапас и звук.</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class CadiaVehicleWeapon
{
    [DataField(required: true)]
    public string Name = string.Empty;

    [DataField(required: true)]
    public EntProtoId Projectile;

    [DataField]
    public float Speed = 25f;

    /// <summary>Разброс выстрела в градусах.</summary>
    [DataField]
    public float Spread = 2f;

    /// <summary>Пауза между выстрелами (у пушки — перезарядка, у пулемёта — темп).</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(0.2);

    /// <summary>Стреляет, пока зажата кнопка.</summary>
    [DataField]
    public bool Automatic;

    [DataField]
    public int Ammo = 100;

    [DataField]
    public int MaxAmmo = 100;

    [DataField]
    public SoundSpecifier? Sound;

    /// <summary>Встряска камеры экипажа при выстреле.</summary>
    [DataField]
    public float Kick;

    /// <summary>Каждый выстрел заряжается вручную: снаряд из боеукладки в казённик.</summary>
    [DataField]
    public bool Breech;

    /// <summary>Навесной огонь: снаряд падает на точку под прицелом, над ней загорается метка удара.</summary>
    [DataField]
    public bool Indirect;

    [DataField]
    public float IndirectRange = 28f;

    /// <summary>Сила взрыва навесного снаряда.</summary>
    [DataField]
    public float IndirectIntensity = 120f;
}
