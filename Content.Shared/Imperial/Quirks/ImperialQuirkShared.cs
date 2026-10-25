using Content.Shared.Roles;
using Content.Shared.Traits;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Quirks;

/// <summary>
/// /datum/quirk из TG. Имя, описание и стоимость (value) лежат в черте с тем же id в категории ImperialQuirks,
/// здесь — что причуда делает при появлении персонажа.
/// </summary>
[Prototype]
public sealed partial class ImperialQuirkPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Компоненты, которые получает носитель (mob_trait и add()).</summary>
    [DataField]
    public ComponentRegistry Components = new();

    /// <summary>Особые эффекты как у черт и профессий (статусы и т. п.).</summary>
    [DataField(serverOnly: true)]
    public List<JobSpecial> Specials = new();

    /// <summary>Предметы в рюкзак, иначе в руки, иначе под ноги (give_item_to_holder).</summary>
    [DataField]
    public List<EntProtoId> Items = new();

    /// <summary>Предметы сразу в слот (очки, повязка, перчатки); занятый слот освобождается в рюкзак.</summary>
    [DataField]
    public Dictionary<string, EntProtoId> Equip = new();

    /// <summary>GLOB.quirk_blacklist: причуды, которые нельзя взять вместе с этой.</summary>
    [DataField]
    public List<ProtoId<TraitPrototype>> Incompatible = new();

    /// <summary>gain_text.</summary>
    [DataField]
    public LocId? GainText;
}

/// <summary>Причуды, полученные при появлении (для осмотра и эмпатов).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialQuirksComponent : Component
{
    [DataField, AutoNetworkedField]
    public List<ProtoId<TraitPrototype>> Quirks = new();
}

/// <summary>
/// Изменение скорости от причуд: хромота, паралич половины тела, поселенец, космический житель.
/// Сервер меняет множители, клиент предсказывает движение.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialQuirkSpeedComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Walk = 1f;

    [DataField, AutoNetworkedField]
    public float Sprint = 1f;
}

/// <summary>Throwing Arm: брошенное летит сильнее и дальше.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialQuirkThrowingArmComponent : Component
{
    [DataField]
    public float Multiplier = 1.5f;
}

/// <summary>Alcohol Tolerance: пьянеет медленнее.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialQuirkAlcoholToleranceComponent : Component
{
    [DataField]
    public float Multiplier = 0.5f;
}

/// <summary>Fluoride Stare: моргнуть вручную (manual_blinking).</summary>
public sealed partial class ImperialQuirkBlinkActionEvent : Content.Shared.Actions.InstantActionEvent;
