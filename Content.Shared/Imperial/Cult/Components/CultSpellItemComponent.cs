using Robust.Shared.GameStates;
using Content.Shared.Imperial.Cult;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>
/// Помечает предмет как материализованное заклинание культа.
/// Появляется в руке при активации действия заклинания.
/// При взаимодействии с целью активирует соответствующее заклинание.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultSpellItemComponent : Component
{
    /// <summary>
    /// Тип заклинания: "stun", "shackles", "teleport", "construction", "bloodrites"
    /// </summary>
    [DataField]
    public string SpellType = "stun";

    /// <summary>
    /// Активный режим Кровавого обряда для материализованной тёмной энергии.
    /// </summary>
    [DataField]
    public CultBloodRitesMode BloodRitesMode = CultBloodRitesMode.Gather;

    /// <summary>
    /// Prototype ID of the prepared action that created this spell item.
    /// Used to consume remaining uses only after a successful cast.
    /// </summary>
    [DataField]
    public string PreparedActionId = string.Empty;
}
