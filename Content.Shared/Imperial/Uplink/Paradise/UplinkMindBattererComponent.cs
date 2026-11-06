using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Paradise;

/// <summary>Mind Batterer (Paradise, /obj/item/batterer): бьёт по разуму всех вокруг.</summary>
[RegisterComponent]
public sealed partial class UplinkMindBattererComponent : Component
{
    [DataField]
    public int MaxUses = 5;

    [DataField]
    public float Range = 7f;

    /// <summary>Одна зарядка восстанавливается примерно за 20 секунд.</summary>
    [DataField]
    public TimeSpan RechargeTime = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public int TimesUsed;

    [ViewVariables]
    public TimeSpan NextRecharge;

    [ViewVariables]
    public TimeSpan ThrowCooldownUntil;

    [DataField]
    public TimeSpan BattererUseDelay = TimeSpan.FromSeconds(2);

    [DataField]
    public TimeSpan ThrowCooldown = TimeSpan.FromSeconds(3);
}
