using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Категории книги (category у /datum/spellbook_entry).</summary>
[Serializable, NetSerializable]
public enum ImperialSpellbookCategory : byte
{
    Offensive,
    Defensive,
    Mobility,
    Assistance,
    Challenges,
    Rituals,
    Perks,
}

/// <summary>buy_word: «Выучить», «Призвать», «Произнести».</summary>
[Serializable, NetSerializable]
public enum ImperialSpellbookBuyWord : byte
{
    Learn,
    Summon,
    Cast,
    Accept,
}

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

/// <summary>/obj/item/spellbook: очки, владелец, сколько раз куплена каждая запись.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialSpellbookComponent : Component
{
    /// <summary>uses.</summary>
    [DataField]
    public int Uses = 10;

    /// <summary>initial(uses).</summary>
    [DataField]
    public int MaxUses = 10;

    [DataField]
    public int SemiRandomBonus = 2;

    [DataField]
    public int FullRandomBonus = 5;

    [DataField]
    public bool RefundsAllowed = true;

    /// <summary>owner: разум, к которому привязана книга.</summary>
    [ViewVariables]
    public EntityUid? Owner;

    [ViewVariables]
    public string OwnerName = string.Empty;

    /// <summary>times у записей.</summary>
    [ViewVariables]
    public Dictionary<string, int> Times = new();
}

[Serializable, NetSerializable]
public enum ImperialSpellbookUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class ImperialSpellbookEntryState(string id, string name, int times, float? cooldown, bool requiresGarb)
{
    public string Id = id;

    /// <summary>Имя с приставкой уровня («Ускоренный Огненный шар»).</summary>
    public string Name = name;

    public int Times = times;
    public float? Cooldown = cooldown;
    public bool RequiresGarb = requiresGarb;
}

[Serializable, NetSerializable]
public sealed class ImperialSpellbookState(string owner, int points, int maxPoints, int semiRandom, int fullRandom,
    List<ImperialSpellbookEntryState> entries) : BoundUserInterfaceState
{
    public string Owner = owner;
    public int Points = points;
    public int MaxPoints = maxPoints;
    public int SemiRandom = semiRandom;
    public int FullRandom = fullRandom;
    public List<ImperialSpellbookEntryState> Entries = entries;
}

[Serializable, NetSerializable]
public sealed class ImperialSpellbookPurchaseMessage(string entry) : BoundUserInterfaceMessage
{
    public string Entry = entry;
}

[Serializable, NetSerializable]
public sealed class ImperialSpellbookRefundMessage(string entry) : BoundUserInterfaceMessage
{
    public string Entry = entry;
}

/// <summary>semirandomize / randomize.</summary>
[Serializable, NetSerializable]
public sealed class ImperialSpellbookRandomizeMessage(bool full) : BoundUserInterfaceMessage
{
    public bool Full = full;
}

/// <summary>purchase_loadout.</summary>
[Serializable, NetSerializable]
public sealed class ImperialSpellbookLoadoutMessage(string loadout) : BoundUserInterfaceMessage
{
    public string Loadout = loadout;
}
