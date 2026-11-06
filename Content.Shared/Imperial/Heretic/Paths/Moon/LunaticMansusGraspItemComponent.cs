using Robust.Shared.GameStates;
using Content.Shared.Damage;

namespace Content.Shared.Imperial.Heretic.Paths.Moon;

/// <summary>Предмет Хватки Мансуса лунатика (obj/item/melee/touch_attack/mansus_fist).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LunaticMansusGraspItemComponent : Component
{
    [DataField]
    public DamageSpecifier GraspHitBluntDamage = new() { DamageDict = { ["Blunt"] = 10 } };

    [DataField]
    public TimeSpan GraspHitTime = TimeSpan.FromSeconds(0.5);

    [DataField]
    public TimeSpan GraspHitDuration = TimeSpan.FromSeconds(4);

    [DataField]
    public TimeSpan GraspHitTime2 = TimeSpan.FromSeconds(5);
}
