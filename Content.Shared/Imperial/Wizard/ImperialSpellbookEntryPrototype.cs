using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>/datum/spellbook_entry: запись книги заклинаний.</summary>
[Prototype]
public sealed partial class ImperialSpellbookEntryPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Desc;

    [DataField(required: true)]
    public ImperialSpellbookCategory Category;

    [DataField]
    public int Cost = 2;

    /// <summary>limit: сколько раз можно купить (null — у заклинаний spell_max_level, у предметов без ограничения).</summary>
    [DataField]
    public int? Limit;

    [DataField]
    public bool Refundable = true;

    [DataField]
    public ImperialSpellbookBuyWord BuyWord = ImperialSpellbookBuyWord.Learn;

    /// <summary>spell_type: заклинание-действие.</summary>
    [DataField]
    public EntProtoId? Spell;

    /// <summary>item_path и сопутствующие предметы (/datum/spellbook_entry/item).</summary>
    [DataField]
    public List<EntProtoId> Items = new();

    /// <summary>Иконка в книге для записей без заклинания и предметов (перки, ритуалы).</summary>
    [DataField]
    public EntProtoId? IconEntity;

    /// <summary>Особая покупка (призывы, перки, испытания) — обрабатывается системой по этому ключу.</summary>
    [DataField]
    public string? Special;

    /// <summary>no_coexistence_typecache: записи, с которыми нельзя совмещать.</summary>
    [DataField]
    public List<ProtoId<ImperialSpellbookEntryPrototype>> NoCoexistence = new();

    /// <summary>Свой откат для показа (у предметов и ритуалов его нет).</summary>
    [DataField]
    public float? Cooldown;

    /// <summary>requires_wizard_garb у записей-предметов задаётся вручную.</summary>
    [DataField]
    public bool? RequiresGarb;

    /// <summary>can_be_purchased(): запись скрыта (например, отключена конфигом).</summary>
    [DataField]
    public bool Hidden;
}
