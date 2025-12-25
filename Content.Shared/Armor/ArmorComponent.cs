using Content.Shared.Damage;
using Content.Shared.Imperial.Medieval.SmithingSystem;
using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Shared.Armor;

/// <summary>
/// Used for clothing that reduces damage when worn.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedArmorSystem), typeof(SharedSmithingSystem))] // Imperial medieval smithing
public sealed partial class ArmorComponent : Component
{
    /// <summary>
    /// The damage reduction
    /// </summary>
    [DataField(required: true), AutoNetworkedField] // Imperial medieval smithing
    public DamageModifierSet Modifiers = default!;

    /// <summary>
    /// A multiplier applied to the calculated point value
    /// to determine the monetary value of the armor
    /// </summary>
    [DataField, AutoNetworkedField] // Imperial medieval smithing
    public float PriceMultiplier = 1f; // Imperial medieval smithing

    /// <summary>
    /// If true, you can examine the armor to see the protection. If false, the verb won't appear.
    /// </summary>
    [DataField, AutoNetworkedField] // Imperial medieval smithing
    public bool ShowArmorOnExamine = true;
}

/// <summary>
/// Event raised on an armor entity to get additional examine text relating to its armor.
/// </summary>
/// <param name="Msg"></param>
[ByRefEvent]
public record struct ArmorExamineEvent(FormattedMessage Msg);

/// <summary>
/// A Relayed inventory event, gets the total Armor for all Inventory slots defined by the Slotflags in TargetSlots
/// </summary>
public sealed class CoefficientQueryEvent : EntityEventArgs, IInventoryRelayEvent
{
    /// <summary>
    /// All slots to relay to
    /// </summary>
    public SlotFlags TargetSlots { get; set; }

    /// <summary>
    /// The Total of all Coefficients.
    /// </summary>
    public DamageModifierSet DamageModifiers { get; set; } = new DamageModifierSet();

    public CoefficientQueryEvent(SlotFlags slots)
    {
        TargetSlots = slots;
    }
}
