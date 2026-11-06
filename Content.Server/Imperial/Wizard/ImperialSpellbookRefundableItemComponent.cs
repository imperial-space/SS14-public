using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Wizard;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Wizard;

/// <summary>Возврат записи-предмета (контракт ученика, бутылки демонов): предмет возвращается в книгу.</summary>
[RegisterComponent]
public sealed partial class ImperialSpellbookRefundableItemComponent : Component
{
    [DataField(required: true)]
    public ProtoId<ImperialSpellbookEntryPrototype> Entry;

    /// <summary>Сообщение при возврате.</summary>
    [DataField]
    public LocId Message = "imperial-spellbook-item-refunded";

    /// <summary>Предмет уже использован — вернуть нельзя (contract.used).</summary>
    [ViewVariables]
    public bool Used;
}
