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
