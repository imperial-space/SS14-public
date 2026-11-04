using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Оружие культа: не-культиста отталкивает (obj/item/melee/cultblade/attack).</summary>
[RegisterComponent]
public sealed partial class CultWeaponComponent : Component
{
    /// <summary>free_use.</summary>
    [DataField]
    public bool FreeUse;

    /// <summary>Наказание за попытку удара не-культистом.</summary>
    [DataField]
    public bool PunishAttack = true;

    /// <summary>Сообщение «I wouldn't advise that.» при подборе.</summary>
    [DataField]
    public bool WarnPickup = true;

    [DataField]
    public float Force = 30f;
}
