using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Масс-реактивный снаряд: сам по себе бьёт слабо, главный урон — подрыв в цели. Сила падает к краю радиуса,
/// стрелка не задевает, сквозь стену не проходит. Без движка взрывов: сотня болтеров не положит сервер.
/// </summary>
[RegisterComponent]
public sealed partial class CadiaMassReactiveComponent : Component
{
    [DataField(required: true)]
    public DamageSpecifier Blast = new();

    [DataField]
    public float Radius = 1f;

    /// <summary>Какая доля урона остаётся на краю радиуса.</summary>
    [DataField]
    public float EdgeFactor = 0.5f;

    [DataField]
    public EntProtoId Effect = "CadiaBoltBlast";

    [DataField]
    public SoundSpecifier? Sound;
}

/// <summary>Обойма с зарядом: заряжается на станции, а не в оружии.</summary>
[RegisterComponent]
public sealed partial class CadiaEnergyPackComponent : Component;

/// <summary>Зарядная станция: пополняет вставленные обоймы, по выстрелу за шаг.</summary>
[RegisterComponent]
public sealed partial class CadiaPackChargerComponent : Component
{
    [DataField]
    public List<string> Slots = new() { "pack1", "pack2", "pack3", "pack4" };

    /// <summary>Сколько выстрелов в секунду на обойму.</summary>
    [DataField]
    public int ChargePerSecond = 2;

    [DataField]
    public SoundSpecifier DoneSound = new SoundPathSpecifier("/Audio/Imperial/Cadia/Weapons/charger_done.ogg");
}

/// <summary>Оружие «греется»: плазма иногда обжигает стрелка, как ей и положено.</summary>
[RegisterComponent]
public sealed partial class CadiaGetsHotComponent : Component
{
    [DataField]
    public float Chance = 0.03f;

    [DataField(required: true)]
    public DamageSpecifier Burn = new();

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/lightburn.ogg");
}
