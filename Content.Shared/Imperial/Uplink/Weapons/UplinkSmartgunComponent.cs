using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Uplink.Weapons;

/// <summary>
/// Смарт-ПП Abielle (/obj/item/gun/ballistic/automatic/smartgun, c160smart/ready_proj):
/// пули захватывают цель в момент выстрела — то, по чему кликнули, а при клике по полу ближайшее живое существо
/// в пределах auto_lock_range; на цели вспыхивает голографическое перекрестие.
/// </summary>
[RegisterComponent]
public sealed partial class UplinkSmartgunComponent : Component
{
    /// <summary>auto_lock_range.</summary>
    [DataField]
    public float AutoLockRange = 2f;

    /// <summary>/obj/effect/temp_visual/smartgun_target.</summary>
    [DataField]
    public Robust.Shared.Prototypes.EntProtoId Reticle = "ImperialSmartgunReticle";
}
