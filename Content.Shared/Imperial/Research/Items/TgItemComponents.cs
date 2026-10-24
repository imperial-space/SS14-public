using Content.Shared.Actions;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

#region Крафт TG

/// <summary>
/// Предмет, из которого по рецептам TG (code/datums/components/crafting) собирается что-то новое:
/// наборы деталей оружия, технологический патрон. Нужные предметы ищутся в руках и рядом с игроком.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialTgCraftComponent : Component
{
    [DataField(required: true)]
    public List<ImperialTgCraftRecipe> Recipes = new();

    /// <summary>Радиус поиска предметов вокруг игрока.</summary>
    [DataField]
    public float Range = 1.5f;
}

[DataDefinition]
public sealed partial class ImperialTgCraftRecipe
{
    /// <summary>Ключ локализации названия рецепта.</summary>
    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public EntProtoId Result;

    [DataField]
    public List<ImperialTgCraftItem> Items = new();

    [DataField]
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> Reagents = new();

    /// <summary>Время сборки в секундах.</summary>
    [DataField]
    public float Time = 1f;
}

[DataDefinition]
public sealed partial class ImperialTgCraftItem
{
    /// <summary>Подходящие прототипы (с учётом наследников).</summary>
    [DataField]
    public List<EntProtoId> Protos = new();

    [DataField]
    public ProtoId<TagPrototype>? Tag;

    [DataField]
    public int Count = 1;

    /// <summary>Ключ локализации названия требования для подсказки.</summary>
    [DataField(required: true)]
    public LocId Name;
}

[Serializable, NetSerializable]
public sealed partial class ImperialTgCraftDoAfterEvent : SimpleDoAfterEvent
{
    public int Recipe;

    public ImperialTgCraftDoAfterEvent(int recipe)
    {
        Recipe = recipe;
    }
}

#endregion

#region Бойки

[Serializable, NetSerializable]
public enum ImperialFiringPinMode : byte
{
    /// <summary>firing_pin/implant/mindshield: только с имплантом защиты разума.</summary>
    Mindshield,
    /// <summary>firing_pin/test_range: только рядом с мишенями тира.</summary>
    TestRange,
    /// <summary>firing_pin/explorer: не стреляет на станции.</summary>
    Explorer,
}

/// <summary>Боёк TG: вставляется в оружие кликом.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialFiringPinComponent : Component
{
    [DataField(required: true)]
    public ImperialFiringPinMode Mode;
}

/// <summary>Оружие с бойком TG.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialFiringPinnedComponent : Component
{
    [DataField, AutoNetworkedField]
    public ImperialFiringPinMode Mode;
}

#endregion

#region Капкан, бумеранг, снаряды

/// <summary>Медвежий капкан (restraints/legcuffs/beartrap): взводится в руке, срабатывает при наступании.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialBearTrapComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Armed;

    [DataField]
    public float Damage = 20f;

    [DataField]
    public float ArmTime = 2f;
}

[Serializable, NetSerializable]
public enum ImperialBearTrapVisuals : byte
{
    Armed,
}

[Serializable, NetSerializable]
public sealed partial class ImperialBearTrapArmDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Бумеранг (baton/security/boomerang): после броска возвращается к бросившему.</summary>
[RegisterComponent]
public sealed partial class ImperialBoomerangComponent : Component
{
    [DataField]
    public float ReturnRange = 10f;

    [DataField]
    public float ReturnSpeed = 10f;

    [ViewVariables]
    public bool Returning;
}

/// <summary>Особые эффекты пули TG при попадании.</summary>
[RegisterComponent]
public sealed partial class ImperialTgProjectileEffectsComponent : Component
{
    /// <summary>Целевая температура тела (Iceblox: 100 K).</summary>
    [DataField]
    public float? BodyTemperature;

    /// <summary>Имплант, вживляемый цели, если его ещё нет (TRAC).</summary>
    [DataField]
    public EntProtoId? Implant;

    /// <summary>Подсветка цели (Flare), секунды.</summary>
    [DataField]
    public float GlowTime;

    [DataField]
    public Color GlowColor = Color.FromHex("#ff9933");

    /// <summary>Удар током (тесла-пушка).</summary>
    [DataField]
    public int Shock;

    /// <summary>Отбрасывание на тайлы (метеоритная пуля).</summary>
    [DataField]
    public float Knockback;

    /// <summary>Что появляется на месте попадания (разрыв реальности «Горизонта событий»).</summary>
    [DataField]
    public EntProtoId? SpawnOnHit;
}

/// <summary>Временная подсветка от вспышки .38 Flare.</summary>
[RegisterComponent]
public sealed partial class ImperialTgGlowComponent : Component
{
    [ViewVariables]
    public TimeSpan EndTime;

    [ViewVariables]
    public bool AddedLight;
}

#endregion

#region Перчатки для захвата

/// <summary>
/// Перчатки для захвата TG (gloves/tackler): действие «Бросок» — прыжок к цели, сбивающий её с ног.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialTacklerComponent : Component
{
    [DataField]
    public EntProtoId Action = "ImperialActionTackle";

    [ViewVariables]
    public EntityUid? ActionEntity;

    /// <summary>Дальность прыжка (tackle_range).</summary>
    [DataField]
    public float Range = 4f;

    /// <summary>Скорость прыжка (tackle_speed).</summary>
    [DataField]
    public float Speed = 8f;

    /// <summary>Длительность сбивания цели (base_knockdown), секунды.</summary>
    [DataField]
    public float Knockdown = 1f;

    /// <summary>Бонус к броску (skill_mod): больше — чаще цель падает, а сам прыгун остаётся на ногах.</summary>
    [DataField]
    public int SkillMod = 1;

    /// <summary>Урон стамине прыгуна (stamina_cost).</summary>
    [DataField]
    public float StaminaCost = 25f;

    /// <summary>Шанс врезаться при промахе (rocket gloves).</summary>
    [DataField]
    public float CrashChance;
}

/// <summary>Сущность в прыжке-захвате.</summary>
[RegisterComponent]
public sealed partial class ImperialTacklingComponent : Component
{
    [ViewVariables]
    public EntityUid Gloves;
}

public sealed partial class ImperialTackleActionEvent : WorldTargetActionEvent;

#endregion

#region Прочее СБ

/// <summary>N-spect сканер: проверка цели на контрабанду с распечаткой отчёта.</summary>
[RegisterComponent]
public sealed partial class ImperialInspectorComponent : Component
{
    [DataField]
    public float Range = 5f;

    [DataField]
    public EntProtoId Report = "Paper";

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(5);
}

[Serializable, NetSerializable]
public sealed partial class ImperialInspectorDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Ручка СБ: голограмма «СТОЯТЬ» над человеком.</summary>
[RegisterComponent]
public sealed partial class ImperialSecurityPenComponent : Component
{
    [DataField]
    public EntProtoId Hologram = "ImperialSecurityPenHolo";

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public TimeSpan NextUse;
}

/// <summary>
/// Корпус химгранаты TG: нагревает содержимое при срабатывании, а «улучшенный» выпускает по частям.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialTgGrenadeCasingComponent : Component
{
    /// <summary>ignition_temp: на сколько кельвинов меняется температура смеси.</summary>
    [DataField]
    public float TemperatureDelta;

    /// <summary>unit_spread у adv_release: объём одного выброса. 0 — всё сразу.</summary>
    [DataField]
    public float ReleaseUnits;

    [DataField]
    public float ReleaseDelay = 5f;

    [ViewVariables]
    public Dictionary<EntityUid, Content.Shared.Chemistry.Components.Solution> Stash = new();
}

#endregion
