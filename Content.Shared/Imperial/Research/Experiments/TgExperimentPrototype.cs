using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Research.Experiments;

/// <summary>
/// Эксперимент techweb TG (code/modules/experisci): обязательный для узла (required_experiments)
/// или дающий скидку на узел (discount_experiments). Скидка в SS14 — возврат очков на сервер.
/// </summary>
[Prototype("imperialTgExperiment")]
public sealed partial class TgExperimentPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    /// <summary>Узел TG (например, selection), которому нужен или на который даёт скидку этот эксперимент.</summary>
    [DataField(required: true)]
    public string Node = string.Empty;

    /// <summary>required_experiments: без эксперимента узел не открыть.</summary>
    [DataField]
    public bool Required;

    /// <summary>discount_experiments: сколько очков исследований вернуть серверу.</summary>
    [DataField]
    public int Discount;

    [DataField(required: true)]
    public TgExperimentRequirement Requirement = default!;
}

/// <summary>Чем выполняется эксперимент.</summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class TgExperimentRequirement
{
    /// <summary>Сколько нужно (сканов, очков, побед).</summary>
    [DataField]
    public int Count = 1;
}

/// <summary>Цель сканирования: подходящие прототипы и/или вайтлист, сколько очков даёт.</summary>
[DataDefinition]
public sealed partial class TgScanTarget
{
    [DataField]
    public List<EntProtoId> Prototypes = new();

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public int Points = 1;
}

/// <summary>scanning, scanning/points: отсканировать разные объекты (каждый — один раз).</summary>
public sealed partial class TgScanRequirement : TgExperimentRequirement
{
    [DataField(required: true)]
    public List<TgScanTarget> Targets = new();
}

/// <summary>scanning/reagent: ёмкость с реагентом.</summary>
public sealed partial class TgReagentRequirement : TgExperimentRequirement
{
    [DataField(required: true)]
    public string Reagent = string.Empty;

    [DataField]
    public float MinAmount = 1f;
}

/// <summary>scanning/random/janitor_trash: лужи с одним из реагентов (рвота, кровь).</summary>
public sealed partial class TgPuddleRequirement : TgExperimentRequirement
{
    [DataField(required: true)]
    public List<string> Reagents = new();
}

/// <summary>ordnance/gaseous: баллон или канистра с заданным газом.</summary>
public sealed partial class TgGasRequirement : TgExperimentRequirement
{
    [DataField(required: true)]
    public string Gas = string.Empty;

    [DataField]
    public float Moles = 10f;
}

/// <summary>ordnance/explosive: взрыв радиусом не меньше заданного на карте сервера.</summary>
public sealed partial class TgExplosionRequirement : TgExperimentRequirement
{
    [DataField]
    public int MinRadius = 5;
}

/// <summary>Особые условия сканирования, проверяемые кодом.</summary>
public enum TgScanCondition : byte
{
    /// <summary>scanning/people/mutant: живой гуманоид с активной мутацией.</summary>
    Mutant,
    /// <summary>scanning/people/augmented_organs: гуманоид с кибернетическим органом.</summary>
    AugmentedOrgans,
    /// <summary>scanning/people/android: полностью аугментированный гуманоид.</summary>
    Android,
    /// <summary>scanning/people/skillchip: гуманоид со скилл-чипом.</summary>
    Skillchip,
    /// <summary>scanning/random/mecha_equipped_scan: мех с оборудованием.</summary>
    MechEquipped,
    /// <summary>scanning/random/mecha_damage_scan: повреждённый мех.</summary>
    MechDamaged,
    /// <summary>scanning/random/plants/wild: мутировавшее растение в лотке.</summary>
    PlantMutated,
    /// <summary>scanning/random/plants/traits: растение с особой чертой.</summary>
    PlantTrait,
    /// <summary>autopsy/human: труп человека.</summary>
    AutopsyHuman,
    /// <summary>autopsy/nonhuman: труп гуманоида не-человека.</summary>
    AutopsyNonHuman,
    /// <summary>autopsy/xenomorph: труп ксеноморфа.</summary>
    AutopsyXeno,
    /// <summary>machinery_tiered_scan: работающая машина, собранная из деталей.</summary>
    Machine,
}

public sealed partial class TgConditionRequirement : TgExperimentRequirement
{
    [DataField(required: true)]
    public TgScanCondition Condition;
}

/// <summary>physical/arcade_winner: победа на привязанном сканером аркадном автомате.</summary>
public sealed partial class TgArcadeRequirement : TgExperimentRequirement;

/// <summary>Сканер экспериментов (handheld_scanner.dm).</summary>
[RegisterComponent]
public sealed partial class TgExperiScannerComponent : Component;

/// <summary>Копия прогресса экспериментов сервера на консоли исследований — для окна консоли.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class TgExperimentViewComponent : Component
{
    [AutoNetworkedField]
    public Dictionary<string, int> Progress = new();

    [AutoNetworkedField]
    public List<string> Completed = new();
}
