using Content.Shared.Imperial.Cult.Components;
using Robust.Shared.Serialization;
using Robust.Shared.ViewVariables;

namespace Content.Server.Imperial.Cult.Components;


/// <summary>
/// Сдвигатель вуали — телепортирует владельца вперёд на 1–9 тайлов. 4 заряда.
/// </summary>
[RegisterComponent]
public sealed partial class CultVeilShifterComponent : Component
{
    [DataField] public int Charges = 4;
    [DataField] public int MinTiles = 1;
    [DataField] public int MaxTiles = 9;
}

/// <summary>
/// Проклятая сфера — задерживает прибытие эвакуационного шаттла на 3 минуты. 2 использования.
/// </summary>
[RegisterComponent]
public sealed partial class CultCursedOrbComponent : Component
{
    [DataField] public int UsesLeft = 2;
    [DataField] public TimeSpan DelayAmount = TimeSpan.FromMinutes(3);
}

/// <summary>
/// Жуткое точило — заостряет оружие ближнего боя, добавляя +5 к урону. Одно использование.
/// </summary>
[RegisterComponent]
public sealed partial class CultWhetstoneComponent : Component
{
    [DataField] public float DamageBonus = 5f;
    [DataField] public bool Used = false;
}

/// <summary>
/// Маркер: это оружие уже заточено культовым точилом.
/// </summary>
[RegisterComponent]
public sealed partial class CultSharpenedComponent : Component { }

/// <summary>
/// Флакон проклятой воды — лечит культиста при использовании (по 20 Brute и Burn).
/// </summary>
[RegisterComponent]
public sealed partial class CultUnholyFlaskComponent : Component
{
    [DataField] public int UsesLeft = 1;
    [DataField] public float HealAmount = 20f;
}

/// <summary>
/// Пустая оболочка — принимает заполненный камень душ для создания конструкта.
/// </summary>
[RegisterComponent]
public sealed partial class CultEmptyShellComponent : Component { }

/// <summary>
/// Повязка зилота — при надевании добавляет медицинский HUD (ShowHealthBars).
/// </summary>
[RegisterComponent]
public sealed partial class CultZealotBlindfoldComponent : Component
{
    [ViewVariables] public bool AddedHealthBars;
    [ViewVariables] public bool HadEyeState;
    [ViewVariables] public bool PreviousDrawLight = true;
}

/// <summary>
/// Кровавая сфера — переносимый контейнер зарядов Кровавого обряда.
/// </summary>
[RegisterComponent]
public sealed partial class CultBloodOrbComponent : Component
{
    [DataField] public int Charges = 50;
}

[RegisterComponent]
public sealed partial class CultSoulStoneMarkerComponent : Component { }

/// <summary>
/// Маркер копья крови и его изначального владельца.
/// </summary>
[RegisterComponent]
public sealed partial class CultBloodSpearComponent : Component
{
    [ViewVariables] public EntityUid OwnerUid;
    [ViewVariables] public EntityUid? RecallAction;
}
