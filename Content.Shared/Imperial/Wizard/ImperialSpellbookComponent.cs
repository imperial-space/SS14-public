using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

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
    public EntityUid? OwnerEntity;

    [ViewVariables]
    public string OwnerName = string.Empty;

    /// <summary>times у записей.</summary>
    [ViewVariables]
    public Dictionary<string, int> Times = new();
}
