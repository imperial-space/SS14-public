using Content.Shared.Imperial.Heretic.Rituals;
using Content.Shared.Imperial.Heretic.Paths.Void;
using Content.Shared.Imperial.Heretic.Paths.Moon.Parade;
using Content.Shared.Imperial.Heretic.Paths.Moon;
using Content.Shared.Imperial.Heretic.Items;
using Content.Shared.Imperial.Heretic.Effects;
using Content.Shared.Imperial.Heretic.Core;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Paths.Moon.Parade;

/// <summary>
/// mob/living/basic/illusion/shover из Восшествия Артистов: клон идёт за своей жертвой,
/// а от любого удара не еретика лопается, выжигая рассудок всем вокруг.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HereticMoonRingleaderCloneComponent : Component
{
    /// <summary>Еретик, чьё это отражение.</summary>
    [ViewVariables]
    public EntityUid Caster;

    /// <summary>Жертва, за которой клон следует.</summary>
    [ViewVariables]
    public EntityUid Target;

    /// <summary>range(3) взрыва.</summary>
    [DataField]
    public float BurstRadius = 3f;

    /// <summary>adjust_organ_loss(ORGAN_SLOT_BRAIN, 50, 150).</summary>
    [DataField]
    public float BurstBrainDamage = 50f;

    /// <summary>mob_mood.adjust_sanity(-50).</summary>
    [DataField]
    public float BurstSanityDamage = 50f;

    /// <summary>AdjustStun/AdjustKnockdown(1 SECONDS).</summary>
    [DataField]
    public TimeSpan BurstStun = TimeSpan.FromSeconds(1);
}
