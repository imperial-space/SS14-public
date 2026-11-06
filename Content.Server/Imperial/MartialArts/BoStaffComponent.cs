using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

/// <summary>obj/item/staff/bostaff: правый клик двумя руками бьёт по выносливости.</summary>
[RegisterComponent]
public sealed partial class BoStaffComponent : Component
{
    [DataField]
    public TimeSpan BoStaffHitTime = TimeSpan.FromSeconds(6);

    [DataField]
    public float BoStaffHitChance = 0.5f;
}
