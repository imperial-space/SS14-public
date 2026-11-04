using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Xenobiology.VatGrowing;

/// <summary>
/// element/swabable: что можно взять мазком и какую пробу это даст.
/// Источник определяется по прототипу сущности (или его родителям) либо по реагенту лужи.
/// </summary>
[Prototype]
public sealed partial class SwabSourcePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Прототипы (и наследники), у которых можно взять пробу.</summary>
    [DataField]
    public List<string> Entities = new();

    /// <summary>Лужи, содержащие один из реагентов (кровь, рвота).</summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> PuddleReagents = new();

    [DataField(required: true)]
    public ProtoId<CellLineTablePrototype> Table;

    /// <summary>cell_line_amount: случайно от Min до Max.</summary>
    [DataField]
    public int MinAmount = 1;

    [DataField]
    public int MaxAmount = 1;

    /// <summary>virus_chance, проценты.</summary>
    [DataField]
    public float VirusChance = 5f;

    /// <summary>Источник — живое существо (берётся биопсией, а не тампоном).</summary>
    [DataField]
    public bool Mob;
}
